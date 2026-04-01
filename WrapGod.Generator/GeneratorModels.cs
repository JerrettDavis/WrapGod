using System;
using System.Collections.Generic;

namespace WrapGod.Generator;

/// <summary>
/// Lightweight generation plan parsed from a <c>*.wrapgod.json</c> manifest file.
/// These models are designed to be equatable for incremental caching.
/// </summary>
internal sealed class GenerationPlan : IEquatable<GenerationPlan>
{
    public string AssemblyName { get; }
    public IReadOnlyList<TypePlan> Types { get; }

    public GenerationPlan(string assemblyName, IReadOnlyList<TypePlan> types)
    {
        AssemblyName = assemblyName;
        Types = types;
    }

    public bool Equals(GenerationPlan? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        if (AssemblyName != other.AssemblyName) return false;
        if (Types.Count != other.Types.Count) return false;

        for (int i = 0; i < Types.Count; i++)
        {
            if (!Types[i].Equals(other.Types[i])) return false;
        }

        return true;
    }

    public override bool Equals(object? obj) => Equals(obj as GenerationPlan);

    public override int GetHashCode()
    {
        int hash = AssemblyName.GetHashCode();
        foreach (var t in Types)
        {
            hash = (hash * 397) ^ t.GetHashCode();
        }

        return hash;
    }
}

/// <summary>
/// Describes a single type to generate an interface + facade pair for.
/// </summary>
internal sealed class TypePlan : IEquatable<TypePlan>
{
    public string FullName { get; }
    public string Name { get; }
    public string Namespace { get; }
    public IReadOnlyList<MemberPlan> Members { get; }

    /// <summary>
    /// Optional override name from user configuration.
    /// When set, the generated interface/facade use this name instead of the
    /// default derived from <see cref="Name"/>.
    /// </summary>
    public string? TargetName { get; }

    /// <summary>
    /// When <c>true</c>, the source type is a static class.
    /// Static types emit a static facade class instead of interface + instance facade.
    /// </summary>
    public bool IsStatic { get; }

    /// <summary>
    /// Version in which this type first appeared, or <c>null</c> when
    /// version metadata is not available.
    /// </summary>
    public string? IntroducedIn { get; }

    /// <summary>
    /// Version in which this type was removed, or <c>null</c> if it is
    /// still present in the latest version.
    /// </summary>
    public string? RemovedIn { get; }

    /// <summary>
    /// Type-level generic parameters (e.g. <c>["T"]</c> for <c>Repository&lt;T&gt;</c>,
    /// <c>["TKey", "TValue"]</c> for <c>Dictionary&lt;TKey, TValue&gt;</c>).
    /// </summary>
    public IReadOnlyList<GenericTypeParameterPlan> GenericTypeParameters { get; }

    /// <summary>
    /// Returns <see cref="TargetName"/> when set, otherwise <see cref="Name"/>.
    /// </summary>
    public string EffectiveName => TargetName ?? Name;

    public TypePlan(
        string fullName,
        string name,
        string ns,
        IReadOnlyList<MemberPlan> members,
        string? targetName = null,
        bool isStatic = false,
        string? introducedIn = null,
        string? removedIn = null,
        IReadOnlyList<GenericTypeParameterPlan>? genericTypeParameters = null)
    {
        FullName = fullName;
        Name = name;
        Namespace = ns;
        Members = members;
        TargetName = targetName;
        IsStatic = isStatic;
        IntroducedIn = introducedIn;
        RemovedIn = removedIn;
        GenericTypeParameters = genericTypeParameters ?? Array.Empty<GenericTypeParameterPlan>();
    }

    public bool Equals(TypePlan? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        if (FullName != other.FullName || Name != other.Name || Namespace != other.Namespace) return false;
        if (TargetName != other.TargetName || IsStatic != other.IsStatic) return false;
        if (IntroducedIn != other.IntroducedIn || RemovedIn != other.RemovedIn) return false;
        if (Members.Count != other.Members.Count) return false;
        if (GenericTypeParameters.Count != other.GenericTypeParameters.Count) return false;

        for (int i = 0; i < Members.Count; i++)
        {
            if (!Members[i].Equals(other.Members[i])) return false;
        }

        for (int i = 0; i < GenericTypeParameters.Count; i++)
        {
            if (!GenericTypeParameters[i].Equals(other.GenericTypeParameters[i])) return false;
        }

        return true;
    }

    public override bool Equals(object? obj) => Equals(obj as TypePlan);

    public override int GetHashCode()
    {
        int hash = FullName.GetHashCode();
        hash = (hash * 397) ^ Name.GetHashCode();
        hash = (hash * 397) ^ Namespace.GetHashCode();
        if (TargetName != null) hash = (hash * 397) ^ TargetName.GetHashCode();
        hash = (hash * 397) ^ IsStatic.GetHashCode();
        if (IntroducedIn != null) hash = (hash * 397) ^ IntroducedIn.GetHashCode();
        if (RemovedIn != null) hash = (hash * 397) ^ RemovedIn.GetHashCode();
        foreach (var m in Members)
        {
            hash = (hash * 397) ^ m.GetHashCode();
        }

        foreach (var gp in GenericTypeParameters)
        {
            hash = (hash * 397) ^ gp.GetHashCode();
        }

        return hash;
    }
}

