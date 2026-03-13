using BenchmarkDotNet.Running;
using BenchmarkDotNet.Configs;

namespace TermRTS.Benchmark;

internal static class Program
{
    public static void Main(string[] args)
    {
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
    }
}
