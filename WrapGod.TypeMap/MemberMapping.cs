namespace WrapGod.TypeMap;

/// <summary>
/// Describes how a single member on the source type maps to a member
/// on the destination type, with an optional converter for non-trivial transforms.
/// </summary>
public sealed class MemberMapping
{
    /// <summary>Name of the member on the source type.</summary>
    public string SourceMember { get; set; } = string.Empty;

    /// <summary>Name of the member on the destination type.</summary>
    public string DestinationMember { get; set; } = string.Empty;

    /// <summary>
    /// Optional converter reference for members that require custom
    /// transformation logic (e.g. different types, formatting).
    /// </summary>
    public ConverterRef? Converter { get; set; }
}
