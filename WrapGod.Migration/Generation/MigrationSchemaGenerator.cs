using WrapGod.Extractor;

namespace WrapGod.Migration.Generation;

/// <summary>
/// Converts a <see cref="VersionDiff"/> (produced by <c>MultiVersionExtractor</c>) into a
/// draft <see cref="MigrationSchema"/> with auto-inferred rules.
/// </summary>
/// <remarks>
/// The generator performs similarity-based rename detection using Jaro-Winkler on the short
/// (unqualified) name. Removed types/members with a close-enough match in the added set are
/// emitted as rename rules; those without a match become <see cref="RemoveMemberRule"/> entries
/// with <see cref="RuleConfidence.Manual"/>. Added-only entries are intentionally skipped
/// (additions cannot be automatically rewritten in callers).
/// </remarks>
public static class MigrationSchemaGenerator
{
    private static readonly MigrationSchemaGeneratorOptions DefaultOptions = new();

    /// <summary>
    /// Produces a draft <see cref="MigrationSchema"/> from a <see cref="VersionDiff"/>.
    /// </summary>
    /// <param name="diff">The diff to convert. Must contain at least two version labels.</param>
    /// <param name="library">The NuGet package / library name (e.g. <c>"MudBlazor"</c>).</param>
    /// <param name="options">Optional tuning options. If <see langword="null"/>, defaults are used.</param>
    /// <param name="stableIdToFullName">
    /// Optional lookup from <c>StableId</c> to fully-qualified type name for types that are
    /// <em>stable</em> across versions (present in both, neither added nor removed). When provided,
    /// the generator resolves <c>TypeName</c>/<c>DeclaringType</c> fields on rules emitted for
    /// changed members (e.g. <see cref="ChangeParameterRule"/>) using this map instead of falling
    /// back to the raw StableId. The CLI command (<c>migrate generate</c>) builds this map from
    /// the merged <c>ApiManifest</c>. Callers without the manifest may omit it; in that case the
    /// declaring-type name falls back to the entry's <c>FullName</c> (if found in
    /// <c>diff.RemovedTypes</c>/<c>AddedTypes</c>) or to the raw <c>StableId</c> as a last resort.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="diff"/> or <paramref name="library"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="library"/> is empty/whitespace, or when <paramref name="diff"/> contains fewer than two version labels.</exception>
    public static MigrationSchema FromDiff(
        VersionDiff diff,
        string library,
        MigrationSchemaGeneratorOptions? options = null,
        IReadOnlyDictionary<string, string>? stableIdToFullName = null)
    {
        ArgumentNullException.ThrowIfNull(diff);
        ArgumentNullException.ThrowIfNull(library);
        if (string.IsNullOrWhiteSpace(library)) throw new ArgumentException("library must not be empty or whitespace", nameof(library));
        if (diff.Versions == null || diff.Versions.Count < 2)
            throw new ArgumentException("diff.Versions must contain at least two version labels", nameof(diff));

        options ??= DefaultOptions;

        var unsortedRules = new List<(SortKey key, MigrationRule rule)>();

        // ── 1. Detect namespace relocations ─────────────────────────────────────────────────
        // Group removed and added types by their namespace prefix.
        // If all removed types in an old-namespace are matched by added types with identical
        // short names in a new-namespace, collapse to a single RenameNamespaceRule.

        var remainingRemovedTypes = new List<RemovedTypeEntry>(diff.RemovedTypes ?? []);
        var remainingAddedTypes = new List<AddedTypeEntry>(diff.AddedTypes ?? []);

        if (!options.DisableRenameDetection)
        {
            var nsRules = DetectNamespaceRelocations(
                remainingRemovedTypes,
                remainingAddedTypes,
                options);

            foreach (var (oldNs, newNs, confidence, removedConsumed, addedConsumed) in nsRules)
            {
                var rule = new RenameNamespaceRule
                {
                    Confidence = confidence,
                    OldNamespace = oldNs,
                    NewNamespace = newNs,
                };
                unsortedRules.Add((new SortKey(0, oldNs, string.Empty), rule));

                // Remove consumed entries so they don't generate per-type renames
                foreach (var r in removedConsumed)
                    remainingRemovedTypes.Remove(r);
                foreach (var a in addedConsumed)
                    remainingAddedTypes.Remove(a);
            }
        }

        // ── 2. Per-type rename / remove ─────────────────────────────────────────────────────
        foreach (var removed in remainingRemovedTypes)
        {
            if (!options.DisableRenameDetection)
            {
                var (match, similarity) = BestTypeMatch(removed, remainingAddedTypes, options);
                if (match != null)
                {
                    // Determine confidence
                    var confidence = similarity >= options.VerifiedSimilarityThreshold
                        ? RuleConfidence.Verified
                        : RuleConfidence.Auto;

                    var rule = new RenameTypeRule
                    {
                        Confidence = confidence,
                        OldName = removed.FullName,
                        NewName = match.FullName,
                    };
                    unsortedRules.Add((new SortKey(1, removed.FullName, string.Empty), rule));
                    remainingAddedTypes.Remove(match);
                    continue;
                }
            }

            // No match — emit RemoveMemberRule (synthetic type-level remove)
            unsortedRules.Add((new SortKey(1, removed.FullName, string.Empty), new RemoveMemberRule
            {
                Confidence = RuleConfidence.Manual,
                TypeName = "<global>",
                MemberName = removed.FullName,
                Note = $"Type '{removed.FullName}' was removed with no detected rename target.",
            }));
        }

        // ── 3. Per-member rename / remove ───────────────────────────────────────────────────
        var remainingRemovedMembers = new List<RemovedMemberEntry>(diff.RemovedMembers ?? []);
        var remainingAddedMembers = new List<AddedMemberEntry>(diff.AddedMembers ?? []);

        // Group added members by declaring type for efficient lookup
        var addedByType = remainingAddedMembers
            .GroupBy(m => m.DeclaringTypeStableId)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var removed in remainingRemovedMembers)
        {
            if (!options.DisableRenameDetection &&
                addedByType.TryGetValue(removed.DeclaringTypeStableId, out var candidates) &&
                candidates.Count > 0)
            {
                var (match, similarity) = BestMemberMatch(removed, candidates, options);
                if (match != null)
                {
                    var confidence = similarity >= options.VerifiedSimilarityThreshold
                        ? RuleConfidence.Verified
                        : RuleConfidence.Auto;

                    var declaringType = GetDeclaringTypeName(removed.DeclaringTypeStableId, diff, stableIdToFullName);
                    var rule = new RenameMemberRule
                    {
                        Confidence = confidence,
                        TypeName = declaringType,
                        OldMemberName = removed.Name,
                        NewMemberName = match.Name,
                    };
                    unsortedRules.Add((new SortKey(2, declaringType, removed.Name), rule));
                    candidates.Remove(match);
                    continue;
                }

                // Check for ambiguous tie: two removed members compete for same added member
                // (handled above by BestMemberMatch returning null on tie if needed)
            }

            // No match — emit RemoveMemberRule with Manual confidence
            var declaringTypeName = GetDeclaringTypeName(removed.DeclaringTypeStableId, diff, stableIdToFullName);
            unsortedRules.Add((new SortKey(2, declaringTypeName, removed.Name), new RemoveMemberRule
            {
                Confidence = RuleConfidence.Manual,
                TypeName = declaringTypeName,
                MemberName = removed.Name,
                Note = $"Member '{removed.Name}' on '{declaringTypeName}' was removed with no detected rename target.",
            }));
        }

