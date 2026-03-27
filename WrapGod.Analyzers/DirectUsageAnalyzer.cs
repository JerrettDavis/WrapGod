using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json;
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
/// <b>Primary source (preferred):</b> derives type mappings from
/// <c>*.wrapgod.json</c> manifest files and optional <c>*.wrapgod.config.json</c>
/// config files (the same files the generator reads). Config renames
/// (<c>TargetName</c>) are applied when deriving wrapper/facade names.
///
/// <b>Fallback:</b> reads <c>*.wrapgod-types.txt</c> files (legacy format,
/// one mapping per line):
///   <c>Acme.Lib.FooService -&gt; IWrappedFooService, FooServiceFacade</c>
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
            var additionalFiles = compilationStart.Options.AdditionalFiles;

            // Primary: derive mappings from manifest + config.
            var mappings = LoadMappingsFromManifests(additionalFiles);

            // Fallback: legacy .wrapgod-types.txt files.
            if (mappings.Count == 0)
                mappings = LoadMappingsFromTxtFiles(additionalFiles);

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

    // ── Manifest + config mapping discovery ──────────────────────────

    /// <summary>
    /// Derives type mappings from <c>*.wrapgod.json</c> manifests, applying
    /// any rename rules found in <c>*.wrapgod.config.json</c> config files.
    /// </summary>
    internal static List<WrappedTypeMapping> LoadMappingsFromManifests(
        ImmutableArray<AdditionalText> additionalFiles)
    {
        // 1. Parse config files to build a rename lookup.
        var configRenames = new Dictionary<string, string>(StringComparer.Ordinal);
        var configExcludes = new HashSet<string>(StringComparer.Ordinal);

        foreach (var file in additionalFiles)
        {
            var fileName = Path.GetFileName(file.Path);
            if (!fileName.EndsWith(".wrapgod.config.json", StringComparison.OrdinalIgnoreCase))
                continue;

            var text = file.GetText();
            if (text is null)
                continue;

            ParseConfigJson(text.ToString(), configRenames, configExcludes);
        }

        // 2. Parse manifest files and derive mappings.
        var results = new List<WrappedTypeMapping>();

        foreach (var file in additionalFiles)
        {
            var fileName = Path.GetFileName(file.Path);
            if (!fileName.EndsWith(".wrapgod.json", StringComparison.OrdinalIgnoreCase))
                continue;
            // Skip config files that also end with .wrapgod.json pattern.
            if (fileName.EndsWith(".wrapgod.config.json", StringComparison.OrdinalIgnoreCase))
                continue;

            var text = file.GetText();
            if (text is null)
                continue;

            ParseManifestJson(text.ToString(), configRenames, configExcludes, results);
        }

        return results;
    }

    /// <summary>
    /// Parses a <c>*.wrapgod.config.json</c> and populates rename and exclude lookups.
    /// </summary>
    private static void ParseConfigJson(
        string json,
        Dictionary<string, string> renames,
        HashSet<string> excludes)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("types", out var typesEl) ||
                typesEl.ValueKind != JsonValueKind.Array)
                return;

            foreach (var typeEl in typesEl.EnumerateArray())
            {
                var sourceType = GetJsonString(typeEl, "sourceType");
                if (string.IsNullOrEmpty(sourceType))
                    continue;

                // Check for explicit exclusion.
                if (typeEl.TryGetProperty("include", out var includeEl) &&
                    includeEl.ValueKind == JsonValueKind.False)
                {
                    excludes.Add(sourceType);
                    continue;
                }

                // Check for rename.
                var targetName = GetJsonString(typeEl, "targetName");
                if (!string.IsNullOrEmpty(targetName))
                {
                    renames[sourceType] = targetName;
                }
            }
        }
#pragma warning disable CA1031 // analyzer must not crash
        catch
#pragma warning restore CA1031
        {
            // Silently ignore malformed config.
        }
    }

    /// <summary>
    /// Parses a <c>*.wrapgod.json</c> manifest and appends derived mappings to <paramref name="results"/>.
    /// Applies config renames and exclusions.
    /// </summary>
    private static void ParseManifestJson(
        string json,
        Dictionary<string, string> renames,
        HashSet<string> excludes,
        List<WrappedTypeMapping> results)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("types", out var typesEl) ||
                typesEl.ValueKind != JsonValueKind.Array)
                return;

            foreach (var typeEl in typesEl.EnumerateArray())
            {
                var fullName = GetJsonString(typeEl, "fullName");
                var name = GetJsonString(typeEl, "name");

                if (string.IsNullOrEmpty(fullName) || string.IsNullOrEmpty(name))
                    continue;

                // Skip excluded types.
                if (excludes.Contains(fullName))
                    continue;

                // Apply config rename if present.
                var effectiveName = renames.TryGetValue(fullName, out var renamed)
                    ? renamed
                    : name;

                // Derive wrapper names using same convention as the generator.
                var wrapperInterface = "IWrapped" + effectiveName;
                var facadeType = effectiveName + "Facade";

                results.Add(new WrappedTypeMapping(fullName, wrapperInterface, facadeType));
            }
        }
#pragma warning disable CA1031 // analyzer must not crash
        catch
#pragma warning restore CA1031
        {
            // Silently ignore malformed manifest.
        }
    }

    private static string GetJsonString(JsonElement el, string propertyName)
    {
        return el.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String
            ? prop.GetString() ?? string.Empty
            : string.Empty;
    }

    // ── Legacy .wrapgod-types.txt parsing (fallback) ─────────────────

    /// <summary>
    /// Parses additional files matching <c>*.wrapgod-types.txt</c>.
    /// Each line: <c>Acme.Lib.FooService -&gt; IWrappedFooService, FooServiceFacade</c>
    /// </summary>
    private static List<WrappedTypeMapping> LoadMappingsFromTxtFiles(
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
