using System;
using System.Collections.Generic;

namespace WrapGod.Manifest;

/// <summary>
/// Root manifest describing a third-party assembly's public API surface.
/// This is the canonical source of truth for all downstream generation.
/// </summary>
public sealed class ApiManifest
{
    /// <summary>Schema version for forward compatibility.</summary>
    public string SchemaVersion { get; set; } = "1.0";

    /// <summary>When this manifest was generated.</summary>
    public DateTimeOffset GeneratedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Assembly identity (name, version, culture, public key token).</summary>
    public AssemblyIdentity Assembly { get; set; } = new();

    /// <summary>SHA-256 hash of the source assembly file for drift detection.</summary>
    public string? SourceHash { get; set; }

    /// <summary>All public types in the assembly.</summary>
    public List<ApiTypeNode> Types { get; set; } = [];
}

/// <summary>
/// Identifies an assembly by its metadata.
/// </summary>
public sealed class AssemblyIdentity
{
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string? Culture { get; set; }
    public string? PublicKeyToken { get; set; }
    public string? TargetFramework { get; set; }
}

/// <summary>
/// Represents a public type (class, struct, interface, enum, delegate).
/// </summary>
public sealed class ApiTypeNode
{
    /// <summary>Stable identifier: namespace.typeName (with generic arity).</summary>
    public string StableId { get; set; } = string.Empty;

    /// <summary>Fully qualified name.</summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>Simple type name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Namespace.</summary>
    public string Namespace { get; set; } = string.Empty;

    /// <summary>Kind of type.</summary>
    public ApiTypeKind Kind { get; set; }

    /// <summary>Base type (null for interfaces/System.Object).</summary>
    public string? BaseType { get; set; }

    /// <summary>Implemented interfaces.</summary>
    public List<string> Interfaces { get; set; } = [];

    /// <summary>Generic type parameters.</summary>
    public List<GenericParameterInfo> GenericParameters { get; set; } = [];

    /// <summary>Whether this type is generic (definition or constructed).</summary>
    public bool IsGenericType { get; set; }

    /// <summary>Whether this type is an open generic type definition.</summary>
    public bool IsGenericTypeDefinition { get; set; }

    /// <summary>Whether this type is a constructed generic type instance.</summary>
    public bool IsConstructedGenericType { get; set; }

    /// <summary>Whether this type (or any containing type arguments) contains unbound generic parameters.</summary>
    public bool ContainsGenericParameters { get; set; }

    /// <summary>Whether the type is sealed.</summary>
    public bool IsSealed { get; set; }

    /// <summary>Whether the type is abstract.</summary>
    public bool IsAbstract { get; set; }

    /// <summary>Whether the type is static.</summary>
    public bool IsStatic { get; set; }

    /// <summary>Public members of this type.</summary>
    public List<ApiMemberNode> Members { get; set; } = [];

    /// <summary>Version metadata: when this type was introduced/removed.</summary>
    public VersionPresence? Presence { get; set; }
}

/// <summary>
/// Represents a public member (method, property, field, event, constructor).
/// </summary>
public sealed class ApiMemberNode
{
    /// <summary>Stable identifier: typeStableId.memberName(paramTypes).</summary>
    public string StableId { get; set; } = string.Empty;

    /// <summary>Member name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Kind of member.</summary>
    public ApiMemberKind Kind { get; set; }

    /// <summary>Return type (for methods/properties).</summary>
    public string? ReturnType { get; set; }

    /// <summary>Parameters (for methods/constructors/indexers).</summary>
    public List<ApiParameterInfo> Parameters { get; set; } = [];

    /// <summary>Generic type parameters (for generic methods).</summary>
    public List<GenericParameterInfo> GenericParameters { get; set; } = [];

    /// <summary>Whether this member is a generic method (definition or constructed).</summary>
    public bool IsGenericMethod { get; set; }

    /// <summary>Whether this member is an open generic method definition.</summary>
    public bool IsGenericMethodDefinition { get; set; }

    /// <summary>Whether this member is a constructed generic method instance.</summary>
    public bool IsConstructedGenericMethod { get; set; }

    /// <summary>Whether this member contains unbound generic parameters.</summary>
    public bool ContainsGenericParameters { get; set; }

    /// <summary>Whether the member is static.</summary>
    public bool IsStatic { get; set; }

    /// <summary>Whether the member is virtual/overridable.</summary>
    public bool IsVirtual { get; set; }

    /// <summary>Whether the member is abstract.</summary>
    public bool IsAbstract { get; set; }

    /// <summary>Property getter accessibility (for properties).</summary>
    public bool HasGetter { get; set; }

    /// <summary>Property setter accessibility (for properties).</summary>
    public bool HasSetter { get; set; }

    /// <summary>Version metadata.</summary>
    public VersionPresence? Presence { get; set; }
}

/// <summary>Method/constructor parameter.</summary>
public sealed class ApiParameterInfo
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool IsOptional { get; set; }
    public bool IsParams { get; set; }
    public bool IsOut { get; set; }
    public bool IsRef { get; set; }
    public string? DefaultValue { get; set; }
}

/// <summary>Generic type parameter info.</summary>
public sealed class GenericParameterInfo
{
    public string Name { get; set; } = string.Empty;
    public int Position { get; set; }
    public GenericParameterVariance Variance { get; set; }
    public List<string> Constraints { get; set; } = [];
}

public enum GenericParameterVariance
{
    None,
    In,
    Out,
}

/// <summary>Version presence metadata for multi-version manifests.</summary>
public sealed class VersionPresence
{
    public string? IntroducedIn { get; set; }
    public string? RemovedIn { get; set; }
    public string? ChangedIn { get; set; }
}

/// <summary>Type classification.</summary>
public enum ApiTypeKind
{
    Class,
    Struct,
    Interface,
    Enum,
    Delegate,
    Record,
    RecordStruct,
}

/// <summary>Member classification.</summary>
public enum ApiMemberKind
{
    Method,
    Property,
    Field,
    Event,
    Constructor,
    Indexer,
    Operator,
}
