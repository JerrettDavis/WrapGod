using System;
using System.Text.Json;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace WrapGod.Tests;

[Feature("MigrationSchema JSON serialization round-trip and validation")]
public sealed class MigrationSchemaTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    // ── Helpers ──────────────────────────────────────────────────────

    private static MigrationSchema BuildFullSchema() => new()
    {
        Schema = "wrapgod-migration/1.0",
        Library = "MudBlazor",
        From = "6.0.0",
        To = "7.0.0",
        GeneratedFrom = "manifest-diff",
        LastEdited = new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero),
        Rules =
        [
            new RenameTypeRule
            {
                Id = "rename-Button",
                Confidence = RuleConfidence.Auto,
                OldName = "MudBlazor.Button",
                NewName = "MudBlazor.MudButton",
            },
            new RenameMemberRule
            {
                Id = "rename-Color-prop",
                Confidence = RuleConfidence.Verified,
                TypeName = "MudBlazor.MudButton",
                OldMemberName = "Color",
                NewMemberName = "ButtonColor",
            },
            new RenameNamespaceRule
            {
                Id = "rename-ns-components",
                Confidence = RuleConfidence.Auto,
                OldNamespace = "MudBlazor.Components",
                NewNamespace = "MudBlazor",
            },
            new ChangeParameterRule
            {
                Id = "change-param-size",
                Confidence = RuleConfidence.Manual,
                TypeName = "MudBlazor.MudButton",
                MethodName = "SetSize",
                OldParameterName = "size",
                NewParameterName = "buttonSize",
                OldParameterType = "int",
                NewParameterType = "MudBlazor.Size",
            },
            new RemoveMemberRule
            {
                Id = "remove-legacy-ctor",
                Confidence = RuleConfidence.Manual,
                TypeName = "MudBlazor.MudButton",
                MemberName = ".ctor",
            },
            new AddRequiredParameterRule
            {
                Id = "add-required-theme",
                Confidence = RuleConfidence.Manual,
                TypeName = "MudBlazor.MudThemeProvider",
                MethodName = "Apply",
                ParameterName = "theme",
                ParameterType = "MudBlazor.MudTheme",
                Position = 0,
            },
            new ChangeTypeReferenceRule
            {
                Id = "change-ilist-to-ireadonly",
                Confidence = RuleConfidence.Auto,
                OldType = "System.Collections.Generic.IList`1",
                NewType = "System.Collections.Generic.IReadOnlyList`1",
            },
            new SplitMethodRule
            {
                Id = "split-render",
                Confidence = RuleConfidence.Manual,
                TypeName = "MudBlazor.MudCard",
                OldMethodName = "Render",
                NewMethodNames = ["RenderHeader", "RenderBody", "RenderFooter"],
            },
            new ExtractParameterObjectRule
            {
                Id = "extract-params",
                Confidence = RuleConfidence.Manual,
                TypeName = "MudBlazor.MudDialog",
                MethodName = "ShowAsync",
                ParameterObjectType = "MudBlazor.DialogParameters",
                ExtractedParameters = ["title", "content"],
            },
            new PropertyToMethodRule
            {
                Id = "prop-to-method-disabled",
                Confidence = RuleConfidence.Auto,
                TypeName = "MudBlazor.MudButton",
                OldPropertyName = "Disabled",
                NewMethodName = "SetDisabled",
            },
            new MoveMemberRule
            {
                Id = "move-helper",
                Confidence = RuleConfidence.Verified,
                OldTypeName = "MudBlazor.Utilities",
                NewTypeName = "MudBlazor.MudHelpers",
                MemberName = "GetColor",
            },
        ],
    };

    // ── Serialization round-trip ──────────────────────────────────────

    [Scenario("Full schema round-trips through JSON serialization")]
    [Fact]
    public Task FullSchemaRoundTrip() =>
        Given("a full migration schema serialized and deserialized", () =>
        {
            var original = BuildFullSchema();
            var json = MigrationSchemaSerializer.Serialize(original);
            var roundtrip = MigrationSchemaSerializer.Deserialize(json);
            return (Original: original, Roundtrip: roundtrip!, Json: json);
        })
        .Then("schema identifier is preserved", r => r.Roundtrip.Schema == "wrapgod-migration/1.0")
        .And("library is preserved", r => r.Roundtrip.Library == "MudBlazor")
        .And("from version is preserved", r => r.Roundtrip.From == "6.0.0")
        .And("to version is preserved", r => r.Roundtrip.To == "7.0.0")
        .And("generatedFrom is preserved", r => r.Roundtrip.GeneratedFrom == "manifest-diff")
        .And("lastEdited is preserved", r => r.Roundtrip.LastEdited == r.Original.LastEdited)
        .And("all 11 rules are preserved", r => r.Roundtrip.Rules.Count == 11)
        .AssertPassed();

    [Scenario("RenameTypeRule round-trips correctly")]
    [Fact]
    public Task RenameTypeRuleRoundTrip() =>
        Given("a schema with a RenameTypeRule serialized and deserialized", () =>
        {
            var schema = new MigrationSchema
            {
                Library = "TestLib", From = "1.0", To = "2.0",
                Rules = [new RenameTypeRule { Id = "r1", OldName = "Foo", NewName = "Bar" }],
            };
            var json = MigrationSchemaSerializer.Serialize(schema);
            var roundtrip = MigrationSchemaSerializer.Deserialize(json)!;
            return (Rule: roundtrip.Rules[0], Json: json);
        })
        .Then("rule is a RenameTypeRule", r => r.Rule is RenameTypeRule)
        .And("kind is renameType", r => r.Rule.Kind == MigrationRuleKind.RenameType)
        .And("oldName is preserved", r => ((RenameTypeRule)r.Rule).OldName == "Foo")
        .And("newName is preserved", r => ((RenameTypeRule)r.Rule).NewName == "Bar")
        .And("JSON contains camelCase kind", r => r.Json.Contains("\"renameType\""))
        .AssertPassed();

    [Scenario("RenameMemberRule round-trips correctly")]
    [Fact]
    public Task RenameMemberRuleRoundTrip() =>
        Given("a schema with a RenameMemberRule", () =>
        {
            var schema = new MigrationSchema
            {
                Library = "L", From = "1", To = "2",
                Rules = [new RenameMemberRule
                {
                    Id = "r1", TypeName = "Ns.MyClass",
                    OldMemberName = "OldProp", NewMemberName = "NewProp",
                }],
            };
            var json = MigrationSchemaSerializer.Serialize(schema);
            return (RenameMemberRule)MigrationSchemaSerializer.Deserialize(json)!.Rules[0];
        })
        .Then("typeName is preserved", r => r.TypeName == "Ns.MyClass")
        .And("oldMemberName is preserved", r => r.OldMemberName == "OldProp")
        .And("newMemberName is preserved", r => r.NewMemberName == "NewProp")
        .AssertPassed();

    [Scenario("RemoveMemberRule round-trips correctly")]
    [Fact]
    public Task RemoveMemberRuleRoundTrip() =>
        Given("a schema with a RemoveMemberRule", () =>
        {
            var schema = new MigrationSchema
            {
                Library = "L", From = "1", To = "2",
                Rules = [new RemoveMemberRule { Id = "r1", TypeName = "T", MemberName = "M" }],
            };
            var json = MigrationSchemaSerializer.Serialize(schema);
            return (RemoveMemberRule)MigrationSchemaSerializer.Deserialize(json)!.Rules[0];
        })
        .Then("typeName is preserved", r => r.TypeName == "T")
        .And("memberName is preserved", r => r.MemberName == "M")
        .AssertPassed();

    [Scenario("AddRequiredParameterRule round-trips correctly")]
    [Fact]
    public Task AddRequiredParameterRuleRoundTrip() =>
        Given("a schema with a AddRequiredParameterRule", () =>
        {
            var schema = new MigrationSchema
            {
                Library = "L", From = "1", To = "2",
                Rules = [new AddRequiredParameterRule
                {
                    Id = "r1", TypeName = "T", MethodName = "M",
                    ParameterName = "p", ParameterType = "int", Position = 2,
                }],
            };
            var json = MigrationSchemaSerializer.Serialize(schema);
            return (AddRequiredParameterRule)MigrationSchemaSerializer.Deserialize(json)!.Rules[0];
        })
        .Then("parameterName is preserved", r => r.ParameterName == "p")
        .And("parameterType is preserved", r => r.ParameterType == "int")
        .And("position is preserved", r => r.Position == 2)
        .AssertPassed();

    [Scenario("SplitMethodRule round-trips correctly")]
    [Fact]
    public Task SplitMethodRuleRoundTrip() =>
        Given("a schema with a SplitMethodRule", () =>
        {
            var schema = new MigrationSchema
            {
                Library = "L", From = "1", To = "2",
                Rules = [new SplitMethodRule
                {
                    Id = "r1", TypeName = "T", OldMethodName = "Do",
                    NewMethodNames = ["DoA", "DoB"],
                }],
            };
            var json = MigrationSchemaSerializer.Serialize(schema);
            return (SplitMethodRule)MigrationSchemaSerializer.Deserialize(json)!.Rules[0];
        })
        .Then("newMethodNames count is preserved", r => r.NewMethodNames.Count == 2)
        .And("first new method name is preserved", r => r.NewMethodNames[0] == "DoA")
        .And("second new method name is preserved", r => r.NewMethodNames[1] == "DoB")
        .AssertPassed();

    [Scenario("ExtractParameterObjectRule round-trips correctly")]
    [Fact]
    public Task ExtractParameterObjectRuleRoundTrip() =>
        Given("a schema with an ExtractParameterObjectRule", () =>
        {
            var schema = new MigrationSchema
            {
                Library = "L", From = "1", To = "2",
                Rules = [new ExtractParameterObjectRule
                {
                    Id = "r1", TypeName = "T", MethodName = "M",
                    ParameterObjectType = "ParamObj",
                    ExtractedParameters = ["a", "b"],
                }],
            };
            var json = MigrationSchemaSerializer.Serialize(schema);
            return (ExtractParameterObjectRule)MigrationSchemaSerializer.Deserialize(json)!.Rules[0];
        })
        .Then("parameterObjectType is preserved", r => r.ParameterObjectType == "ParamObj")
        .And("extractedParameters count is preserved", r => r.ExtractedParameters.Count == 2)
        .AssertPassed();

    [Scenario("PropertyToMethodRule round-trips correctly")]
    [Fact]
    public Task PropertyToMethodRuleRoundTrip() =>
        Given("a schema with a PropertyToMethodRule", () =>
        {
            var schema = new MigrationSchema
            {
                Library = "L", From = "1", To = "2",
                Rules = [new PropertyToMethodRule
                {
                    Id = "r1", TypeName = "T",
                    OldPropertyName = "IsEnabled", NewMethodName = "SetEnabled",
                }],
            };
            var json = MigrationSchemaSerializer.Serialize(schema);
            return (PropertyToMethodRule)MigrationSchemaSerializer.Deserialize(json)!.Rules[0];
        })
        .Then("oldPropertyName is preserved", r => r.OldPropertyName == "IsEnabled")
        .And("newMethodName is preserved", r => r.NewMethodName == "SetEnabled")
        .AssertPassed();

    [Scenario("MoveMemberRule round-trips correctly")]
    [Fact]
    public Task MoveMemberRuleRoundTrip() =>
        Given("a schema with a MoveMemberRule", () =>
        {
            var schema = new MigrationSchema
            {
                Library = "L", From = "1", To = "2",
                Rules = [new MoveMemberRule
                {
                    Id = "r1", OldTypeName = "OldT", NewTypeName = "NewT", MemberName = "M",
                }],
            };
            var json = MigrationSchemaSerializer.Serialize(schema);
            return (MoveMemberRule)MigrationSchemaSerializer.Deserialize(json)!.Rules[0];
        })
        .Then("oldTypeName is preserved", r => r.OldTypeName == "OldT")
        .And("newTypeName is preserved", r => r.NewTypeName == "NewT")
        .And("memberName is preserved", r => r.MemberName == "M")
        .AssertPassed();

    // ── JSON format ──────────────────────────────────────────────────

    [Scenario("Serialized JSON uses camelCase property names")]
    [Fact]
    public Task SerializedJsonUsesCamelCase() =>
        Given("a migration schema serialized to JSON", () =>
            MigrationSchemaSerializer.Serialize(new MigrationSchema
            {
                Library = "Lib", From = "1.0", To = "2.0",
                LastEdited = DateTimeOffset.UtcNow,
            }))
        .Then("JSON contains camelCase 'library'", json => json.Contains("\"library\""))
        .And("JSON contains camelCase 'lastEdited'", json => json.Contains("\"lastEdited\""))
        .And("JSON does not contain PascalCase 'Library'", json => !json.Contains("\"Library\""))
        .AssertPassed();

    [Scenario("JSON output is indented")]
    [Fact]
    public Task SerializedJsonIsIndented() =>
        Given("a migration schema serialized to JSON", () =>
            MigrationSchemaSerializer.Serialize(new MigrationSchema { Library = "L", From = "1", To = "2" }))
        .Then("JSON output contains newlines", json => json.Contains('\n'))
        .AssertPassed();

    [Scenario("Null properties are omitted from JSON")]
    [Fact]
    public Task NullPropertiesOmitted() =>
        Given("a schema with null GeneratedFrom, null LastEdited, and a rule with null Note", () =>
        {
            var schema = new MigrationSchema
            {
                Library = "L",
                From = "1",
                To = "2",
                Rules = [new RenameTypeRule { Id = "r1", OldName = "A", NewName = "B" }],
            };
            return MigrationSchemaSerializer.Serialize(schema);
        })
        .Then("JSON does not contain 'generatedFrom'", json => !json.Contains("\"generatedFrom\""))
        .And("JSON does not contain 'lastEdited'", json => !json.Contains("\"lastEdited\""))
        .And("JSON does not contain 'note'", json => !json.Contains("\"note\""))
        .AssertPassed();

    [Scenario("Comments in JSON are skipped during deserialization")]
    [Fact]
    public Task JsonWithCommentsDeserializes() =>
        Given("a JSON string with comments", () =>
        {
            var json = """
                // This is a migration schema
                {
                  "schema": "wrapgod-migration/1.0",
                  // library name
                  "library": "SomeLib",
                  "from": "1.0.0",
                  "to": "2.0.0",
                  "rules": []
                }
                """;
            return MigrationSchemaSerializer.Deserialize(json);
        })
        .Then("deserialization succeeds", schema => schema != null)
        .And("library is parsed correctly", schema => schema!.Library == "SomeLib")
        .AssertPassed();

    [Scenario("Confidence enum round-trips as camelCase string")]
    [Fact]
    public Task ConfidenceEnumCamelCase() =>
        Given("a rule with confidence=verified serialized", () =>
        {
            var schema = new MigrationSchema
            {
                Library = "L", From = "1", To = "2",
                Rules = [new RenameTypeRule
                {
                    Id = "r", Confidence = RuleConfidence.Verified,
                    OldName = "A", NewName = "B",
                }],
            };
            return MigrationSchemaSerializer.Serialize(schema);
        })
        .Then("JSON contains 'verified' (camelCase)", json => json.Contains("\"verified\""))
        .AssertPassed();

    // ── Validation ───────────────────────────────────────────────────

    [Scenario("Deserializing rule with missing 'kind' throws JsonException")]
    [Fact]
    public Task MissingKindThrows() =>
        Given("a JSON rule object without 'kind'", () =>
        {
            var json = """
                {
                  "schema": "wrapgod-migration/1.0",
                  "library": "L",
                  "from": "1.0",
                  "to": "2.0",
                  "rules": [{ "id": "r1" }]
                }
                """;
            return json;
        })
        .Then("deserialization throws JsonException", json =>
        {
            try
            {
                MigrationSchemaSerializer.Deserialize(json);
                return false;
            }
            catch (JsonException)
            {
                return true;
            }
        })
        .AssertPassed();

    [Scenario("Deserializing rule with unknown 'kind' throws JsonException")]
    [Fact]
    public Task UnknownKindThrows() =>
        Given("a JSON rule object with an unknown kind value", () =>
        {
            var json = """
                {
                  "schema": "wrapgod-migration/1.0",
                  "library": "L",
                  "from": "1.0",
                  "to": "2.0",
                  "rules": [{ "id": "r1", "kind": "unknownMagicKind" }]
                }
                """;
            return json;
        })
        .Then("deserialization throws JsonException", json =>
        {
            try
            {
                MigrationSchemaSerializer.Deserialize(json);
                return false;
            }
            catch (JsonException)
            {
                return true;
            }
        })
        .AssertPassed();

    [Scenario("Deserializing rule with numeric 'kind' throws JsonException")]
    [Fact]
    public Task NumericKindThrows() =>
        Given("a JSON rule object with a numeric kind value", () =>
        {
            var json = """
                {
                  "schema": "wrapgod-migration/1.0",
                  "library": "L",
                  "from": "1.0",
                  "to": "2.0",
                  "rules": [{ "id": "r1", "kind": "0" }]
                }
                """;
            return json;
        })
        .Then("deserialization throws JsonException", json =>
        {
            try
            {
                MigrationSchemaSerializer.Deserialize(json);
                return false;
            }
            catch (JsonException)
            {
                return true;
            }
        })
        .AssertPassed();

    [Scenario("Deserializing null JSON returns null")]
    [Fact]
    public Task DeserializeNullJson() =>
        Given("the JSON null literal", () => "null")
        .Then("deserialization returns null", json =>
            MigrationSchemaSerializer.Deserialize(json) is null)
        .AssertPassed();

    [Scenario("Empty rules list round-trips correctly")]
    [Fact]
    public Task EmptyRulesListRoundTrips() =>
        Given("a schema with no rules", () =>
        {
            var schema = new MigrationSchema { Library = "L", From = "1.0", To = "2.0" };
            var json = MigrationSchemaSerializer.Serialize(schema);
            return MigrationSchemaSerializer.Deserialize(json);
        })
        .Then("rules list is empty", schema => schema!.Rules.Count == 0)
        .AssertPassed();

    // ── Enums ────────────────────────────────────────────────────────

    [Scenario("All MigrationRuleKind values are covered by model types")]
    [Fact]
    public Task AllRuleKindsCovered() =>
        Given("each MigrationRuleKind value has a corresponding concrete rule in a schema", () =>
            BuildFullSchema())
        .Then("has renameType rule", s => s.Rules.Exists(r => r.Kind == MigrationRuleKind.RenameType))
        .And("has renameMember rule", s => s.Rules.Exists(r => r.Kind == MigrationRuleKind.RenameMember))
        .And("has renameNamespace rule", s => s.Rules.Exists(r => r.Kind == MigrationRuleKind.RenameNamespace))
        .And("has changeParameter rule", s => s.Rules.Exists(r => r.Kind == MigrationRuleKind.ChangeParameter))
        .And("has removeMember rule", s => s.Rules.Exists(r => r.Kind == MigrationRuleKind.RemoveMember))
        .And("has addRequiredParameter rule", s => s.Rules.Exists(r => r.Kind == MigrationRuleKind.AddRequiredParameter))
        .And("has changeTypeReference rule", s => s.Rules.Exists(r => r.Kind == MigrationRuleKind.ChangeTypeReference))
        .And("has splitMethod rule", s => s.Rules.Exists(r => r.Kind == MigrationRuleKind.SplitMethod))
        .And("has extractParameterObject rule", s => s.Rules.Exists(r => r.Kind == MigrationRuleKind.ExtractParameterObject))
        .And("has propertyToMethod rule", s => s.Rules.Exists(r => r.Kind == MigrationRuleKind.PropertyToMethod))
        .And("has moveMember rule", s => s.Rules.Exists(r => r.Kind == MigrationRuleKind.MoveMember))
        .AssertPassed();

    [Scenario("RuleConfidence enum values are defined")]
    [Fact]
    public Task RuleConfidenceEnumValues() =>
        Given("the RuleConfidence enum", () => true)
        .Then("Auto is defined", _ => Enum.IsDefined(RuleConfidence.Auto))
        .And("Verified is defined", _ => Enum.IsDefined(RuleConfidence.Verified))
        .And("Manual is defined", _ => Enum.IsDefined(RuleConfidence.Manual))
        .AssertPassed();

    [Scenario("GetOptions returns shared serializer options")]
    [Fact]
    public Task GetOptionsReturnsOptions() =>
        Given("MigrationSchemaSerializer options", () => MigrationSchemaSerializer.GetOptions())
        .Then("options are not null", opts => opts != null)
        .And("options use camelCase", opts => opts.PropertyNamingPolicy == JsonNamingPolicy.CamelCase)
        .And("options write indented", opts => opts.WriteIndented)
        .AssertPassed();
}
