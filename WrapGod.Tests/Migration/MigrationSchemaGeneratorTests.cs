using System;
using System.Linq;
using TinyBDD;
using TinyBDD.Xunit;
using WrapGod.Migration;
using WrapGod.Migration.Generation;
using WrapGod.Tests.Fixtures.Migration;
using Xunit.Abstractions;

namespace WrapGod.Tests.Migration;

[Feature("MigrationSchemaGenerator.FromDiff produces draft migration schemas from VersionDiff")]
public sealed class MigrationSchemaGeneratorTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    // ── Happy path ──────────────────────────────────────────────────────────────────────────

    [Scenario("Single removed+added type with same short name produces RenameTypeRule (Verified)")]
    [Fact]
    public Task FromDiff_SingleRenameType_ProducesRenameTypeRule() =>
        Given("FromDiff called with a diff containing one removed+added type pair sharing the same short name",
            () => MigrationSchemaGenerator.FromDiff(DiffFixtures.BuildRenameTypeDiff(), "TestLib"))
        .Then("the schema contains exactly one rule", schema =>
            schema.Rules.Count == 1)
        .And("the rule is a RenameTypeRule", schema =>
            schema.Rules[0] is RenameTypeRule)
        .And("confidence is Verified (similarity = 1.0 >= 0.85 threshold)", schema =>
            schema.Rules[0].Confidence == RuleConfidence.Verified)
        .And("the old name is populated", schema =>
            ((RenameTypeRule)schema.Rules[0]).OldName == "OldNs.FooButton")
        .And("the new name is populated", schema =>
            ((RenameTypeRule)schema.Rules[0]).NewName == "NewNs.FooButton")
        .AssertPassed();

    [Scenario("Removed member and added member on same type with high similarity produces RenameMemberRule (Verified)")]
    [Fact]
    public Task FromDiff_SingleRenameMember_ProducesRenameMemberRule() =>
        Given("FromDiff called with a diff containing removed member 'GetColorValue' and added member 'GetColorValues' on the same declaring type (high JW similarity)",
            () => MigrationSchemaGenerator.FromDiff(DiffFixtures.BuildRenameMemberDiff(), "TestLib"))
        .Then("the schema contains exactly one rule", schema =>
            schema.Rules.Count == 1)
        .And("the rule is a RenameMemberRule", schema =>
            schema.Rules[0] is RenameMemberRule)
        .And("confidence is Verified (short-name similarity >= 0.85)", schema =>
            schema.Rules[0].Confidence == RuleConfidence.Verified)
        .And("OldMemberName is 'GetColorValue'", schema =>
            ((RenameMemberRule)schema.Rules[0]).OldMemberName == "GetColorValue")
        .And("NewMemberName is 'GetColorValues'", schema =>
            ((RenameMemberRule)schema.Rules[0]).NewMemberName == "GetColorValues")
        .AssertPassed();

    [Scenario("Changed member with different return types produces ChangeTypeReferenceRule (Auto)")]
    [Fact]
    public Task FromDiff_ReturnTypeChange_ProducesChangeTypeReferenceRule() =>
        Given("FromDiff called with a diff containing a ChangedMemberEntry with a return-type delta",
            () => MigrationSchemaGenerator.FromDiff(DiffFixtures.BuildReturnTypeChangedDiff(), "TestLib"))
        .Then("the schema contains a ChangeTypeReferenceRule", schema =>
            schema.Rules.Any(r => r is ChangeTypeReferenceRule))
        .And("the rule has Auto confidence", schema =>
            schema.Rules.First(r => r is ChangeTypeReferenceRule).Confidence == RuleConfidence.Auto)
        .And("OldType is the old return type", schema =>
            ((ChangeTypeReferenceRule)schema.Rules.First(r => r is ChangeTypeReferenceRule)).OldType ==
            "System.Collections.Generic.IList`1")
        .AssertPassed();

    [Scenario("Rule IDs are sequential and stable across two invocations")]
    [Fact]
    public Task FromDiff_RuleIds_AreSequentialAndStable() =>
        Given("two FromDiff calls with identical inputs producing 5+ rules", () =>
        {
            var diff = DiffFixtures.BuildFiveRuleDiff();
            var s1 = MigrationSchemaGenerator.FromDiff(diff, "LIB");
            var s2 = MigrationSchemaGenerator.FromDiff(diff, "LIB");
            return (s1, s2);
        })
        .Then("both schemas have the same number of rules", pair =>
            pair.s1.Rules.Count == pair.s2.Rules.Count)
        .And("IDs are identical across both runs", pair =>
            pair.s1.Rules.Select(r => r.Id).SequenceEqual(pair.s2.Rules.Select(r => r.Id)))
        .And("IDs start with the library prefix", pair =>
            pair.s1.Rules.All(r => r.Id.StartsWith("LIB-", StringComparison.Ordinal)))
        .And("IDs are sequential starting at LIB-001", pair =>
            pair.s1.Rules[0].Id == "LIB-001")
        .AssertPassed();

    [Scenario("Three types relocated from OldNs to NewNs collapse to a single RenameNamespaceRule")]
    [Fact]
    public Task FromDiff_NamespaceRelocation_CollapsesToOneRule() =>
        Given("FromDiff called with 3 removed types from OldNs and 3 matching added types in NewNs",
            () => MigrationSchemaGenerator.FromDiff(DiffFixtures.BuildNamespaceShuffleDiff(), "TestLib"))
        .Then("exactly one rule is emitted", schema =>
            schema.Rules.Count == 1)
        .And("the rule is a RenameNamespaceRule", schema =>
            schema.Rules[0] is RenameNamespaceRule)
        .And("OldNamespace is 'OldNs'", schema =>
            ((RenameNamespaceRule)schema.Rules[0]).OldNamespace == "OldNs")
        .And("NewNamespace is 'NewNs'", schema =>
            ((RenameNamespaceRule)schema.Rules[0]).NewNamespace == "NewNs")
        .AssertPassed();

    // ── Sad path ────────────────────────────────────────────────────────────────────────────

    [Scenario("Null VersionDiff throws ArgumentNullException")]
    [Fact]
    public Task FromDiff_NullDiff_Throws() =>
        Given("a call to FromDiff with a null VersionDiff", () => true)
        .Then("FromDiff throws ArgumentNullException for the diff parameter", _ =>
        {
            try
            {
                MigrationSchemaGenerator.FromDiff(null!, "TestLib");
                return false;
            }
            catch (ArgumentNullException ex) when (ex.ParamName == "diff")
            {
                return true;
            }
        })
        .AssertPassed();

    [Scenario("Empty library string throws ArgumentException")]
    [Fact]
    public Task FromDiff_EmptyLibrary_Throws() =>
        Given("a call to FromDiff with an empty library string", () => DiffFixtures.BuildEmptyDiff())
        .Then("FromDiff throws ArgumentException for the library parameter", diff =>
        {
            try
            {
                MigrationSchemaGenerator.FromDiff(diff, "");
                return false;
            }
            catch (ArgumentException ex) when (ex.ParamName == "library")
            {
                return true;
            }
        })
        .AssertPassed();

    [Scenario("VersionDiff with only one version label throws ArgumentException")]
    [Fact]
    public Task FromDiff_SingleVersion_Throws() =>
        Given("a VersionDiff with only one version label", () =>
        {
            var diff = DiffFixtures.BuildEmptyDiff();
            diff.Versions.RemoveAt(1);
            return diff;
        })
        .Then("FromDiff throws ArgumentException with a descriptive message mentioning 'two version'", diff =>
        {
            try
            {
                MigrationSchemaGenerator.FromDiff(diff, "TestLib");
                return false;
            }
            catch (ArgumentException ex) when (ex.ParamName == "diff" &&
                ex.Message.Contains("two version", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        })
        .AssertPassed();

    [Scenario("Unmatched removed member produces RemoveMemberRule with Manual confidence and non-null Note")]
    [Fact]
    public Task FromDiff_UnmatchedRemove_ProducesManualRule() =>
        Given("FromDiff called with a diff containing one removed member and no similar added members",
            () => MigrationSchemaGenerator.FromDiff(DiffFixtures.BuildUnmatchedRemoveDiff(), "TestLib"))
        .Then("the schema contains a RemoveMemberRule", schema =>
            schema.Rules.Any(r => r is RemoveMemberRule))
        .And("the rule has Manual confidence", schema =>
            schema.Rules.First(r => r is RemoveMemberRule).Confidence == RuleConfidence.Manual)
        .And("the rule has a non-null Note", schema =>
            schema.Rules.First(r => r is RemoveMemberRule).Note != null)
        .And("the MemberName is 'ObsoleteMethod'", schema =>
            ((RemoveMemberRule)schema.Rules.First(r => r is RemoveMemberRule)).MemberName == "ObsoleteMethod")
        .AssertPassed();

    [Scenario("Parameter arity grew by 1 produces AddRequiredParameterRule with Manual confidence")]
    [Fact]
    public Task FromDiff_ArityGrew_ProducesAddRequiredParameter() =>
        Given("FromDiff called with a diff where a method's parameter count went from 2 to 3",
            () => MigrationSchemaGenerator.FromDiff(DiffFixtures.BuildArityGrewDiff(), "TestLib"))
        .Then("the schema contains an AddRequiredParameterRule", schema =>
            schema.Rules.Any(r => r is AddRequiredParameterRule))
        .And("the rule has Manual confidence (value of required arg is unknown)", schema =>
            schema.Rules.First(r => r is AddRequiredParameterRule).Confidence == RuleConfidence.Manual)
        .And("the MethodName is 'Apply'", schema =>
            ((AddRequiredParameterRule)schema.Rules.First(r => r is AddRequiredParameterRule)).MethodName == "Apply")
        .AssertPassed();

    // ── Edge cases ──────────────────────────────────────────────────────────────────────────

    [Scenario("Two removed types with equal similarity to one added type use deterministic tiebreak by StableId")]
    [Fact]
    public Task FromDiff_AmbiguousRename_DeterministicTiebreak() =>
        Given("FromDiff called with two removed types competing for one added type",
            () => MigrationSchemaGenerator.FromDiff(DiffFixtures.BuildAmbiguousRenameDiff(), "TestLib"))
        .Then("the schema contains at least one rename rule", schema =>
            schema.Rules.Any(r => r is RenameTypeRule))
        .And("exactly one rename rule is emitted (not two)", schema =>
            schema.Rules.Count(r => r is RenameTypeRule) == 1)
        .And("the loser becomes a RemoveMemberRule with Manual confidence", schema =>
            schema.Rules.Any(r => r is RemoveMemberRule && r.Confidence == RuleConfidence.Manual))
        .AssertPassed();

    [Scenario("DisableRenameDetection suppresses all rename rules")]
    [Fact]
    public Task FromDiff_RenameDetectionDisabled_SuppressesRenames() =>
        Given("FromDiff called with DisableRenameDetection=true on a diff with an obvious type rename",
            () => MigrationSchemaGenerator.FromDiff(
                DiffFixtures.BuildRenameTypeDiff(),
                "TestLib",
                new MigrationSchemaGeneratorOptions { DisableRenameDetection = true }))
        .Then("the schema contains no RenameTypeRule", schema =>
            !schema.Rules.Any(r => r is RenameTypeRule))
        .And("the schema contains a RemoveMemberRule instead (type removed, no rename)", schema =>
            schema.Rules.Any(r => r is RemoveMemberRule))
        .AssertPassed();

    [Scenario("Custom RuleIdPrefix overrides the library-derived prefix")]
    [Fact]
    public Task FromDiff_CustomPrefix_UsesCustomPrefix() =>
        Given("FromDiff called with RuleIdPrefix='X' on a diff that produces at least one rule",
            () => MigrationSchemaGenerator.FromDiff(
                DiffFixtures.BuildReturnTypeChangedDiff(),
                "TestLib",
                new MigrationSchemaGeneratorOptions { RuleIdPrefix = "X" }))
        .Then("all rule IDs start with 'X-'", schema =>
            schema.Rules.All(r => r.Id.StartsWith("X-", StringComparison.Ordinal)))
        .And("first rule ID is 'X-001'", schema =>
            schema.Rules[0].Id == "X-001")
        .AssertPassed();

    [Scenario("Empty VersionDiff returns schema with empty Rules list but populated metadata")]
    [Fact]
    public Task FromDiff_NoChanges_ReturnsEmptyRules() =>
        Given("FromDiff called with a VersionDiff that has zero changes",
            () => MigrationSchemaGenerator.FromDiff(DiffFixtures.BuildEmptyDiff(), "AcmeLib"))
        .Then("schema.Rules is empty", schema =>
            schema.Rules.Count == 0)
        .And("schema.Library is 'AcmeLib'", schema =>
            schema.Library == "AcmeLib")
        .And("schema.From is '1.0.0'", schema =>
            schema.From == "1.0.0")
        .And("schema.To is '2.0.0'", schema =>
            schema.To == "2.0.0")
        .And("schema.Schema is 'wrapgod-migration/1.0'", schema =>
            schema.Schema == "wrapgod-migration/1.0")
        .And("schema.GeneratedFrom is 'manifest-diff'", schema =>
            schema.GeneratedFrom == "manifest-diff")
        .And("schema.LastEdited is set", schema =>
            schema.LastEdited.HasValue)
        .AssertPassed();

    [Scenario("Schema from a 5-rule diff round-trips through MigrationSchemaSerializer without data loss")]
    [Fact]
    public Task FromDiff_Schema_RoundTripsThroughSerializer() =>
        Given("a schema produced by FromDiff that is serialized and deserialized", () =>
        {
            var diff = DiffFixtures.BuildFiveRuleDiff();
            var original = MigrationSchemaGenerator.FromDiff(diff, "LIB");
            var json = MigrationSchemaSerializer.Serialize(original);
            var roundtrip = MigrationSchemaSerializer.Deserialize(json)!;
            return (original, roundtrip);
        })
        .Then("the rule count is preserved", pair =>
            pair.roundtrip.Rules.Count == pair.original.Rules.Count)
        .And("the library is preserved", pair =>
            pair.roundtrip.Library == pair.original.Library)
        .And("the from version is preserved", pair =>
            pair.roundtrip.From == pair.original.From)
        .And("all rule IDs are preserved in order", pair =>
            pair.roundtrip.Rules.Select(x => x.Id)
                .SequenceEqual(pair.original.Rules.Select(x => x.Id)))
        .AssertPassed();

    [Scenario("Whitespace-only library string throws ArgumentException")]
    [Fact]
    public Task FromDiff_WhitespaceLibrary_Throws() =>
        Given("a call to FromDiff with a whitespace-only library string", () => DiffFixtures.BuildEmptyDiff())
        .Then("FromDiff throws ArgumentException for the library parameter", diff =>
        {
            try
            {
                MigrationSchemaGenerator.FromDiff(diff, "   ");
                return false;
            }
            catch (ArgumentException ex) when (ex.ParamName == "library")
            {
                return true;
            }
        })
        .AssertPassed();

    [Scenario("Null library string throws ArgumentNullException")]
    [Fact]
    public Task FromDiff_NullLibrary_Throws() =>
        Given("a call to FromDiff with a null library string", () => DiffFixtures.BuildEmptyDiff())
        .Then("FromDiff throws ArgumentNullException for the library parameter", diff =>
        {
            try
            {
                MigrationSchemaGenerator.FromDiff(diff, null!);
                return false;
            }
            catch (ArgumentNullException ex) when (ex.ParamName == "library")
            {
                return true;
            }
        })
        .AssertPassed();

    [Scenario("Schema metadata From/To match the first and last version in the diff")]
    [Fact]
    public Task FromDiff_MetadataFromTo_MatchDiffVersions() =>
        Given("FromDiff called with a diff that has versions 3.0.0 and 4.0.0", () =>
        {
            var diff = DiffFixtures.BuildEmptyDiff();
            diff.Versions.Clear();
            diff.Versions.Add("3.0.0");
            diff.Versions.Add("4.0.0");
            return MigrationSchemaGenerator.FromDiff(diff, "SomeLib");
        })
        .Then("schema.From is '3.0.0'", schema =>
            schema.From == "3.0.0")
        .And("schema.To is '4.0.0'", schema =>
            schema.To == "4.0.0")
        .AssertPassed();

    [Scenario("Changed member with same-arity parameter type change produces ChangeParameterRule (Auto)")]
    [Fact]
    public Task FromDiff_SameArityParamChange_ProducesChangeParameterRule() =>
        Given("FromDiff called with a diff where a method parameter type changed (same arity)", () =>
        {
            var diff = DiffFixtures.BuildEmptyDiff();
            diff.ChangedMembers.Add(new ChangedMemberEntry
            {
                StableId = "Ns.Foo::Bar",
                Name = "Bar",
                DeclaringTypeStableId = "Ns.Foo",
                ChangedIn = "2.0.0",
                OldParameterTypes = ["System.Int32", "System.String"],
                NewParameterTypes = ["System.Int64", "System.String"],
            });
            return MigrationSchemaGenerator.FromDiff(diff, "TestLib");
        })
        .Then("the schema contains a ChangeParameterRule for the changed slot", schema =>
            schema.Rules.Any(r => r is ChangeParameterRule))
        .And("the rule has Auto confidence", schema =>
            schema.Rules.First(r => r is ChangeParameterRule).Confidence == RuleConfidence.Auto)
        .And("the OldParameterType is System.Int32", schema =>
            ((ChangeParameterRule)schema.Rules.First(r => r is ChangeParameterRule)).OldParameterType == "System.Int32")
        .AssertPassed();

    [Scenario("Changed member with arity shrink produces Manual ChangeParameterRule with note")]
    [Fact]
    public Task FromDiff_ArityShrink_ProducesManualChangeParameterRule() =>
        Given("FromDiff called with a diff where a method's parameter count shrank from 3 to 1", () =>
        {
            var diff = DiffFixtures.BuildEmptyDiff();
            diff.ChangedMembers.Add(new ChangedMemberEntry
            {
                StableId = "Ns.Foo::Baz",
                Name = "Baz",
                DeclaringTypeStableId = "Ns.Foo",
                ChangedIn = "2.0.0",
                OldParameterTypes = ["System.Int32", "System.String", "System.Boolean"],
                NewParameterTypes = ["System.String"],
            });
            return MigrationSchemaGenerator.FromDiff(diff, "TestLib");
        })
        .Then("the schema contains a ChangeParameterRule", schema =>
            schema.Rules.Any(r => r is ChangeParameterRule))
        .And("the rule has Manual confidence", schema =>
            schema.Rules.First(r => r is ChangeParameterRule).Confidence == RuleConfidence.Manual)
        .And("the rule has a non-null Note describing the reshape", schema =>
            schema.Rules.First(r => r is ChangeParameterRule).Note != null)
        .AssertPassed();

    [Scenario("Only one type relocated (below namespace-rule threshold) produces individual type rule not namespace rule")]
    [Fact]
    public Task FromDiff_SingleTypeRelocation_ProducesRenameTypeNotNamespaceRule() =>
        Given("FromDiff called with only one type moved from OldNs to NewNs (below 2-type threshold)", () =>
        {
            var diff = DiffFixtures.BuildEmptyDiff();
            diff.RemovedTypes.Add(new RemovedTypeEntry
            {
                StableId = "OldNs.Widget",
                FullName = "OldNs.Widget",
                LastPresentIn = "1.0.0",
                RemovedIn = "2.0.0",
            });
            diff.AddedTypes.Add(new AddedTypeEntry
            {
                StableId = "NewNs.Widget",
                FullName = "NewNs.Widget",
                IntroducedIn = "2.0.0",
            });
            return MigrationSchemaGenerator.FromDiff(diff, "TestLib");
        })
        .Then("the schema does not contain a RenameNamespaceRule (only 1 type moved)", schema =>
            !schema.Rules.Any(r => r is RenameNamespaceRule))
        .And("the schema contains a RenameTypeRule", schema =>
            schema.Rules.Any(r => r is RenameTypeRule))
        .AssertPassed();

    [Scenario("Library name longer than 6 chars gets truncated to 6 in rule ID prefix")]
    [Fact]
    public Task FromDiff_LongLibraryName_PrefixTruncatedToSix() =>
        Given("FromDiff called with library 'VeryLongLibraryName' that produces one rule", () =>
        {
            var diff = DiffFixtures.BuildReturnTypeChangedDiff();
            return MigrationSchemaGenerator.FromDiff(diff, "VeryLongLibraryName");
        })
        .Then("the first rule ID prefix is at most 6 chars before the dash", schema =>
            schema.Rules[0].Id.Split('-')[0].Length <= 6)
        .AssertPassed();

    [Scenario("ChangedMember with no parameter changes and no return type change produces no rule")]
    [Fact]
    public Task FromDiff_ChangedMemberNoActualChange_ProducesNoRule() =>
        Given("FromDiff called with a ChangedMemberEntry where old/new return type are the same and params are empty", () =>
        {
            var diff = DiffFixtures.BuildEmptyDiff();
            diff.ChangedMembers.Add(new ChangedMemberEntry
            {
                StableId = "Ns.Foo::NoOp",
                Name = "NoOp",
                DeclaringTypeStableId = "Ns.Foo",
                ChangedIn = "2.0.0",
                // null old/new return type — no change
                OldParameterTypes = [],
                NewParameterTypes = [],
            });
            return MigrationSchemaGenerator.FromDiff(diff, "TestLib");
        })
        .Then("the schema contains no rules", schema =>
            schema.Rules.Count == 0)
        .AssertPassed();
}
