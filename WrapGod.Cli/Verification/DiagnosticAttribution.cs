using WrapGod.Migration.Engine;

namespace WrapGod.Cli.Verification;

/// <summary>The result of attributing a single <see cref="CompilerDiagnostic"/>.</summary>
internal sealed class DiagnosticAttribution
{
    /// <summary>The diagnostic that was attributed.</summary>
    public CompilerDiagnostic Diagnostic { get; init; } = null!;

    /// <summary>
    /// The <c>RuleId</c> of the applied rewrite that is closest to this diagnostic,
    /// or <see langword="null"/> when the diagnostic is unattributed (pre-existing or
    /// no rewrite within ±3 lines).
    /// </summary>
    public string? AttributedRuleId { get; init; }

    /// <summary>Absolute distance in lines between the diagnostic and the attributed rewrite.</summary>
    public int Distance { get; init; }

    /// <summary>
    /// <see langword="true"/> when the diagnostic existed in the baseline snapshot and is
    /// therefore classified as pre-existing rather than migration-introduced.
    /// </summary>
    public bool IsPreExisting { get; init; }
}

/// <summary>
/// Attributes compiler diagnostics to migration rules by comparing each diagnostic's
/// source location against the <see cref="AppliedRewrite"/> entries from the state file,
/// using a ±3 line proximity window.
/// </summary>
internal static class RuleAttributor
{
    private const int ProximityWindow = 3;

    /// <summary>
    /// Attributes each diagnostic in <paramref name="diagnostics"/> to the nearest
    /// <see cref="AppliedRewrite"/> within ±3 lines of the same file.
    /// </summary>
    /// <param name="diagnostics">Parsed compiler diagnostics to attribute.</param>
    /// <param name="appliedRewrites">Applied rewrites from the migration state file.</param>
    /// <param name="baselineDiagnostics">
    /// Optional baseline diagnostics (pre-migration). When provided, diagnostics that appear
    /// in both <paramref name="diagnostics"/> and <paramref name="baselineDiagnostics"/>
    /// (matched by file+line+code) are marked <see cref="DiagnosticAttribution.IsPreExisting"/>.
    /// </param>
    /// <returns>One <see cref="DiagnosticAttribution"/> per input diagnostic.</returns>
    public static IReadOnlyList<DiagnosticAttribution> Attribute(
        IEnumerable<CompilerDiagnostic> diagnostics,
        IEnumerable<AppliedRewrite> appliedRewrites,
        IEnumerable<CompilerDiagnostic>? baselineDiagnostics = null)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        ArgumentNullException.ThrowIfNull(appliedRewrites);

        // Build a quick-lookup set from the baseline.
        var baselineSet = BuildBaselineSet(baselineDiagnostics);

        // Group applied rewrites by normalised file path for O(1) lookup.
        var rewritesByFile = appliedRewrites
            .GroupBy(r => NormalisePath(r.File), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var results = new List<DiagnosticAttribution>();

        foreach (var diag in diagnostics)
        {
            var isPreExisting = baselineSet.Contains(BaselineKey(diag));

            string? attributedRuleId = null;
            var bestDistance = int.MaxValue;

            if (diag.FilePath is not null)
            {
                var normFile = NormalisePath(diag.FilePath);

                if (rewritesByFile.TryGetValue(normFile, out var candidates))
                {
                    // Find the nearest rewrite within ±3 lines.
                    // Tiebreak: smallest distance wins; if equal, latest AppliedAt wins.
                    // AppliedRewrite does not currently carry an AppliedAt timestamp exposed
                    // by the model, so we use declaration order as the secondary tiebreak
                    // (later in the list = later applied, consistent with append-only semantics).
                    AppliedRewrite? best = null;
                    int bestIdx = -1;

                    for (var idx = 0; idx < candidates.Count; idx++)
                    {
                        var candidate = candidates[idx];
                        var dist = Math.Abs(candidate.Line - diag.Line);
                        if (dist > ProximityWindow)
                            continue;

                        if (dist < bestDistance ||
                            (dist == bestDistance && idx > bestIdx))
                        {
                            bestDistance      = dist;
                            best              = candidate;
                            bestIdx           = idx;
                        }
                    }

                    if (best is not null)
                        attributedRuleId = best.RuleId;
                }
            }

            results.Add(new DiagnosticAttribution
            {
                Diagnostic       = diag,
                AttributedRuleId = attributedRuleId,
                Distance         = attributedRuleId is null ? -1 : bestDistance,
                IsPreExisting    = isPreExisting,
            });
        }

        return results;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string NormalisePath(string path)
    {
        // Normalise separators to forward slash for cross-platform key comparison.
        return path.Replace('\\', '/').Trim();
    }

    private static string BaselineKey(CompilerDiagnostic d) =>
        $"{NormalisePath(d.FilePath ?? string.Empty)}|{d.Line}|{d.Code}";

    private static HashSet<string> BuildBaselineSet(IEnumerable<CompilerDiagnostic>? baseline)
    {
        if (baseline is null)
            return [];

        return [.. baseline.Select(BaselineKey)];
    }
}
