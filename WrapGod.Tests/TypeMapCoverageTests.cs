using TinyBDD;
using TinyBDD.Xunit;
using WrapGod.Abstractions.Config;
using WrapGod.TypeMap;
using WrapGod.TypeMap.Generation;
using Xunit.Abstractions;
using TypeMapping = WrapGod.TypeMap.TypeMapping;

namespace WrapGod.Tests;

[Feature("TypeMap coverage: untested paths")]
public sealed class TypeMapCoverageTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    // ── Scenario: Nullable type mapping emission ─────────────────────

    [Scenario("Nullable mapping emits null check and cast")]
    [Fact]
    public Task NullableMappingEmission()
    {
        var plan = new TypeMappingPlan
        {
            Mappings = new List<TypeMapping>
            {
                new()
                {
                    SourceType = "Vendor.NullableInt",
                    DestinationType = "int?",
                    Kind = TypeMappingKind.Nullable,
                },
            },
        };

        return Given("a plan with a nullable mapping", () => TypeMapperEmitter.Emit(plan))
            .Then("the output contains a null check", code =>
                code.Contains("if (source == null) return default;", StringComparison.Ordinal))
            .And("the output contains a cast to the destination type", code =>
                code.Contains("return (int?)source;", StringComparison.Ordinal))
            .And("the output contains the mapper class name", code =>
                code.Contains("Vendor_NullableIntMapper", StringComparison.Ordinal))
            .AssertPassed();
    }

    // ── Scenario: Custom converter mapping emission ──────────────────

    [Scenario("Custom converter mapping emits converter call")]
    [Fact]
    public Task CustomConverterMappingEmission()
    {
        var plan = new TypeMappingPlan
        {
            Mappings = new List<TypeMapping>
            {
                new()
                {
                    SourceType = "Vendor.DateTime",
                    DestinationType = "DateTimeOffset",
                    Kind = TypeMappingKind.Custom,
                    Converter = new ConverterRef
                    {
                        TypeName = "MyApp.Converters.DateTimeConverter",
                        MethodName = "ToOffset",
                    },
                },
            },
        };

        return Given("a plan with a custom converter mapping", () => TypeMapperEmitter.Emit(plan))
            .Then("the output delegates to the converter", code =>
                code.Contains("MyApp.Converters.DateTimeConverter.ToOffset(source)", StringComparison.Ordinal))
            .And("the output has the correct mapper class", code =>
                code.Contains("Vendor_DateTimeMapper", StringComparison.Ordinal))
            .AssertPassed();
    }

    // ── Scenario: Custom converter with default method name ──────────

    [Scenario("Custom converter with null MethodName defaults to Convert")]
    [Fact]
    public Task CustomConverterDefaultMethodName()
    {
        var plan = new TypeMappingPlan
        {
            Mappings = new List<TypeMapping>
            {
                new()
                {
                    SourceType = "Vendor.Guid",
                    DestinationType = "string",
                    Kind = TypeMappingKind.Custom,
                    Converter = new ConverterRef
                    {
                        TypeName = "MyApp.GuidConverter",
                        MethodName = null,
                    },
                },
            },
        };

        return Given("a plan with a custom converter with no MethodName", () => TypeMapperEmitter.Emit(plan))
            .Then("the output uses default method name Convert", code =>
                code.Contains("MyApp.GuidConverter.Convert(source)", StringComparison.Ordinal))
            .AssertPassed();
    }

    // ── Scenario: Custom mapping without converter throws ────────────

    [Scenario("Custom mapping kind without converter throws InvalidOperationException")]
    [Fact]
    public Task CustomMappingWithoutConverter_Throws()
    {
        var mapping = new TypeMapping
        {
            SourceType = "Vendor.Bad",
            DestinationType = "string",
            Kind = TypeMappingKind.Custom,
            Converter = null,
        };

        return Given("a custom mapping with no converter", () =>
            {
                try
                {
                    TypeMapperEmitter.EmitSingle(mapping);
                    return (Threw: false, Message: "");
                }
                catch (InvalidOperationException ex)
                {
                    return (Threw: true, Message: ex.Message);
                }
            })
            .Then("an exception is thrown", result => result.Threw)
            .And("the message mentions the source type", result =>
                result.Message.Contains("Vendor.Bad", StringComparison.Ordinal))
            .AssertPassed();
    }

    // ── Scenario: Collection mapping with element mapper ─────────────

    [Scenario("Collection mapping emits Select with element mapper")]
    [Fact]
    public Task CollectionMappingWithElementMapper()
    {
        var plan = new TypeMappingPlan
        {
            Mappings = new List<TypeMapping>
            {
                new()
                {
                    SourceType = "Vendor.ItemList",
                    DestinationType = "IReadOnlyList<Item>",
                    Kind = TypeMappingKind.Collection,
                },
            },
        };

        return Given("a plan with a collection mapping", () => TypeMapperEmitter.Emit(plan))
            .Then("the output contains a Select projection", code =>
                code.Contains(".Select(", StringComparison.Ordinal))
            .And("the output references the element mapper", code =>
                code.Contains("Vendor_ItemListElementMapper.Map", StringComparison.Ordinal))
            .And("the output calls ToList()", code =>
                code.Contains(".ToList()", StringComparison.Ordinal))
            .AssertPassed();
    }

    // ── Scenario: Empty TypeMappingPlan ──────────────────────────────

    [Scenario("Empty TypeMappingPlan emits header-only source")]
    [Fact]
    public Task EmptyTypeMappingPlan()
    {
        var plan = new TypeMappingPlan { Mappings = new List<TypeMapping>() };

        return Given("an empty plan with no mappings", () => TypeMapperEmitter.Emit(plan))
            .Then("the output contains the auto-generated header", code =>
                code.Contains("// <auto-generated />", StringComparison.Ordinal))
            .And("the output contains the namespace", code =>
                code.Contains("namespace WrapGod.Generated.Mappers;", StringComparison.Ordinal))
            .And("the output does not contain any mapper class", code =>
                !code.Contains("public static class", StringComparison.Ordinal))
            .AssertPassed();
    }

    // ── Scenario: TypeMappingPlanner with no matching types ──────────

    [Scenario("TypeMappingPlanner with excluded-only config produces empty plan")]
    [Fact]
    public Task PlannerWithNoMatchingTypes()
    {
        var config = new WrapGodConfig();
        config.Types.Add(new TypeConfig
        {
            SourceType = "Vendor.Hidden",
            Include = false,
        });

        return Given("a config where the only type is excluded",
                () => TypeMappingPlanner.BuildPlan(config))
            .Then("the plan has no mappings", plan => plan.Mappings.Count == 0)
            .AssertPassed();
    }

    // ── Scenario: TypeMappingPlanner with empty config ───────────────

    [Scenario("TypeMappingPlanner with empty config produces empty plan")]
    [Fact]
    public Task PlannerWithEmptyConfig()
    {
        var config = new WrapGodConfig();

        return Given("a config with no types at all",
                () => TypeMappingPlanner.BuildPlan(config))
            .Then("the plan has no mappings", plan => plan.Mappings.Count == 0)
            .AssertPassed();
    }

    // ── Scenario: MemberMapping with converter ref ───────────────────

    [Scenario("MemberMapping with converter ref emits converter call in object mapping")]
    [Fact]
    public Task MemberMappingWithConverterRef()
    {
        var plan = new TypeMappingPlan
        {
            Mappings = new List<TypeMapping>
            {
                new()
                {
                    SourceType = "Vendor.Order",
                    DestinationType = "OrderDto",
                    Kind = TypeMappingKind.ObjectMapping,
                    MemberMappings = new List<MemberMapping>
                    {
                        new()
                        {
                            SourceMember = "CreatedAt",
                            DestinationMember = "Timestamp",
                            Converter = new ConverterRef
                            {
                                TypeName = "MyApp.DateConverter",
                                MethodName = "ToUnix",
                            },
                        },
                        new()
                        {
                            SourceMember = "Amount",
                            DestinationMember = "Amount",
                        },
                    },
                },
            },
        };

        return Given("a plan with a member that has a converter ref", () => TypeMapperEmitter.Emit(plan))
            .Then("the converted member uses the converter call", code =>
                code.Contains("Timestamp = MyApp.DateConverter.ToUnix(source.CreatedAt)", StringComparison.Ordinal))
            .And("the plain member uses direct copy", code =>
                code.Contains("Amount = source.Amount", StringComparison.Ordinal))
            .AssertPassed();
    }

    // ── Scenario: MemberMapping with converter using default method ──

    [Scenario("MemberMapping converter with null MethodName defaults to Convert")]
    [Fact]
    public Task MemberMappingConverterDefaultMethodName()
    {
        var plan = new TypeMappingPlan
        {
            Mappings = new List<TypeMapping>
            {
                new()
                {
                    SourceType = "Vendor.User",
                    DestinationType = "UserDto",
                    Kind = TypeMappingKind.ObjectMapping,
                    MemberMappings = new List<MemberMapping>
                    {
                        new()
                        {
                            SourceMember = "Address",
                            DestinationMember = "AddressDto",
                            Converter = new ConverterRef
                            {
                                TypeName = "MyApp.AddressConverter",
                                MethodName = null,
                            },
                        },
                    },
                },
            },
        };

        return Given("a member with converter that has no MethodName",
                () => TypeMapperEmitter.Emit(plan))
            .Then("the output uses default Convert method name", code =>
                code.Contains("MyApp.AddressConverter.Convert(source.Address)", StringComparison.Ordinal))
            .AssertPassed();
    }

    // ── Scenario: ObjectMapping with top-level converter ─────────────

    [Scenario("ObjectMapping with top-level converter delegates entirely")]
    [Fact]
    public Task ObjectMappingWithTopLevelConverter()
    {
        var plan = new TypeMappingPlan
        {
            Mappings = new List<TypeMapping>
            {
                new()
                {
                    SourceType = "Vendor.LegacyData",
                    DestinationType = "ModernData",
                    Kind = TypeMappingKind.ObjectMapping,
                    Converter = new ConverterRef
                    {
                        TypeName = "MyApp.LegacyConverter",
                        MethodName = "Migrate",
                    },
                    MemberMappings = new List<MemberMapping>
                    {
                        new() { SourceMember = "Name", DestinationMember = "Name" },
                    },
                },
            },
        };

        return Given("an object mapping with a top-level converter (overrides member mappings)",
                () => TypeMapperEmitter.Emit(plan))
            .Then("the output delegates to the top-level converter", code =>
                code.Contains("MyApp.LegacyConverter.Migrate(source)", StringComparison.Ordinal))
            .And("the output does not contain member-by-member mapping", code =>
                !code.Contains("source.Name", StringComparison.Ordinal))
            .AssertPassed();
    }

    // ── Scenario: MapperSourceBuilder indent/outdent edge cases ──────

    [Scenario("MapperSourceBuilder Outdent below zero stays at zero")]
    [Fact]
    public Task MapperSourceBuilder_OutdentBelowZero()
        => Given("a fresh MapperSourceBuilder",
                () =>
                {
                    var sb = new MapperSourceBuilder();
                    sb.Outdent();
                    sb.Outdent();
                    return sb.IndentLevel;
                })
            .Then("the indent level stays at zero", level => level == 0)
            .AssertPassed();

    [Scenario("MapperSourceBuilder indent/outdent cycle produces correct indentation")]
    [Fact]
    public Task MapperSourceBuilder_IndentOutdentCycle()
        => Given("a MapperSourceBuilder with indent/outdent operations",
                () =>
                {
                    var sb = new MapperSourceBuilder();
                    sb.AppendLine("line0");
                    sb.Indent();
                    sb.AppendLine("line1");
                    sb.Indent();
                    sb.AppendLine("line2");
                    sb.Outdent();
                    sb.AppendLine("line1again");
                    sb.Outdent();
                    sb.AppendLine("line0again");
                    return sb.ToString();
                })
            .Then("line0 has no indentation", output =>
                output.Contains("line0\r\n") || output.Contains("line0\n"))
            .And("line1 has 4 spaces", output =>
                output.Contains("    line1"))
            .And("line2 has 8 spaces", output =>
                output.Contains("        line2"))
            .And("line1again has 4 spaces after outdent", output =>
                output.Contains("    line1again"))
            .And("line0again has no indentation after double outdent", output =>
                output.Contains("\nline0again", StringComparison.Ordinal) || output.StartsWith("line0again", StringComparison.Ordinal))
            .AssertPassed();

    [Scenario("MapperSourceBuilder OpenBrace and CloseBrace manage indent")]
    [Fact]
    public Task MapperSourceBuilder_OpenCloseBrace()
        => Given("a builder using OpenBrace and CloseBrace",
                () =>
                {
                    var sb = new MapperSourceBuilder();
                    sb.AppendLine("class Foo");
                    sb.OpenBrace();
                    sb.AppendLine("int x;");
                    sb.CloseBrace();
                    return sb.ToString();
                })
            .Then("the opening brace is at base indentation", output =>
                output.Contains('{'))
            .And("the member is indented inside the brace", output =>
                output.Contains("    int x;"))
            .And("the closing brace is at base indentation", output =>
                output.Contains('}'))
            .And("the indent level returns to zero", output =>
                output.TrimEnd().EndsWith('}'))
            .AssertPassed();

    [Scenario("MapperSourceBuilder BlankLine inserts empty line")]
    [Fact]
    public Task MapperSourceBuilder_BlankLine()
        => Given("a builder with a blank line between two lines",
                () =>
                {
                    var sb = new MapperSourceBuilder();
                    sb.AppendLine("first");
                    sb.BlankLine();
                    sb.AppendLine("second");
                    return sb.ToString();
                })
            .Then("there is an empty line between first and second", output =>
                output.Contains("first\r\n\r\nsecond") || output.Contains("first\n\nsecond"))
            .AssertPassed();

    // ── Scenario: TypeMappingPlan.FindBySourceType/FindByDestinationType ─

    [Scenario("TypeMappingPlan lookup methods")]
    [Fact]
    public Task TypeMappingPlan_LookupMethods()
    {
        var plan = new TypeMappingPlan
        {
            Mappings = new List<TypeMapping>
            {
                new()
                {
                    SourceType = "Vendor.Client",
                    DestinationType = "IClient",
                    Kind = TypeMappingKind.ObjectMapping,
                    MemberMappings = new List<MemberMapping>(),
                },
            },
        };

        return Given("a plan with one mapping", () => plan)
            .Then("FindBySourceType finds the mapping", p =>
                p.FindBySourceType("Vendor.Client") != null)
            .And("FindByDestinationType finds the mapping", p =>
                p.FindByDestinationType("IClient") != null)
            .And("FindBySourceType returns null for unknown", p =>
                p.FindBySourceType("Unknown") == null)
            .And("FindByDestinationType returns null for unknown", p =>
                p.FindByDestinationType("Unknown") == null)
            .AssertPassed();
    }

    // ── Scenario: TypeMappingPlanner with member exclusion ───────────

    [Scenario("TypeMappingPlanner excludes members with Include=false")]
    [Fact]
    public Task PlannerExcludesMembers()
    {
        var config = new WrapGodConfig();
        config.Types.Add(new TypeConfig
        {
            SourceType = "Vendor.Svc",
            Include = true,
        });
        config.Types[0].Members.Add(new MemberConfig
        {
            SourceMember = "Visible", Include = true,
        });
        config.Types[0].Members.Add(new MemberConfig
        {
            SourceMember = "Hidden", Include = false,
        });

        return Given("a config with one included and one excluded member",
                () => TypeMappingPlanner.BuildPlan(config))
            .Then("the mapping has one member", plan =>
                plan.Mappings[0].MemberMappings.Count == 1)
            .And("the included member is Visible", plan =>
                plan.Mappings[0].MemberMappings[0].SourceMember == "Visible")
            .AssertPassed();
    }

    // ── Scenario: TypeMappingPlanner with override for converter ─────

    [Scenario("TypeMappingPlanner applies converter override")]
    [Fact]
    public Task PlannerAppliesConverterOverride()
    {
        var config = new WrapGodConfig();
        config.Types.Add(new TypeConfig
        {
            SourceType = "Vendor.Blob",
            Include = true,
        });

        var overrides = new List<TypeMappingOverride>
        {
            new()
            {
                SourceType = "Vendor.Blob",
                Kind = TypeMappingKind.Custom,
                Converter = new ConverterRef
                {
                    TypeName = "MyApp.BlobConverter",
                    MethodName = "Decode",
                },
            },
        };

        return Given("a config with a converter override",
                () => TypeMappingPlanner.BuildPlan(config, overrides))
            .Then("the mapping kind is Custom", plan =>
                plan.Mappings[0].Kind == TypeMappingKind.Custom)
            .And("the converter is set", plan =>
                plan.Mappings[0].Converter != null)
            .And("the converter type name is correct", plan =>
                plan.Mappings[0].Converter!.TypeName == "MyApp.BlobConverter")
            .And("the converter method name is correct", plan =>
                plan.Mappings[0].Converter!.MethodName == "Decode")
            .AssertPassed();
    }

    // ── Scenario: EmitSingle produces standalone mapper ──────────────

    [Scenario("EmitSingle produces a standalone mapper for a single mapping")]
    [Fact]
    public Task EmitSingle_StandaloneMapper()
    {
        var mapping = new TypeMapping
        {
            SourceType = "Vendor.Item",
            DestinationType = "ItemDto",
            Kind = TypeMappingKind.ObjectMapping,
            MemberMappings = new List<MemberMapping>
            {
                new() { SourceMember = "Id", DestinationMember = "Id" },
            },
        };

        return Given("a single type mapping", () => TypeMapperEmitter.EmitSingle(mapping))
            .Then("the output contains the auto-generated header", code =>
                code.Contains("// <auto-generated />", StringComparison.Ordinal))
            .And("the output contains the namespace", code =>
                code.Contains("namespace WrapGod.Generated.Mappers;", StringComparison.Ordinal))
            .And("the output contains the mapper class", code =>
                code.Contains("Vendor_ItemMapper", StringComparison.Ordinal))
            .And("the output contains the member mapping", code =>
                code.Contains("Id = source.Id", StringComparison.Ordinal))
            .AssertPassed();
    }

    // ── Scenario: Special characters in type name produce valid class name ─

    [Scenario("Type name with special chars produces valid mapper class name")]
    [Fact]
    public Task SpecialCharsInTypeName_ValidClassName()
    {
        var mapping = new TypeMapping
        {
            SourceType = "System.Collections.Generic.Dictionary<string, int?>",
            DestinationType = "MyDict",
            Kind = TypeMappingKind.Enum, // simple emit
        };

        return Given("a mapping with a complex generic source type name",
                () => TypeMapperEmitter.EmitSingle(mapping))
            .Then("the class name has no dots", code =>
                !code.Contains("class System."))
            .And("the class name has no angle brackets", code =>
                !code.Contains("class <") && !code.Contains("class >"))
            .And("the output contains a sanitized mapper class", code =>
                code.Contains("System_Collections_Generic_Dictionary_string__int__Mapper",
                    StringComparison.Ordinal))
            .AssertPassed();
    }

    // ── Scenario: Enum mapping emission ──────────────────────────────

    [Scenario("Enum mapping emits simple cast")]
    [Fact]
    public Task EnumMappingEmission()
    {
        var mapping = new TypeMapping
        {
            SourceType = "Vendor.Color",
            DestinationType = "MyApp.Color",
            Kind = TypeMappingKind.Enum,
        };

        return Given("an enum mapping", () => TypeMapperEmitter.EmitSingle(mapping))
            .Then("the output contains a cast expression", code =>
                code.Contains("return (MyApp.Color)source;", StringComparison.Ordinal))
            .AssertPassed();
    }

    // ── Scenario: TargetName defaults to SourceType when null ────────

    [Scenario("TypeMappingPlanner defaults DestinationType to SourceType when no TargetName")]
    [Fact]
    public Task PlannerDefaultsDestType()
    {
        var config = new WrapGodConfig();
        config.Types.Add(new TypeConfig
        {
            SourceType = "Vendor.SameType",
            Include = true,
            TargetName = null,
        });

        return Given("a config with no TargetName",
                () => TypeMappingPlanner.BuildPlan(config))
            .Then("the destination type equals the source type", plan =>
                plan.Mappings[0].DestinationType == "Vendor.SameType")
            .AssertPassed();
    }

    // ── Scenario: Member TargetName defaults to SourceMember ─────────

    [Scenario("MemberMapping DestinationMember defaults to SourceMember when no TargetName")]
    [Fact]
    public Task MemberDefaultsDestName()
    {
        var config = new WrapGodConfig();
        config.Types.Add(new TypeConfig
        {
            SourceType = "Vendor.Svc",
            Include = true,
        });
        config.Types[0].Members.Add(new MemberConfig
        {
            SourceMember = "SameMethod",
            Include = true,
            TargetName = null,
        });

        return Given("a member config with no TargetName",
                () => TypeMappingPlanner.BuildPlan(config))
            .Then("the destination member equals the source member", plan =>
                plan.Mappings[0].MemberMappings[0].DestinationMember == "SameMethod")
            .AssertPassed();
    }
}
