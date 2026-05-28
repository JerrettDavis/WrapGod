using System.Collections.Generic;
using WrapGod.Extractor;

namespace WrapGod.Tests.Fixtures.Migration;

/// <summary>
/// Named builders for <see cref="VersionDiff"/> fixtures used by MigrationSchemaGeneratorTests.
/// </summary>
internal static class DiffFixtures
{
    private static VersionDiff Base(string from = "1.0.0", string to = "2.0.0") => new()
    {
        Versions = [from, to],
    };

    /// <summary>One removed type and one added type with the same short name (sim = 1.0).</summary>
    public static VersionDiff BuildRenameTypeDiff()
    {
        var diff = Base();
        diff.RemovedTypes.Add(new RemovedTypeEntry
        {
            StableId = "OldNs.FooButton",
            FullName = "OldNs.FooButton",
            LastPresentIn = "1.0.0",
            RemovedIn = "2.0.0",
        });
        diff.AddedTypes.Add(new AddedTypeEntry
        {
            StableId = "NewNs.FooButton",
            FullName = "NewNs.FooButton",
            IntroducedIn = "2.0.0",
        });
        return diff;
    }

    /// <summary>One removed member and one added member on the same declaring type (high similarity, same short prefix).</summary>
    public static VersionDiff BuildRenameMemberDiff()
    {
        var diff = Base();
        // "GetColorValue" → "GetColorValues" — Jaro-Winkler similarity is very high (≥0.9)
        diff.RemovedMembers.Add(new RemovedMemberEntry
        {
            StableId = "OldNs.FooButton::GetColorValue",
            Name = "GetColorValue",
            DeclaringTypeStableId = "OldNs.FooButton",
            LastPresentIn = "1.0.0",
            RemovedIn = "2.0.0",
        });
        diff.AddedMembers.Add(new AddedMemberEntry
        {
            StableId = "OldNs.FooButton::GetColorValues",
            Name = "GetColorValues",
            DeclaringTypeStableId = "OldNs.FooButton",
            IntroducedIn = "2.0.0",
        });
        return diff;
    }

    /// <summary>One changed member with a different return type.</summary>
    public static VersionDiff BuildReturnTypeChangedDiff()
    {
        var diff = Base();
        diff.ChangedMembers.Add(new ChangedMemberEntry
        {
            StableId = "Ns.MyClass::GetItems",
            Name = "GetItems",
            DeclaringTypeStableId = "Ns.MyClass",
            ChangedIn = "2.0.0",
            OldReturnType = "System.Collections.Generic.IList`1",
            NewReturnType = "System.Collections.Generic.IReadOnlyList`1",
        });
        return diff;
    }

    /// <summary>Five rules across types/members/changed to test sequential ID assignment.</summary>
    public static VersionDiff BuildFiveRuleDiff()
    {
        var diff = Base();
        // 2 removed types (both get rename rules)
        diff.RemovedTypes.Add(new RemovedTypeEntry { StableId = "N.Alpha", FullName = "N.Alpha", LastPresentIn = "1.0.0", RemovedIn = "2.0.0" });
        diff.RemovedTypes.Add(new RemovedTypeEntry { StableId = "N.Beta", FullName = "N.Beta", LastPresentIn = "1.0.0", RemovedIn = "2.0.0" });
        diff.AddedTypes.Add(new AddedTypeEntry { StableId = "N.Alpha2", FullName = "N.Alpha2", IntroducedIn = "2.0.0" });
        diff.AddedTypes.Add(new AddedTypeEntry { StableId = "N.Beta2", FullName = "N.Beta2", IntroducedIn = "2.0.0" });
        // 2 removed members (both get rename rules)
        diff.RemovedMembers.Add(new RemovedMemberEntry { StableId = "N.C::A", Name = "A", DeclaringTypeStableId = "T1", LastPresentIn = "1.0.0", RemovedIn = "2.0.0" });
        diff.RemovedMembers.Add(new RemovedMemberEntry { StableId = "N.C::B", Name = "B", DeclaringTypeStableId = "T2", LastPresentIn = "1.0.0", RemovedIn = "2.0.0" });
        diff.AddedMembers.Add(new AddedMemberEntry { StableId = "N.C::A2", Name = "A2", DeclaringTypeStableId = "T1", IntroducedIn = "2.0.0" });
        diff.AddedMembers.Add(new AddedMemberEntry { StableId = "N.C::B2", Name = "B2", DeclaringTypeStableId = "T2", IntroducedIn = "2.0.0" });
        // 1 changed return type
        diff.ChangedMembers.Add(new ChangedMemberEntry
        {
            StableId = "T3::GetX", Name = "GetX", DeclaringTypeStableId = "T3", ChangedIn = "2.0.0",
            OldReturnType = "System.String", NewReturnType = "System.Int32",
        });
        return diff;
    }

