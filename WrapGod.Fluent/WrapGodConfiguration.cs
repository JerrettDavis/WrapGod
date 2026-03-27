using System.Collections.Generic;

namespace WrapGod.Fluent;

/// <summary>
/// Fluent entry point for programmatic wrapper configuration.
/// Produces a <see cref="GenerationPlan"/> — the same normalized shape
/// that JSON config and attribute-based config will produce.
/// </summary>
/// <example>
/// <code>
/// var plan = WrapGodConfiguration.Create()
///     .ForAssembly("Vendor.Lib")
///     .WrapType("Vendor.Lib.HttpClient")
///         .As("IHttpClient")
///         .WrapMethod("SendAsync").As("SendRequestAsync")
///         .WrapProperty("Timeout")
///         .ExcludeMember("Dispose")
///     .WrapType("Vendor.Lib.Logger")
///         .As("ILogger")
///         .WrapAllPublicMembers()
///     .MapType("Vendor.Lib.Config", "MyApp.Config")
///     .ExcludeType("Vendor.Lib.Internal*")
///     .Build();
/// </code>
/// </example>
public sealed class WrapGodConfiguration
{
    private string _assemblyName = string.Empty;
    private string? _compatibilityMode;
    private readonly List<TypeDirectiveBuilder> _typeBuilders = [];
    private readonly List<TypeMapping> _typeMappings = [];
    private readonly List<string> _exclusionPatterns = [];

    private WrapGodConfiguration() { }

    /// <summary>Create a new fluent configuration builder.</summary>
    public static WrapGodConfiguration Create() => new();

    /// <summary>Set the source assembly to generate wrappers for.</summary>
    public WrapGodConfiguration ForAssembly(string assemblyName)
    {
        _assemblyName = assemblyName;
        return this;
    }

    /// <summary>Set the compatibility mode for generation.</summary>
    public WrapGodConfiguration WithCompatibilityMode(string mode)
    {
        _compatibilityMode = mode;
        return this;
    }

    /// <summary>
    /// Begin configuring a type wrapper. Returns a <see cref="TypeDirectiveBuilder"/>
    /// that chains back into this configuration.
    /// </summary>
    public TypeDirectiveBuilder WrapType(string sourceType)
    {
        var builder = new TypeDirectiveBuilder(this, sourceType);
        _typeBuilders.Add(builder);
        return builder;
    }

    /// <summary>Map a source type to a destination type.</summary>
    public WrapGodConfiguration MapType(string sourceType, string destinationType)
    {
        _typeMappings.Add(new TypeMapping
        {
            SourceType = sourceType,
            DestinationType = destinationType,
        });
        return this;
    }

    /// <summary>Exclude types matching a glob pattern from generation.</summary>
    public WrapGodConfiguration ExcludeType(string pattern)
    {
        _exclusionPatterns.Add(pattern);
        return this;
    }

    /// <summary>
    /// Finalize the configuration and produce a <see cref="GenerationPlan"/>.
    /// </summary>
    public GenerationPlan Build()
    {
        var directives = new List<TypeDirective>(_typeBuilders.Count);
        foreach (var tb in _typeBuilders)
        {
            directives.Add(tb.ToDirective());
        }

        return new GenerationPlan
        {
            AssemblyName = _assemblyName,
            TypeDirectives = directives,
            TypeMappings = _typeMappings,
            ExclusionPatterns = _exclusionPatterns,
            CompatibilityMode = _compatibilityMode,
        };
    }
}
