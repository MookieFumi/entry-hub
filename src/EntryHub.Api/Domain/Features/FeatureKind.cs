using System.Text.Json.Serialization;

namespace EntryHub.Api.Domain.Features;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FeatureKind
{
    Equipment,
    Accessibility,
}