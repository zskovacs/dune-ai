using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dune.Engine.Configuration;

/// <summary>Configuration persistence; Compose performs semantic validation after deserialization.</summary>
public static class ConfigurationJson
{
    internal static JsonSerializerOptions Options { get; } = CreateOptions();

    public static string Serialize(GameConfiguration configuration) => JsonSerializer.Serialize(configuration, Options);
    public static GameConfiguration Deserialize(string json) =>
        JsonSerializer.Deserialize<GameConfiguration>(json, Options) ?? throw new JsonException("A configuration object is required.");

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        };
        options.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }
}
