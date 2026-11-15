using EntryHub.Api.Data;

using Microsoft.EntityFrameworkCore;

using Npgsql;

namespace EntryHub.Api.Features.V1.Features.UpdateFeature;

public enum UpdateFeatureOutcome
{
    Updated,
    NotFound,
    NameInUse,
}

public sealed class UpdateFeatureHandler(EntryHubDbContext db)
{
    public async Task<(UpdateFeatureOutcome Outcome, FeatureResponse? Feature)> HandleAsync(
        Guid id,
        UpdateFeatureRequest request,
        CancellationToken cancellationToken)
    {
        var feature = await db.Features.SingleOrDefaultAsync(feature => feature.Id == id, cancellationToken);

        if (feature is null)
        {
            return (UpdateFeatureOutcome.NotFound, null);
        }

        if (request.Name?.Trim() is { Length: > 0 } name && name != feature.Name)
        {
            var others = await db.Features.AsNoTracking().Where(other => other.Id != id).ToListAsync(cancellationToken);

            if (others.Exists(other => string.Equals(other.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                return (UpdateFeatureOutcome.NameInUse, null);
            }

            feature.Name = name;
        }

        feature.Kind = request.Kind is { } kind && Enum.IsDefined(kind) ? kind : feature.Kind;
        feature.IsActive = request.IsActive ?? feature.IsActive;

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return (UpdateFeatureOutcome.NameInUse, null);
        }

        return (UpdateFeatureOutcome.Updated, FeatureResponse.From(feature));
    }
}