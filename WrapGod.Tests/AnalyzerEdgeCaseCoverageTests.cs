using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using WrapGod.Abstractions.Diagnostics;
using WrapGod.Analyzers;

namespace WrapGod.Tests;

public sealed class AnalyzerEdgeCaseCoverageTests
{
    [Fact]
    public async Task Analyzer_IgnoresAdditionalFilesWithNullText()
    {
        const string source = """
            namespace Acme.Lib
            {
                public class FooService { }
            }

            namespace MyApp
            {
                public sealed class Consumer
                {
                    private readonly Acme.Lib.FooService _svc = new Acme.Lib.FooService();
                }
            }
            """;

        var diagnostics = await RunAnalyzerAsync(
            source,
            new AdditionalText[]
            {
                new NullTextAdditionalText("broken.wrapgod.config.json"),
                new NullTextAdditionalText("broken.wrapgod.json"),
                new NullTextAdditionalText("legacy.wrapgod-types.txt"),
            });

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void RoslynDiagnosticAdapter_UnknownSeverityFallsBackToNote()
    {
        var descriptor = new DiagnosticDescriptor(
            id: "WG9999",
            title: "Unknown Severity",
            messageFormat: "unknown severity test",
            category: "migration",
            defaultSeverity: (DiagnosticSeverity)123,
            isEnabledByDefault: true);

        var diagnostic = Diagnostic.Create(descriptor, Location.None);
        var converted = RoslynDiagnosticAdapter.ToWgDiagnosticV1(diagnostic, DateTime.UnixEpoch);

        Assert.Equal(WgDiagnosticSeverity.Note, converted.Severity);
    }

    private static async Task<ImmutableArray<Diagnostic>> RunAnalyzerAsync(
        string source,
        AdditionalText[] additionalTexts)
    {
        var compilation = CSharpCompilation.Create(
            "AnalyzerEdgeCaseCoverageTests",
            new[] { CSharpSyntaxTree.ParseText(source) },
            new[]
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
                MetadataReference.CreateFromFile(
                    typeof(System.Runtime.AssemblyTargetedPatchBandAttribute).Assembly.Location),
            },
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var analyzer = new DirectUsageAnalyzer();
        var compilationWithAnalyzers = compilation.WithAnalyzers(
            ImmutableArray.Create<DiagnosticAnalyzer>(analyzer),
            new AnalyzerOptions(additionalTexts.ToImmutableArray()));

        return await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();
    }

    private sealed class NullTextAdditionalText(string path) : AdditionalText
    {
        public override string Path { get; } = path;
        public override SourceText? GetText(CancellationToken cancellationToken = default) => null;
    }
}
