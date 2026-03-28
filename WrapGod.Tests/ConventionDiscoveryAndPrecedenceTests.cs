using WrapGod.Abstractions.Config;
using WrapGod.Manifest.Config;
using Xunit;

namespace WrapGod.Tests;

public sealed class ConventionDiscoveryAndPrecedenceTests
{
    [Fact]
    public void SourceDiscovery_UsesConfiguredOrder()
    {
        var result = SourceDiscoveryEngine.Discover(new SourceDiscoveryInput
        {
            WrapGodPackage = "Vendor.Primary",
            PackageReferences = new[] { "Vendor.Secondary" },
            HasSelfSource = true,
            ExplicitSource = "Vendor.Explicit"
        });

        Assert.Equal("Vendor.Primary", result.Source);
        Assert.Equal("WrapGodPackage", result.Strategy);
    }

    [Fact]
    public void SourceDiscovery_PackageReferences_UseDeclarationOrder()
    {
        var result = SourceDiscoveryEngine.Discover(new SourceDiscoveryInput
        {
            PackageReferences = ["Vendor.Secondary", "Vendor.Primary"],
            HasSelfSource = true,
            ExplicitSource = "Vendor.Explicit"
        });

        Assert.Equal("Vendor.Secondary", result.Source);
        Assert.Equal("PackageReference", result.Strategy);
    }

    [Fact]
    public void SourceDiscovery_NoSource_EmitsActionableDiagnostic()
    {
        var result = SourceDiscoveryEngine.Discover(new SourceDiscoveryInput());

        Assert.Null(result.Source);
        var diag = Assert.Single(result.Diagnostics);
        Assert.Equal("WG6011", diag.Code);
        Assert.Contains("WrapGodPackage", diag.Message);
        Assert.Contains("@self", diag.Message);
        Assert.Contains("explicit", diag.Message);
    }

    [Fact]
    public void PrecedenceEngine_AppliesDefaultChain_Deterministically()
    {
        var layers = new ConfigSourceLayers
        {
            Defaults = MakeConfig(include: true, targetName: "DefaultName"),
            RootJson = MakeConfig(include: true, targetName: "RootName"),
            ProjectJson = MakeConfig(include: true, targetName: "ProjectName"),
            Attributes = MakeConfig(include: false, targetName: "AttributeName"),
            Fluent = MakeConfig(include: true, targetName: "FluentName")
        };

        var merged = ConfigPrecedenceEngine.Merge(layers);
        var type = Assert.Single(merged.Config.Types);

        Assert.Equal("Vendor.Client", type.SourceType);
        Assert.True(type.Include);
        Assert.Equal("FluentName", type.TargetName);
    }

    [Fact]
    public void PrecedenceEngine_NoLayers_EmitsActionableDiagnostic()
    {
        var merged = ConfigPrecedenceEngine.Merge(new ConfigSourceLayers());

        Assert.Empty(merged.Config.Types);
        var diag = Assert.Single(merged.Diagnostics);
        Assert.Equal("WG6010", diag.Code);
        Assert.Contains("root/project", diag.Message);
        Assert.Contains("[WrapType]", diag.Message);
        Assert.Contains("fluent", diag.Message);
    }

    private static WrapGodConfig MakeConfig(bool? include, string? targetName)
    {
        return new WrapGodConfig
        {
            Types = new System.Collections.Generic.List<TypeConfig>
            {
                new TypeConfig
                {
                    SourceType = "Vendor.Client",
                    Include = include,
                    TargetName = targetName,
                    Members = new System.Collections.Generic.List<MemberConfig>
                    {
                        new MemberConfig
                        {
                            SourceMember = "DoWork",
                            Include = include,
                            TargetName = targetName is null ? null : targetName + "Member"
                        }
                    }
                }
            }
        };
    }
}
