using Microsoft.Extensions.Options;

namespace EntryHub.Api.Domain.Bookings;

/// <summary>
/// The booking rules that need no database. It receives the clock value as an argument, so tests pick the time.
/// </summary>
public sealed class BookingPolicy(IOptions<BookingRulesOptions> options)
{
    private readonly BookingRulesOptions _rules = options.Value;

    private readonly TimeZoneInfo _office = TimeZoneInfo.FindSystemTimeZoneById(options.Value.OfficeTimeZoneId);

    public int MaxActiveBookingsPerEmployee => _rules.MaxActiveBookingsPerEmployee;

    public IReadOnlyList<RuleViolation> EvaluateNewBooking(
        DateTimeOffset start,
        DateTimeOffset end,
        int attendees,
        int roomCapacity,
        DateTimeOffset now)
    {
        var violations = new List<RuleViolation>();
        var duration = end - start;

        if (duration < _rules.MinDuration)
        {
            violations.Add(new(BookingRuleCodes.DurationTooShort, $"A booking lasts at least {_rules.MinDuration.TotalMinutes:0} minutes."));
        }

        if (duration > _rules.MaxDuration)
        {
            violations.Add(new(BookingRuleCodes.DurationTooLong, $"A booking lasts at most {_rules.MaxDuration.TotalHours:0.##} hours."));
        }

        if (!OnSlotBoundary(start) || !OnSlotBoundary(end))
        {
            violations.Add(new(BookingRuleCodes.NotOnSlotBoundary, $"Start and end must fall on a multiple of {_rules.Slot.TotalMinutes:0} minutes."));
        }

        if (!WithinOfficeHours(start, end))
        {
            violations.Add(new(
                BookingRuleCodes.OutsideOfficeHours,
                $"Rooms can be booked Monday to Friday between {_rules.OfficeOpens:HH\\:mm} and {_rules.OfficeCloses:HH\\:mm} ({_rules.OfficeTimeZoneId}), within a single day."));
        }

        if (start < now + _rules.MinLeadTime)
        {
            violations.Add(new(BookingRuleCodes.TooSoon, $"A booking must start at least {_rules.MinLeadTime.TotalMinutes:0} minutes from now."));
        }

        if (start > now + _rules.MaxAdvance)
        {
            violations.Add(new(BookingRuleCodes.TooFarAhead, $"A booking cannot start more than {_rules.MaxAdvance.TotalDays:0} days from now."));
        }

        if (attendees > roomCapacity)
        {
            violations.Add(new(BookingRuleCodes.ExceedsCapacity, $"The room holds {roomCapacity} people and {attendees} were announced."));
        }

        return violations;
    }

    /// <returns>The reason the booking cannot be cancelled, or <see langword="null"/> when it can.</returns>
    public RuleViolation? EvaluateCancellation(DateTimeOffset start, DateTimeOffset now, bool isFacilities) =>
        !isFacilities && start - now < _rules.CancellationNotice
            ? new(BookingRuleCodes.TooLateToCancel, $"A booking can be cancelled until {_rules.CancellationNotice.TotalMinutes:0} minutes before it starts.")
            : null;

    private bool OnSlotBoundary(DateTimeOffset moment) => moment.UtcTicks % _rules.Slot.Ticks == 0;

    private bool WithinOfficeHours(DateTimeOffset start, DateTimeOffset end)
    {
        var localStart = TimeZoneInfo.ConvertTime(start, _office);
        var localEnd = TimeZoneInfo.ConvertTime(end, _office);

        return localStart.Date == localEnd.Date
            && localStart.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)
            && TimeOnly.FromDateTime(localStart.DateTime) >= _rules.OfficeOpens
            && TimeOnly.FromDateTime(localEnd.DateTime) <= _rules.OfficeCloses;
    }
}