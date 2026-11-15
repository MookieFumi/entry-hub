using EntryHub.Api.Domain.Bookings;
using EntryHub.Api.Domain.Sites;

namespace EntryHub.Api.Features.V1.Bookings.CreateBooking;

/// <summary>
/// One occurrence the recurrence asked for. <paramref name="Violation"/> is set when its wall-clock time does not
/// exist (it falls in the hour that clocks skip when daylight saving starts).
/// </summary>
internal sealed record PlannedOccurrence(int Position, DateTimeOffset StartAt, DateTimeOffset EndAt, RuleViolation? Violation);

internal sealed record SeriesPlan(IReadOnlyList<PlannedOccurrence> Occurrences, RuleViolation? Violation);

/// <summary>
/// Turns a recurrence into the start and end of each occurrence. The days are counted in the time zone of the site and
/// the time of day is kept, so a 10:00 booking is still at 10:00 after the clocks change.
/// </summary>
internal static class BookingSeriesOccurrences
{
    public static SeriesPlan Plan(RecurrenceRequest recurrence, DateTimeOffset start, DateTimeOffset end, SiteHours site)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(site.TimeZoneId);
        var localStart = TimeZoneInfo.ConvertTime(start, zone).DateTime;
        var wallDuration = TimeZoneInfo.ConvertTime(end, zone).DateTime - localStart;
        var stepDays = recurrence.Interval * (recurrence.Frequency == RecurrenceFrequency.Weekly ? 7 : 1);
        var occurrences = new List<PlannedOccurrence>();

        for (var index = 0; ; index++)
        {
            var local = localStart.AddDays((long)index * stepDays);

            if (recurrence.Count is { } count ? index >= count : DateOnly.FromDateTime(local) > recurrence.Until)
            {
                break;
            }

            if (index >= BookingSeries.MaxOccurrences)
            {
                return new(
                    [],
                    new(BookingRuleCodes.InvalidRecurrence, $"A series can have at most {BookingSeries.MaxOccurrences} occurrences; end it earlier."));
            }

            occurrences.Add(index == 0 ? new(1, start, end, null) : Occurrence(index + 1, local, wallDuration, zone));
        }

        return occurrences.Count == 0
            ? new([], new(BookingRuleCodes.InvalidRecurrence, "The recurrence ends before the first occurrence."))
            : new(occurrences, null);
    }

    private static PlannedOccurrence Occurrence(int position, DateTime local, TimeSpan wallDuration, TimeZoneInfo zone)
    {
        if (zone.IsInvalidTime(local) || zone.IsInvalidTime(local + wallDuration))
        {
            // Only to show which occurrence was skipped: the hour after the gap holds the same wall-clock time.
            var shown = local.AddHours(1);
            return new(
                position,
                new(TimeZoneInfo.ConvertTimeToUtc(shown, zone), TimeSpan.Zero),
                new(TimeZoneInfo.ConvertTimeToUtc(shown + wallDuration, zone), TimeSpan.Zero),
                new(BookingRuleCodes.NonexistentLocalTime, $"{local:yyyy-MM-dd HH:mm} does not exist in {zone.Id} because the clocks skip that hour."));
        }

        return new(
            position,
            new(TimeZoneInfo.ConvertTimeToUtc(local, zone), TimeSpan.Zero),
            new(TimeZoneInfo.ConvertTimeToUtc(local + wallDuration, zone), TimeSpan.Zero),
            null);
    }
}