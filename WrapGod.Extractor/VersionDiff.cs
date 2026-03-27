using System.Collections.Generic;
using WrapGod.Manifest;

namespace WrapGod.Extractor;

/// <summary>
/// Machine-readable compatibility report produced by comparing API manifests
/// across multiple versions. Each entry captures what changed and whether the
/// change is binary-breaking.
/// </summary>
public sealed class VersionDiff
{
    /// <summary>The ordered list of version labels that were compared.</summary>
    public List<string> Versions { get; set; } = [];

    /// <summary>Types that were added in a later version.</summary>
    public List<AddedTypeEntry> AddedTypes { get; set; } = [];

    /// <summary>Types that were removed in a later version.</summary>
    public List<RemovedTypeEntry> RemovedTypes { get; set; } = [];

    /// <summary>Members that were added in a later version.</summary>
    public List<AddedMemberEntry> AddedMembers { get; set; } = [];

    /// <summary>Members that were removed in a later version.</summary>
    public List<RemovedMemberEntry> RemovedMembers { get; set; } = [];

    /// <summary>Members whose signature changed between versions.</summary>
    public List<ChangedMemberEntry> ChangedMembers { get; set; } = [];

    /// <summary>Subset of all entries that are classified as breaking changes.</summary>
    public List<BreakingChange> BreakingChanges { get; set; } = [];
}

/// <summary>A type that appeared for the first time in a given version.</summary>
public sealed class AddedTypeEntry
{
    public string StableId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string IntroducedIn { get; set; } = string.Empty;
}

/// <summary>A type that was present in an earlier version but absent in a later one.</summary>
public sealed class RemovedTypeEntry
{
    public string StableId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string LastPresentIn { get; set; } = string.Empty;
    public string RemovedIn { get; set; } = string.Empty;
}

/// <summary>A member that appeared for the first time in a given version.</summary>
public sealed class AddedMemberEntry
{
    public string StableId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string DeclaringTypeStableId { get; set; } = string.Empty;
    public string IntroducedIn { get; set; } = string.Empty;
}

/// <summary>A member that was present in an earlier version but absent in a later one.</summary>
public sealed class RemovedMemberEntry
{
    public string StableId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string DeclaringTypeStableId { get; set; } = string.Empty;
    public string LastPresentIn { get; set; } = string.Empty;
    public string RemovedIn { get; set; } = string.Empty;
}

/// <summary>A member whose return type or parameters changed between versions.</summary>
public sealed class ChangedMemberEntry
{
    public string StableId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string DeclaringTypeStableId { get; set; } = string.Empty;
    public string ChangedIn { get; set; } = string.Empty;
    public string? OldReturnType { get; set; }
    public string? NewReturnType { get; set; }
    public List<string> OldParameterTypes { get; set; } = [];
    public List<string> NewParameterTypes { get; set; } = [];
}

/// <summary>
/// A breaking change entry referencing one of the diff entries above,
/// with a human-readable reason and severity classification.
/// </summary>
public sealed class BreakingChange
{
    public string StableId { get; set; } = string.Empty;
    public BreakingChangeKind Kind { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
}

/// <summary>Classification of a breaking change.</summary>
public enum BreakingChangeKind
{
    /// <summary>A public type was removed.</summary>
    TypeRemoved,

    /// <summary>A public member was removed.</summary>
    MemberRemoved,

    /// <summary>A member's return type changed.</summary>
    ReturnTypeChanged,

    /// <summary>A member's parameter types changed.</summary>
    ParameterTypesChanged,
}
