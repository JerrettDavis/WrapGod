using System.Collections.Generic;

namespace WrapGod.Fluent;

/// <summary>
/// Nested builder for configuring how a single type should be wrapped.
/// Returned by <see cref="WrapGodConfiguration.WrapType"/> and supports
/// chaining back into the parent configuration.
/// </summary>
public sealed class TypeDirectiveBuilder
{
    private readonly WrapGodConfiguration _parent;
    private readonly string _sourceType;
    private string? _targetName;
    private bool _wrapAll;
    private readonly List<MemberDirectiveBuilder> _memberBuilders = [];
    private readonly List<string> _excludedMembers = [];
    private readonly bool _isGenericPattern;
    private readonly int _genericArity;

    internal TypeDirectiveBuilder(WrapGodConfiguration parent, string sourceType)
    {
        _parent = parent;
        _sourceType = sourceType;

        // Auto-detect open generic patterns like "IRepository<>" or "Dictionary<,>".
        var openAngle = sourceType.IndexOf('<');
        if (openAngle >= 0)
        {
            var closeAngle = sourceType.LastIndexOf('>');
            if (closeAngle > openAngle)
            {
                var inner = sourceType.Substring(openAngle + 1, closeAngle - openAngle - 1).Trim();
                bool isOpen = inner.Length == 0 ||
                              inner.Replace(",", "").Replace(" ", "").Length == 0;
                if (isOpen)
                {
                    _isGenericPattern = true;
                    _genericArity = inner.Length == 0 ? 1 : inner.Split(',').Length;
                }
            }
        }
    }

    /// <summary>Set the target interface / wrapper name for this type.</summary>
    public TypeDirectiveBuilder As(string targetName)
    {
        _targetName = targetName;
        return this;
    }

    /// <summary>Add a method-wrapping directive. Returns a member builder for further config.</summary>
    public MemberDirectiveBuilder WrapMethod(string methodName)
    {
        var builder = new MemberDirectiveBuilder(this, methodName, MemberDirectiveKind.Method);
        _memberBuilders.Add(builder);
        return builder;
    }

    /// <summary>Add a property-wrapping directive (no rename support, returns this).</summary>
    public TypeDirectiveBuilder WrapProperty(string propertyName)
    {
        _memberBuilders.Add(
            new MemberDirectiveBuilder(this, propertyName, MemberDirectiveKind.Property));
        return this;
    }

    /// <summary>Exclude a member by name from generation.</summary>
    public TypeDirectiveBuilder ExcludeMember(string memberName)
    {
        _excludedMembers.Add(memberName);
        return this;
    }

    /// <summary>Wrap all public members of this type.</summary>
    public TypeDirectiveBuilder WrapAllPublicMembers()
    {
        _wrapAll = true;
        return this;
    }

    // --- Passthrough methods so the chain can continue at the parent level ---

    /// <summary>Start configuring another type (delegates to parent).</summary>
    public TypeDirectiveBuilder WrapType(string sourceType) => _parent.WrapType(sourceType);

    /// <summary>Add a type mapping (delegates to parent).</summary>
    public WrapGodConfiguration MapType(string sourceType, string destinationType) =>
        _parent.MapType(sourceType, destinationType);

    /// <summary>Add a type exclusion pattern (delegates to parent).</summary>
    public WrapGodConfiguration ExcludeType(string pattern) =>
        _parent.ExcludeType(pattern);

    /// <summary>Build the final generation plan (delegates to parent).</summary>
    public GenerationPlan Build() => _parent.Build();

    internal TypeDirective ToDirective()
    {
        var members = new List<MemberDirective>(_memberBuilders.Count);
        foreach (var mb in _memberBuilders)
        {
            members.Add(mb.ToDirective());
        }

        return new TypeDirective
        {
            SourceType = _sourceType,
            TargetName = _targetName,
            WrapAllPublicMembers = _wrapAll,
            MemberDirectives = members,
            ExcludedMembers = _excludedMembers,
            IsGenericPattern = _isGenericPattern,
            GenericArity = _genericArity,
        };
    }
}

/// <summary>
/// Builder for a single member directive, supporting rename via <c>.As()</c>.
/// </summary>
public sealed class MemberDirectiveBuilder
{
    private readonly TypeDirectiveBuilder _parent;
    private readonly string _sourceName;
    private readonly MemberDirectiveKind _kind;
    private string? _targetName;

    internal MemberDirectiveBuilder(
        TypeDirectiveBuilder parent, string sourceName, MemberDirectiveKind kind)
    {
        _parent = parent;
        _sourceName = sourceName;
        _kind = kind;
    }

    /// <summary>Rename this member on the generated wrapper.</summary>
    public TypeDirectiveBuilder As(string targetName)
    {
        _targetName = targetName;
        return _parent;
    }

    internal MemberDirective ToDirective() => new()
    {
        SourceName = _sourceName,
        TargetName = _targetName,
        Kind = _kind,
    };
}
