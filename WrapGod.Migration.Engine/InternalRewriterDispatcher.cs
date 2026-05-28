using Microsoft.CodeAnalysis;
using WrapGod.Migration;

namespace WrapGod.Migration.Engine;

/// <summary>
/// Drives a single <see cref="IRuleRewriter"/> over a syntax root.
/// Each rewriter internally uses a <c>CSharpSyntaxRewriter</c> that walks the tree
/// when <c>TryRewrite</c> is called with the compilation-unit root, so this
/// dispatcher simply invokes that single entry point.
///
/// Composition strategy — Option A: sequential per-file chain.
/// Rules execute in schema order; the output of rule N becomes the input of rule N+1.
/// This means one tree-walk per (file, rule) pair.  Option A was chosen over the
/// single-dispatch Option B because (a) the perf test passes well within the 5 s
/// budget, (b) the code is straightforward and easy to reason about, and (c) each
/// existing rewriter already encapsulates its own tree walk so no refactoring is
/// needed.  If profiling shows per-rule walks are a bottleneck, Option B can be
/// introduced incrementally.
/// </summary>
internal static class InternalRewriterDispatcher
{
    /// <summary>
    /// Applies <paramref name="rewriter"/> to <paramref name="root"/> using
    /// <paramref name="rule"/> and records outcomes in <paramref name="ctx"/>.
    /// Returns the (possibly rewritten) root, or the same root if the rewriter
    /// did not match anything.
    /// </summary>
    internal static SyntaxNode Apply(
        IRuleRewriter rewriter,
        SyntaxNode root,
        MigrationRule rule,
        RewriteContext ctx)
    {
        var result = rewriter.TryRewrite(root, rule, ctx);
        return result ?? root;
    }
}
