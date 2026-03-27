using System.Collections.Generic;

namespace WrapGod.TypeMap;

/// <summary>
/// Core mapping model that describes how a source type maps to a destination type,
/// including the mapping kind, per-member mappings, and an optional top-level converter.
/// </summary>
public sealed class TypeMapping
{
    /// <summary>Fully-qualified source type name.</summary>
    public string SourceType { get; set; } = string.Empty;

    /// <summary>Fully-qualified destination type name.</summary>
    public string DestinationType { get; set; } = string.Empty;

    /// <summary>The kind of mapping (ObjectMapping, Enum, Collection, Nullable, Custom).</summary>
    public TypeMappingKind Kind { get; set; }

    /// <summary>Per-member mappings for Object-kind type mappings.</summary>
    public IReadOnlyList<MemberMapping> MemberMappings { get; set; } = new List<MemberMapping>();

    /// <summary>
    /// Optional top-level converter reference. When set, the entire type
    /// conversion is delegated to this converter instead of member-by-member mapping.
    /// </summary>
    public ConverterRef? Converter { get; set; }
}
