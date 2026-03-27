using WrapGod.Extractor;
using WrapGod.Manifest;

namespace WrapGod.Tests;

public class MultiVersionExtractorTests
{
    private static readonly string CoreLibPath = typeof(object).Assembly.Location;

    // Helper: extract the same assembly under two different version labels.
    // Since the assembly is identical, there should be zero diffs.
    private static MultiVersionExtractor.MultiVersionResult ExtractSameAssemblyTwice()
    {
        var versions = new List<MultiVersionExtractor.VersionInput>
        {
            new("1.0.0", CoreLibPath),
            new("2.0.0", CoreLibPath),
        };
        return MultiVersionExtractor.Extract(versions);
    }

    [Fact]
    public void Extract_SingleVersion_ProducesMergedManifestWithPresence()
    {
        var versions = new List<MultiVersionExtractor.VersionInput>
        {
            new("1.0.0", CoreLibPath),
        };

        var result = MultiVersionExtractor.Extract(versions);

        Assert.NotNull(result.MergedManifest);
        Assert.NotEmpty(result.MergedManifest.Types);

        // Every type should have presence with IntroducedIn set to the single version.
        foreach (var type in result.MergedManifest.Types)
        {
            Assert.NotNull(type.Presence);
            Assert.Equal("1.0.0", type.Presence!.IntroducedIn);
            Assert.Null(type.Presence.RemovedIn);
        }
    }

    [Fact]
    public void Extract_TwoIdenticalVersions_ProducesNoDiffs()
    {
        var result = ExtractSameAssemblyTwice();

        Assert.Empty(result.Diff.AddedTypes);
        Assert.Empty(result.Diff.RemovedTypes);
        Assert.Empty(result.Diff.AddedMembers);
        Assert.Empty(result.Diff.RemovedMembers);
        Assert.Empty(result.Diff.ChangedMembers);
        Assert.Empty(result.Diff.BreakingChanges);
    }

    [Fact]
    public void Extract_TwoIdenticalVersions_AllTypesHavePresence()
    {
        var result = ExtractSameAssemblyTwice();

        foreach (var type in result.MergedManifest.Types)
        {
            Assert.NotNull(type.Presence);
            Assert.Equal("1.0.0", type.Presence!.IntroducedIn);
            Assert.Null(type.Presence.RemovedIn);
        }
    }

    [Fact]
    public void Extract_TwoIdenticalVersions_AllMembersHavePresence()
    {
        var result = ExtractSameAssemblyTwice();

        var members = result.MergedManifest.Types
            .SelectMany(t => t.Members)
            .Take(200)
            .ToList();

        Assert.NotEmpty(members);

        foreach (var member in members)
        {
            Assert.NotNull(member.Presence);
            Assert.Equal("1.0.0", member.Presence!.IntroducedIn);
        }
    }

    [Fact]
    public void Extract_TwoIdenticalVersions_DiffVersionListIsCorrect()
    {
        var result = ExtractSameAssemblyTwice();

        Assert.Equal(2, result.Diff.Versions.Count);
        Assert.Equal("1.0.0", result.Diff.Versions[0]);
        Assert.Equal("2.0.0", result.Diff.Versions[1]);
    }

    [Fact]
    public void Extract_EmptyVersionList_ThrowsArgumentException()
    {
        var versions = new List<MultiVersionExtractor.VersionInput>();

        Assert.Throws<ArgumentException>(() => MultiVersionExtractor.Extract(versions));
    }

    [Fact]
    public void Merge_DetectsAddedType()
    {
        // v1 has no types; v2 has one type.
        var v1Manifest = new ApiManifest { Types = [] };

        var v2Manifest = new ApiManifest
        {
            Types =
            [
                new ApiTypeNode
                {
                    StableId = "MyNamespace.NewClass",
                    FullName = "MyNamespace.NewClass",
                    Name = "NewClass",
                    Namespace = "MyNamespace",
                    Kind = ApiTypeKind.Class,
                },
            ],
        };

        var result = MultiVersionExtractor.Merge(
        [
            ("1.0.0", v1Manifest),
            ("2.0.0", v2Manifest),
        ]);

        Assert.Single(result.Diff.AddedTypes);
        Assert.Equal("MyNamespace.NewClass", result.Diff.AddedTypes[0].StableId);
        Assert.Equal("2.0.0", result.Diff.AddedTypes[0].IntroducedIn);

        var mergedType = Assert.Single(result.MergedManifest.Types);
        Assert.Equal("2.0.0", mergedType.Presence?.IntroducedIn);
    }

