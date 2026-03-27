using System;

namespace WrapGod.Abstractions.Config;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class WrapTypeAttribute : Attribute
{
    public WrapTypeAttribute(string sourceType)
    {
        SourceType = sourceType;
    }

    public string SourceType { get; }

    public bool Include { get; set; } = true;

    public string? TargetName { get; set; }
}

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class WrapMemberAttribute : Attribute
{
    public WrapMemberAttribute(string sourceMember)
    {
        SourceMember = sourceMember;
    }

    public string SourceMember { get; }

    public bool Include { get; set; } = true;

    public string? TargetName { get; set; }
}
