using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;

namespace Rivulet.Benchmarks;

// ReSharper disable once MemberCanBeInternal
public sealed class Program
{
    public static void Main(string[] args)
    {
        var config = DefaultConfig.Instance
            .WithOptions(ConfigOptions.JoinSummary);

        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config);
    }
}