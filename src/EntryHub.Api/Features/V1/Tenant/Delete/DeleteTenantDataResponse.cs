namespace EntryHub.Api.Features.V1.Tenant.Delete;

/// <summary>
/// What was removed. The tenant itself and one audit entry about the deletion stay.
/// </summary>
/// <param name="DeletedAt">When the data was removed.</param>
/// <param name="Bookings">Bookings removed.</param>
/// <param name="Rooms">Rooms removed.</param>
/// <param name="Sites">Sites removed.</param>
/// <param name="Features">Entries of the equipment catalog removed.</param>
/// <param name="Blocks">Maintenance blocks removed.</param>
/// <param name="Holidays">Closed days removed.</param>
/// <param name="AuditEntries">Entries of the audit trail removed.</param>
public sealed record DeleteTenantDataResponse(DateTimeOffset DeletedAt, int Bookings, int Rooms, int Sites, int Features, int Blocks, int Holidays, int AuditEntries);