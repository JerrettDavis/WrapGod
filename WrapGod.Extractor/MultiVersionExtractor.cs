using WrapGod.Manifest;

namespace WrapGod.Extractor;

/// <summary>
/// Accepts multiple (version-label, assembly-path) pairs, extracts each into an
/// <see cref="ApiManifest"/>, then diffs them to produce a merged manifest with
/// <see cref="VersionPresence"/> metadata and a <see cref="VersionDiff"/> compatibility report.
/// </summary>
public static class MultiVersionExtractor
{
    /// <summary>
    /// A single version input: a human-readable label and the path to the assembly.
    /// </summary>
    /// <param name="VersionLabel">Semantic version or arbitrary label (e.g. "1.0.0", "2.0.0-rc1").</param>
    /// <param name="AssemblyPath">Absolute path to the assembly DLL for this version.</param>
    public sealed record VersionInput(string VersionLabel, string AssemblyPath);

    /// <summary>
    /// Result of a multi-version extraction: a merged manifest and a compatibility diff.
    /// </summary>
    public sealed record MultiVersionResult(ApiManifest MergedManifest, VersionDiff Diff);

    /// <summary>
    /// Extracts all provided versions and produces a merged manifest with version presence
    /// metadata plus a compatibility diff report.
    /// </summary>
    /// <param name="versions">
    /// Ordered list of versions from oldest to newest. At least one version is required.
    /// </param>
    /// <returns>Merged manifest and diff report.</returns>
    /// <exception cref="ArgumentException">When <paramref name="versions"/> is empty.</exception>
    public static MultiVersionResult Extract(IReadOnlyList<VersionInput> versions)
    {
        if (versions.Count == 0)
            throw new ArgumentException("At least one version is required.", nameof(versions));

        // Extract each version's manifest.
        var manifests = new List<(string Label, ApiManifest Manifest)>();
        foreach (var v in versions)
        {
            var manifest = AssemblyExtractor.Extract(v.AssemblyPath);
            manifests.Add((v.VersionLabel, manifest));
        }

        return Merge(manifests);
    }

