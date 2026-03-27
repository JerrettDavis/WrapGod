using System.Collections.Generic;

namespace WrapGod.Fluent;

/// <summary>
/// The normalized output of the fluent DSL builder. Represents the complete
/// set of directives for wrapper code generation — the same shape that
/// JSON config and attribute-based config will produce once merged.
/// </summary>
public sealed class GenerationPlan
{
    /// <summary>Source assembly name to generate wrappers for.</summary>
    public string AssemblyName { get; init; } = string.Empty;

    /// <summary>Per-type wrapper directives.</summary>
    public IReadOnlyList<TypeDirective> TypeDirectives { get; init; } = [];

    /// <summary>Source → destination type mappings (rename / remap).</summary>
    public IReadOnlyList<TypeMapping> TypeMappings { get; init; } = [];

    /// <summary>Glob patterns for types to exclude from generation.</summary>
    public IReadOnlyList<string> ExclusionPatterns { get; init; } = [];

    /// <summary>
    /// When set, the generated wrappers target a specific compatibility mode
    /// (e.g. "strict" for 1-to-1 surface, "relaxed" for best-effort).
    /// </summary>
    public string? CompatibilityMode { get; init; }
}

/// <summary>
/// Directive describing how a single source type should be wrapped.
/// </summary>
public sealed class TypeDirective
{
    /// <summary>Fully-qualified source type name.</summary>
    public string SourceType { get; init; } = string.Empty;

    /// <summary>Target interface / wrapper name (e.g. "IHttpClient").</summary>
    public string? TargetName { get; init; }

    /// <summary>Whether all public members should be wrapped.</summary>
    public bool WrapAllPublicMembers { get; init; }

    /// <summary>Explicit member-level directives.</summary>
    public IReadOnlyList<MemberDirective> MemberDirectives { get; init; } = [];

    /// <summary>Members to exclude by name.</summary>
    public IReadOnlyList<string> ExcludedMembers { get; init; } = [];

    /// <summary>
    /// When <c>true</c>, <see cref="SourceType"/> is an open generic pattern
    /// (e.g. <c>"IRepository&lt;&gt;"</c>) that matches any closed construction.
    /// </summary>
    public bool IsGenericPattern { get; init; }

    /// <summary>
    /// The arity (number of type parameters) of the generic pattern.
    /// Only meaningful when <see cref="IsGenericPattern"/> is <c>true</c>.
    /// </summary>
    public int GenericArity { get; init; }
}

/// <summary>
/// Directive for a single member (method, property, event, etc.).
/// </summary>
public sealed class MemberDirective
{
    /// <summary>The original member name in the source type.</summary>
    public string SourceName { get; init; } = string.Empty;

    /// <summary>Optional renamed member name on the wrapper.</summary>
    public string? TargetName { get; init; }

    /// <summary>Kind of member (Method, Property, etc.).</summary>
    public MemberDirectiveKind Kind { get; init; }
}

/// <summary>
/// Classifies the kind of member a directive applies to.
/// </summary>
public enum MemberDirectiveKind
{
    Method,
    Property,
}

/// <summary>
/// Maps a source type to a destination type for generation output.
/// </summary>
public sealed class TypeMapping
{
    /// <summary>Fully-qualified source type.</summary>
    public string SourceType { get; init; } = string.Empty;

    /// <summary>Fully-qualified destination type.</summary>
    public string DestinationType { get; init; } = string.Empty;
}
