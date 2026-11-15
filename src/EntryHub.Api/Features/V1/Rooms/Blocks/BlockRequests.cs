using System.ComponentModel.DataAnnotations;

namespace EntryHub.Api.Features.V1.Rooms.Blocks;

/// <param name="StartAt">First moment of the block.</param>
/// <param name="EndAt">The block ends here; must be after <paramref name="StartAt"/>.</param>
/// <param name="Reason">Optional explanation.</param>
public sealed record CreateBlockRequest(DateTimeOffset StartAt, DateTimeOffset EndAt, [property: StringLength(200)] string? Reason) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (EndAt <= StartAt)
        {
            yield return new ValidationResult("EndAt must be after StartAt.", [nameof(EndAt)]);
        }
    }
}

/// <param name="From">Only blocks that end after this moment.</param>
/// <param name="To">Only blocks that start before this moment.</param>
public sealed record ListBlocksQuery(DateTimeOffset? From, DateTimeOffset? To);