    /// <summary>Three removed types from OldNs, three added types with identical short names in NewNs.</summary>
    public static VersionDiff BuildNamespaceShuffleDiff()
    {
        var diff = Base();
        string[] names = ["Widget", "Gadget", "Thingamajig"];
        foreach (var name in names)
        {
            diff.RemovedTypes.Add(new RemovedTypeEntry
            {
                StableId = $"OldNs.{name}",
                FullName = $"OldNs.{name}",
                LastPresentIn = "1.0.0",
                RemovedIn = "2.0.0",
            });
            diff.AddedTypes.Add(new AddedTypeEntry
            {
                StableId = $"NewNs.{name}",
                FullName = $"NewNs.{name}",
                IntroducedIn = "2.0.0",
            });
        }
        return diff;
    }

    /// <summary>Empty diff (no changes at all).</summary>
    public static VersionDiff BuildEmptyDiff() => Base();

    /// <summary>
    /// Two removed types both have similarity 0.7 to a single added type.
    /// Deterministic tiebreak should pick the lexicographically smaller StableId.
    /// </summary>
    public static VersionDiff BuildAmbiguousRenameDiff()
    {
        var diff = Base();
        // Both "FooBarA" and "FooBarB" removed; only "FooBar" added
        // JaroWinkler("foobara", "foobar") ≈ JaroWinkler("foobarbaz", "foobar") — we use
        // identical names so similarity = 1.0 for both → perfect tie
        diff.RemovedTypes.Add(new RemovedTypeEntry
        {
            StableId = "N.FooBar-AAA",    // lexicographically first
            FullName = "N.FooBarAAA",
            LastPresentIn = "1.0.0",
            RemovedIn = "2.0.0",
        });
        diff.RemovedTypes.Add(new RemovedTypeEntry
        {
            StableId = "N.FooBar-ZZZ",    // lexicographically last
            FullName = "N.FooBarZZZ",
            LastPresentIn = "1.0.0",
            RemovedIn = "2.0.0",
        });
        diff.AddedTypes.Add(new AddedTypeEntry
        {
            StableId = "N.FooBar-NEW",
            FullName = "N.FooBarNEW",
            IntroducedIn = "2.0.0",
        });
        return diff;
    }

    /// <summary>One removed member (no similar add) to test Manual RemoveMemberRule.</summary>
    public static VersionDiff BuildUnmatchedRemoveDiff()
    {
        var diff = Base();
        diff.RemovedMembers.Add(new RemovedMemberEntry
        {
            StableId = "Ns.Foo::ObsoleteMethod",
            Name = "ObsoleteMethod",
            DeclaringTypeStableId = "Ns.Foo",
            LastPresentIn = "1.0.0",
            RemovedIn = "2.0.0",
        });
        return diff;
    }

    /// <summary>Parameter arity grew from 2→3 (new parameter inserted).</summary>
    public static VersionDiff BuildArityGrewDiff()
    {
        var diff = Base();
        diff.ChangedMembers.Add(new ChangedMemberEntry
        {
            StableId = "Ns.Provider::Apply",
            Name = "Apply",
            DeclaringTypeStableId = "Ns.Provider",
            ChangedIn = "2.0.0",
            OldParameterTypes = ["System.String", "System.Int32"],
            NewParameterTypes = ["Ns.MudTheme", "System.String", "System.Int32"],
        });
        return diff;
    }
}
