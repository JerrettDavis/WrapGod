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
        return config ?? new WrapGodConfig();
    }
}
