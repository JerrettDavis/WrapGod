using System.Text.Json;
using Json.Schema;

namespace WrapGod.Tests;

public class ManifestSchemaTests
{
    private static readonly string RepoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
    private static readonly JsonSchema Schema = JsonSchema.FromText(
        File.ReadAllText(Path.Combine(RepoRoot, "schemas", "wrapgod.manifest.v1.schema.json")));

    [Fact]
    public void ManifestSchemaValidExamplePassesValidation()
    {
        var validText = File.ReadAllText(Path.Combine(RepoRoot, "schemas", "examples", "manifest.valid.json"));

        using var validDoc = JsonDocument.Parse(validText);
        var result = Schema.Evaluate(validDoc.RootElement);

        Assert.True(result.IsValid, "Expected valid manifest example to pass schema validation.");
    }

    [Fact]
    public void ManifestSchemaInvalidExampleFailsValidation()
    {
        var invalidText = File.ReadAllText(Path.Combine(RepoRoot, "schemas", "examples", "manifest.invalid.missing-assembly.json"));

        using var invalidDoc = JsonDocument.Parse(invalidText);
        var result = Schema.Evaluate(invalidDoc.RootElement);

        Assert.False(result.IsValid, "Expected invalid manifest example to fail schema validation.");
    }
}
