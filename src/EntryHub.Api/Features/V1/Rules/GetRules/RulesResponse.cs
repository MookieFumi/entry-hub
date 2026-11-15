namespace EntryHub.Api.Features.V1.Rules.GetRules;

/// <summary>
/// The booking rules of the tenant of the token, the ones a client can show or check before it sends a booking. The server still decides: a booking that
/// breaks a rule gets a 422 with the code of the rule.
/// </summary>
/// <param name="MinDurationMinutes">Shortest booking.</param>
/// <param name="MaxDurationMinutes">Longest booking.</param>
/// <param name="SlotMinutes">Starts and ends fall on multiples of this length.</param>
/// <param name="OfficeOpens">First start time of the day at the default site (the oldest active one), <c>HH:mm</c>, in <paramref name="TimeZone"/>. Each site has its own: see <c>GET /sites</c>.</param>
/// <param name="OfficeCloses">Last end time of the day, <c>HH:mm</c>, in <paramref name="TimeZone"/>.</param>
/// <param name="OfficeDays">Days of the week rooms can be booked.</param>
/// <param name="TimeZone">IANA time zone of the office: the one the hours and the days refer to.</param>
/// <param name="MinLeadTimeMinutes">A booking starts at least this many minutes from now.</param>
/// <param name="MaxAdvanceDays">A booking starts at most this many days from now.</param>
/// <param name="CancellationNoticeMinutes">An Employee can cancel until this many minutes before the start.</param>
/// <param name="MaxActiveBookings">Active bookings the caller can hold; <see langword="null"/> when there is no limit.</param>
public sealed record RulesResponse(
    int MinDurationMinutes,
    int MaxDurationMinutes,
    int SlotMinutes,
    string OfficeOpens,
    string OfficeCloses,
    IReadOnlyList<DayOfWeek> OfficeDays,
    string TimeZone,
    int MinLeadTimeMinutes,
    int MaxAdvanceDays,
    int CancellationNoticeMinutes,
    int? MaxActiveBookings);