using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Dune.Engine.Tests")]
[assembly: InternalsVisibleTo("Dune.Benchmarks")]
// The explicit --turn-preview command links the same labeled synthetic inventory as tests/benchmarks.
[assembly: InternalsVisibleTo("Dune.Cli")]
