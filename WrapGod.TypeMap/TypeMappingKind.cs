namespace WrapGod.TypeMap;

/// <summary>
/// Classifies the kind of type mapping being described.
/// </summary>
public enum TypeMappingKind
{
    /// <summary>Object-to-object mapping (class/struct/record).</summary>
    ObjectMapping,

    /// <summary>Enum-to-enum mapping.</summary>
    Enum,

    /// <summary>Collection-to-collection mapping (e.g. List to IReadOnlyList).</summary>
    Collection,

    /// <summary>Nullable wrapper mapping (e.g. int? to int?).</summary>
    Nullable,

    /// <summary>Fully custom mapping handled by a user-provided converter.</summary>
    Custom,
}