    /// <summary>
    /// Merges pre-extracted manifests. Useful when callers already have manifests in hand
    /// (e.g., from deserialized JSON) and want to skip re-extraction.
    /// </summary>
    public static MultiVersionResult Merge(IReadOnlyList<(string Label, ApiManifest Manifest)> manifests)
    {
        if (manifests.Count == 0)
            throw new ArgumentException("At least one manifest is required.", nameof(manifests));

        var versionLabels = manifests.Select(m => m.Label).ToList();

        // Build per-version type/member lookup: StableId -> node.
        var perVersionTypes = new List<Dictionary<string, ApiTypeNode>>();
        var perVersionMembers = new List<Dictionary<string, (ApiMemberNode Member, string TypeStableId)>>();

        foreach (var (_, manifest) in manifests)
        {
            var typeDict = new Dictionary<string, ApiTypeNode>(StringComparer.Ordinal);
            var memberDict = new Dictionary<string, (ApiMemberNode, string)>(StringComparer.Ordinal);

            foreach (var type in manifest.Types)
            {
                typeDict[type.StableId] = type;

                foreach (var member in type.Members)
                {
                    memberDict[member.StableId] = (member, type.StableId);
                }
            }

            perVersionTypes.Add(typeDict);
            perVersionMembers.Add(memberDict);
        }

        // Collect all unique type/member stable IDs across every version.
        var allTypeIds = perVersionTypes
            .SelectMany(d => d.Keys)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();

        var allMemberIds = perVersionMembers
            .SelectMany(d => d.Keys)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();

        // Build the diff report.
        var diff = new VersionDiff { Versions = versionLabels };

        // Build merged types with presence metadata.
        var mergedTypes = new Dictionary<string, ApiTypeNode>(StringComparer.Ordinal);

        foreach (var typeId in allTypeIds)
        {
            var presence = ComputeTypePresence(typeId, versionLabels, perVersionTypes, diff);

            // Use the latest version that has the type as the "canonical" shape.
            ApiTypeNode? canonical = null;
            for (int i = manifests.Count - 1; i >= 0; i--)
            {
                if (perVersionTypes[i].TryGetValue(typeId, out canonical))
                    break;
            }

            if (canonical is null) continue;

            var merged = CloneTypeNode(canonical);
            merged.Presence = presence;
            mergedTypes[typeId] = merged;
        }

        // Process members: detect added, removed, changed.
        foreach (var memberId in allMemberIds)
        {
            var memberPresence = ComputeMemberPresence(
                memberId, versionLabels, perVersionMembers, perVersionTypes, diff);

            // Attach presence to the merged type's member.
            // Find declaring type from the latest version that has this member.
            string? declaringTypeId = null;
            ApiMemberNode? canonicalMember = null;

            for (int i = manifests.Count - 1; i >= 0; i--)
            {
                if (perVersionMembers[i].TryGetValue(memberId, out var entry))
                {
                    declaringTypeId = entry.TypeStableId;
                    canonicalMember = entry.Member;
                    break;
                }
            }

            if (declaringTypeId is null || canonicalMember is null) continue;

            if (mergedTypes.TryGetValue(declaringTypeId, out var mergedType))
            {
                // Replace or add the member with presence metadata.
                var existing = mergedType.Members.FindIndex(m => m.StableId == memberId);
                var mergedMember = CloneMemberNode(canonicalMember);
                mergedMember.Presence = memberPresence;

                if (existing >= 0)
                    mergedType.Members[existing] = mergedMember;
                else
                    mergedType.Members.Add(mergedMember);
            }
        }

        // Detect changed signatures.
        DetectChangedMembers(versionLabels, perVersionMembers, diff);

        // Classify breaking changes.
        ClassifyBreakingChanges(diff);

        // Build the merged manifest.
        var lastManifest = manifests[^1].Manifest;
        var mergedManifest = new ApiManifest
        {
            SchemaVersion = "1.0",
            GeneratedAt = DateTimeOffset.UtcNow,
            Assembly = lastManifest.Assembly,
            SourceHash = lastManifest.SourceHash,
            Types = mergedTypes.Values
                .OrderBy(t => t.StableId, StringComparer.Ordinal)
                .ToList(),
        };

        // Sort members within each type.
        foreach (var type in mergedManifest.Types)
        {
            type.Members = type.Members
                .OrderBy(m => m.StableId, StringComparer.Ordinal)
                .ToList();
        }

        return new MultiVersionResult(mergedManifest, diff);
    }

    private static VersionPresence ComputeTypePresence(
        string typeId,
        List<string> versionLabels,
        List<Dictionary<string, ApiTypeNode>> perVersionTypes,
        VersionDiff diff)
    {
        var presence = new VersionPresence();

        // Find first version containing this type.
        int firstIndex = -1;
        int lastIndex = -1;

        for (int i = 0; i < versionLabels.Count; i++)
        {
            if (perVersionTypes[i].ContainsKey(typeId))
            {
                if (firstIndex == -1) firstIndex = i;
                lastIndex = i;
            }
        }

        if (firstIndex > 0)
        {
            presence.IntroducedIn = versionLabels[firstIndex];

            // Find the full name from the version that introduced it.
            var node = perVersionTypes[firstIndex][typeId];
            diff.AddedTypes.Add(new AddedTypeEntry
            {
                StableId = typeId,
                FullName = node.FullName,
                IntroducedIn = versionLabels[firstIndex],
            });
        }
        else if (firstIndex == 0)
        {
            presence.IntroducedIn = versionLabels[0];
        }

        // Check if the type was removed (present in some version but absent in a later one).
        if (lastIndex >= 0 && lastIndex < versionLabels.Count - 1)
        {
            // Verify it is actually absent in the version after lastIndex.
            bool absentAfter = true;
            for (int i = lastIndex + 1; i < versionLabels.Count; i++)
            {
                if (perVersionTypes[i].ContainsKey(typeId))
                {
                    absentAfter = false;
                    break;
                }
            }

            if (absentAfter)
            {
                presence.RemovedIn = versionLabels[lastIndex + 1];

                var node = perVersionTypes[lastIndex][typeId];
                diff.RemovedTypes.Add(new RemovedTypeEntry
                {
                    StableId = typeId,
                    FullName = node.FullName,
                    LastPresentIn = versionLabels[lastIndex],
                    RemovedIn = versionLabels[lastIndex + 1],
                });
            }
        }

        return presence;
    }

