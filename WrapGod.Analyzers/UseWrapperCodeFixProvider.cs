using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace WrapGod.Analyzers;

/// <summary>
/// Code fix provider that offers automatic migration from direct third-party API
/// usage to generated wrapper usage.
///
/// Fixes:
///   WG2001 — replaces direct type references with the wrapper interface
///   WG2002 — replaces direct method calls with facade calls
///
/// Reads the same <c>*.wrapgod-types.txt</c> additional files used by
/// <see cref="DirectUsageAnalyzer"/> to determine the deterministic mapping.
/// Supports FixAll via the built-in <see cref="WellKnownFixAllProviders.BatchFixer"/>.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(UseWrapperCodeFixProvider))]
[Shared]
public sealed class UseWrapperCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds { get; } =
        ImmutableArray.Create("WG2001", "WG2002");

    public override FixAllProvider GetFixAllProvider() =>
        WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken)
            .ConfigureAwait(false);
        if (root is null)
            return;

        foreach (var diagnostic in context.Diagnostics)
        {
            var span = diagnostic.Location.SourceSpan;
            var node = root.FindNode(span, getInnermostNodeForTie: true);

            if (diagnostic.Id == "WG2001")
            {
                RegisterTypeReplacementFix(context, diagnostic, root, node);
            }
            else if (diagnostic.Id == "WG2002")
            {
                RegisterMethodReplacementFix(context, diagnostic, root, node);
            }
        }
    }

    // ── WG2001: Replace type reference with wrapper interface ────────

    private static void RegisterTypeReplacementFix(
        CodeFixContext context,
        Diagnostic diagnostic,
        SyntaxNode root,
        SyntaxNode node)
    {
        var wrapperInterface = GetMessageArg(diagnostic, index: 1);
        if (string.IsNullOrEmpty(wrapperInterface))
            return;

        var title = $"Use '{wrapperInterface}' instead";

        context.RegisterCodeFix(
            CodeAction.Create(
                title,
                ct => ReplaceTypeReferenceAsync(context.Document, root, node, wrapperInterface!, ct),
                equivalenceKey: "WG2001_UseWrapper"),
            diagnostic);
    }

    private static Task<Document> ReplaceTypeReferenceAsync(
        Document document,
        SyntaxNode root,
        SyntaxNode node,
        string wrapperInterface,
        CancellationToken cancellationToken)
    {
        var newIdentifier = SyntaxFactory.IdentifierName(wrapperInterface)
            .WithTriviaFrom(node);

        var newRoot = root.ReplaceNode(node, newIdentifier);
        return Task.FromResult(document.WithSyntaxRoot(newRoot));
    }

    // ── WG2002: Replace method call with facade call ─────────────────

    private static void RegisterMethodReplacementFix(
        CodeFixContext context,
        Diagnostic diagnostic,
        SyntaxNode root,
        SyntaxNode node)
    {
        var facadeType = GetMessageArg(diagnostic, index: 2);
        if (string.IsNullOrEmpty(facadeType))
            return;

        var title = $"Use facade '{facadeType}'";

        context.RegisterCodeFix(
            CodeAction.Create(
                title,
                ct => ReplaceMemberAccessWithFacadeAsync(
                    context.Document, root, node, facadeType!, ct),
                equivalenceKey: "WG2002_UseFacade"),
            diagnostic);
    }

    private static Task<Document> ReplaceMemberAccessWithFacadeAsync(
        Document document,
        SyntaxNode root,
        SyntaxNode node,
        string facadeType,
        CancellationToken cancellationToken)
    {
        if (node is not MemberAccessExpressionSyntax memberAccess)
            return Task.FromResult(document);

        // Replace the receiver (e.g. svc) with the facade type name,
        // keeping the method name intact.
        var facadeIdentifier = SyntaxFactory.IdentifierName(facadeType)
            .WithTriviaFrom(memberAccess.Expression);

        var newMemberAccess = memberAccess.WithExpression(facadeIdentifier);
        var newRoot = root.ReplaceNode(memberAccess, newMemberAccess);

        return Task.FromResult(document.WithSyntaxRoot(newRoot));
    }

    // ── Helpers ──────────────────────────────────────────────────────

    /// <summary>
    /// Extracts a single-quoted argument from the diagnostic message by index.
    /// The analyzer message formats embed arguments in single quotes:
    ///   WG2001: "Type '{0}' has a generated wrapper interface; use '{1}' instead"
    ///   WG2002: "Method '{0}' on type '{1}' should be called through the facade '{2}'"
    /// </summary>
    private static string? GetMessageArg(Diagnostic diagnostic, int index)
    {
        var message = diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture);

        var args = new List<string>();
        var i = 0;
        while (i < message.Length)
        {
            var start = message.IndexOf('\'', i);
            if (start < 0) break;
            var end = message.IndexOf('\'', start + 1);
            if (end < 0) break;
            args.Add(message.Substring(start + 1, end - start - 1));
            i = end + 1;
        }

        return index < args.Count ? args[index] : null;
    }
}
