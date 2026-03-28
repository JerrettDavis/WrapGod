using System.Text.Json;
using Json.Schema;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace WrapGod.Tests;

[Feature("Compatibility report JSON schema validation")]
public sealed class CompatibilityReportSchemaTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    private static readonly string RepoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
    private static readonly JsonSchema Schema = JsonSchema.FromText(
        File.ReadAllText(Path.Combine(RepoRoot, "schemas", "compatibility-report.v1.schema.json")));

    private static EvaluationResults EvaluateFile(string relativePath)
    {
        var text = File.ReadAllText(Path.Combine(RepoRoot, relativePath));
        using var doc = JsonDocument.Parse(text);
        return Schema.Evaluate(doc.RootElement);
    }

    [Scenario("FluentAssertions compatibility report passes schema validation")]
    [Fact]
    public Task FluentAssertionsReport_PassesValidation()
        => Given("the FluentAssertions report is evaluated against the schema", () =>
                EvaluateFile("examples/migrations/nuget-version-matrix/fluent-assertions/compatibility-report.json"))
            .Then("the validation passes", result => result.IsValid)
            .AssertPassed();

    [Scenario("Moq compatibility report passes schema validation")]
    [Fact]
    public Task MoqReport_PassesValidation()
        => Given("the Moq report is evaluated against the schema", () =>
                EvaluateFile("examples/migrations/nuget-version-matrix/moq/compatibility-report.json"))
            .Then("the validation passes", result => result.IsValid)
            .AssertPassed();

    [Scenario("Serilog compatibility report passes schema validation")]
    [Fact]
    public Task SerilogReport_PassesValidation()
        => Given("the Serilog report is evaluated against the schema", () =>
                EvaluateFile("examples/migrations/nuget-version-matrix/serilog/compatibility-report.json"))
            .Then("the validation passes", result => result.IsValid)
            .AssertPassed();

    [Scenario("Compatibility report missing migration recommendation fails schema validation")]
    [Fact]
    public Task MissingMigrationRecommendation_FailsValidation()
        => Given("an invalid compatibility report is evaluated against the schema", () =>
                EvaluateFile("schemas/examples/compatibility-report.invalid.missing-migration.json"))
            .Then("the validation fails", result => !result.IsValid)
            .AssertPassed();
}
