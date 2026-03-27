using System;
using System.IO;
using System.Text.Json;
using WrapGod.Abstractions.Config;

namespace WrapGod.Manifest.Config;

public static class JsonConfigLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    public static WrapGodConfig LoadFromFile(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Config file not found.", path);
        }

        var json = File.ReadAllText(path);
        return LoadFromJson(json);
    }

    public static WrapGodConfig LoadFromJson(string json)
    {
        var config = JsonSerializer.Deserialize<WrapGodConfig>(json, Options);
        if (config is null)
            return new WrapGodConfig();

        // Post-process: detect open generic patterns in sourceType values.
        foreach (var type in config.Types)
        {
            InferGenericPattern(type);
        }

        return config;
    }

    /// <summary>
    /// Detects open generic patterns like <c>"Dictionary&lt;,&gt;"</c> or
    /// <c>"IRepository&lt;&gt;"</c> and sets <see cref="TypeConfig.IsGenericPattern"/>
    /// and <see cref="TypeConfig.GenericArity"/> accordingly.
    /// </summary>
    internal static void InferGenericPattern(TypeConfig type)
    {
        var source = type.SourceType;
        var openAngle = source.IndexOf('<');
        if (openAngle < 0)
            return;

        var closeAngle = source.LastIndexOf('>');
        if (closeAngle <= openAngle)
            return;

        // Extract inner content between < and >.
        var inner = source.Substring(openAngle + 1, closeAngle - openAngle - 1).Trim();

        // Open generic pattern: inner is empty or contains only commas/whitespace.
        bool isOpenGeneric = inner.Length == 0 ||
                             inner.Replace(",", "").Replace(" ", "").Length == 0;

        if (isOpenGeneric)
        {
            type.IsGenericPattern = true;
            type.GenericArity = inner.Length == 0 ? 1 : inner.Split(',').Length;
        }
    }
}
