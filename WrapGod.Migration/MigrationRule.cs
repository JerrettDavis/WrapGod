using System.Collections.Generic;

namespace WrapGod.Migration;

/// <summary>
/// Base class for a single migration rule.
/// Each concrete subclass carries kind-specific properties.
/// The <see cref="Kind"/> property acts as the discriminator during JSON deserialization.
/// </summary>
public abstract class MigrationRule
{
    /// <summary>Stable, human-readable identifier for this rule, e.g. <c>rename-Button-to-MudButton</c>.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>The kind of change described by this rule (discriminator).</summary>
    public abstract MigrationRuleKind Kind { get; }

    /// <summary>How confidently this rule can be applied automatically.</summary>
    public RuleConfidence Confidence { get; set; } = RuleConfidence.Auto;

    /// <summary>Optional human-readable note or migration guidance.</summary>
    public string? Note { get; set; }
}

/// <summary>A type was renamed; namespace is unchanged.</summary>
public sealed class RenameTypeRule : MigrationRule
{
    /// <inheritdoc/>
    public override MigrationRuleKind Kind => MigrationRuleKind.RenameType;

    /// <summary>Fully-qualified old type name.</summary>
    public string OldName { get; set; } = string.Empty;

    /// <summary>Fully-qualified new type name.</summary>
    public string NewName { get; set; } = string.Empty;
}

/// <summary>A member (method/property/field/event) was renamed on its declaring type.</summary>
public sealed class RenameMemberRule : MigrationRule
{
    /// <inheritdoc/>
    public override MigrationRuleKind Kind => MigrationRuleKind.RenameMember;

    /// <summary>Fully-qualified type that owns the member.</summary>
    public string TypeName { get; set; } = string.Empty;

    /// <summary>Old member name.</summary>
    public string OldMemberName { get; set; } = string.Empty;

    /// <summary>New member name.</summary>
    public string NewMemberName { get; set; } = string.Empty;
}

/// <summary>A type's namespace changed.</summary>
public sealed class RenameNamespaceRule : MigrationRule
{
    /// <inheritdoc/>
    public override MigrationRuleKind Kind => MigrationRuleKind.RenameNamespace;

    /// <summary>Old namespace prefix.</summary>
    public string OldNamespace { get; set; } = string.Empty;

    /// <summary>New namespace prefix.</summary>
    public string NewNamespace { get; set; } = string.Empty;
}

/// <summary>A method parameter's name and/or type was changed.</summary>
public sealed class ChangeParameterRule : MigrationRule
{
    /// <inheritdoc/>
    public override MigrationRuleKind Kind => MigrationRuleKind.ChangeParameter;

    /// <summary>Fully-qualified type that owns the method.</summary>
    public string TypeName { get; set; } = string.Empty;

    /// <summary>Method name.</summary>
    public string MethodName { get; set; } = string.Empty;

    /// <summary>Old parameter name.</summary>
    public string OldParameterName { get; set; } = string.Empty;

    /// <summary>New parameter name (null if the name did not change).</summary>
    public string? NewParameterName { get; set; }

    /// <summary>Old parameter type (null if the type did not change).</summary>
    public string? OldParameterType { get; set; }

    /// <summary>New parameter type (null if the type did not change).</summary>
    public string? NewParameterType { get; set; }
}

/// <summary>A member was removed with no direct replacement.</summary>
public sealed class RemoveMemberRule : MigrationRule
{
    /// <inheritdoc/>
    public override MigrationRuleKind Kind => MigrationRuleKind.RemoveMember;

    /// <summary>Fully-qualified type that owned the member.</summary>
    public string TypeName { get; set; } = string.Empty;

    /// <summary>Name of the removed member.</summary>
    public string MemberName { get; set; } = string.Empty;
}

/// <summary>A required parameter was added to an existing method.</summary>
public sealed class AddRequiredParameterRule : MigrationRule
{
    /// <inheritdoc/>
    public override MigrationRuleKind Kind => MigrationRuleKind.AddRequiredParameter;

    /// <summary>Fully-qualified type that owns the method.</summary>
    public string TypeName { get; set; } = string.Empty;

    /// <summary>Method name.</summary>
    public string MethodName { get; set; } = string.Empty;

    /// <summary>Name of the new required parameter.</summary>
    public string ParameterName { get; set; } = string.Empty;

    /// <summary>Type of the new required parameter.</summary>
    public string ParameterType { get; set; } = string.Empty;

    /// <summary>Zero-based position where the new parameter was inserted.</summary>
    public int Position { get; set; }
}

/// <summary>A type reference was changed across the API (e.g., an interface type narrowed).</summary>
public sealed class ChangeTypeReferenceRule : MigrationRule
{
    /// <inheritdoc/>
    public override MigrationRuleKind Kind => MigrationRuleKind.ChangeTypeReference;

    /// <summary>Old type reference (fully qualified).</summary>
    public string OldType { get; set; } = string.Empty;

    /// <summary>New type reference (fully qualified).</summary>
    public string NewType { get; set; } = string.Empty;
}

/// <summary>A method was split into multiple separate methods.</summary>
public sealed class SplitMethodRule : MigrationRule
{
    /// <inheritdoc/>
    public override MigrationRuleKind Kind => MigrationRuleKind.SplitMethod;

    /// <summary>Fully-qualified type that owned the original method.</summary>
    public string TypeName { get; set; } = string.Empty;

    /// <summary>Original method name.</summary>
    public string OldMethodName { get; set; } = string.Empty;

    /// <summary>New methods that replace the original.</summary>
    public List<string> NewMethodNames { get; set; } = [];
}

/// <summary>Several parameters were extracted into a dedicated parameter-object type.</summary>
public sealed class ExtractParameterObjectRule : MigrationRule
{
    /// <inheritdoc/>
    public override MigrationRuleKind Kind => MigrationRuleKind.ExtractParameterObject;

    /// <summary>Fully-qualified type that owns the method.</summary>
    public string TypeName { get; set; } = string.Empty;

    /// <summary>Method name.</summary>
    public string MethodName { get; set; } = string.Empty;

    /// <summary>Fully-qualified name of the newly introduced parameter-object type.</summary>
    public string ParameterObjectType { get; set; } = string.Empty;

    /// <summary>Parameters that were extracted into the parameter object.</summary>
    public List<string> ExtractedParameters { get; set; } = [];
}

/// <summary>A property was converted to a method or a method was converted to a property.</summary>
public sealed class PropertyToMethodRule : MigrationRule
{
    /// <inheritdoc/>
    public override MigrationRuleKind Kind => MigrationRuleKind.PropertyToMethod;

    /// <summary>Fully-qualified type that owns the member.</summary>
    public string TypeName { get; set; } = string.Empty;

    /// <summary>Old property name.</summary>
    public string OldPropertyName { get; set; } = string.Empty;

    /// <summary>New method name.</summary>
    public string NewMethodName { get; set; } = string.Empty;
}

/// <summary>A member was moved from one type to another.</summary>
public sealed class MoveMemberRule : MigrationRule
{
    /// <inheritdoc/>
    public override MigrationRuleKind Kind => MigrationRuleKind.MoveMember;

    /// <summary>Fully-qualified type that originally declared the member.</summary>
    public string OldTypeName { get; set; } = string.Empty;

    /// <summary>Fully-qualified type that now declares the member.</summary>
    public string NewTypeName { get; set; } = string.Empty;

    /// <summary>Member name (unchanged).</summary>
    public string MemberName { get; set; } = string.Empty;
}
