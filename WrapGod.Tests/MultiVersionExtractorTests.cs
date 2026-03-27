using TinyBDD;
using TinyBDD.Xunit;
using WrapGod.Extractor;
using WrapGod.Manifest;
using Xunit.Abstractions;

namespace WrapGod.Tests;

[Feature("Multi-version extraction and diffing")]
public partial class MultiVersionExtractorTests : TinyBddXunitBase
{
    private static readonly string CoreLibPath = typeof(object).Assembly.Location;

    public MultiVersionExtractorTests(ITestOutputHelper output) : base(output) { }

    private static MultiVersionExtractor.MultiVersionResult ExtractSameAssemblyTwice()
    {
        var versions = new List<MultiVersionExtractor.VersionInput>
        {
            new("1.0.0", CoreLibPath),
            new("2.0.0", CoreLibPath),
        };
        return MultiVersionExtractor.Extract(versions);
    }

    private static MultiVersionExtractor.MultiVersionResult ExtractSingleVersion()
    {
        var versions = new List<MultiVersionExtractor.VersionInput>
        {
            new("1.0.0", CoreLibPath),
        };
        return MultiVersionExtractor.Extract(versions);
    }

    private static MultiVersionExtractor.MultiVersionResult ExtractThreeIdenticalVersions()
    {
        var versions = new List<MultiVersionExtractor.VersionInput>
        {
            new("1.0.0", CoreLibPath),
            new("2.0.0", CoreLibPath),
            new("3.0.0", CoreLibPath),
        };
        return MultiVersionExtractor.Extract(versions);
    }

    private static MultiVersionExtractor.MultiVersionResult MergeAddedType()
    {
        var v1 = new ApiManifest { Types = new List<ApiTypeNode>() };
        var v2 = new ApiManifest
        {
            Types = new List<ApiTypeNode>
            {
                new ApiTypeNode
                {
                    StableId = "MyNamespace.NewClass",
                    FullName = "MyNamespace.NewClass",
                    Name = "NewClass",
                    Namespace = "MyNamespace",
                    Kind = ApiTypeKind.Class,
                },
            },
        };
        return MultiVersionExtractor.Merge(
            new List<(string, ApiManifest)> { ("1.0.0", v1), ("2.0.0", v2) });
    }

    private static MultiVersionExtractor.MultiVersionResult MergeRemovedType()
    {
        var v1 = new ApiManifest
        {
            Types = new List<ApiTypeNode>
            {
                new ApiTypeNode
                {
                    StableId = "MyNamespace.OldClass",
                    FullName = "MyNamespace.OldClass",
                    Name = "OldClass",
                    Namespace = "MyNamespace",
                    Kind = ApiTypeKind.Class,
                },
            },
        };
        var v2 = new ApiManifest { Types = new List<ApiTypeNode>() };
        return MultiVersionExtractor.Merge(
            new List<(string, ApiManifest)> { ("1.0.0", v1), ("2.0.0", v2) });
    }

    private static MultiVersionExtractor.MultiVersionResult MergeAddedMember()
    {
        var v1 = new ApiManifest
        {
            Types = new List<ApiTypeNode>
            {
                new ApiTypeNode
                {
                    StableId = "MyNamespace.MyClass",
                    FullName = "MyNamespace.MyClass",
                    Name = "MyClass",
                    Namespace = "MyNamespace",
                    Kind = ApiTypeKind.Class,
                    Members = new List<ApiMemberNode>(),
                },
            },
        };
        var v2 = new ApiManifest
        {
            Types = new List<ApiTypeNode>
            {
                new ApiTypeNode
                {
                    StableId = "MyNamespace.MyClass",
                    FullName = "MyNamespace.MyClass",
                    Name = "MyClass",
                    Namespace = "MyNamespace",
                    Kind = ApiTypeKind.Class,
                    Members = new List<ApiMemberNode>
                    {
                        new ApiMemberNode
                        {
                            StableId = "MyNamespace.MyClass.NewMethod()",
                            Name = "NewMethod",
                            Kind = ApiMemberKind.Method,
                            ReturnType = "System.Void",
                        },
                    },
                },
            },
        };
        return MultiVersionExtractor.Merge(
            new List<(string, ApiManifest)> { ("1.0.0", v1), ("2.0.0", v2) });
    }

