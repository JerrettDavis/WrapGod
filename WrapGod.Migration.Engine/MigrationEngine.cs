using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using WrapGod.Migration;
using WrapGod.Migration.Engine.Rewriters;
using WrapGod.Migration.Engine.Rewriters.Structural;

namespace WrapGod.Migration.Engine;

/// <summary>
/// Top-level orchestrator that applies a <see cref="MigrationSchema"/> to a set of
/// C# source files and produces a <see cref="MigrationResult"/> audit trail.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Composition strategy (Option A — sequential chain):</strong>
/// Rules execute in schema order; rewriter N receives the output of rewriter N−1.
/// One tree-walk per (file, rule) pair.  Option A was chosen because it is simple,
/// the existing rewriters each encapsulate their own walk, and the perf target
/// (&lt;5 s for 1 000 files) is met comfortably.
/// </para>
/// <para>
/// <strong>File I/O:</strong> Inject <see cref="IMigrationFileSystem"/> for testability.
/// The default constructor uses <see cref="RealFileSystem"/>.
/// </para>
/// <para>
/// <strong>Manual-confidence rules:</strong> Collected into
/// <see cref="MigrationResult.Manual"/>; never applied automatically.
/// </para>
/// </remarks>
public sealed class MigrationEngine
{
    // ── Fields ────────────────────────────────────────────────────────────────

    private readonly Dictionary<string, IRuleRewriter> _rewritersByKind;
    private readonly IMigrationFileSystem _fs;

    // ── Constructors ─────────────────────────────────────────────────────────

    /// <summary>
    /// Initializes a new <see cref="MigrationEngine"/> with the provided rewriters
    /// and the default real file system.
    /// </summary>
    /// <param name="rewriters">
    /// The set of <see cref="IRuleRewriter"/> instances to use.
    /// When two rewriters share the same <see cref="IRuleRewriter.Kind"/>, the first
    /// one wins and subsequent duplicates are silently ignored.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="rewriters"/> is <see langword="null"/>.
    /// </exception>
    public MigrationEngine(IEnumerable<IRuleRewriter> rewriters)
        : this(rewriters, new RealFileSystem())
    {
    }

    /// <summary>
    /// Initializes a new <see cref="MigrationEngine"/> with the provided rewriters
    /// and a custom file system.  Intended for testing.
    /// </summary>
    /// <param name="rewriters">The rewriter set (first-wins on duplicate kinds).</param>
    /// <param name="fileSystem">The file system abstraction to use for reads and writes.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="rewriters"/> or <paramref name="fileSystem"/> is
    /// <see langword="null"/>.
    /// </exception>
    internal MigrationEngine(IEnumerable<IRuleRewriter> rewriters, IMigrationFileSystem fileSystem)
    {
        ArgumentNullException.ThrowIfNull(rewriters);
        ArgumentNullException.ThrowIfNull(fileSystem);

        _rewritersByKind = new Dictionary<string, IRuleRewriter>(StringComparer.Ordinal);
        foreach (var r in rewriters)
        {
            // First-wins: duplicate Kinds are silently dropped (a debug-log line would
            // appear here in a production logger but is not asserted in tests).
            _rewritersByKind.TryAdd(r.Kind, r);
        }

        _fs = fileSystem;
    }

    // ── Factory ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a <see cref="MigrationEngine"/> pre-loaded with all A-level and B-level
    /// rewriters that ship with this package.
    /// </summary>
    public static MigrationEngine CreateDefault() =>
        new(
        [
            // A-level rewriters
            new RenameTypeRewriter(),
            new RenameNamespaceRewriter(),
            new RenameMemberRewriter(),
            new ChangeParameterRewriter(),
            new RemoveMemberRewriter(),
            new AddRequiredParameterRewriter(),
            new ChangeTypeReferenceRewriter(),
            // B-level structural rewriters
            new SplitMethodRewriter(),
            new ExtractParameterObjectRewriter(),
            new PropertyToMethodRewriter(),
            new MoveMemberRewriter(),
        ]);

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Applies all rules in <paramref name="schema"/> to every file in
    /// <paramref name="filePaths"/>, writes modified files back to disk, and
    /// returns the aggregated <see cref="MigrationResult"/>.
    /// </summary>
    /// <param name="schema">The migration schema to apply.</param>
    /// <param name="filePaths">The source file paths to process.</param>
    /// <returns>A <see cref="MigrationResult"/> with <see cref="MigrationResult.DryRun"/> = <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="schema"/> or <paramref name="filePaths"/> is
    /// <see langword="null"/>.
    /// </exception>
    public MigrationResult Apply(MigrationSchema schema, IEnumerable<string> filePaths) =>
        Run(schema, filePaths, dryRun: false, alreadyApplied: null);

