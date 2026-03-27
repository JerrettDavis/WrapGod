using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using WrapGod.Analyzers;
using WrapGod.Extractor;
using WrapGod.Generator;
using WrapGod.Manifest;

var root = GetRepoRoot();
var outputDir = Path.Combine(root, "examples", "output");
Directory.CreateDirectory(outputDir);

Console.WriteLine("== WrapGod end-to-end workflow example ==");

// 1) EXTRACT
var acmeAssemblyPath = BuildAcmeLibrary(root);
ApiManifest manifest = AssemblyExtractor.Extract(acmeAssemblyPath);
var manifestPath = Path.Combine(outputDir, "acme.wrapgod.json");
await File.WriteAllTextAsync(manifestPath, ManifestSerializer.Serialize(manifest));
Console.WriteLine($"[extract] manifest written: {manifestPath}");

// 2) CONFIG
const string configJson = """
{
  "types": [
    {
      "sourceType": "Acme.Lib.FooService",
      "include": true,
      "targetName": "BetterFoo",
      "members": [
        { "sourceMember": "GetStatus", "include": false }
      ]
    },
    {
      "sourceType": "Acme.Lib.BarClient",
      "include": false
    }
  ]
}
""";
var configPath = Path.Combine(outputDir, "acme.wrapgod.config.json");
await File.WriteAllTextAsync(configPath, configJson);
Console.WriteLine($"[config] config written: {configPath}");

// 3) GENERATE
GeneratorDriverRunResult genResult = RunGenerator(
    manifestJson: await File.ReadAllTextAsync(manifestPath),
    configJson: configJson);
var generatedDir = Path.Combine(outputDir, "generated");
Directory.CreateDirectory(generatedDir);

foreach (var generated in genResult.Results.SelectMany(r => r.GeneratedSources))
{
    var path = Path.Combine(generatedDir, generated.HintName);
    await File.WriteAllTextAsync(path, generated.SourceText.ToString());
}

Console.WriteLine($"[generate] files emitted: {genResult.Results.SelectMany(r => r.GeneratedSources).Count()}");

// 4) ANALYZE
const string mappings = "Acme.Lib.FooService -> IWrappedBetterFoo, BetterFooFacade";
var mappingPath = Path.Combine(outputDir, "acme.wrapgod-types.txt");
await File.WriteAllTextAsync(mappingPath, mappings + Environment.NewLine);

const string consumerSource = """
namespace MyApp;

public sealed class Consumer
{
    private Acme.Lib.FooService _svc = new Acme.Lib.FooService();

    public void Run()
    {
        _ = _svc.DoWork("hello");
    }
}
""";

ImmutableArray<Diagnostic> diagnostics = RunAnalyzer(consumerSource, mappings);
var diagnosticsPath = Path.Combine(outputDir, "diagnostics.txt");
await File.WriteAllLinesAsync(diagnosticsPath, diagnostics.Select(d => d.ToString()));
Console.WriteLine($"[analyze] diagnostics: {string.Join(", ", diagnostics.Select(d => d.Id).Distinct())}");

// 5) FIX
var fixedSource = ApplyCodeFix(consumerSource, "WG2001", mappings);
var fixedPath = Path.Combine(outputDir, "Consumer.fixed.cs");
await File.WriteAllTextAsync(fixedPath, fixedSource);
Console.WriteLine($"[fix] code fix output written: {fixedPath}");

Console.WriteLine();
Console.WriteLine("Summary:");
Console.WriteLine($"- WG2001 present: {diagnostics.Any(d => d.Id == "WG2001")}");
Console.WriteLine($"- WG2002 present: {diagnostics.Any(d => d.Id == "WG2002")}");
Console.WriteLine($"- Generated BetterFoo interface: {File.Exists(Path.Combine(generatedDir, "IWrappedBetterFoo.g.cs"))}");
Console.WriteLine($"- Generated BarClient wrapper (expected false): {File.Exists(Path.Combine(generatedDir, "IWrappedBarClient.g.cs"))}");

return;

static string GetRepoRoot()
{
    var current = new DirectoryInfo(AppContext.BaseDirectory);
    while (current is not null)
    {
        if (File.Exists(Path.Combine(current.FullName, "WrapGod.slnx")))
            return current.FullName;

        current = current.Parent;
    }

    throw new InvalidOperationException("Could not locate repo root.");
}

static string BuildAcmeLibrary(string repoRoot)
{
    var projectPath = Path.Combine(repoRoot, "examples", "Acme.Lib", "Acme.Lib.csproj");
    var psi = new System.Diagnostics.ProcessStartInfo("dotnet", $"build \"{projectPath}\" -c Release")
    {
        WorkingDirectory = repoRoot,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
    };

    using var process = System.Diagnostics.Process.Start(psi)
        ?? throw new InvalidOperationException("Failed to start dotnet build.");

    string output = process.StandardOutput.ReadToEnd();
    string error = process.StandardError.ReadToEnd();
    process.WaitForExit();

    if (process.ExitCode != 0)
    {
        throw new InvalidOperationException($"Acme.Lib build failed.{Environment.NewLine}{output}{Environment.NewLine}{error}");
    }

    var dllPath = Path.Combine(repoRoot, "examples", "Acme.Lib", "bin", "Release", "net10.0", "Acme.Lib.dll");
    if (!File.Exists(dllPath))
        throw new FileNotFoundException("Expected Acme.Lib.dll was not found.", dllPath);

    return dllPath;
}

