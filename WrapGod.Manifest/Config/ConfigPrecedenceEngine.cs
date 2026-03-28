using System;
using System.Collections.Generic;
using System.Linq;
using WrapGod.Abstractions.Config;

namespace WrapGod.Manifest.Config;

/// <summary>
/// Resolves a deterministic multi-layer configuration precedence chain.
/// Default precedence: defaults -&gt; root json -&gt; project json -&gt; attributes -&gt; fluent.
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

            // Old engine merges json + attributes where attributes can be set higher.
            // Treat current as lower and next as higher to apply the configured chain.
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

        var unique = new List<ConfigDiagnostic>(diagnostics.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var d in diagnostics)
        {
            var key = $"{d.Code}|{d.Message}|{d.Target}";
            if (seen.Add(key))
            {
                unique.Add(d);
            }
        }

        return new ConfigMergeResult
        {
            Config = merged,
            Diagnostics = unique
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
