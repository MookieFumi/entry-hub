using System.Globalization;

using EntryHub.Api.Domain.Sites;

namespace EntryHub.Api.Features.V1.Sites;

/// <summary>
/// Checks the fields of a site that have more rules than a data annotation can say.
/// </summary>
internal static class SiteInput
{
    public static bool TryParseTime(string? text, out TimeOnly time) =>
        TimeOnly.TryParseExact(text, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out time);

    public static bool IsKnownTimeZone(string? id) =>
        !string.IsNullOrWhiteSpace(id) && id.Contains('/', StringComparison.Ordinal) && TimeZoneInfo.TryFindSystemTimeZoneById(id, out _);

    /// <summary>
    /// Validates the fields that are present and returns the parsed hours, or the errors by field name.
    /// </summary>
    public static Dictionary<string, string[]> Validate(
        string? timeZone,
        string? opens,
        string? closes,
        IReadOnlyCollection<DayOfWeek>? openDays,
        Site? current)
    {
        var errors = new Dictionary<string, string[]>();

        if (timeZone is not null && !IsKnownTimeZone(timeZone))
        {
            errors["timeZone"] = ["Use an IANA time zone such as 'Europe/Madrid'."];
        }

        var opensAt = current?.Opens;
        var closesAt = current?.Closes;

        if (opens is not null)
        {
            if (TryParseTime(opens, out var parsed))
            {
                opensAt = parsed;
            }
            else
            {
                errors["opens"] = ["Use the format HH:mm."];
            }
        }

        if (closes is not null)
        {
            if (TryParseTime(closes, out var parsed))
            {
                closesAt = parsed;
            }
            else
            {
                errors["closes"] = ["Use the format HH:mm."];
            }
        }

        if (!errors.ContainsKey("opens") && !errors.ContainsKey("closes") && opensAt is { } start && closesAt is { } end && end <= start)
        {
            errors["closes"] = ["'closes' must be after 'opens'."];
        }

        if (openDays is not null && (openDays.Count == 0 || openDays.Any(day => !Enum.IsDefined(day))))
        {
            errors["openDays"] = ["Give at least one valid day of the week."];
        }

        return errors;
    }
}