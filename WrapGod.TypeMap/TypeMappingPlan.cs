using System.Collections.Generic;
using System.Linq;

namespace WrapGod.TypeMap;

/// <summary>
/// A complete collection of type mappings consumable by the generator and analyzers.
/// Produced by <see cref="TypeMappingPlanner"/> from config inputs.
/// </summary>
public sealed class TypeMappingPlan
{
    /// <summary>All type mappings in this plan.</summary>
    public IReadOnlyList<TypeMapping> Mappings { get; set; } = new List<TypeMapping>();

    /// <summary>
    /// Look up the mapping for a given source type, or null if no mapping exists.
    /// </summary>
    public TypeMapping? FindBySourceType(string sourceType) =>
        Mappings.FirstOrDefault(m => m.SourceType == sourceType);

    /// <summary>
    /// Look up the mapping for a given destination type, or null if no mapping exists.
    /// </summary>
    public TypeMapping? FindByDestinationType(string destinationType) =>
        Mappings.FirstOrDefault(m => m.DestinationType == destinationType);
}
