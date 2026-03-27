using TinyBDD;
using TinyBDD.Xunit;
using WrapGod.Extractor;
using WrapGod.Manifest;
using Xunit.Abstractions;

namespace WrapGod.Tests;

[Feature("Multi-version extraction and diffing")]
public sealed class MultiVersionExtractorTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    // ── Helpers ──────────────────────────────────────────────────────

    private static readonly string CoreLibPath = typeof(object).Assembly.Location;

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

    // ── Scenarios ────────────────────────────────────────────────────

    [Scenario("Single version produces merged manifest with presence")]
    [Fact]
    public Task Extract_SingleVersion_ProducesMergedManifestWithPresence()
        => Given("a single version input", ExtractSingleVersion)
            .Then("the merged manifest has types", result =>
                result.MergedManifest is not null && result.MergedManifest.Types.Count > 0)
            .And("every type is introduced in the single version", result =>
                result.MergedManifest.Types.All(t =>
                    t.Presence is not null
                    && t.Presence.IntroducedIn == "1.0.0"
                    && t.Presence.RemovedIn is null))
            .AssertPassed();

    [Scenario("Two identical versions produce no diffs")]
    [Fact]
    public Task Extract_TwoIdenticalVersions_ProducesNoDiffs()
        => Given("two identical version extractions", ExtractSameAssemblyTwice)
            .Then("added types is empty", result => result.Diff.AddedTypes.Count == 0)
            .And("removed types is empty", result => result.Diff.RemovedTypes.Count == 0)
            .And("added members is empty", result => result.Diff.AddedMembers.Count == 0)
            .And("removed members is empty", result => result.Diff.RemovedMembers.Count == 0)
            .And("changed members is empty", result => result.Diff.ChangedMembers.Count == 0)
            .And("breaking changes is empty", result => result.Diff.BreakingChanges.Count == 0)
            .AssertPassed();

    [Scenario("Two identical versions assign presence to all types")]
    [Fact]
    public Task Extract_TwoIdenticalVersions_AllTypesHavePresence()
        => Given("two identical version extractions", ExtractSameAssemblyTwice)
            .Then("every type is introduced in the first version with no removal", result =>
                result.MergedManifest.Types.All(t =>
                    t.Presence is not null
                    && t.Presence.IntroducedIn == "1.0.0"
                    && t.Presence.RemovedIn is null))
            .AssertPassed();

    [Scenario("Two identical versions assign presence to all members")]
    [Fact]
    public Task Extract_TwoIdenticalVersions_AllMembersHavePresence()
        => Given("two identical version extractions", ExtractSameAssemblyTwice)
            .Then("sampled members are introduced in the first version", result =>
            {
                var members = result.MergedManifest.Types
                    .SelectMany(t => t.Members)
                    .Take(200)
                    .ToList();
                return members.Count > 0
                    && members.All(m =>
                        m.Presence is not null
                        && m.Presence.IntroducedIn == "1.0.0");
            })
            .AssertPassed();

    [Scenario("Diff version list is correct for two versions")]
    [Fact]
    public Task Extract_TwoIdenticalVersions_DiffVersionListIsCorrect()
        => Given("two identical version extractions", ExtractSameAssemblyTwice)
            .Then("the diff lists both versions in order", result =>
                result.Diff.Versions.Count == 2
                && result.Diff.Versions[0] == "1.0.0"
                && result.Diff.Versions[1] == "2.0.0")
            .AssertPassed();

    [Scenario("Empty version list throws ArgumentException")]
    [Fact]
    public Task Extract_EmptyVersionList_ThrowsArgumentException()
        => Given("an empty version list", () => new List<MultiVersionExtractor.VersionInput>())
            .Then("extraction throws ArgumentException", versions =>
            {
                try { MultiVersionExtractor.Extract(versions); return false; }
                catch (ArgumentException) { return true; }
            })
            .AssertPassed();

    [Scenario("Merge detects an added type")]
    [Fact]
    public Task Merge_DetectsAddedType()
        => Given("v1 with no types and v2 with one type", MergeAddedType)
            .Then("one added type is detected", result =>
                result.Diff.AddedTypes.Count == 1
                && result.Diff.AddedTypes[0].StableId == "MyNamespace.NewClass"
                && result.Diff.AddedTypes[0].IntroducedIn == "2.0.0")
            .And("the merged manifest type has correct presence", result =>
                result.MergedManifest.Types.Count == 1
                && result.MergedManifest.Types[0].Presence?.IntroducedIn == "2.0.0")
            .AssertPassed();

    [Scenario("Merge detects a removed type")]
    [Fact]
    public Task Merge_DetectsRemovedType()
        => Given("v1 with one type and v2 with no types", MergeRemovedType)
            .Then("one removed type is detected", result =>
                result.Diff.RemovedTypes.Count == 1
                && result.Diff.RemovedTypes[0].StableId == "MyNamespace.OldClass"
                && result.Diff.RemovedTypes[0].LastPresentIn == "1.0.0"
                && result.Diff.RemovedTypes[0].RemovedIn == "2.0.0")
            .And("it is flagged as a breaking change", result =>
                result.Diff.BreakingChanges.Any(b =>
                    b.Kind == BreakingChangeKind.TypeRemoved
                    && b.StableId == "MyNamespace.OldClass"))
            .AssertPassed();

    [Scenario("Merge detects an added member")]
    [Fact]
    public Task Merge_DetectsAddedMember()
        => Given("v1 with an empty class and v2 with a new method", MergeAddedMember)
            .Then("one added member is detected", result =>
                result.Diff.AddedMembers.Count == 1
                && result.Diff.AddedMembers[0].StableId == "MyNamespace.MyClass.NewMethod()"
                && result.Diff.AddedMembers[0].IntroducedIn == "2.0.0")
            .AssertPassed();

    [Scenario("Merge detects a removed member")]
    [Fact]
    public Task Merge_DetectsRemovedMember()
        => Given("v1 with a method and v2 with the method removed", MergeRemovedMember)
            .Then("one removed member is detected", result =>
                result.Diff.RemovedMembers.Count == 1
                && result.Diff.RemovedMembers[0].StableId == "MyNamespace.MyClass.OldMethod()"
                && result.Diff.RemovedMembers[0].RemovedIn == "2.0.0")
            .And("it is flagged as a breaking change", result =>
                result.Diff.BreakingChanges.Any(b =>
                    b.Kind == BreakingChangeKind.MemberRemoved))
            .AssertPassed();

    [Scenario("Merge detects a changed return type")]
    [Fact]
    public Task Merge_DetectsChangedReturnType()
        => Given("v1 with Int32 return type and v2 with String return type", MergeChangedReturnType)
            .Then("the changed member is detected with correct return types", result =>
                result.Diff.ChangedMembers.Count == 1
                && result.Diff.ChangedMembers[0].OldReturnType == "System.Int32"
                && result.Diff.ChangedMembers[0].NewReturnType == "System.String"
                && result.Diff.ChangedMembers[0].ChangedIn == "2.0.0")
            .And("it is flagged as a breaking change", result =>
                result.Diff.BreakingChanges.Any(b =>
                    b.Kind == BreakingChangeKind.ReturnTypeChanged))
            .AssertPassed();

    [Scenario("Merge detects changed parameter types")]
    [Fact]
    public Task Merge_DetectsChangedParameterTypes()
        => Given("v1 with one parameter and v2 with two parameters", MergeChangedParameterTypes)
            .Then("the changed member is detected with correct parameter counts", result =>
                result.Diff.ChangedMembers.Count == 1
                && result.Diff.ChangedMembers[0].OldParameterTypes.Count == 1
                && result.Diff.ChangedMembers[0].NewParameterTypes.Count == 2)
            .And("it is flagged as a breaking change", result =>
                result.Diff.BreakingChanges.Any(b =>
                    b.Kind == BreakingChangeKind.ParameterTypesChanged))
            .AssertPassed();

    [Scenario("Three versions track presence across all")]
    [Fact]
    public Task Merge_ThreeVersions_TracksPresenceAcrossAll()
        => Given("three versions where TypeA is removed and TypeB is added", MergeThreeVersionsPresence)
            .Then("TypeA was introduced in 1.0 and removed in 3.0", result =>
            {
                var typeA = result.MergedManifest.Types.First(t => t.StableId == "TypeA");
                return typeA.Presence?.IntroducedIn == "1.0"
                    && typeA.Presence?.RemovedIn == "3.0";
            })
            .And("TypeB was introduced in 2.0 and is still present", result =>
            {
                var typeB = result.MergedManifest.Types.First(t => t.StableId == "TypeB");
                return typeB.Presence?.IntroducedIn == "2.0"
                    && typeB.Presence?.RemovedIn is null;
            })
            .And("the diff shows TypeB added in 2.0", result =>
                result.Diff.AddedTypes.Any(a => a.StableId == "TypeB" && a.IntroducedIn == "2.0"))
            .And("the diff shows TypeA removed in 3.0", result =>
                result.Diff.RemovedTypes.Any(r => r.StableId == "TypeA" && r.RemovedIn == "3.0"))
            .AssertPassed();

    [Scenario("Merged manifest types are sorted by stable ID")]
    [Fact]
    public Task Merge_MergedManifestTypesAreSorted()
        => Given("two identical version extractions", ExtractSameAssemblyTwice)
            .Then("types are sorted by stable ID", result =>
            {
                var ids = result.MergedManifest.Types.Select(t => t.StableId).ToList();
                var sorted = ids.OrderBy(id => id, StringComparer.Ordinal).ToList();
                return ids.SequenceEqual(sorted);
            })
            .AssertPassed();

    [Scenario("Three identical versions produce no diffs")]
    [Fact]
    public Task Extract_ThreeIdenticalVersions_ProducesNoDiffs()
        => Given("three identical version extractions", ExtractThreeIdenticalVersions)
            .Then("added types is empty", result => result.Diff.AddedTypes.Count == 0)
            .And("removed types is empty", result => result.Diff.RemovedTypes.Count == 0)
            .And("changed members is empty", result => result.Diff.ChangedMembers.Count == 0)
            .And("breaking changes is empty", result => result.Diff.BreakingChanges.Count == 0)
            .And("three versions are listed", result => result.Diff.Versions.Count == 3)
            .AssertPassed();
}