    /// <summary>
    /// Applies all rules in <paramref name="schema"/> to every file in
    /// <paramref name="filePaths"/>, skipping <c>(ruleId, file)</c> pairs present in
    /// <paramref name="alreadyApplied"/> (state-tracking overload).
    /// Writes modified files back to disk and returns the new <see cref="MigrationResult"/>.
    /// </summary>
    /// <param name="schema">The migration schema to apply.</param>
    /// <param name="filePaths">The source file paths to process.</param>
    /// <param name="alreadyApplied">
    /// A set of <c>(ruleId, file)</c> pairs already applied in a prior run.
    /// Pass an empty set or <see langword="null"/> for an unconditional full run.
    /// </param>
    /// <returns>A <see cref="MigrationResult"/> with <see cref="MigrationResult.DryRun"/> = <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="schema"/> or <paramref name="filePaths"/> is
    /// <see langword="null"/>.
    /// </exception>
    internal MigrationResult Apply(
        MigrationSchema schema,
        IEnumerable<string> filePaths,
        HashSet<(string RuleId, string File)>? alreadyApplied) =>
        Run(schema, filePaths, dryRun: false, alreadyApplied);

    /// <summary>
    /// Runs the same pipeline as <see cref="Apply(MigrationSchema,IEnumerable{string})"/>
    /// but does not write any files to disk.  The returned
    /// <see cref="MigrationResult.RewrittenFiles"/> dictionary is still populated with the
    /// would-be new content for each modified file.
    /// </summary>
    /// <param name="schema">The migration schema to apply.</param>
    /// <param name="filePaths">The source file paths to process.</param>
    /// <returns>A <see cref="MigrationResult"/> with <see cref="MigrationResult.DryRun"/> = <see langword="true"/>.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="schema"/> or <paramref name="filePaths"/> is
    /// <see langword="null"/>.
    /// </exception>
    public MigrationResult DryRun(MigrationSchema schema, IEnumerable<string> filePaths) =>
        Run(schema, filePaths, dryRun: true, alreadyApplied: null);

    /// <summary>
    /// Dry-run overload that honours prior-state filtering (used by
    /// <see cref="StatefulMigrationEngine"/>).
    /// </summary>
    internal MigrationResult DryRun(
        MigrationSchema schema,
        IEnumerable<string> filePaths,
        HashSet<(string RuleId, string File)>? alreadyApplied) =>
        Run(schema, filePaths, dryRun: true, alreadyApplied);

    // ── Core pipeline ─────────────────────────────────────────────────────────

    private MigrationResult Run(
        MigrationSchema schema,
        IEnumerable<string> filePaths,
        bool dryRun,
        HashSet<(string RuleId, string File)>? alreadyApplied)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(filePaths);

        var allApplied = new List<AppliedRewrite>();
        var allSkipped = new List<SkippedRewrite>();
        // key = ruleId → (rule, matched files)
        var manualMatches = new Dictionary<string, (MigrationRule Rule, List<string> Files)>(StringComparer.Ordinal);
        var rewrittenFiles = new Dictionary<string, string>(StringComparer.Ordinal);

        // Deduplicate file paths — same file must not be processed twice.
        var dedupedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Pre-build the list of manual vs. auto rules once per run.
        var autoRules = new List<MigrationRule>();
        // Track unknown-kind auto rules to emit a single schema-level SkippedRewrite
        // per (rule, kind) instead of one per file (Should-fix #3).
        var unknownKindRules = new List<MigrationRule>();
        foreach (var rule in schema.Rules)
        {
            if (rule.Confidence == RuleConfidence.Manual)
            {
                if (!manualMatches.ContainsKey(rule.Id))
                    manualMatches[rule.Id] = (rule, []);
            }
            else
            {
                autoRules.Add(rule);
                if (!_rewritersByKind.ContainsKey(KindKey(rule)))
                    unknownKindRules.Add(rule);
            }
        }

