using System.Text.Json;
using System.Text.Json.Serialization;

namespace WrapGod.Migration.Engine.State;

/// <summary>
/// JSON serialization helpers for <see cref="MigrationState"/>.
/// Uses camelCase naming, enum-as-string, indented output, and null-ignoring
/// conventions consistent with <c>WrapGod.Migration.MigrationSchemaSerializer</c>.
/// </summary>
public static class MigrationStateSerializer
{
    private static readonly JsonSerializerOptions Options = CreateOptions();

    /// <summary>Serializes a <see cref="MigrationState"/> to an indented JSON string.</summary>
    public static string Serialize(MigrationState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return JsonSerializer.Serialize(state, Options);
    }

    /// <summary>
    /// Deserializes a <see cref="MigrationState"/> from a JSON string.
    /// Returns <see langword="null"/> if the input is the JSON <c>null</c> literal,
    /// an empty string, or contains invalid JSON.
    /// </summary>
    public static MigrationState? Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<MigrationState>(json, Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            ReadCommentHandling = JsonCommentHandling.Skip,
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        return options;
    }
}