static GeneratorDriverRunResult RunGenerator(string manifestJson, string configJson)
{
    var syntaxTree = CSharpSyntaxTree.ParseText("namespace Placeholder; public class Marker { }");
    var references = new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) };

    var compilation = CSharpCompilation.Create(
        assemblyName: "ExampleGeneration",
        syntaxTrees: [syntaxTree],
        references: references,
        options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

    var additionalTexts = new List<AdditionalText>
    {
        new InMemoryAdditionalText("acme.wrapgod.json", manifestJson),
        new InMemoryAdditionalText("acme.wrapgod.config.json", configJson),
    };

    IIncrementalGenerator generator = new WrapGodIncrementalGenerator();
    GeneratorDriver driver = CSharpGeneratorDriver.Create(
        generators: [generator.AsSourceGenerator()],
        additionalTexts: additionalTexts);

    driver = driver.RunGenerators(compilation);
    return driver.GetRunResult();
}

static ImmutableArray<Diagnostic> RunAnalyzer(string source, string mappings)
{
    var syntaxTree = CSharpSyntaxTree.ParseText(source);

    var references = new List<MetadataReference>
    {
        MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
        MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
        MetadataReference.CreateFromFile(typeof(Acme.Lib.FooService).Assembly.Location),
    };

    var netStandardPath = Path.Combine(
        Path.GetDirectoryName(typeof(object).Assembly.Location)!,
        "netstandard.dll");

    if (File.Exists(netStandardPath))
        references.Add(MetadataReference.CreateFromFile(netStandardPath));

    var compilation = CSharpCompilation.Create(
        "AnalyzerExample",
        [syntaxTree],
        references,
        new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

    var analyzer = new DirectUsageAnalyzer();
    var additionalTexts = new AdditionalText[]
    {
        new InMemoryAdditionalText("acme.wrapgod-types.txt", mappings),
    };

    var compilationWithAnalyzers = compilation.WithAnalyzers(
        [analyzer],
        new AnalyzerOptions(additionalTexts.ToImmutableArray()));

    return compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync().GetAwaiter().GetResult();
}

static string ApplyCodeFix(string source, string diagnosticId, string mappings)
{
    var syntaxTree = CSharpSyntaxTree.ParseText(source);

    var references = new List<MetadataReference>
    {
        MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
        MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
        MetadataReference.CreateFromFile(typeof(Acme.Lib.FooService).Assembly.Location),
    };

    var netStandardPath = Path.Combine(
        Path.GetDirectoryName(typeof(object).Assembly.Location)!,
        "netstandard.dll");

    if (File.Exists(netStandardPath))
        references.Add(MetadataReference.CreateFromFile(netStandardPath));

    var compilation = CSharpCompilation.Create(
        "CodeFixExample",
        [syntaxTree],
        references,
        new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

    var analyzer = new DirectUsageAnalyzer();
    var additionalTexts = new AdditionalText[]
    {
        new InMemoryAdditionalText("acme.wrapgod-types.txt", mappings),
    };

    var diagnostics = compilation.WithAnalyzers(
            [analyzer],
            new AnalyzerOptions(additionalTexts.ToImmutableArray()))
        .GetAnalyzerDiagnosticsAsync().GetAwaiter().GetResult();

    var target = diagnostics.FirstOrDefault(d => d.Id == diagnosticId);
    if (target is null)
        return source;

    var fixProvider = new UseWrapperCodeFixProvider();

    var workspace = new AdhocWorkspace();
    var projectId = ProjectId.CreateNewId();
    var documentId = DocumentId.CreateNewId(projectId);

    var projectInfo = ProjectInfo.Create(
        projectId,
        VersionStamp.Default,
        name: "ExampleProject",
        assemblyName: "ExampleProject",
        language: LanguageNames.CSharp,
        compilationOptions: compilation.Options,
        metadataReferences: compilation.References);

    var solution = workspace.CurrentSolution
        .AddProject(projectInfo)
        .AddDocument(documentId, "Consumer.cs", SourceText.From(source));

    var document = solution.GetDocument(documentId)
        ?? throw new InvalidOperationException("Could not create document for code fix.");

    CodeAction? codeAction = null;
    var context = new CodeFixContext(
        document,
        target,
        registerCodeFix: (action, _) => codeAction = action,
        cancellationToken: CancellationToken.None);

    fixProvider.RegisterCodeFixesAsync(context).GetAwaiter().GetResult();
    if (codeAction is null)
        return source;

    var operations = codeAction.GetOperationsAsync(CancellationToken.None).GetAwaiter().GetResult();
    var changedSolution = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution;
    var changedDocument = changedSolution.GetDocument(documentId)
        ?? throw new InvalidOperationException("Could not fetch changed document.");

    return changedDocument.GetTextAsync().GetAwaiter().GetResult().ToString();
}

file sealed class InMemoryAdditionalText(string path, string content) : AdditionalText
{
    private readonly SourceText _text = SourceText.From(content);

    public override string Path { get; } = path;

    public override SourceText? GetText(CancellationToken cancellationToken = default)
        => _text;
}
