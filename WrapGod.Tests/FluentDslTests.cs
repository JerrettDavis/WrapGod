using WrapGod.Fluent;

namespace WrapGod.Tests;

public class FluentDslTests
{
    [Fact]
    public void Build_ProducesValidGenerationPlan()
    {
        var plan = WrapGodConfiguration.Create()
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

        Assert.NotNull(plan);
        Assert.Equal("Vendor.Lib", plan.AssemblyName);
        Assert.Equal(2, plan.TypeDirectives.Count);
        Assert.Single(plan.TypeMappings);
        Assert.Single(plan.ExclusionPatterns);
    }

    [Fact]
    public void TypeDirectives_RecordSourceAndTargetNames()
    {
        var plan = WrapGodConfiguration.Create()
            .ForAssembly("TestLib")
            .WrapType("TestLib.Foo")
                .As("IFoo")
            .Build();

        var directive = Assert.Single(plan.TypeDirectives);
        Assert.Equal("TestLib.Foo", directive.SourceType);
        Assert.Equal("IFoo", directive.TargetName);
    }

    [Fact]
    public void MethodWrapping_RecordedWithRename()
    {
        var plan = WrapGodConfiguration.Create()
            .ForAssembly("TestLib")
            .WrapType("TestLib.Svc")
                .WrapMethod("DoWork").As("Execute")
            .Build();

        var directive = Assert.Single(plan.TypeDirectives);
        var member = Assert.Single(directive.MemberDirectives);
        Assert.Equal("DoWork", member.SourceName);
        Assert.Equal("Execute", member.TargetName);
        Assert.Equal(MemberDirectiveKind.Method, member.Kind);
    }

    [Fact]
    public void PropertyWrapping_Recorded()
    {
        var plan = WrapGodConfiguration.Create()
            .ForAssembly("TestLib")
            .WrapType("TestLib.Svc")
                .WrapProperty("Timeout")
            .Build();

        var directive = Assert.Single(plan.TypeDirectives);
        var member = Assert.Single(directive.MemberDirectives);
        Assert.Equal("Timeout", member.SourceName);
        Assert.Null(member.TargetName);
        Assert.Equal(MemberDirectiveKind.Property, member.Kind);
    }

    [Fact]
    public void ExcludeMember_Recorded()
    {
        var plan = WrapGodConfiguration.Create()
            .ForAssembly("TestLib")
            .WrapType("TestLib.Svc")
                .ExcludeMember("Dispose")
                .ExcludeMember("Finalize")
            .Build();

        var directive = Assert.Single(plan.TypeDirectives);
        Assert.Equal(2, directive.ExcludedMembers.Count);
        Assert.Contains("Dispose", directive.ExcludedMembers);
        Assert.Contains("Finalize", directive.ExcludedMembers);
    }

    [Fact]
    public void WrapAllPublicMembers_FlagSet()
    {
        var plan = WrapGodConfiguration.Create()
            .ForAssembly("TestLib")
            .WrapType("TestLib.Logger")
                .WrapAllPublicMembers()
            .Build();

        var directive = Assert.Single(plan.TypeDirectives);
        Assert.True(directive.WrapAllPublicMembers);
    }

    [Fact]
    public void TypeMappings_Recorded()
    {
        var plan = WrapGodConfiguration.Create()
            .ForAssembly("TestLib")
            .MapType("TestLib.Config", "MyApp.Config")
            .MapType("TestLib.Options", "MyApp.Options")
            .Build();

        Assert.Equal(2, plan.TypeMappings.Count);
        Assert.Equal("TestLib.Config", plan.TypeMappings[0].SourceType);
        Assert.Equal("MyApp.Config", plan.TypeMappings[0].DestinationType);
        Assert.Equal("TestLib.Options", plan.TypeMappings[1].SourceType);
        Assert.Equal("MyApp.Options", plan.TypeMappings[1].DestinationType);
    }

    [Fact]
    public void ExclusionPatterns_Recorded()
    {
        var plan = WrapGodConfiguration.Create()
            .ForAssembly("TestLib")
            .ExcludeType("TestLib.Internal*")
            .ExcludeType("TestLib.Debug*")
            .Build();

        Assert.Equal(2, plan.ExclusionPatterns.Count);
        Assert.Contains("TestLib.Internal*", plan.ExclusionPatterns);
        Assert.Contains("TestLib.Debug*", plan.ExclusionPatterns);
    }

    [Fact]
    public void CompatibilityMode_Recorded()
    {
        var plan = WrapGodConfiguration.Create()
            .ForAssembly("TestLib")
            .WithCompatibilityMode("strict")
            .Build();

        Assert.Equal("strict", plan.CompatibilityMode);
    }

    [Fact]
    public void CompatibilityMode_NullByDefault()
    {
        var plan = WrapGodConfiguration.Create()
            .ForAssembly("TestLib")
            .Build();

        Assert.Null(plan.CompatibilityMode);
    }

    [Fact]
    public void MultipleTypes_WithMixedConfig()
    {
        var plan = WrapGodConfiguration.Create()
            .ForAssembly("Vendor.Lib")
            .WrapType("Vendor.Lib.HttpClient")
                .As("IHttpClient")
                .WrapMethod("SendAsync").As("SendRequestAsync")
                .WrapProperty("Timeout")
                .ExcludeMember("Dispose")
            .WrapType("Vendor.Lib.Logger")
                .As("ILogger")
                .WrapAllPublicMembers()
            .Build();

        Assert.Equal(2, plan.TypeDirectives.Count);

        var http = plan.TypeDirectives[0];
        Assert.Equal("Vendor.Lib.HttpClient", http.SourceType);
        Assert.Equal("IHttpClient", http.TargetName);
        Assert.Equal(2, http.MemberDirectives.Count);
        Assert.Single(http.ExcludedMembers);
        Assert.False(http.WrapAllPublicMembers);

        var logger = plan.TypeDirectives[1];
        Assert.Equal("Vendor.Lib.Logger", logger.SourceType);
        Assert.Equal("ILogger", logger.TargetName);
        Assert.True(logger.WrapAllPublicMembers);
        Assert.Empty(logger.MemberDirectives);
    }
}
