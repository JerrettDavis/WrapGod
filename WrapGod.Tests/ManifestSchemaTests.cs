using System.Text.Json;
using Json.Schema;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace WrapGod.Tests;

[Feature("Manifest JSON schema validation")]
public partial class ManifestSchemaTests : TinyBddXunitBase
{
    private static readonly string RepoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
    private static readonly JsonSchema Schema = JsonSchema.FromText(
        File.ReadAllText(Path.Combine(RepoRoot, "schemas", "wrapgod.manifest.v1.schema.json")));

    public ManifestSchemaTests(ITestOutputHelper output) : base(output) { }

    private static EvaluationResults EvaluateFile(string relativePath)
    {
        var text = File.ReadAllText(Path.Combine(RepoRoot, relativePath));
        using var doc = JsonDocument.Parse(text);
        return Schema.Evaluate(doc.RootElement);
    }

    private static EvaluationResults EvaluateValidManifest() =>
        EvaluateFile("schemas/examples/manifest.valid.json");

    private static EvaluationResults EvaluateInvalidManifest() =>
        EvaluateFile("schemas/examples/manifest.invalid.missing-assembly.json");

    [Scenario("Valid manifest example passes schema validation")]
    [Fact]
    public async Task ManifestSchemaValidExamplePassesValidation()
    {
        await Flow.Given("the valid manifest evaluated against the schema", EvaluateValidManifest)
            .Then("the validation passes", result =>
                Assert.True(result.IsValid, "Expected valid manifest example to pass schema validation."))
            .AssertPassed();
    }

    [Scenario("Invalid manifest example fails schema validation")]
    [Fact]
    public async Task ManifestSchemaInvalidExampleFailsValidation()
    {
        await Flow.Given("the invalid manifest evaluated against the schema", EvaluateInvalidManifest)
            .Then("the validation fails", result =>
                Assert.False(result.IsValid, "Expected invalid manifest example to fail schema validation."))
            .AssertPassed();
    }
}