    private static MultiVersionExtractor.MultiVersionResult MergeRemovedMember()
    {
        var v1 = new ApiManifest
        {
            Types = new List<ApiTypeNode>
            {
                new ApiTypeNode
                {
                    StableId = "MyNamespace.MyClass",
                    FullName = "MyNamespace.MyClass",
                    Name = "MyClass",
                    Namespace = "MyNamespace",
                    Kind = ApiTypeKind.Class,
                    Members = new List<ApiMemberNode>
                    {
                        new ApiMemberNode
                        {
                            StableId = "MyNamespace.MyClass.OldMethod()",
                            Name = "OldMethod",
                            Kind = ApiMemberKind.Method,
                            ReturnType = "System.Void",
                        },
                    },
                },
            },
        };
        var v2 = new ApiManifest
        {
            Types = new List<ApiTypeNode>
            {
                new ApiTypeNode
                {
                    StableId = "MyNamespace.MyClass",
                    FullName = "MyNamespace.MyClass",
                    Name = "MyClass",
                    Namespace = "MyNamespace",
                    Kind = ApiTypeKind.Class,
                    Members = new List<ApiMemberNode>(),
                },
            },
        };
        return MultiVersionExtractor.Merge(
            new List<(string, ApiManifest)> { ("1.0.0", v1), ("2.0.0", v2) });
    }

    private static MultiVersionExtractor.MultiVersionResult MergeChangedReturnType()
    {
        var v1 = new ApiManifest
        {
            Types = new List<ApiTypeNode>
            {
                new ApiTypeNode
                {
                    StableId = "MyNamespace.MyClass",
                    FullName = "MyNamespace.MyClass",
                    Name = "MyClass",
                    Namespace = "MyNamespace",
                    Kind = ApiTypeKind.Class,
                    Members = new List<ApiMemberNode>
                    {
                        new ApiMemberNode
                        {
                            StableId = "MyNamespace.MyClass.GetValue()",
                            Name = "GetValue",
                            Kind = ApiMemberKind.Method,
                            ReturnType = "System.Int32",
                        },
                    },
                },
            },
        };
        var v2 = new ApiManifest
        {
            Types = new List<ApiTypeNode>
            {
                new ApiTypeNode
                {
                    StableId = "MyNamespace.MyClass",
                    FullName = "MyNamespace.MyClass",
                    Name = "MyClass",
                    Namespace = "MyNamespace",
                    Kind = ApiTypeKind.Class,
                    Members = new List<ApiMemberNode>
                    {
                        new ApiMemberNode
                        {
                            StableId = "MyNamespace.MyClass.GetValue()",
                            Name = "GetValue",
                            Kind = ApiMemberKind.Method,
                            ReturnType = "System.String",
                        },
                    },
                },
            },
        };
        return MultiVersionExtractor.Merge(
            new List<(string, ApiManifest)> { ("1.0.0", v1), ("2.0.0", v2) });
    }

    private static MultiVersionExtractor.MultiVersionResult MergeChangedParameterTypes()
    {
        var v1 = new ApiManifest
        {
            Types = new List<ApiTypeNode>
            {
                new ApiTypeNode
                {
                    StableId = "MyNamespace.MyClass",
                    FullName = "MyNamespace.MyClass",
                    Name = "MyClass",
                    Namespace = "MyNamespace",
                    Kind = ApiTypeKind.Class,
                    Members = new List<ApiMemberNode>
                    {
                        new ApiMemberNode
                        {
                            StableId = "MyNamespace.MyClass.DoWork(System.Int32)",
                            Name = "DoWork",
                            Kind = ApiMemberKind.Method,
                            ReturnType = "System.Void",
                            Parameters = new List<ApiParameterInfo>
                            {
                                new ApiParameterInfo { Name = "x", Type = "System.Int32" },
                            },
                        },
                    },
                },
            },
        };
        var v2 = new ApiManifest
        {
            Types = new List<ApiTypeNode>
            {
                new ApiTypeNode
                {
                    StableId = "MyNamespace.MyClass",
                    FullName = "MyNamespace.MyClass",
                    Name = "MyClass",
                    Namespace = "MyNamespace",
                    Kind = ApiTypeKind.Class,
                    Members = new List<ApiMemberNode>
                    {
                        new ApiMemberNode
                        {
                            StableId = "MyNamespace.MyClass.DoWork(System.Int32)",
                            Name = "DoWork",
                            Kind = ApiMemberKind.Method,
                            ReturnType = "System.Void",
                            Parameters = new List<ApiParameterInfo>
                            {
                                new ApiParameterInfo { Name = "x", Type = "System.Int32" },
                                new ApiParameterInfo { Name = "y", Type = "System.String" },
                            },
                        },
                    },
                },
            },
        };
        return MultiVersionExtractor.Merge(
            new List<(string, ApiManifest)> { ("1.0.0", v1), ("2.0.0", v2) });
    }

