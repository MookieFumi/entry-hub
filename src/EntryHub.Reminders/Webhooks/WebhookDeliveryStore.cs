using Npgsql;

namespace EntryHub.Reminders.Webhooks;

/// <summary>
/// Reads and updates <c>webhook_deliveries</c> of the API database, one tenant at a time. The API writes the rows
/// (an outbox saved with the booking change); this service picks them up and records what happened.
/// </summary>
public sealed class WebhookDeliveryStore(NpgsqlDataSource dataSource)
{
    // A subscription that was disabled after the event happened gets nothing: its pending deliveries are given up.
    private const string GiveUpDisabledSql = """
        UPDATE webhook_deliveries AS delivery
        SET "Status" = 'Failed', "LastError" = 'The subscription is disabled.', "CompletedAt" = @now
        FROM webhook_subscriptions AS subscription
        WHERE subscription."Id" = delivery."SubscriptionId"
          AND delivery."Status" = 'Pending'
          AND NOT subscription."IsEnabled"
        """;

    // One statement finds the due deliveries, locks them and reserves them for the lease, so several copies of this
    // service never send the same one: the others skip the rows that this one holds. If the service stops before it
    // records the outcome, the delivery is due again when the lease ends.
    private const string ClaimSql = """
        WITH due AS (
            SELECT delivery."Id"
            FROM webhook_deliveries AS delivery
            JOIN webhook_subscriptions AS subscription ON subscription."Id" = delivery."SubscriptionId"
            WHERE delivery."Status" = 'Pending'
              AND delivery."NextAttemptAt" <= @now
              AND subscription."IsEnabled"
            ORDER BY delivery."NextAttemptAt"
            LIMIT @batch
            FOR UPDATE OF delivery SKIP LOCKED)
        UPDATE webhook_deliveries AS delivery
        SET "NextAttemptAt" = @leaseEnds
        FROM due, webhook_subscriptions AS subscription
        WHERE delivery."Id" = due."Id" AND subscription."Id" = delivery."SubscriptionId"
        RETURNING delivery."Id", delivery."EventId", delivery."EventType", delivery."Payload", delivery."Attempts",
                  subscription."Url", subscription."Secret"
        """;

    private const string RecordSql = """
        UPDATE webhook_deliveries
        SET "Attempts" = "Attempts" + 1,
            "LastAttemptAt" = @now,
            "LastStatusCode" = @status,
            "LastError" = @error,
            "Status" = @newStatus,
            "NextAttemptAt" = @next,
            "CompletedAt" = @completedAt
        WHERE "Id" = @id
        """;

    /// <summary>
    /// Gives up the deliveries of disabled subscriptions and reserves the ones that are due.
    /// </summary>
    public async Task<IReadOnlyList<(Guid Tenant, DueDelivery Delivery)>> ClaimDueAsync(
        DateTimeOffset now,
        int batchSize,
        TimeSpan lease,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var due = new List<(Guid, DueDelivery)>();

        foreach (var tenant in await ListTenantsAsync(connection, cancellationToken))
        {
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            await SetTenantAsync(connection, transaction, tenant, cancellationToken);

            await using (var giveUp = new NpgsqlCommand(GiveUpDisabledSql, connection, transaction))
            {
                giveUp.Parameters.AddWithValue("now", now);
                await giveUp.ExecuteNonQueryAsync(cancellationToken);
            }

            await using (var claim = new NpgsqlCommand(ClaimSql, connection, transaction))
            {
                claim.Parameters.AddWithValue("now", now);
                claim.Parameters.AddWithValue("batch", batchSize);
                claim.Parameters.AddWithValue("leaseEnds", now + lease);

                await using var reader = await claim.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    due.Add((tenant, new DueDelivery(
                        reader.GetGuid(0),
                        reader.GetGuid(1),
                        reader.GetString(2),
                        reader.GetString(3),
                        reader.GetInt32(4),
                        reader.GetString(5),
                        reader.GetString(6))));
                }
            }

            await transaction.CommitAsync(cancellationToken);
        }

        return due;
    }

    /// <summary>
    /// Records an attempt. A failure schedules the next one after <paramref name="retryDelay"/>, or gives the delivery up
    /// when <paramref name="finalAttempt"/> is true.
    /// </summary>
    public async Task RecordAsync(
        Guid tenant,
        Guid deliveryId,
        DateTimeOffset now,
        SendResult result,
        bool finalAttempt,
        TimeSpan retryDelay,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await SetTenantAsync(connection, transaction, tenant, cancellationToken);

        var done = result.Succeeded || finalAttempt;

        await using (var record = new NpgsqlCommand(RecordSql, connection, transaction))
        {
            record.Parameters.AddWithValue("id", deliveryId);
            record.Parameters.AddWithValue("now", now);
            record.Parameters.AddWithValue("status", (object?)result.StatusCode ?? DBNull.Value);
            record.Parameters.AddWithValue("error", (object?)result.Error ?? DBNull.Value);
            record.Parameters.AddWithValue("newStatus", result.Succeeded ? "Succeeded" : finalAttempt ? "Failed" : "Pending");
            record.Parameters.AddWithValue("next", now + (done ? TimeSpan.Zero : retryDelay));
            record.Parameters.AddWithValue("completedAt", done ? now : DBNull.Value);
            await record.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task<List<Guid>> ListTenantsAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        var tenants = new List<Guid>();
        await using var list = new NpgsqlCommand("SELECT \"Id\" FROM tenants", connection);
        await using var reader = await list.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            tenants.Add(reader.GetGuid(0));
        }

        return tenants;
    }

    private static async Task SetTenantAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid tenant, CancellationToken cancellationToken)
    {
        await using var scope = new NpgsqlCommand("SELECT set_config('app.tenant_id', @tenant, true)", connection, transaction);
        scope.Parameters.AddWithValue("tenant", tenant.ToString());
        await scope.ExecuteNonQueryAsync(cancellationToken);
    }
}