        // ── 4. Changed members ─────────────────────────────────────────────────────────────
        foreach (var changed in diff.ChangedMembers ?? [])
        {
            var declaringType = GetDeclaringTypeName(changed.DeclaringTypeStableId, diff, stableIdToFullName);

            // Return type changed → ChangeTypeReferenceRule
            if (changed.OldReturnType != null && changed.NewReturnType != null &&
                changed.OldReturnType != changed.NewReturnType)
            {
                unsortedRules.Add((new SortKey(3, declaringType, changed.Name), new ChangeTypeReferenceRule
                {
                    Confidence = RuleConfidence.Auto,
                    OldType = changed.OldReturnType,
                    NewType = changed.NewReturnType,
                    Note = $"Return type of '{declaringType}.{changed.Name}' changed.",
                }));
            }

            // Parameter types changed
            var oldParams = changed.OldParameterTypes ?? [];
            var newParams = changed.NewParameterTypes ?? [];

            if (oldParams.Count != newParams.Count)
            {
                if (newParams.Count == oldParams.Count + 1)
                {
                    // Arity grew by 1 — new required parameter added
                    int newIdx = FindNewParameterIndex(oldParams, newParams);
                    unsortedRules.Add((new SortKey(4, declaringType, changed.Name), new AddRequiredParameterRule
                    {
                        Confidence = RuleConfidence.Manual,
                        TypeName = declaringType,
                        MethodName = changed.Name,
                        ParameterName = $"param{newIdx}",
                        ParameterType = newIdx < newParams.Count ? newParams[newIdx] : "object",
                        Position = newIdx,
                        Note = $"A required parameter was added to '{declaringType}.{changed.Name}' at position {newIdx}.",
                    }));
                }
                else
                {
                    // Arity shrank or drastically changed
                    unsortedRules.Add((new SortKey(4, declaringType, changed.Name), new ChangeParameterRule
                    {
                        Confidence = RuleConfidence.Manual,
                        TypeName = declaringType,
                        MethodName = changed.Name,
                        OldParameterName = "?",
                        Note = $"Parameter list of '{declaringType}.{changed.Name}' changed shape (arity {oldParams.Count}→{newParams.Count}); manual inspection required.",
                    }));
                }
            }
            else if (oldParams.Count > 0)
            {
                // Same arity — emit ChangeParameterRule for each changed slot
                for (int i = 0; i < oldParams.Count; i++)
                {
                    if (oldParams[i] != newParams[i])
                    {
                        unsortedRules.Add((new SortKey(4, declaringType, changed.Name), new ChangeParameterRule
                        {
                            Confidence = RuleConfidence.Auto,
                            TypeName = declaringType,
                            MethodName = changed.Name,
                            OldParameterName = $"param{i}",
                            OldParameterType = oldParams[i],
                            NewParameterType = newParams[i],
                            Note = $"Parameter {i} of '{declaringType}.{changed.Name}' type changed from '{oldParams[i]}' to '{newParams[i]}'.",
                        }));
                    }
                }
            }
        }

