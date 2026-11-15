using EntryHub.Api.Data;
using EntryHub.Api.Domain.Features;

using Microsoft.EntityFrameworkCore;

using Npgsql;

namespace EntryHub.Api.Features.V1.Features.CreateFeature;

public enum CreateFeatureOutcome
{
    Created,
    Invalid,
    InUse,
}

public sealed class CreateFeatureHandler(EntryHubDbContext db, TimeProvider clock)
{
    public async Task<(CreateFeatureOutcome Outcome, FeatureResponse? Feature)> HandleAsync(CreateFeatureRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        var code = Feature.CodeFor(name);

        if (code.Length == 0 || !Enum.IsDefined(request.Kind))
        {
            return (CreateFeatureOutcome.Invalid, null);
        }

        var catalog = await db.Features.AsNoTracking().ToListAsync(cancellationToken);

        if (catalog.Exists(feature => feature.Code == code || string.Equals(feature.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            return (CreateFeatureOutcome.InUse, null);
        }

        var feature = new Feature { Id = Guid.CreateVersion7(), Code = code, Name = name, Kind = request.Kind, CreatedAt = clock.GetUtcNow() };
        db.Features.Add(feature);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return (CreateFeatureOutcome.InUse, null);
        }

        return (CreateFeatureOutcome.Created, FeatureResponse.From(feature));
    }
}