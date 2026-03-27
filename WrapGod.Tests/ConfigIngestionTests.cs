using TinyBDD;
using TinyBDD.Xunit;
using WrapGod.Abstractions.Config;
using WrapGod.Manifest.Config;
using Xunit.Abstractions;

namespace WrapGod.Tests;

[Feature("Configuration ingestion")]
public partial class ConfigIngestionTests : TinyBddXunitBase
{
    public ConfigIngestionTests(ITestOutputHelper output) : base(output) { }

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

    [Scenario("JSON config loader parses type and member rules")]
    [Fact]
    public async Task JsonConfigLoaderParsesTypeAndMemberRules()
    {
        await Flow.Given("a JSON config string", LoadSampleJson)
            .Then("one type rule is parsed", config => Assert.Single(config.Types))
            .And("the source type is correct", config =>
                Assert.Equal("Vendor.Client", config.Types[0].SourceType))
            .And("one member rule is parsed with correct source", config =>
            {
                Assert.Single(config.Types[0].Members);
                Assert.Equal("GetUser", config.Types[0].Members[0].SourceMember);
            })
            .AssertPassed();
    }

    [Scenario("Attribute config reader builds type and member rules")]
    [Fact]
    public async Task AttributeConfigReaderBuildsTypeAndMemberRules()
    {
        await Flow.Given("an assembly with WrapType and WrapMember attributes", ReadAttributeConfig)
            .Then("the Vendor.Client type rule has target name IClient", config =>
            {
                var type = Assert.Single(config.Types, t => t.SourceType == "Vendor.Client");
                Assert.Equal("IClient", type.TargetName);
            })
            .And("the GetUser member rule has target name FetchUser", config =>
            {
                var type = Assert.Single(config.Types, t => t.SourceType == "Vendor.Client");
                var member = Assert.Single(type.Members, m => m.SourceMember == "GetUser");
                Assert.Equal("FetchUser", member.TargetName);
            })
            .AssertPassed();
    }

    [Scenario("Merge engine uses configured precedence and emits diagnostics on conflict")]
    [Fact]
    public async Task MergeEngineUsesConfiguredPrecedenceAndEmitsDiagnosticsOnConflict()
    {
        await Flow.Given("conflicting JSON and attribute configs", MergeWithAttributePrecedence)
            .Then("attribute config wins for type-level settings", mergeResult =>
            {
                var type = Assert.Single(mergeResult.Config.Types);
                Assert.False(type.Include);
                Assert.Equal("AttrClient", type.TargetName);
            })
            .And("attribute config wins for member-level settings", mergeResult =>
            {
                var type = Assert.Single(mergeResult.Config.Types);
                Assert.Equal("AttrGetUser", Assert.Single(type.Members).TargetName);
            })
            .And("diagnostics are emitted for the conflict", mergeResult =>
                Assert.NotEmpty(mergeResult.Diagnostics))
            .AssertPassed();
    }

    [WrapType("Vendor.Client", TargetName = "IClient")]
    private sealed class AttributedWrapper
    {
        [WrapMember("GetUser", TargetName = "FetchUser")]
        public static string GetUser(string id) => id;
    }
}