/// <summary>
/// Describes a generic type parameter with optional constraints.
/// </summary>
internal sealed class GenericTypeParameterPlan : IEquatable<GenericTypeParameterPlan>
{
    /// <summary>The parameter name (e.g. <c>T</c>, <c>TKey</c>).</summary>
    public string Name { get; }

    /// <summary>
    /// Constraint clauses (e.g. <c>["class"]</c>, <c>["struct"]</c>,
    /// <c>["IComparable&lt;T&gt;"]</c>).
    /// </summary>
    public IReadOnlyList<string> Constraints { get; }

    public GenericTypeParameterPlan(string name, IReadOnlyList<string>? constraints = null)
    {
        Name = name;
        Constraints = constraints ?? Array.Empty<string>();
    }

    public bool Equals(GenericTypeParameterPlan? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        if (Name != other.Name) return false;
        if (Constraints.Count != other.Constraints.Count) return false;
        for (int i = 0; i < Constraints.Count; i++)
        {
            if (Constraints[i] != other.Constraints[i]) return false;
        }

        return true;
    }

    public override bool Equals(object? obj) => Equals(obj as GenericTypeParameterPlan);

    public override int GetHashCode()
    {
        int hash = Name.GetHashCode();
        foreach (var c in Constraints)
        {
            hash = (hash * 397) ^ c.GetHashCode();
        }

        return hash;
    }
}

/// <summary>
/// Describes a single member (method or property) to generate in the interface/facade.
/// </summary>
internal sealed class MemberPlan : IEquatable<MemberPlan>
{
    public string Name { get; }
    public string Kind { get; }
    public string ReturnType { get; }
    public IReadOnlyList<ParameterPlan> Parameters { get; }
    public bool HasGetter { get; }
    public bool HasSetter { get; }
    public bool IsStatic { get; }
    public IReadOnlyList<GenericTypeParameterPlan> GenericParameters { get; }

    /// <summary>
    /// Optional override name from user configuration.
    /// When set, the generated member uses this name instead of <see cref="Name"/>.
    /// </summary>
    public string? TargetName { get; }

    /// <summary>
    /// Version in which this member first appeared, or <c>null</c> when
    /// version metadata is not available.
    /// </summary>
    public string? IntroducedIn { get; }

    /// <summary>
    /// Version in which this member was removed, or <c>null</c> if it is
    /// still present in the latest version.
    /// </summary>
    public string? RemovedIn { get; }

    /// <summary>
    /// Returns <see cref="TargetName"/> when set, otherwise <see cref="Name"/>.
    /// </summary>
    public string EffectiveName => TargetName ?? Name;

    public MemberPlan(
        string name,
        string kind,
        string returnType,
        IReadOnlyList<ParameterPlan> parameters,
        bool hasGetter,
        bool hasSetter,
        bool isStatic = false,
        IReadOnlyList<GenericTypeParameterPlan>? genericParameters = null,
        string? targetName = null,
        string? introducedIn = null,
        string? removedIn = null)
    {
        Name = name;
        Kind = kind;
        ReturnType = returnType;
        Parameters = parameters;
        HasGetter = hasGetter;
        HasSetter = hasSetter;
        IsStatic = isStatic;
        GenericParameters = genericParameters ?? Array.Empty<GenericTypeParameterPlan>();
        TargetName = targetName;
        IntroducedIn = introducedIn;
        RemovedIn = removedIn;
    }

    public bool Equals(MemberPlan? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        if (Name != other.Name || Kind != other.Kind || ReturnType != other.ReturnType) return false;
        if (HasGetter != other.HasGetter || HasSetter != other.HasSetter) return false;
        if (IsStatic != other.IsStatic) return false;
        if (TargetName != other.TargetName) return false;
        if (IntroducedIn != other.IntroducedIn || RemovedIn != other.RemovedIn) return false;
        if (Parameters.Count != other.Parameters.Count) return false;
        if (GenericParameters.Count != other.GenericParameters.Count) return false;

        for (int i = 0; i < Parameters.Count; i++)
        {
            if (!Parameters[i].Equals(other.Parameters[i])) return false;
        }

        for (int i = 0; i < GenericParameters.Count; i++)
        {
            if (!GenericParameters[i].Equals(other.GenericParameters[i])) return false;
        }

        return true;
    }

    public override bool Equals(object? obj) => Equals(obj as MemberPlan);

