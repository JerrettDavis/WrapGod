using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WrapGod.Migration;

/// <summary>
/// JSON serialization helpers for <see cref="MigrationSchema"/>.
/// Uses camelCase naming, enum-as-string, indented output, and polymorphic
/// deserialization with <c>kind</c> as the discriminator.
/// </summary>
public static class MigrationSchemaSerializer
{
    private static readonly JsonSerializerOptions Options = CreateOptions();

    /// <summary>Serializes a <see cref="MigrationSchema"/> to a JSON string.</summary>
    public static string Serialize(MigrationSchema schema)
    {
        return JsonSerializer.Serialize(schema, Options);
    }

    /// <summary>
    /// Deserializes a <see cref="MigrationSchema"/> from a JSON string.
    /// Returns <c>null</c> if the input is the JSON <c>null</c> literal.
    /// </summary>
    public static MigrationSchema? Deserialize(string json)
    {
        return JsonSerializer.Deserialize<MigrationSchema>(json, Options);
    }

    /// <summary>Returns a copy of the serializer options used by this serializer.</summary>
    public static JsonSerializerOptions GetOptions() => new JsonSerializerOptions(Options);

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            ReadCommentHandling = JsonCommentHandling.Skip,
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        options.Converters.Add(new MigrationRuleConverter());
        return options;
    }
}

/// <summary>
/// Custom <see cref="JsonConverter{T}"/> that performs polymorphic (de)serialization of
/// <see cref="MigrationRule"/> subclasses using the <c>kind</c> property as discriminator.
/// </summary>
public sealed class MigrationRuleConverter : JsonConverter<MigrationRule>
{
    /// <summary>
    /// Maps each <see cref="MigrationRuleKind"/> value to its concrete <see cref="MigrationRule"/> type.
    /// </summary>
    private static readonly Dictionary<MigrationRuleKind, Type> KindToType = new()
    {
        [MigrationRuleKind.RenameType]             = typeof(RenameTypeRule),
        [MigrationRuleKind.RenameMember]           = typeof(RenameMemberRule),
        [MigrationRuleKind.RenameNamespace]        = typeof(RenameNamespaceRule),
        [MigrationRuleKind.ChangeParameter]        = typeof(ChangeParameterRule),
        [MigrationRuleKind.RemoveMember]           = typeof(RemoveMemberRule),
        [MigrationRuleKind.AddRequiredParameter]   = typeof(AddRequiredParameterRule),
        [MigrationRuleKind.ChangeTypeReference]    = typeof(ChangeTypeReferenceRule),
        [MigrationRuleKind.SplitMethod]            = typeof(SplitMethodRule),
        [MigrationRuleKind.ExtractParameterObject] = typeof(ExtractParameterObjectRule),
        [MigrationRuleKind.PropertyToMethod]       = typeof(PropertyToMethodRule),
        [MigrationRuleKind.MoveMember]             = typeof(MoveMemberRule),
    };

    static MigrationRuleConverter()
    {
        ValidateKindMappings();
    }

    private static void ValidateKindMappings()
    {
        var allKinds = new System.Collections.Generic.HashSet<MigrationRuleKind>(
            (MigrationRuleKind[])Enum.GetValues(typeof(MigrationRuleKind)));
        var typeMappedKinds = new System.Collections.Generic.HashSet<MigrationRuleKind>(KindToType.Keys);
        var stringMappedKinds = new System.Collections.Generic.HashSet<MigrationRuleKind>(KindStringMap.Values);

        if (!typeMappedKinds.SetEquals(stringMappedKinds) || !typeMappedKinds.SetEquals(allKinds))
        {
            throw new InvalidOperationException(
                "MigrationRuleConverter kind mappings are out of sync. " +
                "Ensure KindToType and KindStringMap cover exactly the same MigrationRuleKind values.");
        }
    }

    /// <inheritdoc/>
    public override MigrationRule Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;

        if (!root.TryGetProperty("kind", out var kindElement))
            throw new JsonException("Migration rule is missing required 'kind' property.");

        if (kindElement.ValueKind != JsonValueKind.String)
            throw new JsonException("Migration rule 'kind' property must be a string.");

        var kindString = kindElement.GetString()
            ?? throw new JsonException("Migration rule 'kind' property must be a non-null string.");

        // Perform a case-insensitive camelCase → enum parse.
        if (!TryParseKind(kindString, out var kind))
            throw new JsonException($"Unknown migration rule kind: '{kindString}'.");

        if (!KindToType.TryGetValue(kind, out var concreteType))
            throw new JsonException($"No concrete type registered for kind '{kind}'.");

        return (MigrationRule?)JsonSerializer.Deserialize(root, concreteType, options)
            ?? throw new JsonException($"Failed to deserialize migration rule of kind '{kind}'.");
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, MigrationRule value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value, value.GetType(), options);
    }

    /// <summary>
    /// Maps each camelCase kind string to its <see cref="MigrationRuleKind"/> value.
    /// Only known, named kind strings are accepted; numeric strings are rejected.
    /// </summary>
    private static readonly Dictionary<string, MigrationRuleKind> KindStringMap =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["renameType"]             = MigrationRuleKind.RenameType,
            ["renameMember"]           = MigrationRuleKind.RenameMember,
            ["renameNamespace"]        = MigrationRuleKind.RenameNamespace,
            ["changeParameter"]        = MigrationRuleKind.ChangeParameter,
            ["removeMember"]           = MigrationRuleKind.RemoveMember,
            ["addRequiredParameter"]   = MigrationRuleKind.AddRequiredParameter,
            ["changeTypeReference"]    = MigrationRuleKind.ChangeTypeReference,
            ["splitMethod"]            = MigrationRuleKind.SplitMethod,
            ["extractParameterObject"] = MigrationRuleKind.ExtractParameterObject,
            ["propertyToMethod"]       = MigrationRuleKind.PropertyToMethod,
            ["moveMember"]             = MigrationRuleKind.MoveMember,
        };

    /// <summary>
    /// Tries to parse a camelCase kind string to a <see cref="MigrationRuleKind"/> value.
    /// Only named kind strings are accepted; numeric strings are rejected.
    /// </summary>
    private static bool TryParseKind(string value, out MigrationRuleKind kind) =>
        KindStringMap.TryGetValue(value, out kind);
}