    private static MultiVersionExtractor.MultiVersionResult MergeThreeVersionsPresence()
    {
        var v1 = new ApiManifest
        {
            Types = new List<ApiTypeNode>
            {
                new ApiTypeNode
                {
                    StableId = "TypeA", FullName = "TypeA", Name = "TypeA",
                    Kind = ApiTypeKind.Class,
                },
            },
        };
        var v2 = new ApiManifest
        {
            Types = new List<ApiTypeNode>
            {
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
            },
        };
        var v3 = new ApiManifest
        {
            Types = new List<ApiTypeNode>
            {
                new ApiTypeNode
                {
                    StableId = "TypeB", FullName = "TypeB", Name = "TypeB",
                    Kind = ApiTypeKind.Class,
                },
            },
        };
        return MultiVersionExtractor.Merge(
            new List<(string, ApiManifest)> { ("1.0", v1), ("2.0", v2), ("3.0", v3) });
    }

    [Scenario("Single version produces merged manifest with presence")]
    [Fact]
    public async Task Extract_SingleVersion_ProducesMergedManifestWithPresence()
    {
        await Flow.Given("a single version input", ExtractSingleVersion)
            .Then("the merged manifest has types", result =>
            {
                Assert.NotNull(result.MergedManifest);
                Assert.NotEmpty(result.MergedManifest.Types);
            })
            .And("every type has presence introduced in the single version", result =>
            {
                foreach (var type in result.MergedManifest.Types)
                {
                    Assert.NotNull(type.Presence);
                    Assert.Equal("1.0.0", type.Presence!.IntroducedIn);
                    Assert.Null(type.Presence.RemovedIn);
                }
            })
            .AssertPassed();
    }

    [Scenario("Two identical versions produce no diffs")]
    [Fact]
    public async Task Extract_TwoIdenticalVersions_ProducesNoDiffs()
    {
        await Flow.Given("two identical version extractions", ExtractSameAssemblyTwice)
            .Then("all diff collections are empty", result =>
            {
                Assert.Empty(result.Diff.AddedTypes);
                Assert.Empty(result.Diff.RemovedTypes);
                Assert.Empty(result.Diff.AddedMembers);
                Assert.Empty(result.Diff.RemovedMembers);
                Assert.Empty(result.Diff.ChangedMembers);
                Assert.Empty(result.Diff.BreakingChanges);
            })
            .AssertPassed();
    }

    [Scenario("Two identical versions assign presence to all types")]
    [Fact]
    public async Task Extract_TwoIdenticalVersions_AllTypesHavePresence()
    {
        await Flow.Given("two identical version extractions", ExtractSameAssemblyTwice)
            .Then("every type has presence introduced in the first version", result =>
            {
                foreach (var type in result.MergedManifest.Types)
                {
                    Assert.NotNull(type.Presence);
                    Assert.Equal("1.0.0", type.Presence!.IntroducedIn);
                    Assert.Null(type.Presence.RemovedIn);
                }
            })
            .AssertPassed();
    }

    [Scenario("Two identical versions assign presence to all members")]
    [Fact]
    public async Task Extract_TwoIdenticalVersions_AllMembersHavePresence()
    {
        await Flow.Given("two identical version extractions", ExtractSameAssemblyTwice)
            .Then("sampled members have presence introduced in the first version", result =>
            {
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
            })
            .AssertPassed();
    }

