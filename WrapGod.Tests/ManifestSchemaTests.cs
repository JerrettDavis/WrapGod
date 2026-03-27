using System.Text.Json;
using Json.Schema;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace WrapGod.Tests;

[Feature("Manifest JSON schema validation")]
public sealed class ManifestSchemaTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    // ── Helpers ──────────────────────────────────────────────────────

    private static readonly string RepoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
    private static readonly JsonSchema Schema = JsonSchema.FromText(
        File.ReadAllText(Path.Combine(RepoRoot, "schemas", "wrapgod.manifest.v1.schema.json")));

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

    // ── Scenarios ────────────────────────────────────────────────────

    [Scenario("Valid manifest example passes schema validation")]
    [Fact]
    public Task ManifestSchemaValidExamplePassesValidation()
        => Given("the valid manifest evaluated against the schema", EvaluateValidManifest)
            .Then("the validation passes", result => result.IsValid)
            .AssertPassed();

    [Scenario("Invalid manifest example fails schema validation")]
    [Fact]
    public Task ManifestSchemaInvalidExampleFailsValidation()
        => Given("the invalid manifest evaluated against the schema", EvaluateInvalidManifest)
            .Then("the validation fails", result => !result.IsValid)
            .AssertPassed();
}
