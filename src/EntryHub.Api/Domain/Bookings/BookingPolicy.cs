using EntryHub.Api.Domain.Sites;

namespace EntryHub.Api.Domain.Bookings;

/// <summary>
/// The booking rules that need no database. It keeps no state: the rules of the tenant and the hours of the site
/// arrive as arguments, and so does the clock value, so tests pick all of them.
/// </summary>
public sealed class BookingPolicy
{
    public IReadOnlyList<RuleViolation> EvaluateNewBooking(
        BookingRules rules,
        SiteHours site,
        DateTimeOffset start,
        DateTimeOffset end,
        int attendees,
        int roomCapacity,
        DateTimeOffset now)
    {
        var violations = new List<RuleViolation>();
        var duration = end - start;

        if (duration < rules.MinDuration)
        {
            violations.Add(new(BookingRuleCodes.DurationTooShort, $"A booking lasts at least {rules.MinDuration.TotalMinutes:0} minutes."));
        }

        if (duration > rules.MaxDuration)
        {
            violations.Add(new(BookingRuleCodes.DurationTooLong, $"A booking lasts at most {rules.MaxDuration.TotalHours:0.##} hours."));
        }

        if (!OnSlotBoundary(rules, start) || !OnSlotBoundary(rules, end))
        {
            violations.Add(new(BookingRuleCodes.NotOnSlotBoundary, $"Start and end must fall on a multiple of {rules.Slot.TotalMinutes:0} minutes."));
        }

        if (!WithinOpeningHours(site, start, end))
        {
            violations.Add(new(
                BookingRuleCodes.OutsideOfficeHours,
                $"This site can be booked {DescribeDays(site.OpenDays)} between {site.Opens:HH\\:mm} and {site.Closes:HH\\:mm} ({site.TimeZoneId}), within a single day."));
        }

        if (start < now + rules.MinLeadTime)
        {
            violations.Add(new(BookingRuleCodes.TooSoon, $"A booking must start at least {rules.MinLeadTime.TotalMinutes:0} minutes from now."));
        }

        if (start > now + rules.MaxAdvance)
        {
            violations.Add(new(BookingRuleCodes.TooFarAhead, $"A booking cannot start more than {rules.MaxAdvance.TotalDays:0} days from now."));
        }

        if (attendees > roomCapacity)
        {
            violations.Add(new(BookingRuleCodes.ExceedsCapacity, $"The room holds {roomCapacity} people and {attendees} were announced."));
        }

        return violations;
    }

    /// <returns>The reason the booking cannot be cancelled, or <see langword="null"/> when it can.</returns>
    public RuleViolation? EvaluateCancellation(BookingRules rules, DateTimeOffset start, DateTimeOffset now, bool isFacilities) =>
        !isFacilities && start - now < rules.CancellationNotice
            ? new(BookingRuleCodes.TooLateToCancel, $"A booking can be cancelled until {rules.CancellationNotice.TotalMinutes:0} minutes before it starts.")
            : null;

    /// <summary>
    /// A booking can be moved while it is confirmed and has not ended, and an Employee until the same notice period
    /// as for a cancellation before its current start (Facilities at any time).
    /// </summary>
    /// <returns>The reason the booking cannot be moved, or <see langword="null"/> when it can.</returns>
    public RuleViolation? EvaluateReschedule(BookingRules rules, BookingStatus status, DateTimeOffset currentStart, DateTimeOffset currentEnd, DateTimeOffset now, bool isFacilities)
    {
        if (status != BookingStatus.Confirmed || currentEnd <= now)
        {
            return new(BookingRuleCodes.NotActive, "Only a confirmed booking that has not ended can be moved.");
        }

        return !isFacilities && currentStart - now < rules.CancellationNotice
            ? new(BookingRuleCodes.TooLateToReschedule, $"A booking can be moved until {rules.CancellationNotice.TotalMinutes:0} minutes before it starts.")
            : null;
    }

    private static bool OnSlotBoundary(BookingRules rules, DateTimeOffset moment) => moment.UtcTicks % rules.Slot.Ticks == 0;

    private static bool WithinOpeningHours(SiteHours site, DateTimeOffset start, DateTimeOffset end)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(site.TimeZoneId);
        var localStart = TimeZoneInfo.ConvertTime(start, zone);
        var localEnd = TimeZoneInfo.ConvertTime(end, zone);

        return localStart.Date == localEnd.Date
            && site.OpenDays.Contains(localStart.DayOfWeek)
            && TimeOnly.FromDateTime(localStart.DateTime) >= site.Opens
            && TimeOnly.FromDateTime(localEnd.DateTime) <= site.Closes;
    }

    private static string DescribeDays(IReadOnlyList<DayOfWeek> days) =>
        days.Count == 0 ? "on no day" : "on " + string.Join(", ", days.Order().Select(day => day.ToString()));
}