    [Scenario("Diff version list is correct for two versions")]
    [Fact]
    public async Task Extract_TwoIdenticalVersions_DiffVersionListIsCorrect()
    {
        await Flow.Given("two identical version extractions", ExtractSameAssemblyTwice)
            .Then("the diff lists both versions in order", result =>
            {
                Assert.Equal(2, result.Diff.Versions.Count);
                Assert.Equal("1.0.0", result.Diff.Versions[0]);
                Assert.Equal("2.0.0", result.Diff.Versions[1]);
            })
            .AssertPassed();
    }

    [Scenario("Empty version list throws ArgumentException")]
    [Fact]
    public async Task Extract_EmptyVersionList_ThrowsArgumentException()
    {
        await Flow.Given("an empty version list", () => new List<MultiVersionExtractor.VersionInput>())
            .Then("extraction throws ArgumentException", versions =>
                Assert.Throws<ArgumentException>(() => MultiVersionExtractor.Extract(versions)))
            .AssertPassed();
    }

    [Scenario("Merge detects an added type")]
    [Fact]
    public async Task Merge_DetectsAddedType()
    {
        await Flow.Given("v1 with no types and v2 with one type", MergeAddedType)
            .Then("one added type is detected", result =>
            {
                Assert.Single(result.Diff.AddedTypes);
                Assert.Equal("MyNamespace.NewClass", result.Diff.AddedTypes[0].StableId);
                Assert.Equal("2.0.0", result.Diff.AddedTypes[0].IntroducedIn);
            })
            .And("the merged manifest type has correct presence", result =>
            {
                var mergedType = Assert.Single(result.MergedManifest.Types);
                Assert.Equal("2.0.0", mergedType.Presence?.IntroducedIn);
            })
            .AssertPassed();
    }

    [Scenario("Merge detects a removed type")]
    [Fact]
    public async Task Merge_DetectsRemovedType()
    {
        await Flow.Given("v1 with one type and v2 with no types", MergeRemovedType)
            .Then("one removed type is detected", result =>
            {
                Assert.Single(result.Diff.RemovedTypes);
                Assert.Equal("MyNamespace.OldClass", result.Diff.RemovedTypes[0].StableId);
                Assert.Equal("1.0.0", result.Diff.RemovedTypes[0].LastPresentIn);
                Assert.Equal("2.0.0", result.Diff.RemovedTypes[0].RemovedIn);
            })
            .And("it is flagged as a breaking change", result =>
                Assert.Contains(result.Diff.BreakingChanges,
                    b => b.Kind == BreakingChangeKind.TypeRemoved
                      && b.StableId == "MyNamespace.OldClass"))
            .AssertPassed();
    }

    [Scenario("Merge detects an added member")]
    [Fact]
    public async Task Merge_DetectsAddedMember()
    {
        await Flow.Given("v1 with an empty class and v2 with a new method", MergeAddedMember)
            .Then("one added member is detected", result =>
            {
                Assert.Single(result.Diff.AddedMembers);
                Assert.Equal("MyNamespace.MyClass.NewMethod()", result.Diff.AddedMembers[0].StableId);
                Assert.Equal("2.0.0", result.Diff.AddedMembers[0].IntroducedIn);
            })
            .AssertPassed();
    }

    [Scenario("Merge detects a removed member")]
    [Fact]
    public async Task Merge_DetectsRemovedMember()
    {
        await Flow.Given("v1 with a method and v2 with the method removed", MergeRemovedMember)
            .Then("one removed member is detected", result =>
            {
                Assert.Single(result.Diff.RemovedMembers);
                Assert.Equal("MyNamespace.MyClass.OldMethod()", result.Diff.RemovedMembers[0].StableId);
                Assert.Equal("2.0.0", result.Diff.RemovedMembers[0].RemovedIn);
            })
            .And("it is flagged as a breaking change", result =>
                Assert.Contains(result.Diff.BreakingChanges,
                    b => b.Kind == BreakingChangeKind.MemberRemoved))
            .AssertPassed();
    }

