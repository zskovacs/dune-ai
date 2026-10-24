using System.Text.Json;
using BenchmarkDotNet.Attributes;
using Dune.Engine.Configuration;
using Dune.Engine.Content;
using Dune.Engine.Fixtures;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using StjSerializer = System.Text.Json.JsonSerializer;
using JsonNetSerializer = Newtonsoft.Json.JsonConvert;

namespace Dune.Benchmarks;

/// <summary>Representative JSON operations; fixture construction and settings are outside measurement.</summary>
[MemoryDiagnoser]
public class JsonBenchmarks
{
    private static readonly JsonSerializerSettings JsonNetSettings = new()
    {
        ContractResolver = new CamelCasePropertyNamesContractResolver(),
        MissingMemberHandling = MissingMemberHandling.Error,
        TypeNameHandling = TypeNameHandling.None,
        Converters = { new StringEnumConverter { AllowIntegerValues = false } },
    };

    private GameConfiguration _configuration = null!;
    private ContentCatalog _catalog = null!;
    private string _configurationJson = null!;
    private string _snapshotJson = null!;

    [GlobalSetup]
    public void Setup()
    {
        _configuration = OriginalImperiumFixture.Configuration();
        _catalog = ContentCatalog.LoadRepresentative();
        _configurationJson = ConfigurationJson.Serialize(_configuration);
        _snapshotJson = OriginalImperiumFixture.Create().CaptureTrustedSnapshot().CanonicalJson;

        var jsonNetConfigurationJson = JsonNetSerializer.SerializeObject(_configuration, JsonNetSettings);
        var stjCatalogJson = StjSerializer.Serialize(_catalog, ConfigurationJson.Options);
        var jsonNetCatalogJson = JsonNetSerializer.SerializeObject(_catalog, JsonNetSettings);
        if (!JToken.DeepEquals(JToken.Parse(_configurationJson), JToken.Parse(jsonNetConfigurationJson)) ||
            !JToken.DeepEquals(JToken.Parse(stjCatalogJson), JToken.Parse(jsonNetCatalogJson)))
            throw new InvalidOperationException("JSON serializers produced different configuration or catalog values.");
        if (!string.Equals(_configurationJson, jsonNetConfigurationJson, StringComparison.Ordinal) ||
            !string.Equals(stjCatalogJson, jsonNetCatalogJson, StringComparison.Ordinal))
            throw new InvalidOperationException("JSON serializers produced different configuration or catalog bytes.");
        var stjRead = ConfigurationJson.Deserialize(jsonNetConfigurationJson);
        var jsonNetRead = JsonNetSerializer.DeserializeObject<GameConfiguration>(_configurationJson, JsonNetSettings);
        if (jsonNetRead is null ||
            !JToken.DeepEquals(JToken.Parse(ConfigurationJson.Serialize(stjRead)), JToken.Parse(_configurationJson)) ||
            !JToken.DeepEquals(JToken.Parse(ConfigurationJson.Serialize(jsonNetRead)),
                JToken.Parse(_configurationJson)))
            throw new InvalidOperationException("Configuration round-trip differs between serializers.");
    }

    [Benchmark, BenchmarkCategory("ConfigurationSerialize")]
    public string ConfigurationSerializeStj() => ConfigurationJson.Serialize(_configuration);

    [Benchmark, BenchmarkCategory("ConfigurationSerialize")]
    public string ConfigurationSerializeJsonNet() => JsonNetSerializer.SerializeObject(_configuration, JsonNetSettings);

    [Benchmark, BenchmarkCategory("ConfigurationDeserialize")]
    public GameConfiguration ConfigurationDeserializeStj() => ConfigurationJson.Deserialize(_configurationJson);

    [Benchmark, BenchmarkCategory("ConfigurationDeserialize")]
    public GameConfiguration ConfigurationDeserializeJsonNet() =>
        JsonNetSerializer.DeserializeObject<GameConfiguration>(_configurationJson, JsonNetSettings)!;

    [Benchmark, BenchmarkCategory("CatalogSerialize")]
    public string CatalogSerializeStj() => StjSerializer.Serialize(_catalog, ConfigurationJson.Options);

    [Benchmark, BenchmarkCategory("CatalogSerialize")]
    public string CatalogSerializeJsonNet() => JsonNetSerializer.SerializeObject(_catalog, JsonNetSettings);

    [Benchmark, BenchmarkCategory("SnapshotDomParse")]
    public int SnapshotDomParseStj()
    {
        using var document = JsonDocument.Parse(_snapshotJson);
        return (int)document.RootElement.ValueKind;
    }

    [Benchmark, BenchmarkCategory("SnapshotDomParse")]
    public int SnapshotDomParseJsonNet() => (int)JToken.Parse(_snapshotJson).Type;
}
