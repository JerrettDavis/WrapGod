using TinyBDD;
using TinyBDD.Xunit;
using WrapGod.Abstractions.Config;
using WrapGod.Manifest.Config;
using Xunit.Abstractions;

namespace WrapGod.Tests;

[Feature("Configuration ingestion")]
public sealed class ConfigIngestionTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    // ── Helpers ──────────────────────────────────────────────────────

    private static readonly string SampleJson = """
        {
          "types": [
            {
              "sourceType": "Vendor.Client",
              "include": true,
              "targetName": "IClient",
              "members": [
                { "sourceMember": "GetUser", "include": true, "targetName": "FetchUser" }
              ]
            }
          ]
        }
        """;

    private static WrapGodConfig LoadSampleJson() => JsonConfigLoader.LoadFromJson(SampleJson);

    private static WrapGodConfig ReadAttributeConfig() =>
        AttributeConfigReader.ReadFromAssembly(typeof(AttributedWrapper).Assembly);

    private static ConfigMergeResult MergeWithAttributePrecedence()
    {
        var jsonConfig = new WrapGodConfig();
        jsonConfig.Types.Add(new TypeConfig
        {
            SourceType = "Vendor.Client",
            Include = true,
            TargetName = "JsonClient",
        });
        jsonConfig.Types[0].Members.Add(
            new MemberConfig { SourceMember = "GetUser", Include = true, TargetName = "JsonGetUser" });

        var attrConfig = new WrapGodConfig();
        attrConfig.Types.Add(new TypeConfig
        {
            SourceType = "Vendor.Client",
            Include = false,
            TargetName = "AttrClient",
        });
        attrConfig.Types[0].Members.Add(
            new MemberConfig { SourceMember = "GetUser", Include = false, TargetName = "AttrGetUser" });

        return ConfigMergeEngine.Merge(jsonConfig, attrConfig, new ConfigMergeOptions
        {
            HigherPrecedence = ConfigSource.Attributes,
        });
    }

    [WrapType("Vendor.Client", TargetName = "IClient")]
    private sealed class AttributedWrapper
    {
        [WrapMember("GetUser", TargetName = "FetchUser")]
        public static string GetUser(string id) => id;
    }

    // ── Scenarios ────────────────────────────────────────────────────

    [Scenario("JSON config loader parses type and member rules")]
    [Fact]
    public Task JsonConfigLoaderParsesTypeAndMemberRules()
        => Given("a JSON config string", LoadSampleJson)
            .Then("one type rule is parsed", config => config.Types.Count == 1)
            .And("the source type is correct", config =>
                config.Types[0].SourceType == "Vendor.Client")
            .And("one member rule is parsed", config =>
                config.Types[0].Members.Count == 1)
            .And("the member source is correct", config =>
                config.Types[0].Members[0].SourceMember == "GetUser")
            .AssertPassed();

    [Scenario("Attribute config reader builds type and member rules")]
    [Fact]
    public Task AttributeConfigReaderBuildsTypeAndMemberRules()
        => Given("an assembly with WrapType and WrapMember attributes", ReadAttributeConfig)
            .Then("the Vendor.Client type rule has target name IClient", config =>
                config.Types.Single(t => t.SourceType == "Vendor.Client").TargetName == "IClient")
            .And("the GetUser member rule has target name FetchUser", config =>
                config.Types.Single(t => t.SourceType == "Vendor.Client")
                    .Members.Single(m => m.SourceMember == "GetUser").TargetName == "FetchUser")
            .AssertPassed();

    [Scenario("Merge engine uses configured precedence and emits diagnostics on conflict")]
    [Fact]
    public Task MergeEngineUsesConfiguredPrecedenceAndEmitsDiagnosticsOnConflict()
        => Given("conflicting JSON and attribute configs", MergeWithAttributePrecedence)
            .Then("attribute config wins for type include", mergeResult =>
                mergeResult.Config.Types.Count == 1 && mergeResult.Config.Types[0].Include == false)
            .And("attribute config wins for type target name", mergeResult =>
                mergeResult.Config.Types[0].TargetName == "AttrClient")
            .And("attribute config wins for member target name", mergeResult =>
                mergeResult.Config.Types[0].Members.Count == 1
                && mergeResult.Config.Types[0].Members[0].TargetName == "AttrGetUser")
            .And("diagnostics are emitted for the conflict", mergeResult =>
                mergeResult.Diagnostics.Count > 0)
            .AssertPassed();
}
