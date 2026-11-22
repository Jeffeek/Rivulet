using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.InProcess.Emit;

namespace Rivulet.Benchmarks;

// ReSharper disable once MemberCanBeInternal
public sealed class Program
{
    public static void Main(string[] args)
    {
        // BenchmarkDotNet 0.15.8 has no RuntimeMoniker.Net11_0. Two consequences we work around here:
        //
        // 1. The CsProj toolchain (used by [SimpleJob(RuntimeMoniker.Net*)]) can't build/run net11.0 — its
        //    DotNetSdkValidator throws NotImplementedException on the unrecognized moniker. So every benchmark
        //    runs IN-PROCESS instead (no child build), and the runtime measured is whatever the runner is
        //    launched with. Pick it per invocation:
        //        dotnet run -c Release -f net10.0 -- <filters>
        //        dotnet run -c Release -f net11.0 -- <filters>
        //
        // 2. DotNetSdkValidator is also a *default* validator in DefaultConfig.Instance and runs against the
        //    host runtime regardless of toolchain — on net11.0 it hits the same crash. So we copy DefaultConfig
        //    but drop that one validator.
        //
        // Do NOT pass `--job <name>` (e.g. --job short) on the command line: that injects a CsProj-toolchain
        // job which overrides the in-process job and re-triggers the broken validator on net11.0. Use
        // [SimpleJobAttribute]-free tuning via env/args that don't swap the toolchain if you need fewer iterations.
        //
        // TODO: restore [SimpleJob(RuntimeMoniker.Net10_0)] + [SimpleJob(RuntimeMoniker.Net11_0)] on each
        // benchmark class and delete this whole custom config once BenchmarkDotNet ships the Net11_0 moniker.
        var config = ManualConfig.CreateEmpty()
            .WithOptions(ConfigOptions.JoinSummary)
            .AddJob(Job.Default.WithToolchain(InProcessEmitToolchain.Instance));

        var defaults = DefaultConfig.Instance;
        foreach (var columnProvider in defaults.GetColumnProviders()) config.AddColumnProvider(columnProvider);
        foreach (var exporter in defaults.GetExporters()) config.AddExporter(exporter);
        foreach (var logger in defaults.GetLoggers()) config.AddLogger(logger);
        foreach (var diagnoser in defaults.GetDiagnosers()) config.AddDiagnoser(diagnoser);
        foreach (var analyser in defaults.GetAnalysers()) config.AddAnalyser(analyser);
        foreach (var validator in defaults.GetValidators())
            if (validator.GetType().Name != "DotNetSdkValidator")
                config.AddValidator(validator);

        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config);
    }
}
