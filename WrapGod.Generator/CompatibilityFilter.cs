using System;
using System.Collections.Generic;

namespace WrapGod.Generator;

/// <summary>
/// Filters a <see cref="GenerationPlan"/> based on a <see cref="CompatibilityMode"/>.
/// </summary>
internal static class CompatibilityFilter
{
    /// <summary>
    /// Apply the given compatibility mode to a generation plan, returning a
    /// new plan that contains only the types and members appropriate for the mode.
    /// </summary>
    /// <param name="plan">The full generation plan with version metadata.</param>
    /// <param name="mode">The compatibility mode to apply.</param>
    /// <param name="versions">Ordered list of all known versions (earliest first).</param>
    /// <param name="targetVersion">
    /// Required when <paramref name="mode"/> is <see cref="CompatibilityMode.Targeted"/>;
    /// ignored otherwise.
    /// </param>
    public static GenerationPlan Apply(
        GenerationPlan plan,
        CompatibilityMode mode,
        IReadOnlyList<string> versions,
        string? targetVersion = null)
    {
        if (plan is null) throw new ArgumentNullException(nameof(plan));
        if (versions is null || versions.Count == 0) throw new ArgumentException("At least one version is required.", nameof(versions));

        if (mode == CompatibilityMode.Targeted && string.IsNullOrEmpty(targetVersion))
        {
            throw new ArgumentException("A target version must be specified for Targeted mode.", nameof(targetVersion));
        }

        string earliestVersion = versions[0];

        var filteredTypes = new List<TypePlan>();

        foreach (var type in plan.Types)
        {
            TypePlan? filtered = FilterType(type, mode, versions, earliestVersion, targetVersion);
            if (filtered != null)
            {
                filteredTypes.Add(filtered);
            }
        }

        return new GenerationPlan(plan.AssemblyName, filteredTypes);
    }

    private static TypePlan? FilterType(
        TypePlan type,
        CompatibilityMode mode,
        IReadOnlyList<string> versions,
        string earliestVersion,
        string? targetVersion)
    {
        // Check type-level presence
        if (mode == CompatibilityMode.Lcd)
        {
            // LCD: type must be present in all versions (introduced at earliest, never removed)
            if (!IsLcdPresent(type.IntroducedIn, type.RemovedIn, earliestVersion))
                return null;
        }
        else if (mode == CompatibilityMode.Targeted)
        {
            if (!IsPresentInVersion(type.IntroducedIn, type.RemovedIn, targetVersion!, versions))
                return null;
        }
        // Adaptive: keep all types

        var filteredMembers = new List<MemberPlan>();

        foreach (var member in type.Members)
        {
            if (mode == CompatibilityMode.Lcd)
            {
                if (IsLcdPresent(member.IntroducedIn, member.RemovedIn, earliestVersion))
                {
                    filteredMembers.Add(member);
                }
            }
            else if (mode == CompatibilityMode.Targeted)
            {
                if (IsPresentInVersion(member.IntroducedIn, member.RemovedIn, targetVersion!, versions))
                {
                    filteredMembers.Add(member);
                }
            }
            else
            {
                // Adaptive: keep all members (version metadata is already on the MemberPlan)
                filteredMembers.Add(member);
            }
        }

        return new TypePlan(type.FullName, type.Name, type.Namespace, filteredMembers,
            isStatic: type.IsStatic, introducedIn: type.IntroducedIn, removedIn: type.RemovedIn);
    }

    /// <summary>
    /// LCD rule: present in all versions means introduced at the earliest
    /// version and never removed.
    /// </summary>
    private static bool IsLcdPresent(string? introducedIn, string? removedIn, string earliestVersion)
    {
        // If no version metadata, treat as present in all versions
        if (introducedIn is null)
            return true;

        return introducedIn == earliestVersion && removedIn is null;
    }

    /// <summary>
    /// Targeted rule: the member must have been introduced at or before the
    /// target version and not removed at or before the target version.
    /// </summary>
    private static bool IsPresentInVersion(
        string? introducedIn,
        string? removedIn,
        string targetVersion,
        IReadOnlyList<string> versions)
    {
        // If no version metadata, treat as present in all versions
        if (introducedIn is null)
            return true;

        int targetIndex = IndexOf(versions, targetVersion);
        int introducedIndex = IndexOf(versions, introducedIn);

        // Not introduced yet at the target version
        if (introducedIndex < 0 || targetIndex < 0)
            return false;

        if (introducedIndex > targetIndex)
            return false;

        // Check if removed before or at the target version
        if (removedIn != null)
        {
            int removedIndex = IndexOf(versions, removedIn);
            if (removedIndex >= 0 && removedIndex <= targetIndex)
                return false;
        }

        return true;
    }

    private static int IndexOf(IReadOnlyList<string> list, string value)
    {
        for (int i = 0; i < list.Count; i++)
        {
            if (string.Equals(list[i], value, StringComparison.Ordinal))
                return i;
        }

        return -1;
    }
}