    public override int GetHashCode()
    {
        int hash = Name.GetHashCode();
        hash = (hash * 397) ^ Kind.GetHashCode();
        hash = (hash * 397) ^ ReturnType.GetHashCode();
        hash = (hash * 397) ^ IsStatic.GetHashCode();
        if (TargetName != null) hash = (hash * 397) ^ TargetName.GetHashCode();
        if (IntroducedIn != null) hash = (hash * 397) ^ IntroducedIn.GetHashCode();
        if (RemovedIn != null) hash = (hash * 397) ^ RemovedIn.GetHashCode();
        return hash;
    }
}

/// <summary>
/// Describes a parameter in a method signature.
/// </summary>
internal sealed class ParameterPlan : IEquatable<ParameterPlan>
{
    public string Name { get; }
    public string Type { get; }

    /// <summary>
    /// Parameter modifier: "", "ref", "out", "in", or "params".
    /// </summary>
    public string Modifier { get; }

    /// <summary>
    /// When <c>true</c>, this is the <c>this</c> parameter of an extension method.
    /// </summary>
    public bool IsThis { get; }

    public ParameterPlan(string name, string type, string modifier = "", bool isThis = false)
    {
        Name = name;
        Type = type;
        Modifier = modifier;
        IsThis = isThis;
    }

    public bool Equals(ParameterPlan? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Name == other.Name && Type == other.Type && Modifier == other.Modifier && IsThis == other.IsThis;
    }

    public override bool Equals(object? obj) => Equals(obj as ParameterPlan);

    public override int GetHashCode()
    {
        int hash = (Name.GetHashCode() * 397) ^ Type.GetHashCode();
        hash = (hash * 397) ^ Modifier.GetHashCode();
        hash = (hash * 397) ^ IsThis.GetHashCode();
        return hash;
    }
}

/// <summary>
/// Lightweight config model parsed from <c>*.wrapgod.config.json</c>.
/// Designed for use inside the incremental generator pipeline.
/// </summary>
internal sealed class ConfigPlan : IEquatable<ConfigPlan>
{
    public IReadOnlyList<ConfigTypePlan> Types { get; }
    public string? OutputNamespace { get; }

    public ConfigPlan(IReadOnlyList<ConfigTypePlan> types, string? outputNamespace = null)
    {
        Types = types;
        OutputNamespace = outputNamespace;
    }

    public bool Equals(ConfigPlan? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        if (OutputNamespace != other.OutputNamespace) return false;
        if (Types.Count != other.Types.Count) return false;
        for (int i = 0; i < Types.Count; i++)
        {
            if (!Types[i].Equals(other.Types[i])) return false;
        }

        return true;
    }

    public override bool Equals(object? obj) => Equals(obj as ConfigPlan);

    public override int GetHashCode()
    {
        int hash = 17;
        if (OutputNamespace != null)
            hash = (hash * 397) ^ OutputNamespace.GetHashCode();
        foreach (var t in Types)
        {
            hash = (hash * 397) ^ t.GetHashCode();
        }

        return hash;
    }
}

internal sealed class ConfigTypePlan : IEquatable<ConfigTypePlan>
{
    public string SourceType { get; }
    public bool Include { get; }
    public string? TargetName { get; }
    public IReadOnlyList<ConfigMemberPlan> Members { get; }

    public ConfigTypePlan(string sourceType, bool include, string? targetName, IReadOnlyList<ConfigMemberPlan> members)
    {
        SourceType = sourceType;
        Include = include;
        TargetName = targetName;
        Members = members;
    }

    public bool Equals(ConfigTypePlan? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        if (SourceType != other.SourceType || Include != other.Include || TargetName != other.TargetName) return false;
        if (Members.Count != other.Members.Count) return false;
        for (int i = 0; i < Members.Count; i++)
        {
            if (!Members[i].Equals(other.Members[i])) return false;
        }

        return true;
    }

    public override bool Equals(object? obj) => Equals(obj as ConfigTypePlan);

    public override int GetHashCode()
    {
        int hash = SourceType.GetHashCode();
        hash = (hash * 397) ^ Include.GetHashCode();
        if (TargetName != null) hash = (hash * 397) ^ TargetName.GetHashCode();
        return hash;
    }
}

internal sealed class ConfigMemberPlan : IEquatable<ConfigMemberPlan>
{
    public string SourceMember { get; }
    public bool Include { get; }
    public string? TargetName { get; }

    public ConfigMemberPlan(string sourceMember, bool include, string? targetName)
    {
        SourceMember = sourceMember;
        Include = include;
        TargetName = targetName;
    }

    public bool Equals(ConfigMemberPlan? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return SourceMember == other.SourceMember && Include == other.Include && TargetName == other.TargetName;
    }

    public override bool Equals(object? obj) => Equals(obj as ConfigMemberPlan);

    public override int GetHashCode()
    {
        int hash = SourceMember.GetHashCode();
        hash = (hash * 397) ^ Include.GetHashCode();
        if (TargetName != null) hash = (hash * 397) ^ TargetName.GetHashCode();
        return hash;
    }
}