    [Fact]
    public void Merge_DetectsRemovedType()
    {
        var v1Manifest = new ApiManifest
        {
            Types =
            [
                new ApiTypeNode
                {
                    StableId = "MyNamespace.OldClass",
                    FullName = "MyNamespace.OldClass",
                    Name = "OldClass",
                    Namespace = "MyNamespace",
                    Kind = ApiTypeKind.Class,
                },
            ],
        };

        var v2Manifest = new ApiManifest { Types = [] };

        var result = MultiVersionExtractor.Merge(
        [
            ("1.0.0", v1Manifest),
            ("2.0.0", v2Manifest),
        ]);

        Assert.Single(result.Diff.RemovedTypes);
        Assert.Equal("MyNamespace.OldClass", result.Diff.RemovedTypes[0].StableId);
        Assert.Equal("1.0.0", result.Diff.RemovedTypes[0].LastPresentIn);
        Assert.Equal("2.0.0", result.Diff.RemovedTypes[0].RemovedIn);

        // Should also be a breaking change.
        Assert.Contains(result.Diff.BreakingChanges,
            b => b.Kind == BreakingChangeKind.TypeRemoved
              && b.StableId == "MyNamespace.OldClass");
    }

    [Fact]
    public void Merge_DetectsAddedMember()
    {
        var v1Manifest = new ApiManifest
        {
            Types =
            [
                new ApiTypeNode
                {
                    StableId = "MyNamespace.MyClass",
                    FullName = "MyNamespace.MyClass",
                    Name = "MyClass",
                    Namespace = "MyNamespace",
                    Kind = ApiTypeKind.Class,
                    Members = [],
                },
            ],
        };

        var v2Manifest = new ApiManifest
        {
            Types =
            [
                new ApiTypeNode
                {
                    StableId = "MyNamespace.MyClass",
                    FullName = "MyNamespace.MyClass",
                    Name = "MyClass",
                    Namespace = "MyNamespace",
                    Kind = ApiTypeKind.Class,
                    Members =
                    [
                        new ApiMemberNode
                        {
                            StableId = "MyNamespace.MyClass.NewMethod()",
                            Name = "NewMethod",
                            Kind = ApiMemberKind.Method,
                            ReturnType = "System.Void",
                        },
                    ],
                },
            ],
        };

        var result = MultiVersionExtractor.Merge(
        [
            ("1.0.0", v1Manifest),
            ("2.0.0", v2Manifest),
        ]);

        Assert.Single(result.Diff.AddedMembers);
        Assert.Equal("MyNamespace.MyClass.NewMethod()", result.Diff.AddedMembers[0].StableId);
        Assert.Equal("2.0.0", result.Diff.AddedMembers[0].IntroducedIn);
    }

    [Fact]
    public void Merge_DetectsRemovedMember()
    {
        var v1Manifest = new ApiManifest
        {
            Types =
            [
                new ApiTypeNode
                {
                    StableId = "MyNamespace.MyClass",
                    FullName = "MyNamespace.MyClass",
                    Name = "MyClass",
                    Namespace = "MyNamespace",
                    Kind = ApiTypeKind.Class,
                    Members =
                    [
                        new ApiMemberNode
                        {
                            StableId = "MyNamespace.MyClass.OldMethod()",
                            Name = "OldMethod",
                            Kind = ApiMemberKind.Method,
                            ReturnType = "System.Void",
                        },
                    ],
                },
            ],
        };

        var v2Manifest = new ApiManifest
        {
            Types =
            [
                new ApiTypeNode
                {
                    StableId = "MyNamespace.MyClass",
                    FullName = "MyNamespace.MyClass",
                    Name = "MyClass",
                    Namespace = "MyNamespace",
                    Kind = ApiTypeKind.Class,
                    Members = [],
                },
            ],
        };

        var result = MultiVersionExtractor.Merge(
        [
            ("1.0.0", v1Manifest),
            ("2.0.0", v2Manifest),
        ]);

        Assert.Single(result.Diff.RemovedMembers);
        Assert.Equal("MyNamespace.MyClass.OldMethod()", result.Diff.RemovedMembers[0].StableId);
        Assert.Equal("2.0.0", result.Diff.RemovedMembers[0].RemovedIn);

        Assert.Contains(result.Diff.BreakingChanges,
            b => b.Kind == BreakingChangeKind.MemberRemoved);
    }

