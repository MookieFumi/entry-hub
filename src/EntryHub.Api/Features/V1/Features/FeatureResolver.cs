using EntryHub.Api.Data;
using EntryHub.Api.Domain.Features;

using Microsoft.EntityFrameworkCore;

namespace EntryHub.Api.Features.V1.Features;

/// <summary>
/// Turns what a request says a room has into entries of the catalog.
/// </summary>
public sealed class FeatureResolver(EntryHubDbContext db, TimeProvider clock)
{
    /// <summary>
    /// <paramref name="featureIds"/> must be active entries of the tenant. <paramref name="names"/> are matched to an
    /// entry by name, ignoring case; a name nobody has used yet becomes a new equipment entry, so older clients that
    /// send free text keep working. Returns <see langword="null"/> when neither is sent: the caller keeps what it has.
    /// </summary>
    /// <returns>The entries, or the id of the entry that is unknown or deactivated.</returns>
    public async Task<(List<Feature>? Features, Guid? UnknownId)> ResolveAsync(
        IEnumerable<Guid>? featureIds,
        IEnumerable<string>? names,
        CancellationToken cancellationToken)
    {
        if (featureIds is null && names is null)
        {
            return (null, null);
        }

        var resolved = new Dictionary<Guid, Feature>();

        var ids = featureIds?.Distinct().ToList() ?? [];
        if (ids.Count > 0)
        {
            var found = await db.Features.Where(feature => ids.Contains(feature.Id) && feature.IsActive).ToListAsync(cancellationToken);

            if (ids.FirstOrDefault(id => found.TrueForAll(feature => feature.Id != id)) is var missing && missing != Guid.Empty)
            {
                return (null, missing);
            }

            foreach (var feature in found)
            {
                resolved[feature.Id] = feature;
            }
        }

        var wanted = names?.Select(name => name.Trim()).Where(name => name.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList() ?? [];
        if (wanted.Count > 0)
        {
            var catalog = await db.Features.ToListAsync(cancellationToken);

            foreach (var name in wanted)
            {
                var feature = catalog.Find(existing => string.Equals(existing.Name, name, StringComparison.OrdinalIgnoreCase));

                if (feature is null)
                {
                    var code = Feature.CodeFor(name);

                    if (code.Length == 0)
                    {
                        continue;
                    }

                    // Names that only differ in punctuation share a code: the entry that has it is the one meant.
                    feature = catalog.Find(existing => existing.Code == code);

                    if (feature is null)
                    {
                        feature = new Feature { Id = Guid.CreateVersion7(), Code = code, Name = name, CreatedAt = clock.GetUtcNow() };
                        db.Features.Add(feature);
                        catalog.Add(feature);
                    }
                }

                if (!feature.IsActive)
                {
                    return (null, feature.Id);
                }

                resolved[feature.Id] = feature;
            }
        }

        return ([.. resolved.Values], null);
    }
}