    [Scenario("Merge detects a changed return type")]
    [Fact]
    public async Task Merge_DetectsChangedReturnType()
    {
        await Flow.Given("v1 with Int32 return type and v2 with String return type", MergeChangedReturnType)
            .Then("the changed member is detected with correct return types", result =>
            {
                Assert.Single(result.Diff.ChangedMembers);
                var changed = result.Diff.ChangedMembers[0];
                Assert.Equal("System.Int32", changed.OldReturnType);
                Assert.Equal("System.String", changed.NewReturnType);
                Assert.Equal("2.0.0", changed.ChangedIn);
            })
            .And("it is flagged as a breaking change", result =>
                Assert.Contains(result.Diff.BreakingChanges,
                    b => b.Kind == BreakingChangeKind.ReturnTypeChanged))
            .AssertPassed();
    }

    [Scenario("Merge detects changed parameter types")]
    [Fact]
    public async Task Merge_DetectsChangedParameterTypes()
    {
        await Flow.Given("v1 with one parameter and v2 with two parameters", MergeChangedParameterTypes)
            .Then("the changed member is detected with correct parameter counts", result =>
            {
                Assert.Single(result.Diff.ChangedMembers);
                var changed = result.Diff.ChangedMembers[0];
                Assert.Single(changed.OldParameterTypes);
                Assert.Equal(2, changed.NewParameterTypes.Count);
            })
            .And("it is flagged as a breaking change", result =>
                Assert.Contains(result.Diff.BreakingChanges,
                    b => b.Kind == BreakingChangeKind.ParameterTypesChanged))
            .AssertPassed();
    }

    [Scenario("Three versions track presence across all")]
    [Fact]
    public async Task Merge_ThreeVersions_TracksPresenceAcrossAll()
    {
        await Flow.Given("three versions where TypeA is removed and TypeB is added", MergeThreeVersionsPresence)
            .Then("TypeA was introduced in 1.0 and removed in 3.0", result =>
            {
                var typeA = result.MergedManifest.Types.First(t => t.StableId == "TypeA");
                Assert.Equal("1.0", typeA.Presence?.IntroducedIn);
                Assert.Equal("3.0", typeA.Presence?.RemovedIn);
            })
            .And("TypeB was introduced in 2.0 and is still present", result =>
            {
                var typeB = result.MergedManifest.Types.First(t => t.StableId == "TypeB");
                Assert.Equal("2.0", typeB.Presence?.IntroducedIn);
                Assert.Null(typeB.Presence?.RemovedIn);
            })
            .And("the diff shows TypeB added and TypeA removed", result =>
            {
                Assert.Contains(result.Diff.AddedTypes, a => a.StableId == "TypeB" && a.IntroducedIn == "2.0");
                Assert.Contains(result.Diff.RemovedTypes, r => r.StableId == "TypeA" && r.RemovedIn == "3.0");
            })
            .AssertPassed();
    }

    [Scenario("Merged manifest types are sorted by stable ID")]
    [Fact]
    public async Task Merge_MergedManifestTypesAreSorted()
    {
        await Flow.Given("two identical version extractions", ExtractSameAssemblyTwice)
            .Then("types are sorted by stable ID", result =>
            {
                var ids = result.MergedManifest.Types.Select(t => t.StableId).ToList();
                var sorted = ids.OrderBy(id => id, StringComparer.Ordinal).ToList();
                Assert.Equal(sorted, ids);
            })
            .AssertPassed();
    }

    [Scenario("Three identical versions produce no diffs")]
    [Fact]
    public async Task Extract_ThreeIdenticalVersions_ProducesNoDiffs()
    {
        await Flow.Given("three identical version extractions", ExtractThreeIdenticalVersions)
            .Then("all diff collections are empty", result =>
            {
                Assert.Empty(result.Diff.AddedTypes);
                Assert.Empty(result.Diff.RemovedTypes);
                Assert.Empty(result.Diff.ChangedMembers);
                Assert.Empty(result.Diff.BreakingChanges);
            })
            .And("three versions are listed", result =>
                Assert.Equal(3, result.Diff.Versions.Count))
            .AssertPassed();
    }
}
