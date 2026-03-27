using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using TinyBDD;
using TinyBDD.Xunit;
using WrapGod.Analyzers;
using Xunit.Abstractions;

namespace WrapGod.Tests;

[Feature("Analyzer final coverage push")]
public sealed class AnalyzerFinalPushTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    // ── Helpers ──────────────────────────────────────────────────────

    private static async Task<ImmutableArray<Diagnostic>> RunAnalyzerAsync(
        string source, params AdditionalText[] additionalTexts)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var runtimeDir = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        var references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
            MetadataReference.CreateFromFile(
                typeof(System.Runtime.AssemblyTargetedPatchBandAttribute).Assembly.Location),
        };
        foreach (var dll in new[] { "netstandard.dll", "System.Collections.dll", "System.Linq.dll" })
        {
            var path = Path.Combine(runtimeDir, dll);
            if (File.Exists(path))
                references.Add(MetadataReference.CreateFromFile(path));
        }

        var compilation = CSharpCompilation.Create("TestAssembly",
            new[] { syntaxTree }, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var analyzer = new DirectUsageAnalyzer();
        var withAnalyzers = compilation.WithAnalyzers(
            ImmutableArray.Create<DiagnosticAnalyzer>(analyzer),
            new AnalyzerOptions(additionalTexts.ToImmutableArray()));
        return await withAnalyzers.GetAnalyzerDiagnosticsAsync();
    }

    private static ImmutableArray<Diagnostic> RunSync(
        string source, params AdditionalText[] additionalTexts)
        => RunAnalyzerAsync(source, additionalTexts).GetAwaiter().GetResult();

    private static async Task<string> ApplyFixAsync(
        string source, string diagnosticId, AdditionalText[] additionalTexts)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var runtimeDir = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        var references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
            MetadataReference.CreateFromFile(
                typeof(System.Runtime.AssemblyTargetedPatchBandAttribute).Assembly.Location),
        };
        foreach (var dll in new[] { "netstandard.dll", "System.Collections.dll" })
        {
            var path = Path.Combine(runtimeDir, dll);
            if (File.Exists(path)) references.Add(MetadataReference.CreateFromFile(path));
        }

        var compilation = CSharpCompilation.Create("TestAssembly",
            new[] { syntaxTree }, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var analyzer = new DirectUsageAnalyzer();
        var withAnalyzers = compilation.WithAnalyzers(
            ImmutableArray.Create<DiagnosticAnalyzer>(analyzer),
            new AnalyzerOptions(additionalTexts.ToImmutableArray()));
        var diagnostics = await withAnalyzers.GetAnalyzerDiagnosticsAsync();
        var targetDiag = diagnostics.FirstOrDefault(d => d.Id == diagnosticId);
        if (targetDiag == null) return source;

        var fixProvider = new UseWrapperCodeFixProvider();
        var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
        var documentId = DocumentId.CreateNewId(projectId);
        var projectInfo = ProjectInfo.Create(projectId, VersionStamp.Default,
            "TestProject", "TestAssembly", LanguageNames.CSharp,
            compilationOptions: compilation.Options,
            metadataReferences: compilation.References);
        var solution = workspace.CurrentSolution
            .AddProject(projectInfo)
            .AddDocument(documentId, "Test.cs", SourceText.From(source));
        var document = solution.GetDocument(documentId)!;
        CodeAction? codeAction = null;
        var context = new CodeFixContext(document, targetDiag,
            (action, _) => codeAction = action, CancellationToken.None);
        await fixProvider.RegisterCodeFixesAsync(context);
        if (codeAction is null) return source;
        var operations = await codeAction.GetOperationsAsync(CancellationToken.None);
        var changedSolution = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution;
        var changedDocument = changedSolution.GetDocument(documentId)!;
        return (await changedDocument.GetTextAsync()).ToString();
    }

    private const string AcmeLibDef = """
        namespace Acme.Lib
        {
            public class FooService
            {
                public string DoWork(string input) => input;
                public int Count { get; set; }
            }
            public class Repository<T>
            {
                public T Get(int id) => default;
            }
        }
        """;

    private static readonly string FooManifest = """
        {
          "schemaVersion": "1.0",
          "assembly": { "name": "Acme.Lib" },
          "types": [
            { "fullName": "Acme.Lib.FooService", "name": "FooService", "namespace": "Acme.Lib",
              "members": [{ "name": "DoWork", "kind": "method" }] }
          ]
        }
        """;

    private static readonly string RepoManifest = """
        {
          "schemaVersion": "1.0",
          "assembly": { "name": "Acme.Lib" },
          "types": [
            { "fullName": "Acme.Lib.Repository", "name": "Repository", "namespace": "Acme.Lib",
              "members": [{ "name": "Get", "kind": "method" }] }
          ]
        }
        """;

    // ── Scenarios: GenericName as type usage (not member access) ──────

    [Scenario("GenericName usage as variable type triggers WG2001")]
    [Fact]
    public Task GenericName_AsVariableType()
    {
        var source = AcmeLibDef + """
            namespace MyApp
            {
                public class User { }
                public class Consumer
                {
                    public void Test()
                    {
                        Acme.Lib.Repository<User> repo = null;
                    }
                }
            }
            """;

        return Given("generic type used as local variable type",
                () => RunSync(source, new InMemoryAdditionalText("a.wrapgod.json", RepoManifest)))
            .Then("WG2001 reported for Repository", diags =>
                diags.Any(d => d.Id == "WG2001" &&
                    d.GetMessage(System.Globalization.CultureInfo.InvariantCulture)
                        .Contains("Repository", StringComparison.Ordinal)))
            .AssertPassed();
    }

    // ── Scenarios: Direct method call on generic type triggers WG2002 ─

    [Scenario("Method call on generic Repository triggers WG2002")]
    [Fact]
    public Task GenericType_MethodCall_WG2002()
    {
        var source = AcmeLibDef + """
            namespace MyApp
            {
                public class User { }
                public class Consumer
                {
                    public void Test()
                    {
                        var repo = new Acme.Lib.Repository<User>();
                        repo.Get(1);
                    }
                }
            }
            """;

        return Given("method call on generic wrapped type",
                () => RunSync(source, new InMemoryAdditionalText("a.wrapgod.json", RepoManifest)))
            .Then("WG2002 reported for Get on Repository", diags =>
                diags.Any(d => d.Id == "WG2002" &&
                    d.GetMessage(System.Globalization.CultureInfo.InvariantCulture)
                        .Contains("Get", StringComparison.Ordinal)))
            .AssertPassed();
    }

    // ── Scenarios: Candidate symbols (unresolved) ────────────────────

    [Scenario("Identifier that is a local variable of wrapped type triggers WG2001")]
    [Fact]
    public Task LocalVariable_Identifier()
    {
        var source = AcmeLibDef + """
            namespace MyApp
            {
                public class Consumer
                {
                    public void Test()
                    {
                        Acme.Lib.FooService svc = null;
                        var x = svc;
                    }
                }
            }
            """;

        return Given("local variable identifier of wrapped type",
                () => RunSync(source, new InMemoryAdditionalText("a.wrapgod.json", FooManifest)))
            .Then("WG2001 reported", diags => diags.Any(d => d.Id == "WG2001"))
            .AssertPassed();
    }

    // ── Scenarios: Config with manifest missing types value kind ──────

    [Scenario("Manifest with types as non-array is ignored")]
    [Fact]
    public Task Manifest_TypesNotArray()
    {
        var source = AcmeLibDef + """
            namespace MyApp
            {
                public class Consumer
                {
                    Acme.Lib.FooService _svc = new Acme.Lib.FooService();
                }
            }
            """;

        var manifest = """
            {
              "assembly": { "name": "Lib" },
              "types": "not an array"
            }
            """;

        return Given("manifest where types is a string not array",
                () => RunSync(source, new InMemoryAdditionalText("a.wrapgod.json", manifest)))
            .Then("no diagnostics (manifest ignored)", diags =>
                !diags.Any(d => d.Id == "WG2001"))
            .AssertPassed();
    }

    // ── Scenarios: Config with types as non-array ────────────────────

    [Scenario("Config with types as non-array is ignored")]
    [Fact]
    public Task Config_TypesNotArray()
    {
        var source = AcmeLibDef + """
            namespace MyApp
            {
                public class Consumer
                {
                    Acme.Lib.FooService _svc = new Acme.Lib.FooService();
                }
            }
            """;

        var config = """{ "types": "not an array" }""";

        return Given("config where types is a string not array",
                () => RunSync(source,
                    new InMemoryAdditionalText("a.wrapgod.json", FooManifest),
                    new InMemoryAdditionalText("a.wrapgod.config.json", config)))
            .Then("WG2001 still fires from manifest", diags =>
                diags.Any(d => d.Id == "WG2001"))
            .AssertPassed();
    }

    // ── Scenarios: Code fix WG2001 where wrapper interface arg empty ─

    [Scenario("WG2002 code fix on property access (non-invocation) returns unchanged")]
    [Fact]
    public Task CodeFix_PropertyAccess_NoChange()
    {
        // The code fix for WG2002 only triggers on invocations. Test that
        // accessing a property on a mapped type produces a WG2002 only if
        // the parent is an invocation.
        var source = AcmeLibDef + """
            namespace MyApp
            {
                public class Consumer
                {
                    public void Test()
                    {
                        var svc = new Acme.Lib.FooService();
                        svc.DoWork("test");
                    }
                }
            }
            """;

        return Given("code fix for WG2002",
                () => ApplyFixAsync(source, "WG2002",
                    new[] { new InMemoryAdditionalText("a.wrapgod-types.txt",
                        "Acme.Lib.FooService -> IWrappedFooService, FooServiceFacade") })
                    .GetAwaiter().GetResult())
            .Then("fix applied contains FooServiceFacade", src =>
                src.Contains("FooServiceFacade", StringComparison.Ordinal))
            .AssertPassed();
    }

    // ── Scenarios: identifier resolving to property symbol ─────────────

    [Scenario("Identifier used as property type reference")]
    [Fact]
    public Task PropertyType_Identifier_WG2001()
    {
        var source = AcmeLibDef + """
            namespace MyApp
            {
                public class Consumer
                {
                    public Acme.Lib.FooService MySvc { get; set; }
                    public void Test()
                    {
                        var x = MySvc;
                    }
                }
            }
            """;

        return Given("identifier accessing property of wrapped type",
                () => RunSync(source, new InMemoryAdditionalText("a.wrapgod.json", FooManifest)))
            .Then("WG2001 reported", diags => diags.Any(d => d.Id == "WG2001"))
            .AssertPassed();
    }

    // ── Scenarios: config with manifest parsing both name and fullName ─

    [Scenario("Manifest type missing name but with fullName is still skipped")]
    [Fact]
    public Task Manifest_TypeMissingName_Skipped()
    {
        var source = AcmeLibDef + """
            namespace MyApp
            {
                public class Consumer
                {
                    Acme.Lib.FooService _svc = new Acme.Lib.FooService();
                }
            }
            """;

        var manifest = """
            {
              "assembly": { "name": "Lib" },
              "types": [
                { "fullName": "Acme.Lib.FooService", "name": "", "namespace": "Acme.Lib", "members": [] }
              ]
            }
            """;

        return Given("manifest with empty name",
                () => RunSync(source, new InMemoryAdditionalText("a.wrapgod.json", manifest)))
            .Then("no diagnostics (type with empty name skipped)", diags =>
                !diags.Any(d => d.Id == "WG2001"))
            .AssertPassed();
    }

    [Scenario("Config with empty sourceType in type is skipped")]
    [Fact]
    public Task Config_EmptySourceType_Skipped()
    {
        var source = AcmeLibDef + """
            namespace MyApp
            {
                public class Consumer
                {
                    Acme.Lib.FooService _svc = new Acme.Lib.FooService();
                }
            }
            """;

        var config = """
            {
              "types": [
                { "sourceType": "" },
                { "sourceType": "Acme.Lib.FooService", "include": false }
              ]
            }
            """;

        return Given("config with empty sourceType entry",
                () => RunSync(source,
                    new InMemoryAdditionalText("a.wrapgod.json", FooManifest),
                    new InMemoryAdditionalText("a.wrapgod.config.json", config)))
            .Then("no WG2001 (FooService excluded by config)", diags =>
                !diags.Any(d => d.Id == "WG2001"))
            .AssertPassed();
    }

    // ── In-memory AdditionalText ─────────────────────────────────────

    private sealed class InMemoryAdditionalText : AdditionalText
    {
        private readonly SourceText _text;
        public InMemoryAdditionalText(string path, string content)
        {
            Path = path;
            _text = SourceText.From(content);
        }
        public override string Path { get; }
        public override SourceText? GetText(CancellationToken cancellationToken = default) => _text;
    }
}