        // Emit a single SkippedRewrite per unknown-kind auto rule (schema-level,
        // not per-file). File = "<schema>", Line = 0.  Done up-front so the audit
        // trail is independent of file count.
        foreach (var rule in unknownKindRules)
        {
            allSkipped.Add(new SkippedRewrite(
                rule.Id,
                "<schema>",
                0,
                $"no rewriter for kind '{KindKey(rule)}'"));
        }

        foreach (var filePath in filePaths)
        {
            if (!dedupedPaths.Add(filePath))
                continue; // already processed

            // ── Read ──────────────────────────────────────────────────────────
            string sourceText;
            try
            {
                sourceText = _fs.ReadAllText(filePath);
            }
            catch (IOException ex)
            {
                // Record a synthetic SkippedRewrite and continue.
                allSkipped.Add(new SkippedRewrite("<io>", filePath, 0, ex.Message));
                continue;
            }

            // ── Parse (syntax-only; works on broken code) ─────────────────────
            var tree = CSharpSyntaxTree.ParseText(sourceText, path: filePath);
            var root = tree.GetRoot();

            // ── Manual rule detection ─────────────────────────────────────────
            // Must-fix #2: use a THROWAWAY context for detection so any Applied /
            // Skipped entries recorded by the rewriter do NOT leak into the main
            // audit trail.  We only care whether the rule WOULD match here.
            foreach (var (ruleId, (rule, files)) in manualMatches)
            {
                var kindKey = KindKey(rule);
                if (_rewritersByKind.TryGetValue(kindKey, out var manualRewriter))
                {
                    var detectionCtx = new RewriteContext(filePath);
                    var matchResult = manualRewriter.TryRewrite(root, rule, detectionCtx);
                    if (matchResult is not null ||
                        detectionCtx.Applied.Count > 0 ||
                        detectionCtx.Skipped.Count > 0)
                    {
                        files.Add(filePath);
                    }
                    // detectionCtx is intentionally dropped on the floor here.
                }
            }

            // ── Auto rules (Option A: sequential chain) ───────────────────────
            var ctx2 = new RewriteContext(filePath, alreadyApplied);
            var currentRoot = root;
            bool fileModified = false;

            foreach (var rule in autoRules)
            {
                var kindKey = KindKey(rule);
                if (!_rewritersByKind.TryGetValue(kindKey, out var rewriter))
                {
                    // Schema-level skip already emitted up-front; do not duplicate.
                    continue;
                }

                // State-tracking: skip rules already applied to this file in a prior run.
                if (ctx2.IsAlreadyApplied(rule.Id))
                    continue;

                var newRoot = InternalRewriterDispatcher.Apply(rewriter, currentRoot, rule, ctx2);
                if (!ReferenceEquals(newRoot, currentRoot))
                {
                    currentRoot = newRoot;
                    fileModified = true;
                }
            }

            // ── Cross-namespace using injection ───────────────────────────────
            // After all per-rule rewrites, ensure that every namespace introduced
            // by a ChangeTypeReference or RenameType/RenameNamespace rule that is
            // not already present in the file is added as a using directive.
            // Must-fix #1: iterate autoRules only — Manual-confidence rules MUST
            // NOT contribute to using injection even if their ID coincides with
            // an applied auto rule's ID.
            if (fileModified)
            {
                currentRoot = InjectMissingUsings(currentRoot, autoRules, ctx2, sourceText);
            }

            allApplied.AddRange(ctx2.Applied);
            allSkipped.AddRange(ctx2.Skipped);

            if (fileModified)
            {
                var newText = currentRoot.ToFullString();
                rewrittenFiles[filePath] = newText;

                if (!dryRun)
                {
                    _fs.WriteAllTextAtomic(filePath, newText);
                }
            }
        }

        // ── Assemble Manual list ───────────────────────────────────────────────
        var manualList = manualMatches.Values
            .Select(m => new ManualRewrite(m.Rule.Id, m.Rule.Note ?? string.Empty, m.Files))
            .ToList();

