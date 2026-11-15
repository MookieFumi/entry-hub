namespace EntryHub.Api.Domain.Features;

/// <summary>
/// The catalog every new tenant starts with.
/// </summary>
public static class DefaultCatalog
{
    public static readonly IReadOnlyList<(string Name, FeatureKind Kind)> Entries =
    [
        ("Projector", FeatureKind.Equipment),
        ("Screen", FeatureKind.Equipment),
        ("Video conferencing", FeatureKind.Equipment),
        ("Whiteboard", FeatureKind.Equipment),
        ("Wheelchair access", FeatureKind.Accessibility),
        ("Induction loop", FeatureKind.Accessibility),
    ];
}