    [Fact]
    public void Merge_DetectsChangedReturnType()
    {
        var v1Manifest = new ApiManifest
        {
            Types =
            [
                new ApiTypeNode
                {
                    StableId = "MyNamespace.MyClass",
                    FullName = "MyNamespace.MyClass",
                    Name = "MyClass",
                    Namespace = "MyNamespace",
                    Kind = ApiTypeKind.Class,
                    Members =
                    [
                        new ApiMemberNode
                        {
                            StableId = "MyNamespace.MyClass.GetValue()",
                            Name = "GetValue",
                            Kind = ApiMemberKind.Method,
                            ReturnType = "System.Int32",
                        },
                    ],
                },
            ],
        };

        var v2Manifest = new ApiManifest
        {
            Types =
            [
                new ApiTypeNode
                {
                    StableId = "MyNamespace.MyClass",
                    FullName = "MyNamespace.MyClass",
                    Name = "MyClass",
                    Namespace = "MyNamespace",
                    Kind = ApiTypeKind.Class,
                    Members =
                    [
                        new ApiMemberNode
                        {
                            StableId = "MyNamespace.MyClass.GetValue()",
                            Name = "GetValue",
                            Kind = ApiMemberKind.Method,
                            ReturnType = "System.String",
                        },
                    ],
                },
            ],
        };

        var result = MultiVersionExtractor.Merge(
        [
            ("1.0.0", v1Manifest),
            ("2.0.0", v2Manifest),
        ]);

        Assert.Single(result.Diff.ChangedMembers);
        var changed = result.Diff.ChangedMembers[0];
        Assert.Equal("System.Int32", changed.OldReturnType);
        Assert.Equal("System.String", changed.NewReturnType);
        Assert.Equal("2.0.0", changed.ChangedIn);

        Assert.Contains(result.Diff.BreakingChanges,
            b => b.Kind == BreakingChangeKind.ReturnTypeChanged);
    }

    [Fact]
    public void Merge_DetectsChangedParameterTypes()
    {
        var v1Manifest = new ApiManifest
        {
            Types =
            [
                new ApiTypeNode
                {
                    StableId = "MyNamespace.MyClass",
                    FullName = "MyNamespace.MyClass",
                    Name = "MyClass",
                    Namespace = "MyNamespace",
                    Kind = ApiTypeKind.Class,
                    Members =
                    [
                        new ApiMemberNode
                        {
                            StableId = "MyNamespace.MyClass.DoWork(System.Int32)",
                            Name = "DoWork",
                            Kind = ApiMemberKind.Method,
                            ReturnType = "System.Void",
                            Parameters =
                            [
                                new ApiParameterInfo { Name = "x", Type = "System.Int32" },
                            ],
                        },
                    ],
                },
            ],
        };

        var v2Manifest = new ApiManifest
        {
            Types =
            [
                new ApiTypeNode
                {
                    StableId = "MyNamespace.MyClass",
                    FullName = "MyNamespace.MyClass",
                    Name = "MyClass",
                    Namespace = "MyNamespace",
                    Kind = ApiTypeKind.Class,
                    Members =
                    [
                        new ApiMemberNode
                        {
                            StableId = "MyNamespace.MyClass.DoWork(System.Int32)",
                            Name = "DoWork",
                            Kind = ApiMemberKind.Method,
                            ReturnType = "System.Void",
                            Parameters =
                            [
                                new ApiParameterInfo { Name = "x", Type = "System.Int32" },
                                new ApiParameterInfo { Name = "y", Type = "System.String" },
                            ],
                        },
                    ],
                },
            ],
        };

        var result = MultiVersionExtractor.Merge(
        [
            ("1.0.0", v1Manifest),
            ("2.0.0", v2Manifest),
        ]);

        Assert.Single(result.Diff.ChangedMembers);
        var changed = result.Diff.ChangedMembers[0];
        Assert.Single(changed.OldParameterTypes);
        Assert.Equal(2, changed.NewParameterTypes.Count);

        Assert.Contains(result.Diff.BreakingChanges,
            b => b.Kind == BreakingChangeKind.ParameterTypesChanged);
    }

