namespace WrapGod.Migration;

/// <summary>
/// Classifies a migration rule by the kind of change it describes.
/// </summary>
public enum MigrationRuleKind
{
    /// <summary>A type was renamed (namespace stays the same).</summary>
    RenameType,

    /// <summary>A member (method, property, field, event) was renamed.</summary>
    RenameMember,

    /// <summary>A type was moved to a different namespace.</summary>
    RenameNamespace,

    /// <summary>A method parameter was changed (type or name).</summary>
    ChangeParameter,

    /// <summary>A member was removed with no direct replacement.</summary>
    RemoveMember,

    /// <summary>A required parameter was added to an existing method.</summary>
    AddRequiredParameter,

    /// <summary>A type reference was changed (e.g., <c>IList&lt;T&gt;</c> → <c>IReadOnlyList&lt;T&gt;</c>).</summary>
    ChangeTypeReference,

    /// <summary>A method was split into multiple methods.</summary>
    SplitMethod,

    /// <summary>Several parameters were extracted into a parameter object.</summary>
    ExtractParameterObject,

    /// <summary>A property was converted to a method (or vice-versa).</summary>
    PropertyToMethod,

    /// <summary>A member was moved to a different type.</summary>
    MoveMember,
}
