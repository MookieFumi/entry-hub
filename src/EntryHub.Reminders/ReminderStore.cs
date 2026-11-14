using Npgsql;

namespace EntryHub.Reminders;

/// <summary>
/// A booking whose organizer has just been marked as reminded.
/// </summary>
public sealed record DueReminder(Guid BookingId, string OrganizerId, string Title, string RoomName, DateTimeOffset StartAt);

/// <summary>
/// Reads the bookings of the API database that are about to start. The API owns the schema; this service only
/// reads <c>bookings</c> and <c>rooms</c> and writes <c>ReminderSentAt</c>, one tenant at a time.
/// </summary>
public sealed class ReminderStore(NpgsqlDataSource dataSource)
{
    // One statement finds the due bookings, locks them and marks them, so two copies of this service never pick the
    // same booking: the second one skips the rows the first one holds.
    private const string ClaimSql = """
        WITH due AS (
            SELECT "Id" FROM bookings
            WHERE "Status" = 'Confirmed'
              AND "ReminderSentAt" IS NULL
              AND "StartAt" > @now
              AND "StartAt" <= @until
            FOR UPDATE SKIP LOCKED)
        UPDATE bookings AS booking
        SET "ReminderSentAt" = @now
        FROM due, rooms AS room
        WHERE booking."Id" = due."Id" AND room."Id" = booking."RoomId"
        RETURNING booking."Id", booking."OrganizerId", booking."Title", room."Name", booking."StartAt"
        """;

    /// <summary>
    /// Marks the confirmed bookings that start within <paramref name="leadTime"/> as reminded and returns them.
    /// Each booking comes back once, so the reminder is delivered at most once.
    /// </summary>
    public async Task<IReadOnlyList<DueReminder>> ClaimDueAsync(DateTimeOffset now, TimeSpan leadTime, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        // Row-Level Security shows each connection the rows of one tenant only, so the service visits the tenants
        // one by one. The tenants table has no policy: it is the list of whom to visit.
        var tenants = new List<Guid>();
        await using (var list = new NpgsqlCommand("SELECT \"Id\" FROM tenants", connection))
        await using (var reader = await list.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                tenants.Add(reader.GetGuid(0));
            }
        }

        var due = new List<DueReminder>();
        foreach (var tenant in tenants)
        {
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

            // The last argument makes the setting local to this transaction, so it cannot leak to the next tenant.
            await using (var scope = new NpgsqlCommand("SELECT set_config('app.tenant_id', @tenant, true)", connection, transaction))
            {
                scope.Parameters.AddWithValue("tenant", tenant.ToString());
                await scope.ExecuteNonQueryAsync(cancellationToken);
            }

            await using (var claim = new NpgsqlCommand(ClaimSql, connection, transaction))
            {
                claim.Parameters.AddWithValue("now", now);
                claim.Parameters.AddWithValue("until", now + leadTime);

                await using var reader = await claim.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    due.Add(new DueReminder(
                        reader.GetGuid(0),
                        reader.GetString(1),
                        reader.GetString(2),
                        reader.GetString(3),
                        reader.GetFieldValue<DateTimeOffset>(4)));
                }
            }

            await transaction.CommitAsync(cancellationToken);
        }

        return due;
    }
}