        return new MigrationResult(
            applied: allApplied,
            skipped: allSkipped,
            manual: manualList,
            rewrittenFiles: rewrittenFiles,
            dryRun: dryRun);
    }

    // ── Using injection ───────────────────────────────────────────────────────

    /// <summary>
    /// Inspects the rewritten compilation unit and adds any <c>using</c> directives
    /// that are required by the applied rules but not already present in the file.
    /// Only acts when the rule introduces a new namespace (e.g.
    /// <see cref="ChangeTypeReferenceRule"/>, <see cref="RenameNamespaceRule"/>).
    /// </summary>
    /// <param name="root">The rewritten syntax root (post all rule rewrites).</param>
    /// <param name="autoRules">
    /// The list of auto-confidence rules considered.  Manual-confidence rules MUST NOT
    /// be passed in here — only auto rules may contribute to using injection
    /// (see Must-fix #1 in the #196 review).
    /// </param>
    /// <param name="ctx">The accumulated rewrite context for the file.</param>
    /// <param name="originalSourceText">The original file source, used to detect the
    /// dominant line-ending convention so injected usings respect it.</param>
    private static Microsoft.CodeAnalysis.SyntaxNode InjectMissingUsings(
        Microsoft.CodeAnalysis.SyntaxNode root,
        IEnumerable<MigrationRule> autoRules,
        RewriteContext ctx,
        string originalSourceText)
    {
        if (root is not CompilationUnitSyntax compilationUnit)
            return root;

        // Collect namespaces currently present in the file.
        var existingUsings = new HashSet<string>(StringComparer.Ordinal);
        foreach (var u in compilationUnit.Usings)
        {
            var name = u.Name?.ToString();
            if (name is not null)
                existingUsings.Add(name);
        }

        // Collect namespaces to inject from applied rewrites and matching auto rules.
        var toInject = new List<string>();
        foreach (var rule in autoRules)
        {
            string? ns = rule switch
            {
                ChangeTypeReferenceRule ctr => Rewriters.RewriterHelpers.Namespace(ctr.NewType),
                RenameNamespaceRule rnr => rnr.NewNamespace,
                RenameTypeRule rtr => Rewriters.RewriterHelpers.Namespace(rtr.NewName),
                _ => null,
            };

            // Only inject if:
            // 1. The namespace is non-trivial (not empty = no namespace prefix)
            // 2. At least one applied rewrite is attributed to this rule
            // 3. The namespace is not already present
            if (string.IsNullOrEmpty(ns))
                continue;

            bool ruleWasApplied = ctx.Applied.Any(a => a.RuleId == rule.Id);
            if (!ruleWasApplied)
                continue;

            if (!existingUsings.Contains(ns) && !toInject.Contains(ns))
                toInject.Add(ns);
        }

        if (toInject.Count == 0)
            return root;

        // Should-fix #1: detect the file's line-ending convention so we don't
        // sprinkle CRLF into an LF-only file (or vice versa).
        var newline = DetectNewline(originalSourceText);
        var trailing = newline == "\r\n"
            ? SyntaxFactory.CarriageReturnLineFeed
            : SyntaxFactory.LineFeed;

        // Build new using directives and prepend them to the compilation unit.
        var newUsings = toInject.Select(ns =>
            SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(ns))
                .WithTrailingTrivia(trailing));

        return compilationUnit.AddUsings([.. newUsings]);
    }

    /// <summary>
    /// Returns the dominant line-ending sequence in <paramref name="source"/>.
    /// Returns <c>"\r\n"</c> when the first observed line break is CRLF, else <c>"\n"</c>.
    /// Returns <c>"\n"</c> if no line break is present (single-line file).
    /// </summary>
    private static string DetectNewline(string source)
    {
        for (int i = 0; i < source.Length; i++)
        {
            if (source[i] == '\n')
                return i > 0 && source[i - 1] == '\r' ? "\r\n" : "\n";
        }
        return "\n";
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Converts a <see cref="MigrationRule.Kind"/> enum value to the camelCase
    /// string key used by <see cref="IRuleRewriter.Kind"/>.
    /// </summary>
    private static string KindKey(MigrationRule rule)
    {
        var s = rule.Kind.ToString();
        return char.ToLowerInvariant(s[0]) + s[1..];
    }
}