        // ── 5. Sort rules for determinism, then assign IDs ─────────────────────────────────
        unsortedRules.Sort((x, y) =>
        {
            int cmp = x.key.KindOrdinal.CompareTo(y.key.KindOrdinal);
            if (cmp != 0) return cmp;
            cmp = StringComparer.Ordinal.Compare(x.key.DeclaringType, y.key.DeclaringType);
            if (cmp != 0) return cmp;
            return StringComparer.Ordinal.Compare(x.key.MemberName, y.key.MemberName);
        });

        var allocator = new RuleIdAllocator(library, options.RuleIdPrefix);
        var rules = new List<MigrationRule>(unsortedRules.Count);
        foreach (var (_, rule) in unsortedRules)
        {
            rule.Id = allocator.Next();
            rules.Add(rule);
        }

        return new MigrationSchema
        {
            Schema = "wrapgod-migration/1.0",
            Library = library,
            From = diff.Versions[0],
            To = diff.Versions[diff.Versions.Count - 1],
            GeneratedFrom = "manifest-diff",
            LastEdited = DateTimeOffset.UtcNow,
            Rules = rules,
        };
    }

    // ── Private helpers ──────────────────────────────────────────────────────────────────────

    private static (AddedTypeEntry? match, double similarity) BestTypeMatch(
        RemovedTypeEntry removed,
        List<AddedTypeEntry> candidates,
        MigrationSchemaGeneratorOptions options)
    {
        if (candidates.Count == 0) return (null, 0.0);

        string removedShort = Similarity.ShortName(removed.FullName);

        AddedTypeEntry? best = null;
        double bestSim = -1;
        int bestCount = 0;

        foreach (var candidate in candidates)
        {
            double sim = Similarity.JaroWinkler(removedShort, Similarity.ShortName(candidate.FullName));
            if (sim > bestSim)
            {
                bestSim = sim;
                best = candidate;
                bestCount = 1;
            }
            else if (sim == bestSim)
            {
                bestCount++;
            }
        }

        if (bestSim < options.RenameSimilarityThreshold)
            return (null, bestSim);

        // Tie: two candidates share the same best similarity → ambiguous
        if (bestCount > 1)
        {
            // Deterministic tiebreak: pick lexicographically smallest StableId
            AddedTypeEntry? winner = null;
            foreach (var candidate in candidates)
            {
                double sim = Similarity.JaroWinkler(removedShort, Similarity.ShortName(candidate.FullName));
                if (Math.Abs(sim - bestSim) < 1e-10)
                {
                    if (winner == null ||
                        StringComparer.Ordinal.Compare(candidate.StableId, winner.StableId) < 0)
                        winner = candidate;
                }
            }
            return (winner, bestSim);
        }

        return (best, bestSim);
    }

    private static (AddedMemberEntry? match, double similarity) BestMemberMatch(
        RemovedMemberEntry removed,
        List<AddedMemberEntry> candidates,
        MigrationSchemaGeneratorOptions options)
    {
        if (candidates.Count == 0) return (null, 0.0);

        // Plan §193: for methods, require identical parameter arity.
        // Parse arity from the member StableId. If the removed member has no parameter list
        // (e.g., property/field/event), skip the arity guard entirely.
        int? removedArity = TryParseArityFromStableId(removed.StableId);

        AddedMemberEntry? best = null;
        double bestSim = -1;
        int bestCount = 0;

        foreach (var candidate in candidates)
        {
            // Arity guard: only applies when BOTH sides have parsable parameter lists.
            if (removedArity is int ra)
            {
                int? candArity = TryParseArityFromStableId(candidate.StableId);
                if (candArity is int ca && ca != ra) continue;
            }

            double sim = Similarity.JaroWinkler(removed.Name, candidate.Name);
            if (sim > bestSim)
            {
                bestSim = sim;
                best = candidate;
                bestCount = 1;
            }
            else if (sim == bestSim)
            {
                bestCount++;
            }
        }

        if (best == null || bestSim < options.RenameSimilarityThreshold)
            return (null, bestSim);

        if (bestCount > 1)
        {
            // Deterministic tiebreak: lexicographically smallest StableId
            AddedMemberEntry? winner = null;
            foreach (var candidate in candidates)
            {
                if (removedArity is int ra2)
                {
                    int? candArity = TryParseArityFromStableId(candidate.StableId);
                    if (candArity is int ca && ca != ra2) continue;
                }
                double sim = Similarity.JaroWinkler(removed.Name, candidate.Name);
                if (Math.Abs(sim - bestSim) < 1e-10)
                {
                    if (winner == null ||
                        StringComparer.Ordinal.Compare(candidate.StableId, winner.StableId) < 0)
                        winner = candidate;
                }
            }
            return (winner, bestSim);
        }

        return (best, bestSim);
    }

    private static List<(string oldNs, string newNs, RuleConfidence confidence,
        List<RemovedTypeEntry> removed, List<AddedTypeEntry> added)>
    DetectNamespaceRelocations(
        List<RemovedTypeEntry> removedTypes,
        List<AddedTypeEntry> addedTypes,
        MigrationSchemaGeneratorOptions options)
    {
        var result = new List<(string, string, RuleConfidence, List<RemovedTypeEntry>, List<AddedTypeEntry>)>();

        // Group removed types by namespace
        var removedByNs = removedTypes
            .GroupBy(r => Similarity.Namespace(r.FullName))
            .Where(g => !string.IsNullOrEmpty(g.Key))
            .ToList();

        var usedAdded = new HashSet<string>();

        foreach (var nsGroup in removedByNs)
        {
            var oldNs = nsGroup.Key;
            var nsRemoved = nsGroup.ToList();

            // For each removed type, find its best added match across ALL namespaces by short name
            // Check if all matched added types share the same namespace (i.e., namespace relocation)
            var matchedPairs = new List<(RemovedTypeEntry removed, AddedTypeEntry added)>();
            bool allMatched = true;

            foreach (var r in nsRemoved)
            {
                string shortName = Similarity.ShortName(r.FullName);
                AddedTypeEntry? bestMatch = null;
                double bestSim = -1;

                foreach (var a in addedTypes)
                {
                    if (usedAdded.Contains(a.StableId)) continue;
                    if (Similarity.ShortName(a.FullName) == shortName)
                    {
                        double sim = Similarity.JaroWinkler(r.FullName, a.FullName);
                        if (sim > bestSim)
                        {
                            bestSim = sim;
                            bestMatch = a;
                        }
                    }
                }

                if (bestMatch == null || bestSim < options.RenameSimilarityThreshold)
                {
                    allMatched = false;
                    break;
                }

                matchedPairs.Add((r, bestMatch));
            }

            if (!allMatched || matchedPairs.Count < 2) continue; // Need ≥2 types to justify a namespace rule

            // All matched — check they share the same new namespace
            string? newNs = Similarity.Namespace(matchedPairs[0].added.FullName);
            bool sameNewNs = matchedPairs.All(p => Similarity.Namespace(p.added.FullName) == newNs);
            if (!sameNewNs) continue;
            if (newNs == oldNs) continue; // Namespace didn't change

            // All types moved from oldNs → newNs
            var confidence = RuleConfidence.Auto;
            var removedConsumed = matchedPairs.Select(p => p.removed).ToList();
            var addedConsumed = matchedPairs.Select(p => p.added).ToList();
            foreach (var a in addedConsumed) usedAdded.Add(a.StableId);

            result.Add((oldNs, newNs!, confidence, removedConsumed, addedConsumed));
        }

        return result;
    }

    private static string GetDeclaringTypeName(
        string declaringTypeStableId,
        VersionDiff diff,
        IReadOnlyDictionary<string, string>? stableIdToFullName)
    {
        if (string.IsNullOrEmpty(declaringTypeStableId)) return declaringTypeStableId;

        // 1. Caller-supplied lookup is authoritative (covers STABLE types not in the diff).
        if (stableIdToFullName != null &&
            stableIdToFullName.TryGetValue(declaringTypeStableId, out var stableName) &&
            !string.IsNullOrEmpty(stableName))
        {
            return stableName;
        }

        // 2. Try removed types (members removed from old version).
        foreach (var t in diff.RemovedTypes ?? [])
        {
            if (t.StableId == declaringTypeStableId)
                return t.FullName;
        }
        // 3. Then added types.
        foreach (var t in diff.AddedTypes ?? [])
        {
            if (t.StableId == declaringTypeStableId)
                return t.FullName;
        }

        // 4. Last resort: best-effort cleanup of common StableId prefixes (e.g., "T:Foo.Bar").
        //    Strip a leading "T:" if present, which is the docfx/Roslyn convention.
        if (declaringTypeStableId.StartsWith("T:", StringComparison.Ordinal))
            return declaringTypeStableId.Substring(2);

        return declaringTypeStableId;
    }

    /// <summary>
    /// Parses the parameter arity out of an <see cref="ApiMemberNode.StableId"/>-style identifier.
    /// The format is <c>typeStableId.memberName(paramTypes)</c> per the manifest contract.
    /// Returns <see langword="null"/> when no parameter list is present (properties, fields, events).
    /// </summary>
    private static int? TryParseArityFromStableId(string memberStableId)
    {
        if (string.IsNullOrEmpty(memberStableId)) return null;
        int open = memberStableId.LastIndexOf('(');
        int close = memberStableId.LastIndexOf(')');
        if (open < 0 || close < 0 || close <= open) return null;

        // Inside parens. "()" → 0 params; otherwise count comma separators at depth 0.
        var inner = memberStableId.AsSpan(open + 1, close - open - 1).Trim();
        if (inner.IsEmpty) return 0;

        int count = 1;
        int depth = 0;
        for (int i = 0; i < inner.Length; i++)
        {
            char c = inner[i];
            if (c == '<' || c == '[' || c == '(') depth++;
            else if (c == '>' || c == ']' || c == ')') depth--;
            else if (c == ',' && depth == 0) count++;
        }
        return count;
    }

    private static int FindNewParameterIndex(List<string> oldParams, List<string> newParams)
    {
        // Find the first index where the new list diverges from the old
        for (int i = 0; i < oldParams.Count; i++)
        {
            if (i >= newParams.Count || oldParams[i] != newParams[i])
                return i;
        }
        // New param appended at the end
        return oldParams.Count;
    }

    private readonly struct SortKey
    {
        public readonly int KindOrdinal;
        public readonly string DeclaringType;
        public readonly string MemberName;

        public SortKey(int kindOrdinal, string declaringType, string memberName)
        {
            KindOrdinal = kindOrdinal;
            DeclaringType = declaringType ?? string.Empty;
            MemberName = memberName ?? string.Empty;
        }
    }
}
