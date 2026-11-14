namespace EntryHub.Api.Features.V1.Rooms;

/// <summary>
/// The time window of an availability or schedule request: both ends present, in order, and not wider than a month.
/// </summary>
internal static class ScheduleWindow
{
    public static readonly TimeSpan MaxLength = TimeSpan.FromDays(31);

    public static Dictionary<string, string[]>? Validate(DateTimeOffset? from, DateTimeOffset? to)
    {
        var errors = new Dictionary<string, string[]>();

        if (from is null)
        {
            errors["from"] = ["'from' is required."];
        }

        if (to is null)
        {
            errors["to"] = ["'to' is required."];
        }

        if (from is { } start && to is { } end)
        {
            if (end <= start)
            {
                errors["to"] = ["'to' must be after 'from'."];
            }
            else if (end - start > MaxLength)
            {
                errors["to"] = [$"The window cannot be longer than {MaxLength.TotalDays:0} days."];
            }
        }

        return errors.Count == 0 ? null : errors;
    }
}