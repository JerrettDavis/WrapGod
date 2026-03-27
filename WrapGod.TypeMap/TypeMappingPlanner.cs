using System.Collections.Generic;
using WrapGod.Abstractions.Config;

namespace WrapGod.TypeMap;

/// <summary>
/// Builds a <see cref="TypeMappingPlan"/> from <see cref="WrapGodConfig"/> type
/// configurations and optional explicit mapping overrides.
/// </summary>
public static class TypeMappingPlanner
{
    /// <summary>
    /// Create a <see cref="TypeMappingPlan"/> from a merged <see cref="WrapGodConfig"/>.
    /// Each <see cref="TypeConfig"/> with an included type produces a <see cref="TypeMapping"/>
    /// whose kind is inferred as <see cref="TypeMappingKind.ObjectMapping"/> by default.
    /// </summary>
    public static TypeMappingPlan BuildPlan(WrapGodConfig config) =>
        BuildPlan(config, new List<TypeMappingOverride>());

    /// <summary>
    /// Create a <see cref="TypeMappingPlan"/> from a merged <see cref="WrapGodConfig"/>
    /// plus explicit mapping overrides (e.g. from fluent or JSON config).
    /// </summary>
    public static TypeMappingPlan BuildPlan(
        WrapGodConfig config,
        IReadOnlyList<TypeMappingOverride> overrides)
    {
        var overrideMap = new Dictionary<string, TypeMappingOverride>();
        foreach (var o in overrides)
        {
            overrideMap[o.SourceType] = o;
        }

        var mappings = new List<TypeMapping>();

        foreach (var type in config.Types)
        {
            if (type.Include == false)
            {
                continue;
            }

            overrideMap.TryGetValue(type.SourceType, out var over);

            var kind = over?.Kind ?? TypeMappingKind.ObjectMapping;
            var converter = over?.Converter;
            var destType = type.TargetName ?? type.SourceType;

            var memberMappings = new List<MemberMapping>();
            foreach (var member in type.Members)
            {
                if (member.Include == false)
                {
                    continue;
                }

                memberMappings.Add(new MemberMapping
                {
                    SourceMember = member.SourceMember,
                    DestinationMember = member.TargetName ?? member.SourceMember,
                });
            }

            mappings.Add(new TypeMapping
            {
                SourceType = type.SourceType,
                DestinationType = destType,
                Kind = kind,
                MemberMappings = memberMappings,
                Converter = converter,
            });
        }

        return new TypeMappingPlan { Mappings = mappings };
    }
}

/// <summary>
/// An explicit override for a type mapping, allowing callers to specify
/// the mapping kind and/or a converter reference for a given source type.
/// </summary>
public sealed class TypeMappingOverride
{
    /// <summary>Fully-qualified source type to override.</summary>
    public string SourceType { get; set; } = string.Empty;

    /// <summary>Override the inferred mapping kind.</summary>
    public TypeMappingKind? Kind { get; set; }

    /// <summary>Override with a custom converter reference.</summary>
    public ConverterRef? Converter { get; set; }
}