    private static VersionPresence ComputeMemberPresence(
        string memberId,
        List<string> versionLabels,
        List<Dictionary<string, (ApiMemberNode Member, string TypeStableId)>> perVersionMembers,
        List<Dictionary<string, ApiTypeNode>> perVersionTypes,
        VersionDiff diff)
    {
        var presence = new VersionPresence();

        int firstIndex = -1;
        int lastIndex = -1;

        for (int i = 0; i < versionLabels.Count; i++)
        {
            if (perVersionMembers[i].ContainsKey(memberId))
            {
                if (firstIndex == -1) firstIndex = i;
                lastIndex = i;
            }
        }

        if (firstIndex > 0)
        {
            presence.IntroducedIn = versionLabels[firstIndex];

            var (member, typeStableId) = perVersionMembers[firstIndex][memberId];
            diff.AddedMembers.Add(new AddedMemberEntry
            {
                StableId = memberId,
                Name = member.Name,
                DeclaringTypeStableId = typeStableId,
                IntroducedIn = versionLabels[firstIndex],
            });
        }
        else if (firstIndex == 0)
        {
            presence.IntroducedIn = versionLabels[0];
        }

        if (lastIndex >= 0 && lastIndex < versionLabels.Count - 1)
        {
            bool absentAfter = true;
            for (int i = lastIndex + 1; i < versionLabels.Count; i++)
            {
                if (perVersionMembers[i].ContainsKey(memberId))
                {
                    absentAfter = false;
                    break;
                }
            }

            if (absentAfter)
            {
                presence.RemovedIn = versionLabels[lastIndex + 1];

                var (member, typeStableId) = perVersionMembers[lastIndex][memberId];
                diff.RemovedMembers.Add(new RemovedMemberEntry
                {
                    StableId = memberId,
                    Name = member.Name,
                    DeclaringTypeStableId = typeStableId,
                    LastPresentIn = versionLabels[lastIndex],
                    RemovedIn = versionLabels[lastIndex + 1],
                });
            }
        }

        return presence;
    }

    private static void DetectChangedMembers(
        List<string> versionLabels,
        List<Dictionary<string, (ApiMemberNode Member, string TypeStableId)>> perVersionMembers,
        VersionDiff diff)
    {
        // Collect all member IDs present in at least two versions.
        var allIds = perVersionMembers
            .SelectMany(d => d.Keys)
            .Distinct(StringComparer.Ordinal);

        foreach (var memberId in allIds)
        {
            // Compare consecutive versions where this member exists.
            (ApiMemberNode Member, string TypeStableId)? previous = null;
            int previousIndex = -1;

            for (int i = 0; i < versionLabels.Count; i++)
            {
                if (!perVersionMembers[i].TryGetValue(memberId, out var current))
                    continue;

                if (previous is not null)
                {
                    var prev = previous.Value;
                    bool returnTypeChanged = prev.Member.ReturnType != current.Member.ReturnType;
                    bool paramsChanged = !ParameterTypesEqual(prev.Member.Parameters, current.Member.Parameters);

                    if (returnTypeChanged || paramsChanged)
                    {
                        diff.ChangedMembers.Add(new ChangedMemberEntry
                        {
                            StableId = memberId,
                            Name = current.Member.Name,
                            DeclaringTypeStableId = current.TypeStableId,
                            ChangedIn = versionLabels[i],
                            OldReturnType = prev.Member.ReturnType,
                            NewReturnType = current.Member.ReturnType,
                            OldParameterTypes = prev.Member.Parameters.Select(p => p.Type).ToList(),
                            NewParameterTypes = current.Member.Parameters.Select(p => p.Type).ToList(),
                        });
                    }
                }

                previous = current;
                previousIndex = i;
            }
        }
    }

    private static bool ParameterTypesEqual(
        List<ApiParameterInfo> a, List<ApiParameterInfo> b)
    {
        if (a.Count != b.Count) return false;

        for (int i = 0; i < a.Count; i++)
        {
            if (a[i].Type != b[i].Type) return false;
        }

        return true;
    }

