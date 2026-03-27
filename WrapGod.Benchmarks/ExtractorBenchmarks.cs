using BenchmarkDotNet.Attributes;
using WrapGod.Extractor;
using WrapGod.Manifest;

namespace WrapGod.Benchmarks;

/// <summary>
/// Benchmarks for <see cref="AssemblyExtractor.Extract"/> across assemblies of varying size.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 2, iterationCount: 5)]
public class ExtractorBenchmarks
{
    private string _smallAssemblyPath = null!;
    private string _mediumAssemblyPath = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Small: System.Private.CoreLib (typeof(object)) — lean but always available
        _smallAssemblyPath = typeof(object).Assembly.Location;

        // Medium: System.Text.Json — broader API surface
        _mediumAssemblyPath = typeof(System.Text.Json.JsonSerializer).Assembly.Location;
    }

    [Benchmark(Description = "Extract small assembly (CoreLib)")]
    public ApiManifest ExtractSmallAssembly() =>
        AssemblyExtractor.Extract(_smallAssemblyPath);

    [Benchmark(Description = "Extract medium assembly (System.Text.Json)")]
    public ApiManifest ExtractMediumAssembly() =>
        AssemblyExtractor.Extract(_mediumAssemblyPath);
}
