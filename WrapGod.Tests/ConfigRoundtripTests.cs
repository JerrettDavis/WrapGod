using System.Text.Json;
using TinyBDD;
using TinyBDD.Xunit;
using WrapGod.Abstractions.Config;
using WrapGod.Fluent;
using WrapGod.Manifest.Config;
using Xunit.Abstractions;

namespace WrapGod.Tests;

[Feature("Configuration roundtrip")]
public sealed class ConfigRoundtripTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    // ── Helpers ──────────────────────────────────────────────────────

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    private static GenerationPlan BuildFluentPlan() =>
        WrapGodConfiguration.Create()
            .ForAssembly("Vendor.Lib")
            .WrapType("Vendor.Lib.HttpClient")
                .As("IHttpClient")
                .WrapMethod("SendAsync").As("SendRequestAsync")
                .WrapProperty("Timeout")
                .ExcludeMember("Dispose")
            .WrapType("Vendor.Lib.Logger")
                .As("ILogger")
                .WrapAllPublicMembers()
            .MapType("Vendor.Lib.Config", "MyApp.Config")
            .ExcludeType("Vendor.Lib.Internal*")
            .Build();

    private static readonly string SampleJsonConfig = """
        {
          "types": [
            {
              "sourceType": "Vendor.Client",
              "include": true,
              "targetName": "IClient",
              "members": [
                { "sourceMember": "GetUser", "include": true, "targetName": "FetchUser" },
                { "sourceMember": "Timeout", "include": true }
              ]
            }
          ]
        }
        """;

    private static WrapGodConfig LoadJsonConfig() => JsonConfigLoader.LoadFromJson(SampleJsonConfig);

    private static ConfigMergeResult MergeWithJsonPrecedence()
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
            HigherPrecedence = ConfigSource.Json,
        });
    }

    private static WrapGodConfig SerializeAndDeserialize()
    {
        var original = new WrapGodConfig();
        original.Types.Add(new TypeConfig
        {
            SourceType = "Vendor.Client",
            Include = true,
            TargetName = "IClient",
        });
        original.Types[0].Members.Add(
            new MemberConfig { SourceMember = "GetUser", Include = true, TargetName = "FetchUser" });
        original.Types[0].Members.Add(
            new MemberConfig { SourceMember = "Timeout", Include = true });

        var json = JsonSerializer.Serialize(original, JsonOptions);
        return JsonSerializer.Deserialize<WrapGodConfig>(json, JsonOptions)!;
    }

    private static WrapGodConfig BuildProgrammaticConfig() =>
        ToWrapGodConfig(
            WrapGodConfiguration.Create()
                .ForAssembly("Vendor.Lib")
                .WrapType("Vendor.Lib.HttpClient")
                    .As("IHttpClient")
                    .WrapMethod("SendAsync").As("SendRequestAsync")
                    .WrapProperty("Timeout")
                    .ExcludeMember("Dispose")
                .WrapType("Vendor.Lib.Logger")
                    .As("ILogger")
                    .WrapAllPublicMembers()
                .Build());

    private static string BuildHandWrittenJson() =>
        """
        {
          "types": [
            {
              "sourceType": "Vendor.Lib.HttpClient",
              "include": true,
              "targetName": "IHttpClient",
              "members": [
                { "sourceMember": "SendAsync", "include": true, "targetName": "SendRequestAsync" },
                { "sourceMember": "Timeout", "include": true },
                { "sourceMember": "Dispose", "include": false }
              ]
            },
            {
              "sourceType": "Vendor.Lib.Logger",
              "include": true,
              "targetName": "ILogger",
              "members": []
            }
          ]
        }
        """;

    private static string Serialize(WrapGodConfig config) => JsonSerializer.Serialize(config, JsonOptions);

    private static WrapGodConfig ToWrapGodConfig(GenerationPlan plan)
    {
        var config = new WrapGodConfig();

        foreach (var directive in plan.TypeDirectives)
        {
            var type = new TypeConfig
            {
                SourceType = directive.SourceType,
                Include = directive.WrapAllPublicMembers ? true : null,
                TargetName = directive.TargetName,
            };

            foreach (var member in directive.MemberDirectives)
            {
                type.Members.Add(new MemberConfig
                {
                    SourceMember = member.SourceName,
                    Include = true,
                    TargetName = member.TargetName,
                });
            }

            foreach (var excluded in directive.ExcludedMembers)
            {
                type.Members.Add(new MemberConfig
                {
                    SourceMember = excluded,
                    Include = false,
                });
            }

            config.Types.Add(type);
        }

        return config;
    }

    private static string Normalize(WrapGodConfig config)
    {
        var normalized = new WrapGodConfig
        {
            Types = config.Types
                .OrderBy(t => t.SourceType, StringComparer.Ordinal)
                .Select(t => new TypeConfig
                {
                    SourceType = t.SourceType,
                    Include = t.Include,
                    TargetName = t.TargetName,
                    Members = t.Members
                        .OrderBy(m => m.SourceMember, StringComparer.Ordinal)
                        .Select(m => new MemberConfig
                        {
                            SourceMember = m.SourceMember,
                            Include = m.Include,
                            TargetName = m.TargetName,
                        })
                        .ToList(),
                })
                .ToList(),
        };

        return Serialize(normalized);
    }

    private static WrapGodConfig BuildAttributeBaselineConfig()
    {
        var baseline = new WrapGodConfig();
        baseline.Types.Add(new TypeConfig
        {
            SourceType = "Vendor.Lib.HttpClient",
            Include = true,
            TargetName = "IHttpClient",
            Members =
            {
                new MemberConfig { SourceMember = "SendAsync", Include = true, TargetName = "SendRequestAsync" },
                new MemberConfig { SourceMember = "Timeout", Include = true },
                new MemberConfig { SourceMember = "Dispose", Include = false },
            },
        });

        baseline.Types.Add(new TypeConfig
        {
            SourceType = "Vendor.Lib.Logger",
            Include = true,
            TargetName = "ILogger",
        });

        return baseline;
    }

    // ── Scenarios ────────────────────────────────────────────────────

    [Scenario("Fluent config GenerationPlan matches equivalent hand-built expectations")]
    [Fact]
    public Task FluentPlanMatchesExpectations()
        => Given("a fluent configuration built via the DSL", BuildFluentPlan)
            .Then("assembly name is Vendor.Lib", plan =>
                plan.AssemblyName == "Vendor.Lib")
            .And("there are two type directives", plan =>
                plan.TypeDirectives.Count == 2)
            .And("HttpClient is wrapped as IHttpClient", plan =>
                plan.TypeDirectives[0].SourceType == "Vendor.Lib.HttpClient"
                && plan.TypeDirectives[0].TargetName == "IHttpClient")
            .And("HttpClient has two member directives", plan =>
                plan.TypeDirectives[0].MemberDirectives.Count == 2)
            .And("SendAsync is renamed to SendRequestAsync", plan =>
                plan.TypeDirectives[0].MemberDirectives[0].SourceName == "SendAsync"
                && plan.TypeDirectives[0].MemberDirectives[0].TargetName == "SendRequestAsync")
            .And("Logger wraps all public members", plan =>
                plan.TypeDirectives[1].WrapAllPublicMembers)
            .And("there is one type mapping", plan =>
                plan.TypeMappings.Count == 1
                && plan.TypeMappings[0].SourceType == "Vendor.Lib.Config"
                && plan.TypeMappings[0].DestinationType == "MyApp.Config")
            .And("there is one exclusion pattern", plan =>
                plan.ExclusionPatterns.Count == 1
                && plan.ExclusionPatterns[0] == "Vendor.Lib.Internal*")
            .AssertPassed();

    [Scenario("JSON config loads and produces expected WrapGodConfig")]
    [Fact]
    public Task JsonConfigLoadsCorrectly()
        => Given("a JSON config string", LoadJsonConfig)
            .Then("one type is parsed", config => config.Types.Count == 1)
            .And("the source type is Vendor.Client", config =>
                config.Types[0].SourceType == "Vendor.Client")
            .And("the target name is IClient", config =>
                config.Types[0].TargetName == "IClient")
            .And("include is true", config =>
                config.Types[0].Include == true)
            .And("there are two members", config =>
                config.Types[0].Members.Count == 2)
            .And("the first member is GetUser renamed to FetchUser", config =>
                config.Types[0].Members[0].SourceMember == "GetUser"
                && config.Types[0].Members[0].TargetName == "FetchUser")
            .And("the second member is Timeout with no rename", config =>
                config.Types[0].Members[1].SourceMember == "Timeout"
                && config.Types[0].Members[1].TargetName is null)
            .AssertPassed();

    [Scenario("ConfigMergeEngine precedence — JSON overrides match expected")]
    [Fact]
    public Task MergeEngineJsonPrecedence()
        => Given("conflicting JSON and attribute configs with JSON precedence", MergeWithJsonPrecedence)
            .Then("JSON config wins for type include", result =>
                result.Config.Types.Count == 1 && result.Config.Types[0].Include == true)
            .And("JSON config wins for type target name", result =>
                result.Config.Types[0].TargetName == "JsonClient")
            .And("JSON config wins for member target name", result =>
                result.Config.Types[0].Members.Count == 1
                && result.Config.Types[0].Members[0].TargetName == "JsonGetUser")
            .And("diagnostics are emitted for conflicts", result =>
                result.Diagnostics.Count > 0)
            .AssertPassed();

    [Scenario("Serialize and deserialize WrapGodConfig roundtrip preserves all fields")]
    [Fact]
    public Task SerializeDeserializeRoundtrip()
        => Given("a WrapGodConfig serialized then deserialized", SerializeAndDeserialize)
            .Then("one type survives the roundtrip", config => config.Types.Count == 1)
            .And("source type is preserved", config =>
                config.Types[0].SourceType == "Vendor.Client")
            .And("include is preserved", config =>
                config.Types[0].Include == true)
            .And("target name is preserved", config =>
                config.Types[0].TargetName == "IClient")
            .And("member count is preserved", config =>
                config.Types[0].Members.Count == 2)
            .And("first member source is preserved", config =>
                config.Types[0].Members[0].SourceMember == "GetUser")
            .And("first member target name is preserved", config =>
                config.Types[0].Members[0].TargetName == "FetchUser")
            .And("second member source is preserved", config =>
                config.Types[0].Members[1].SourceMember == "Timeout")
            .And("second member include is preserved", config =>
                config.Types[0].Members[1].Include == true)
            .AssertPassed();

    [Fact]
    public void FluentConfig_RoundtripsThroughJson()
    {
        var fluentConfig = BuildProgrammaticConfig();
        var roundtripped = JsonConfigLoader.LoadFromJson(Serialize(fluentConfig));

        Assert.Equal(Normalize(fluentConfig), Normalize(roundtripped));
    }

    [Fact]
    public void MergeOutput_IsIdenticalAcrossFluentAndJsonInputs()
    {
        var fluentConfig = BuildProgrammaticConfig();
        var jsonConfig = JsonConfigLoader.LoadFromJson(BuildHandWrittenJson());
        var attributeBaseline = BuildAttributeBaselineConfig();

        var fromFluent = ConfigMergeEngine.Merge(fluentConfig, attributeBaseline);
        var fromJson = ConfigMergeEngine.Merge(jsonConfig, attributeBaseline);

        Assert.Equal(Normalize(fromFluent.Config), Normalize(fromJson.Config));
        Assert.Equal(fromFluent.Diagnostics.Count, fromJson.Diagnostics.Count);
    }

    [Fact]
    public void Roundtrip_PreservesExplicitMemberExcludeRegression()
    {
        var json = """
            {
              "types": [
                {
                  "sourceType": "Vendor.Lib.Client",
                  "members": [
                    { "sourceMember": "Dispose", "include": false }
                  ]
                }
              ]
            }
            """;

        var parsed = JsonConfigLoader.LoadFromJson(json);
        var roundtripped = JsonConfigLoader.LoadFromJson(Serialize(parsed));

        Assert.Single(roundtripped.Types);
        Assert.Single(roundtripped.Types[0].Members);
        Assert.Equal("Dispose", roundtripped.Types[0].Members[0].SourceMember);
        Assert.False(roundtripped.Types[0].Members[0].Include);
    }
}
