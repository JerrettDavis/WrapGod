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

    public TypePlan(string fullName, string name, string ns, IReadOnlyList<MemberPlan> members)
    {
        FullName = fullName;
        Name = name;
        Namespace = ns;
        Members = members;
    }

    public bool Equals(TypePlan? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        if (FullName != other.FullName || Name != other.Name || Namespace != other.Namespace) return false;
        if (Members.Count != other.Members.Count) return false;

        for (int i = 0; i < Members.Count; i++)
        {
            if (!Members[i].Equals(other.Members[i])) return false;
        }

        return true;
    }

    public override bool Equals(object? obj) => Equals(obj as TypePlan);

    public override int GetHashCode()
    {
        int hash = FullName.GetHashCode();
        hash = (hash * 397) ^ Name.GetHashCode();
        hash = (hash * 397) ^ Namespace.GetHashCode();
        foreach (var m in Members)
        {
            hash = (hash * 397) ^ m.GetHashCode();
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
    public IReadOnlyList<string> GenericParameters { get; }

    public MemberPlan(
        string name,
        string kind,
        string returnType,
        IReadOnlyList<ParameterPlan> parameters,
        bool hasGetter,
        bool hasSetter,
        bool isStatic = false,
        IReadOnlyList<string>? genericParameters = null)
    {
        Name = name;
        Kind = kind;
        ReturnType = returnType;
        Parameters = parameters;
        HasGetter = hasGetter;
        HasSetter = hasSetter;
        IsStatic = isStatic;
        GenericParameters = genericParameters ?? Array.Empty<string>();
    }

    public bool Equals(MemberPlan? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        if (Name != other.Name || Kind != other.Kind || ReturnType != other.ReturnType) return false;
        if (HasGetter != other.HasGetter || HasSetter != other.HasSetter) return false;
        if (IsStatic != other.IsStatic) return false;
        if (Parameters.Count != other.Parameters.Count) return false;
        if (GenericParameters.Count != other.GenericParameters.Count) return false;

        for (int i = 0; i < Parameters.Count; i++)
        {
            if (!Parameters[i].Equals(other.Parameters[i])) return false;
        }

        for (int i = 0; i < GenericParameters.Count; i++)
        {
            if (GenericParameters[i] != other.GenericParameters[i]) return false;
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

    public ParameterPlan(string name, string type, string modifier = "")
    {
        Name = name;
        Type = type;
        Modifier = modifier;
    }

    public bool Equals(ParameterPlan? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Name == other.Name && Type == other.Type && Modifier == other.Modifier;
    }

    public override bool Equals(object? obj) => Equals(obj as ParameterPlan);

    public override int GetHashCode()
    {
        int hash = (Name.GetHashCode() * 397) ^ Type.GetHashCode();
        hash = (hash * 397) ^ Modifier.GetHashCode();
        return hash;
    }
}
