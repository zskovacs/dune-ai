using BenchmarkDotNet.Attributes;
using Dune.Engine.Toy;
using Dune.Simulation;

namespace Dune.Benchmarks;

/// <summary>Full autonomous toy episode, including fresh game/agents, seeded setup and action trace.</summary>
[MemoryDiagnoser]
public class ToyEpisodeBenchmarks
{
    private readonly ToyConfig _config = new();
    private readonly EpisodeRunner<ToyConfig, ToyObservation, ToyAction, ToyResult> _runner = new(
        () => new ToyGame(), (_, seed) => new RandomAgent<ToyObservation, ToyAction>(seed));

    [Benchmark(Baseline = true)]
    public EpisodeResult<ToyAction, ToyResult> CompleteEpisode() => _runner.Run(42, _config);
}
