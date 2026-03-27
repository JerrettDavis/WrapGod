namespace WrapGod.TypeMap;

public sealed class TypeMappingPlan
{
    public List<TypeMapDefinition> Mappings { get; set; } = new();
}

public sealed class TypeMapDefinition
{
    public string SourceType { get; set; } = string.Empty;
    public string DestinationType { get; set; } = string.Empty;
    public bool Bidirectional { get; set; }
    public List<MemberMapDefinition> Members { get; set; } = new();
}

public sealed class MemberMapDefinition
{
    public string SourceMember { get; set; } = string.Empty;
    public string DestinationMember { get; set; } = string.Empty;
    public string? Converter { get; set; }
}