    [Fact]
    public void Merge_ThreeVersions_TracksPresenceAcrossAll()
    {
        // v1: TypeA only
        // v2: TypeA + TypeB
        // v3: TypeB only (TypeA removed)
        var v1 = new ApiManifest
        {
            Types =
            [
                new ApiTypeNode
                {
                    StableId = "TypeA", FullName = "TypeA", Name = "TypeA",
                    Kind = ApiTypeKind.Class,
                },
            ],
        };

        var v2 = new ApiManifest
        {
            Types =
            [
                new ApiTypeNode
                {
                    StableId = "TypeA", FullName = "TypeA", Name = "TypeA",
                    Kind = ApiTypeKind.Class,
                },
                new ApiTypeNode
                {
                    StableId = "TypeB", FullName = "TypeB", Name = "TypeB",
                    Kind = ApiTypeKind.Class,
                },
            ],
        };

        var v3 = new ApiManifest
        {
            Types =
            [
                new ApiTypeNode
                {
                    StableId = "TypeB", FullName = "TypeB", Name = "TypeB",
                    Kind = ApiTypeKind.Class,
                },
            ],
        };

        var result = MultiVersionExtractor.Merge(
        [
            ("1.0", v1),
            ("2.0", v2),
            ("3.0", v3),
        ]);

        // TypeA: introduced in 1.0, removed in 3.0
        var typeA = result.MergedManifest.Types.First(t => t.StableId == "TypeA");
        Assert.Equal("1.0", typeA.Presence?.IntroducedIn);
        Assert.Equal("3.0", typeA.Presence?.RemovedIn);

        // TypeB: introduced in 2.0, still present
        var typeB = result.MergedManifest.Types.First(t => t.StableId == "TypeB");
        Assert.Equal("2.0", typeB.Presence?.IntroducedIn);
        Assert.Null(typeB.Presence?.RemovedIn);

        // Diff should show TypeB added and TypeA removed
        Assert.Contains(result.Diff.AddedTypes, a => a.StableId == "TypeB" && a.IntroducedIn == "2.0");
        Assert.Contains(result.Diff.RemovedTypes, r => r.StableId == "TypeA" && r.RemovedIn == "3.0");
    }

    [Fact]
    public void Merge_MergedManifestTypesAreSorted()
    {
        var result = ExtractSameAssemblyTwice();

        var ids = result.MergedManifest.Types.Select(t => t.StableId).ToList();
        var sorted = ids.OrderBy(id => id, StringComparer.Ordinal).ToList();

        Assert.Equal(sorted, ids);
    }

    [Fact]
    public void Extract_ThreeIdenticalVersions_ProducesNoDiffs()
    {
        var versions = new List<MultiVersionExtractor.VersionInput>
        {
            new("1.0.0", CoreLibPath),
            new("2.0.0", CoreLibPath),
            new("3.0.0", CoreLibPath),
        };

        var result = MultiVersionExtractor.Extract(versions);

        Assert.Empty(result.Diff.AddedTypes);
        Assert.Empty(result.Diff.RemovedTypes);
        Assert.Empty(result.Diff.ChangedMembers);
        Assert.Empty(result.Diff.BreakingChanges);
        Assert.Equal(3, result.Diff.Versions.Count);
    }
}
