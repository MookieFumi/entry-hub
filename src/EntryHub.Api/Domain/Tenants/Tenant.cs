namespace EntryHub.Api.Domain.Tenants;

/// <summary>
/// A customer of the platform. Every room and booking belongs to exactly one tenant.
/// </summary>
public sealed class Tenant
{
    /// <summary>
    /// The tenant that owns the rows created before tenants existed. Migrations use it to back-fill them.
    /// </summary>
    public static readonly Guid DefaultId = new("00000000-0000-0000-0000-000000000001");

    public Guid Id { get; init; }

    public required string Name { get; set; }

    public DateTimeOffset CreatedAt { get; init; }
}