    private static void ClassifyBreakingChanges(VersionDiff diff)
    {
        foreach (var removed in diff.RemovedTypes)
        {
            diff.BreakingChanges.Add(new BreakingChange
            {
                StableId = removed.StableId,
                Kind = BreakingChangeKind.TypeRemoved,
                Reason = $"Public type '{removed.FullName}' was removed.",
                Version = removed.RemovedIn,
            });
        }

        foreach (var removed in diff.RemovedMembers)
        {
            diff.BreakingChanges.Add(new BreakingChange
            {
                StableId = removed.StableId,
                Kind = BreakingChangeKind.MemberRemoved,
                Reason = $"Public member '{removed.Name}' was removed from '{removed.DeclaringTypeStableId}'.",
                Version = removed.RemovedIn,
            });
        }

        foreach (var changed in diff.ChangedMembers)
        {
            if (changed.OldReturnType != changed.NewReturnType)
            {
                diff.BreakingChanges.Add(new BreakingChange
                {
                    StableId = changed.StableId,
                    Kind = BreakingChangeKind.ReturnTypeChanged,
                    Reason = $"Return type of '{changed.Name}' changed from '{changed.OldReturnType}' to '{changed.NewReturnType}'.",
                    Version = changed.ChangedIn,
                });
            }

            if (!changed.OldParameterTypes.SequenceEqual(changed.NewParameterTypes))
            {
                diff.BreakingChanges.Add(new BreakingChange
                {
                    StableId = changed.StableId,
                    Kind = BreakingChangeKind.ParameterTypesChanged,
                    Reason = $"Parameter types of '{changed.Name}' changed.",
                    Version = changed.ChangedIn,
                });
            }
        }
    }

    private static ApiTypeNode CloneTypeNode(ApiTypeNode source)
    {
        return new ApiTypeNode
        {
            StableId = source.StableId,
            FullName = source.FullName,
            Name = source.Name,
            Namespace = source.Namespace,
            Kind = source.Kind,
            BaseType = source.BaseType,
            Interfaces = [.. source.Interfaces],
            GenericParameters = source.GenericParameters
                .Select(g => new GenericParameterInfo
                {
                    Name = g.Name,
                    Position = g.Position,
                    Variance = g.Variance,
                    Constraints = [.. g.Constraints],
                })
                .ToList(),
            IsGenericType = source.IsGenericType,
            IsGenericTypeDefinition = source.IsGenericTypeDefinition,
            IsConstructedGenericType = source.IsConstructedGenericType,
            ContainsGenericParameters = source.ContainsGenericParameters,
            IsSealed = source.IsSealed,
            IsAbstract = source.IsAbstract,
            IsStatic = source.IsStatic,
            Members = source.Members.Select(CloneMemberNode).ToList(),
        };
    }

    private static ApiMemberNode CloneMemberNode(ApiMemberNode source)
    {
        return new ApiMemberNode
        {
            StableId = source.StableId,
            Name = source.Name,
            Kind = source.Kind,
            ReturnType = source.ReturnType,
            Parameters = source.Parameters
                .Select(p => new ApiParameterInfo
                {
                    Name = p.Name,
                    Type = p.Type,
                    IsOptional = p.IsOptional,
                    IsParams = p.IsParams,
                    IsOut = p.IsOut,
                    IsRef = p.IsRef,
                    DefaultValue = p.DefaultValue,
                })
                .ToList(),
            GenericParameters = source.GenericParameters
                .Select(g => new GenericParameterInfo
                {
                    Name = g.Name,
                    Position = g.Position,
                    Variance = g.Variance,
                    Constraints = [.. g.Constraints],
                })
                .ToList(),
            IsGenericMethod = source.IsGenericMethod,
            IsGenericMethodDefinition = source.IsGenericMethodDefinition,
            IsConstructedGenericMethod = source.IsConstructedGenericMethod,
            ContainsGenericParameters = source.ContainsGenericParameters,
            IsStatic = source.IsStatic,
            IsVirtual = source.IsVirtual,
            IsAbstract = source.IsAbstract,
            HasGetter = source.HasGetter,
            HasSetter = source.HasSetter,
        };
    }
}
