using EntryHub.Api.Domain.Bookings;
using EntryHub.Api.Tests.Support;

namespace EntryHub.Api.Tests.Domain.Bookings;

/// <summary>
/// Layer 1: the booking rules as pure logic. The clock is an argument, so every case picks its own "now".
/// </summary>
public class BookingPolicyTests
{
    private static readonly DateTimeOffset Now = TestClock.MondayMorning;
    private static readonly DateTimeOffset TuesdayNoon = new(2026, 10, 20, 10, 0, 0, TimeSpan.Zero);

    private static readonly BookingPolicy Policy = TestPolicy.Default();

    private static string[] Codes(DateTimeOffset start, DateTimeOffset end, int attendees = 4, int capacity = 8, DateTimeOffset? now = null) =>
        [.. Policy.EvaluateNewBooking(start, end, attendees, capacity, now ?? Now).Select(violation => violation.Code)];

    [Fact]
    public void A_booking_inside_every_limit_has_no_violations() =>
        Assert.Empty(Codes(TuesdayNoon, TuesdayNoon.AddHours(1)));

    [Fact]
    public void Shorter_than_the_minimum_is_rejected() =>
        Assert.Contains(BookingRuleCodes.DurationTooShort, Codes(TuesdayNoon, TuesdayNoon.AddMinutes(10)));

    [Fact]
    public void Longer_than_the_maximum_is_rejected() =>
        Assert.Contains(BookingRuleCodes.DurationTooLong, Codes(TuesdayNoon, TuesdayNoon.AddHours(5)));

    [Fact]
    public void Exactly_the_maximum_is_accepted() =>
        Assert.Empty(Codes(TuesdayNoon, TuesdayNoon.AddHours(4)));

    [Theory]
    [InlineData(0, 50)]
    [InlineData(5, 60)]
    public void Start_and_end_must_fall_on_a_quarter_of_an_hour(int startMinute, int endMinute) =>
        Assert.Contains(
            BookingRuleCodes.NotOnSlotBoundary,
            Codes(TuesdayNoon.AddMinutes(startMinute), TuesdayNoon.AddMinutes(endMinute)));

    [Fact]
    public void Saturday_is_outside_office_hours()
    {
        var saturday = new DateTimeOffset(2026, 10, 24, 10, 0, 0, TimeSpan.Zero);

        Assert.Contains(BookingRuleCodes.OutsideOfficeHours, Codes(saturday, saturday.AddHours(1)));
    }

    [Theory]
    [InlineData(5, 6)] // 07:00-08:00 local: opens at 08:00
    [InlineData(17, 19)] // 19:00-21:00 local: closes at 20:00
    public void Hours_outside_the_opening_window_are_rejected(int startHourUtc, int endHourUtc)
    {
        var start = new DateTimeOffset(2026, 10, 20, startHourUtc, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2026, 10, 20, endHourUtc, 0, 0, TimeSpan.Zero);

        Assert.Contains(BookingRuleCodes.OutsideOfficeHours, Codes(start, end));
    }

    [Fact]
    public void A_booking_that_ends_exactly_at_closing_time_is_accepted()
    {
        var start = new DateTimeOffset(2026, 10, 20, 17, 0, 0, TimeSpan.Zero); // 19:00 local

        Assert.Empty(Codes(start, start.AddHours(1)));
    }

    [Fact]
    public void Office_hours_follow_the_local_clock_across_the_end_of_summer_time()
    {
        // 06:00 UTC is 08:00 in the office in summer time (UTC+2) and 07:00 after the clocks go back (UTC+1).
        var beforeChange = new DateTimeOffset(2026, 10, 19, 6, 0, 0, TimeSpan.Zero);
        var afterChange = new DateTimeOffset(2026, 10, 26, 6, 0, 0, TimeSpan.Zero);

        Assert.Empty(Codes(beforeChange, beforeChange.AddHours(1), now: Now.AddDays(-1)));
        Assert.Contains(BookingRuleCodes.OutsideOfficeHours, Codes(afterChange, afterChange.AddHours(1)));
    }

    [Fact]
    public void A_booking_that_starts_in_less_than_the_lead_time_is_rejected() =>
        Assert.Contains(BookingRuleCodes.TooSoon, Codes(Now, Now.AddHours(1)));

    [Fact]
    public void A_booking_that_starts_exactly_at_the_lead_time_is_accepted() =>
        Assert.DoesNotContain(BookingRuleCodes.TooSoon, Codes(Now.AddMinutes(15), Now.AddMinutes(75)));

    [Fact]
    public void A_booking_too_far_ahead_is_rejected()
    {
        var start = Now.AddDays(91); // Monday 2027-01-18, 08:00 local

        Assert.Contains(BookingRuleCodes.TooFarAhead, Codes(start, start.AddHours(1)));
    }

    [Fact]
    public void More_attendees_than_seats_is_rejected() =>
        Assert.Contains(BookingRuleCodes.ExceedsCapacity, Codes(TuesdayNoon, TuesdayNoon.AddHours(1), attendees: 9, capacity: 8));

    [Fact]
    public void Several_broken_rules_are_all_reported() =>
        Assert.Equal(
            [BookingRuleCodes.DurationTooLong, BookingRuleCodes.ExceedsCapacity],
            Codes(TuesdayNoon, TuesdayNoon.AddHours(5), attendees: 20, capacity: 8));

    [Fact]
    public void An_employee_cannot_cancel_inside_the_notice_period()
    {
        var start = Now.AddMinutes(14);

        Assert.Equal(BookingRuleCodes.TooLateToCancel, Policy.EvaluateCancellation(start, Now, isFacilities: false)?.Code);
    }

    [Fact]
    public void An_employee_can_cancel_exactly_at_the_notice_limit() =>
        Assert.Null(Policy.EvaluateCancellation(Now.AddMinutes(15), Now, isFacilities: false));

    [Fact]
    public void Facilities_can_cancel_at_any_time() =>
        Assert.Null(Policy.EvaluateCancellation(Now.AddMinutes(1), Now, isFacilities: true));
}