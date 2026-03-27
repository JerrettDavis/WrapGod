using BenchmarkDotNet.Attributes;
using WrapGod.Manifest;

namespace WrapGod.Benchmarks;

/// <summary>
/// Benchmarks for manifest parsing and source emission using synthetic manifest data.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 2, iterationCount: 5)]
public class GeneratorBenchmarks
{
    private string _smallManifestJson = null!;
    private string _mediumManifestJson = null!;

    [GlobalSetup]
    public void Setup()
    {
        _smallManifestJson = ManifestSerializer.Serialize(BuildSyntheticManifest(typeCount: 10, membersPerType: 5));
        _mediumManifestJson = ManifestSerializer.Serialize(BuildSyntheticManifest(typeCount: 50, membersPerType: 10));
    }

    [Benchmark(Description = "Parse small manifest (10 types)")]
    public ApiManifest? ParseSmallManifest() =>
        ManifestSerializer.Deserialize(_smallManifestJson);

    [Benchmark(Description = "Parse medium manifest (50 types)")]
    public ApiManifest? ParseMediumManifest() =>
        ManifestSerializer.Deserialize(_mediumManifestJson);

    [Benchmark(Description = "Serialize small manifest (10 types)")]
    public string SerializeSmallManifest() =>
        ManifestSerializer.Serialize(ManifestSerializer.Deserialize(_smallManifestJson)!);

    [Benchmark(Description = "Serialize medium manifest (50 types)")]
    public string SerializeMediumManifest() =>
        ManifestSerializer.Serialize(ManifestSerializer.Deserialize(_mediumManifestJson)!);

    private static ApiManifest BuildSyntheticManifest(int typeCount, int membersPerType)
    {
        var types = new List<ApiTypeNode>();

        for (var t = 0; t < typeCount; t++)
        {
            var members = new List<ApiMemberNode>();
            for (var m = 0; m < membersPerType; m++)
            {
                members.Add(new ApiMemberNode
                {
                    StableId = $"Synthetic.Type{t}.Method{m}()",
                    Name = $"Method{m}",
                    Kind = ApiMemberKind.Method,
                    ReturnType = "System.Void",
                    Parameters =
                    [
                        new ApiParameterInfo
                        {
                            Name = "arg0",
                            Type = "System.String",
                        },
                    ],
                });
            }

            types.Add(new ApiTypeNode
            {
                StableId = $"Synthetic.Type{t}",
                FullName = $"Synthetic.Type{t}",
                Name = $"Type{t}",
                Namespace = "Synthetic",
                Kind = ApiTypeKind.Class,
                Members = members,
            });
        }

        return new ApiManifest
        {
            SchemaVersion = "1.0",
            GeneratedAt = DateTimeOffset.UtcNow,
            SourceHash = "benchmark-synthetic",
            Assembly = new AssemblyIdentity
            {
                Name = "Synthetic.Assembly",
                Version = "1.0.0.0",
            },
            Types = types,
        };
    }
}
