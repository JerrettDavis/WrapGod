using System.Text.Json;
using Json.Schema;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace WrapGod.Tests;

[Feature("Migration JSON schema file validation")]
public sealed class MigrationSchemaFileTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    private static readonly string RepoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
    private static readonly JsonSchema Schema = JsonSchema.FromText(
        File.ReadAllText(Path.Combine(RepoRoot, "schemas", "wrapgod-migration.v1.schema.json")));

    private static EvaluationResults EvaluateFile(string relativePath)
    {
        var text = File.ReadAllText(Path.Combine(RepoRoot, relativePath));
        using var doc = JsonDocument.Parse(text);
        return Schema.Evaluate(doc.RootElement);
    }

    [Scenario("Valid migration example passes schema validation")]
    [Fact]
    public Task ValidMigrationExamplePassesValidation()
        => Given("the valid migration example evaluated against the schema",
                () => EvaluateFile("schemas/examples/migration.valid.json"))
            .Then("the validation passes", result => result.IsValid)
            .AssertPassed();

    [Scenario("Invalid migration example missing library fails schema validation")]
    [Fact]
    public Task InvalidMigrationMissingLibraryFailsValidation()
        => Given("the invalid migration example evaluated against the schema",
                () => EvaluateFile("schemas/examples/migration.invalid.missing-library.json"))
            .Then("the validation fails", result => !result.IsValid)
            .AssertPassed();
}
