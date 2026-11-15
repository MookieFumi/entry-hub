using System.ComponentModel.DataAnnotations;

namespace EntryHub.Api.Features.V1.Rules.UpdateRules;

/// <summary>
/// The rules of the tenant. All fields are required: the tenant keeps exactly what is sent. Opening hours, days and
/// time zone belong to each site, see <c>PATCH /sites/{id}</c>.
/// </summary>
/// <param name="MinDurationMinutes">Shortest booking.</param>
/// <param name="MaxDurationMinutes">Longest booking, at most 24 hours.</param>
/// <param name="SlotMinutes">Starts and ends fall on multiples of this length; it must divide an hour.</param>
/// <param name="MinLeadTimeMinutes">A booking starts at least this many minutes from now.</param>
/// <param name="MaxAdvanceDays">A booking starts at most this many days from now.</param>
/// <param name="CancellationNoticeMinutes">An Employee can cancel until this many minutes before the start.</param>
/// <param name="MaxActiveBookings">Active bookings an Employee can hold.</param>
public sealed record UpdateRulesRequest(
    [property: Range(1, 1440)] int MinDurationMinutes,
    [property: Range(1, 1440)] int MaxDurationMinutes,
    [property: Range(1, 60)] int SlotMinutes,
    [property: Range(0, 10080)] int MinLeadTimeMinutes,
    [property: Range(1, 730)] int MaxAdvanceDays,
    [property: Range(0, 10080)] int CancellationNoticeMinutes,
    [property: Range(1, 1000)] int MaxActiveBookings);