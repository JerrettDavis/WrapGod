using System.Reflection;
using WrapGod.Abstractions.Config;
using WrapGod.Manifest.Config;

namespace WrapGod.Tests;

public class ConfigIngestionTests
{
    [Fact]
    public void JsonConfigLoaderParsesTypeAndMemberRules()
    {
        const string json = """
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

        var config = JsonConfigLoader.LoadFromJson(json);

        Assert.Single(config.Types);
        Assert.Equal("Vendor.Client", config.Types[0].SourceType);
        Assert.Single(config.Types[0].Members);
        Assert.Equal("GetUser", config.Types[0].Members[0].SourceMember);
    }

    [Fact]
    public void AttributeConfigReaderBuildsTypeAndMemberRules()
    {
        var config = AttributeConfigReader.ReadFromAssembly(typeof(AttributedWrapper).Assembly);
        var type = Assert.Single(config.Types, t => t.SourceType == "Vendor.Client");

        Assert.Equal("IClient", type.TargetName);
        var member = Assert.Single(type.Members, m => m.SourceMember == "GetUser");
        Assert.Equal("FetchUser", member.TargetName);
    }

    [Fact]
    public void MergeEngineUsesConfiguredPrecedenceAndEmitsDiagnosticsOnConflict()
    {
        var jsonConfig = new WrapGodConfig
        {
            Types =
            {
                new TypeConfig
                {
                    SourceType = "Vendor.Client",
                    Include = true,
                    TargetName = "JsonClient",
                    Members = { new MemberConfig { SourceMember = "GetUser", Include = true, TargetName = "JsonGetUser" } },
                },
            },
        };

        var attrConfig = new WrapGodConfig
        {
            Types =
            {
                new TypeConfig
                {
                    SourceType = "Vendor.Client",
                    Include = false,
                    TargetName = "AttrClient",
                    Members = { new MemberConfig { SourceMember = "GetUser", Include = false, TargetName = "AttrGetUser" } },
                },
            },
        };

        var result = ConfigMergeEngine.Merge(jsonConfig, attrConfig, new ConfigMergeOptions
        {
            HigherPrecedence = ConfigSource.Attributes,
        });

        var type = Assert.Single(result.Config.Types);
        Assert.False(type.Include);
        Assert.Equal("AttrClient", type.TargetName);
        Assert.Equal("AttrGetUser", Assert.Single(type.Members).TargetName);
        Assert.NotEmpty(result.Diagnostics);
    }

    [WrapType("Vendor.Client", TargetName = "IClient")]
    private sealed class AttributedWrapper
    {
        [WrapMember("GetUser", TargetName = "FetchUser")]
        public static string GetUser(string id) => id;
    }
}
