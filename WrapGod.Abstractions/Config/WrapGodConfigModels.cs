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
}

public sealed class MemberConfig
{
    public string SourceMember { get; set; } = string.Empty;
    public bool? Include { get; set; }
    public string? TargetName { get; set; }
}

public enum ConfigSource
{
    Json,
    Attributes,
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
