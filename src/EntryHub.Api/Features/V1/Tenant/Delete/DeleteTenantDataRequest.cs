namespace EntryHub.Api.Features.V1.Tenant.Delete;

/// <summary>
/// Deleting everything cannot be undone, so the request must say so in words.
/// </summary>
/// <param name="Confirmation">The text <c>DELETE</c>, a space and the exact name of the tenant, as <c>GET /tenant/export</c> shows it (for example <c>DELETE Acme</c>).</param>
public sealed record DeleteTenantDataRequest(string? Confirmation);