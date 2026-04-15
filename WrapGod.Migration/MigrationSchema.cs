using System;
using System.Collections.Generic;

namespace WrapGod.Migration;

/// <summary>
/// Root document describing a migration from one library version to another.
/// </summary>
public sealed class MigrationSchema
{
    /// <summary>Schema version identifier, e.g. <c>wrapgod-migration/1.0</c>.</summary>
    public string Schema { get; set; } = "wrapgod-migration/1.0";

    /// <summary>The NuGet package or library name, e.g. <c>MudBlazor</c>.</summary>
    public string Library { get; set; } = string.Empty;

    /// <summary>The source (old) version, e.g. <c>6.0.0</c>.</summary>
    public string From { get; set; } = string.Empty;

    /// <summary>The target (new) version, e.g. <c>7.0.0</c>.</summary>
    public string To { get; set; } = string.Empty;

    /// <summary>
    /// How this schema was produced, e.g. <c>manifest-diff</c> or <c>manual</c>.
    /// </summary>
    public string? GeneratedFrom { get; set; }

    /// <summary>Timestamp of the last manual edit.</summary>
    public DateTimeOffset? LastEdited { get; set; }

    /// <summary>The migration rules contained in this schema.</summary>
    public List<MigrationRule> Rules { get; set; } = [];
}
