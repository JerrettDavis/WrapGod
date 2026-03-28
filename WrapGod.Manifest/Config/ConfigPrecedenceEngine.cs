using System;
using System.Collections.Generic;
using WrapGod.Abstractions.Config;

namespace WrapGod.Manifest.Config;

/// <summary>
/// Resolves a deterministic multi-layer configuration precedence chain.
/// Default precedence: defaults -> root json -> project json -> attributes -> fluent.
/// </summary>
public static class ConfigPrecedenceEngine
{
    public static ConfigMergeResult Merge(
        ConfigSourceLayers layers,
        ConfigPrecedenceOptions? options = null)
    {
        options ??= new ConfigPrecedenceOptions();

        var merged = new WrapGodConfig();
        var diagnostics = new List<ConfigDiagnostic>();
        var sawAnySource = false;

        foreach (var source in options.SourceOrder)
        {
            var next = GetLayer(layers, source);
            if (next is null || next.Types.Count == 0)
                continue;

            sawAnySource = true;

            var step = ConfigMergeEngine.Merge(
                merged,
                next,
                new ConfigMergeOptions { HigherPrecedence = ConfigSource.Attributes });

            merged = step.Config;
            diagnostics.AddRange(step.Diagnostics);
        }

        if (!sawAnySource)
        {
            diagnostics.Add(new ConfigDiagnostic
            {
                Code = "WG6010",
                Message = "No configuration source was discovered. Add a root/project *.wrapgod.config.json file, annotate wrappers with [WrapType], or provide fluent configuration.",
                Target = "config.sources"
            });
        }

        return new ConfigMergeResult
        {
            Config = merged,
            Diagnostics = diagnostics
        };
    }

    private static WrapGodConfig? GetLayer(ConfigSourceLayers layers, ConfigSource source)
    {
        return source switch
        {
            ConfigSource.Defaults => layers.Defaults,
            ConfigSource.RootJson => layers.RootJson,
            ConfigSource.ProjectJson or ConfigSource.Json => layers.ProjectJson,
            ConfigSource.Attributes => layers.Attributes,
            ConfigSource.Fluent => layers.Fluent,
            _ => null,
        };
    }
}
