using BenchmarkDotNet.Attributes;
using Dune.Engine.Configuration;
using Dune.Engine.Fixtures;
using Dune.Engine.Rulesets;
using Dune.Engine.Runtime;
using Dune.Engine.Setup;

namespace Dune.Benchmarks;

/// <summary>Synthetic catalog, authentic base setup quantities. No card effects or game turns.</summary>
[MemoryDiagnoser]
public class GameInitializationBenchmarks
{
    private GameConfiguration _configuration = null!;
    private ResolvedGameDefinition _definition = null!;
    private readonly GameInitializer _initializer = new();

    [Params(3, 4)]
    public int Players { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _configuration = OriginalImperiumFixture.Configuration(Players);
        _definition = OriginalImperiumFixture.Resolve(_configuration);
        // Measure repeated initialization from a composed, statically validated definition.
        // Configuration validation and all episode-local allocations remain in the operation.
        if (!_initializer.InitializeFixture(_configuration, _definition).IsSuccess)
            throw new InvalidOperationException("Synthetic setup benchmark could not initialize.");
    }

    [Benchmark]
    public GameState InitializeResolvedFixture() =>
        _initializer.InitializeFixture(_configuration, _definition).State
        ?? throw new InvalidOperationException("Synthetic initialization failed.");
}
