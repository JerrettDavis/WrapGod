namespace WrapGod.TypeMap;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true, Inherited = false)]
public sealed class MapTypeAttribute : Attribute
{
    public MapTypeAttribute(string sourceType, string destinationType)
    {
        SourceType = sourceType;
        DestinationType = destinationType;
    }

    public string SourceType { get; }
    public string DestinationType { get; }
    public bool Bidirectional { get; set; }
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
public sealed class MapMemberAttribute : Attribute
{
    public MapMemberAttribute(string sourceMember, string destinationMember)
    {
        SourceMember = sourceMember;
        DestinationMember = destinationMember;
    }

    public string SourceMember { get; }
    public string DestinationMember { get; }
    public string? Converter { get; set; }
}
