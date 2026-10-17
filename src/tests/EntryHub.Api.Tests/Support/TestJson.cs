using System.Text.Json;
using System.Text.Json.Serialization;

namespace EntryHub.Api.Tests.Support;

internal static class TestJson
{
    /// <summary>
    /// The options of the API, so tests read enums the way clients see them: as text.
    /// </summary>
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };
}