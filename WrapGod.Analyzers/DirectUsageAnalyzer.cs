using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace WrapGod.Analyzers;

/// <summary>
/// Roslyn analyzer that detects direct usage of third-party types that should
/// be accessed through WrapGod-generated wrapper interfaces and facades.
///
/// Reads wrapped type mappings from an AdditionalFile named
/// <c>*.wrapgod-types.txt</c> (one mapping per line):
///   <c>Acme.Lib.FooService -> IWrappedFooService, FooServiceFacade</c>
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DirectUsageAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(
            DiagnosticDescriptors.DirectTypeUsage,
            DiagnosticDescriptors.DirectMethodCall);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(compilationStart =>
        {
            var mappings = LoadMappings(compilationStart.Options.AdditionalFiles);
            if (mappings.Count == 0)
                return;

            // Build a lookup from fully-qualified type name -> mapping info.
            var lookup = mappings.ToImmutableDictionary(m => m.OriginalType, StringComparer.Ordinal);

            compilationStart.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeNode(nodeContext, lookup),
                SyntaxKind.IdentifierName,
                SyntaxKind.SimpleMemberAccessExpression);
        });
    }

    // ── Node analysis ────────────────────────────────────────────────

    private static void AnalyzeNode(
        SyntaxNodeAnalysisContext context,
        ImmutableDictionary<string, WrappedTypeMapping> lookup)
    {
        var node = context.Node;
        var semanticModel = context.SemanticModel;

        switch (node)
        {
            case IdentifierNameSyntax identifier:
                AnalyzeIdentifier(context, identifier, semanticModel, lookup);
                break;
            case MemberAccessExpressionSyntax memberAccess:
                AnalyzeMemberAccess(context, memberAccess, semanticModel, lookup);
                break;
        }
    }

    private static void AnalyzeIdentifier(
        SyntaxNodeAnalysisContext context,
        IdentifierNameSyntax identifier,
        SemanticModel semanticModel,
        ImmutableDictionary<string, WrappedTypeMapping> lookup)
    {
        // Skip identifiers that are part of a member access (handled separately).
        if (identifier.Parent is MemberAccessExpressionSyntax)
            return;

        var symbolInfo = semanticModel.GetSymbolInfo(identifier, context.CancellationToken);
        var symbol = symbolInfo.Symbol ?? symbolInfo.CandidateSymbols.FirstOrDefault();
        if (symbol is null)
            return;

        var typeSymbol = symbol switch
        {
            ITypeSymbol ts => ts,
            ILocalSymbol ls => ls.Type,
            IParameterSymbol ps => ps.Type,
            IFieldSymbol fs => fs.Type,
            IPropertySymbol ps => ps.Type,
            _ => null
        };

        if (typeSymbol is null)
            return;

        var fullName = GetFullyQualifiedName(typeSymbol);
        if (lookup.TryGetValue(fullName, out var mapping))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.DirectTypeUsage,
                identifier.GetLocation(),
                fullName,
                mapping.WrapperInterface));
        }
    }

    private static void AnalyzeMemberAccess(
        SyntaxNodeAnalysisContext context,
        MemberAccessExpressionSyntax memberAccess,
        SemanticModel semanticModel,
        ImmutableDictionary<string, WrappedTypeMapping> lookup)
    {
        // We only care about method invocations.
        if (memberAccess.Parent is not InvocationExpressionSyntax)
            return;

        var symbolInfo = semanticModel.GetSymbolInfo(memberAccess, context.CancellationToken);
        var symbol = symbolInfo.Symbol as IMethodSymbol;
        if (symbol is null)
            return;

        var containingType = symbol.ContainingType;
        if (containingType is null)
            return;

        var fullName = GetFullyQualifiedName(containingType);
        if (lookup.TryGetValue(fullName, out var mapping))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.DirectMethodCall,
                memberAccess.GetLocation(),
                symbol.Name,
                fullName,
                mapping.FacadeType));
        }
    }

    // ── Mapping file parsing ─────────────────────────────────────────

    /// <summary>
    /// Parses additional files matching <c>*.wrapgod-types.txt</c>.
    /// Each line: <c>Acme.Lib.FooService -&gt; IWrappedFooService, FooServiceFacade</c>
    /// </summary>
    private static List<WrappedTypeMapping> LoadMappings(
        ImmutableArray<AdditionalText> additionalFiles)
    {
        var results = new List<WrappedTypeMapping>();

        foreach (var file in additionalFiles)
        {
            if (!Path.GetFileName(file.Path).EndsWith(".wrapgod-types.txt", StringComparison.OrdinalIgnoreCase))
                continue;

            var text = file.GetText();
            if (text is null)
                continue;

            foreach (var line in text.Lines)
            {
                var raw = line.ToString().Trim();
                if (string.IsNullOrEmpty(raw) || raw.StartsWith("#", StringComparison.Ordinal))
                    continue;

                // Format: OriginalType -> WrapperInterface, FacadeType
                var arrowIndex = raw.IndexOf("->", StringComparison.Ordinal);
                if (arrowIndex < 0)
                    continue;

                var originalType = raw.Substring(0, arrowIndex).Trim();
                var rightSide = raw.Substring(arrowIndex + 2).Trim();
                var parts = rightSide.Split(',');
                if (parts.Length < 2)
                    continue;

                results.Add(new WrappedTypeMapping(
                    originalType,
                    parts[0].Trim(),
                    parts[1].Trim()));
            }
        }

        return results;
    }

    private static string GetFullyQualifiedName(ITypeSymbol typeSymbol)
    {
        return typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
            .Replace("global::", string.Empty);
    }

    // ── Mapping model ────────────────────────────────────────────────

    internal sealed class WrappedTypeMapping
    {
        public string OriginalType { get; }
        public string WrapperInterface { get; }
        public string FacadeType { get; }

        public WrappedTypeMapping(string originalType, string wrapperInterface, string facadeType)
        {
            OriginalType = originalType;
            WrapperInterface = wrapperInterface;
            FacadeType = facadeType;
        }
    }
}
