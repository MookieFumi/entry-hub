namespace EntryHub.Api.Features.V1.CurrentUser.GetMe;

/// <param name="UserId">The <c>sub</c> claim of the token; the same value as <c>organizerId</c> on the bookings of this person.</param>
/// <param name="Role"><c>Employee</c> or <c>Facilities</c>.</param>
/// <param name="TenantId">The customer this person belongs to, from the token.</param>
/// <param name="TenantName">The display name of that customer.</param>
public sealed record MeResponse(string UserId, string Role, Guid TenantId, string TenantName);