using BenchmarkDotNet.Running;

var benchmarkArgs = args.Length == 0 ? ["--filter", "*"] : args;
var summaries = BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(benchmarkArgs).ToArray();
return summaries.Length > 0 && summaries.All(summary =>
    !summary.HasCriticalValidationErrors && summary.Reports.Length > 0 &&
    summary.Reports.All(report => report.Success))
    ? 0
    : 1;
