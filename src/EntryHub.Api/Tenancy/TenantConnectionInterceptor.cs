using System.Data.Common;

using EntryHub.Api.Data;

using Microsoft.EntityFrameworkCore.Diagnostics;

namespace EntryHub.Api.Tenancy;

/// <summary>
/// Tells PostgreSQL which tenant a connection works for, every time a context opens one. The Row-Level Security
/// policies of <c>rooms</c> and <c>bookings</c> read that setting, so a query that forgets the tenant filter still
/// cannot return another tenant's rows. With no tenant the setting is empty and the policies match nothing.
/// </summary>
public sealed class TenantConnectionInterceptor : DbConnectionInterceptor
{
    public const string Setting = "app.tenant_id";

    public static TenantConnectionInterceptor Instance { get; } = new();

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        using var command = CreateCommand(connection, eventData);
        command.ExecuteNonQuery();
    }

    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        await using var command = CreateCommand(connection, eventData);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static DbCommand CreateCommand(DbConnection connection, ConnectionEndEventData eventData)
    {
        var tenantId = (eventData.Context as EntryHubDbContext)?.TenantId ?? Guid.Empty;

        var command = connection.CreateCommand();
        command.CommandText = "SELECT set_config('app.tenant_id', @tenant, false)";

        var parameter = command.CreateParameter();
        parameter.ParameterName = "tenant";
        parameter.Value = tenantId == Guid.Empty ? string.Empty : tenantId.ToString();
        command.Parameters.Add(parameter);

        return command;
    }
}