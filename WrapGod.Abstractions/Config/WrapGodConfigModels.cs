using System;
using System.Collections.Generic;

namespace WrapGod.Abstractions.Config;

public sealed class WrapGodConfig
{
    public List<TypeConfig> Types { get; set; } = new();
}

public sealed class TypeConfig
{
    public string SourceType { get; set; } = string.Empty;
    public bool? Include { get; set; }
    public string? TargetName { get; set; }
    public List<MemberConfig> Members { get; set; } = new();

    /// <summary>
    /// When <c>true</c>, <see cref="SourceType"/> is treated as an open generic
    /// pattern (e.g. <c>"Dictionary&lt;,&gt;"</c> or <c>"IRepository&lt;&gt;"</c>).
    /// The pattern matches any closed construction of the generic type.
    /// </summary>
    public bool IsGenericPattern { get; set; }

    /// <summary>
    /// The arity (number of type parameters) of the generic pattern.
    /// Derived automatically from the source type pattern when
    /// <see cref="IsGenericPattern"/> is <c>true</c>.
    /// </summary>
    public int GenericArity { get; set; }
}

public sealed class MemberConfig
{
    public string SourceMember { get; set; } = string.Empty;
    public bool? Include { get; set; }
    public string? TargetName { get; set; }
}

public enum ConfigSource
{
    Defaults,
    RootJson,
    ProjectJson,
    Json,
    Attributes,
    Fluent,
}

public sealed class ConfigMergeResult
{
    public WrapGodConfig Config { get; set; } = new();
    public List<ConfigDiagnostic> Diagnostics { get; set; } = new();
}

public sealed class ConfigDiagnostic
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Target { get; set; }
}

public sealed class ConfigMergeOptions
{
    public ConfigSource HigherPrecedence { get; set; } = ConfigSource.Attributes;
}

public sealed class ConfigPrecedenceOptions
{
    public IReadOnlyList<ConfigSource> SourceOrder { get; set; } = new List<ConfigSource>
    {
        ConfigSource.Defaults,
        ConfigSource.RootJson,
        ConfigSource.ProjectJson,
        ConfigSource.Attributes,
        ConfigSource.Fluent,
    };
}

public sealed class ConfigSourceLayers
{
    public WrapGodConfig? Defaults { get; set; }
    public WrapGodConfig? RootJson { get; set; }
    public WrapGodConfig? ProjectJson { get; set; }
    public WrapGodConfig? Attributes { get; set; }
    public WrapGodConfig? Fluent { get; set; }
}

public sealed class SourceDiscoveryInput
{
    public string? WrapGodPackage { get; set; }
    public IReadOnlyList<string> PackageReferences { get; set; } = Array.Empty<string>();
    public bool HasSelfSource { get; set; }
    public string? ExplicitSource { get; set; }
}

public sealed class SourceDiscoveryResult
{
    public string? Source { get; set; }
    public string Strategy { get; set; } = string.Empty;
    public List<ConfigDiagnostic> Diagnostics { get; set; } = new();
}
