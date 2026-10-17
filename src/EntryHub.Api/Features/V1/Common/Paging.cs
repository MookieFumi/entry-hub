namespace EntryHub.Api.Features.V1.Common;

/// <summary>
/// Page numbers and sizes as requested by a client, forced into a safe range.
/// </summary>
public readonly record struct Paging(int Page, int PageSize)
{
    public const int DefaultPageSize = 20;

    public const int MaxPageSize = 100;

    public int Skip => (Page - 1) * PageSize;

    public static Paging Normalize(int? page, int? pageSize) =>
        new(
            Math.Max(page ?? 1, 1),
            Math.Clamp(pageSize ?? DefaultPageSize, 1, MaxPageSize));
}