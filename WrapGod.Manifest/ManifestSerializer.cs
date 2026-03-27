using System.Text.Json;
using System.Text.Json.Serialization;

namespace WrapGod.Manifest;

/// <summary>
/// JSON serialization helpers for <see cref="ApiManifest"/>.
/// Uses camelCase naming, enum-as-string, and deterministic formatting.
/// </summary>
public static class ManifestSerializer
{
    private static readonly JsonSerializerOptions Options = CreateOptions();

    /// <summary>Serializes a manifest to a JSON string.</summary>
    public static string Serialize(ApiManifest manifest)
    {
        return JsonSerializer.Serialize(manifest, Options);
    }

    /// <summary>Deserializes a manifest from a JSON string.</summary>
    public static ApiManifest? Deserialize(string json)
    {
        return JsonSerializer.Deserialize<ApiManifest>(json, Options);
    }

    /// <summary>Returns the shared serializer options.</summary>
    public static JsonSerializerOptions GetOptions() => Options;

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }
}
