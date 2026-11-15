using EntryHub.Api.Domain.Rooms;

namespace EntryHub.Api.Features.V1.Rooms;

/// <summary>
/// The <c>features</c> filter: catalog codes separated by commas, and a room must have all of them.
/// </summary>
internal static class FeatureFilter
{
    public static IQueryable<Room> Apply(IQueryable<Room> rooms, string? codes)
    {
        foreach (var code in Parse(codes))
        {
            var wanted = code;
            rooms = rooms.Where(room => room.Features.Any(feature => feature.Code == wanted));
        }

        return rooms;
    }

    private static IEnumerable<string> Parse(string? codes) =>
        (codes ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(code => code.ToLowerInvariant()).Distinct();
}