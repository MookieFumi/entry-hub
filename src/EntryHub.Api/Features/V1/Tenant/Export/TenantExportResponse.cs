using EntryHub.Api.Features.V1.Audit;
using EntryHub.Api.Features.V1.Bookings;
using EntryHub.Api.Features.V1.Features;
using EntryHub.Api.Features.V1.Rooms;
using EntryHub.Api.Features.V1.Rooms.Blocks;
using EntryHub.Api.Features.V1.Sites;
using EntryHub.Api.Features.V1.Sites.Holidays;

namespace EntryHub.Api.Features.V1.Tenant.Export;

/// <summary>
/// Everything stored for the tenant of the token, in the same shapes the API returns elsewhere.
/// </summary>
/// <param name="TenantId">The tenant.</param>
/// <param name="TenantName">Name of the tenant.</param>
/// <param name="ExportedAt">When the document was made.</param>
/// <param name="Rules">The rules the tenant stored; <see langword="null"/> when it uses the defaults of the platform.</param>
/// <param name="Sites">All sites, active or not.</param>
/// <param name="Holidays">All closed days of the sites.</param>
/// <param name="Features">The whole catalog, active or not.</param>
/// <param name="Rooms">All rooms, active or not, with what they have.</param>
/// <param name="Blocks">All maintenance blocks.</param>
/// <param name="Bookings">All bookings, whatever their status.</param>
/// <param name="Audit">The audit trail, oldest first.</param>
public sealed record TenantExportResponse(
    Guid TenantId,
    string TenantName,
    DateTimeOffset ExportedAt,
    TenantRulesExport? Rules,
    IReadOnlyList<SiteResponse> Sites,
    IReadOnlyList<SiteHolidayResponse> Holidays,
    IReadOnlyList<FeatureResponse> Features,
    IReadOnlyList<RoomResponse> Rooms,
    IReadOnlyList<RoomBlockResponse> Blocks,
    IReadOnlyList<BookingResponse> Bookings,
    IReadOnlyList<AuditResponse> Audit);

/// <summary>
/// The booking rules the tenant stored.
/// </summary>
public sealed record TenantRulesExport(
    int MinDurationMinutes,
    int MaxDurationMinutes,
    int SlotMinutes,
    int MinLeadTimeMinutes,
    int MaxAdvanceDays,
    int CancellationNoticeMinutes,
    int MaxActiveBookingsPerEmployee,
    DateTimeOffset UpdatedAt);