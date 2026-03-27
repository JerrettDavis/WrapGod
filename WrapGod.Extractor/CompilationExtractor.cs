using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis;
using WrapGod.Manifest;

namespace WrapGod.Extractor;

/// <summary>
/// Extracts the public API surface from a Roslyn <see cref="Compilation"/> object,
/// producing an <see cref="ApiManifest"/> equivalent to what <see cref="AssemblyExtractor"/>
/// produces from a physical assembly, but from source symbols instead of reflection.
/// </summary>
public static class CompilationExtractor
{
    /// <summary>
    /// Extracts an <see cref="ApiManifest"/> from the given compilation.
    /// Only public types matching the optional namespace/type patterns are included.
    /// </summary>
    /// <param name="compilation">The Roslyn compilation to extract from.</param>
    /// <param name="namespacePatterns">
    /// Optional namespace prefixes to filter types. When null or empty, all public types are included.
    /// </param>
    /// <param name="typePatterns">
    /// Optional full type name patterns. When null or empty, all public types (subject to namespace filter) are included.
    /// </param>
    /// <returns>A fully-populated, deterministically-sorted manifest.</returns>
    public static ApiManifest Extract(
        Compilation compilation,
        IReadOnlyList<string>? namespacePatterns = null,
        IReadOnlyList<string>? typePatterns = null)
    {
        var types = new List<ApiTypeNode>();

        var visitor = new PublicTypeVisitor(namespacePatterns, typePatterns, types);
        visitor.Visit(compilation.Assembly.GlobalNamespace);

        var assemblyName = compilation.AssemblyName ?? "Unknown";
        var sourceHash = ComputeCompilationHash(compilation);

        return new ApiManifest
        {
            SchemaVersion = "1.0",
            GeneratedAt = DateTimeOffset.UtcNow,
            SourceHash = sourceHash,
            Assembly = new Manifest.AssemblyIdentity
            {
                Name = assemblyName,
                Version = compilation.Assembly.Identity.Version.ToString(),
            },
            Types = types
                .OrderBy(t => t.StableId, StringComparer.Ordinal)
                .ToList(),
        };
    }

    private static string ComputeCompilationHash(Compilation compilation)
    {
        var sb = new StringBuilder();
        foreach (var tree in compilation.SyntaxTrees.OrderBy(t => t.FilePath, StringComparer.Ordinal))
        {
            sb.Append(tree.FilePath);
            sb.Append(':');
            sb.AppendLine(tree.GetText().ToString().Length.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private sealed class PublicTypeVisitor : SymbolVisitor
    {
        private readonly IReadOnlyList<string>? _namespacePatterns;
        private readonly IReadOnlyList<string>? _typePatterns;
        private readonly List<ApiTypeNode> _types;

        public PublicTypeVisitor(
            IReadOnlyList<string>? namespacePatterns,
            IReadOnlyList<string>? typePatterns,
            List<ApiTypeNode> types)
        {
            _namespacePatterns = namespacePatterns is { Count: > 0 } ? namespacePatterns : null;
            _typePatterns = typePatterns is { Count: > 0 } ? typePatterns : null;
            _types = types;
        }

        public override void VisitNamespace(INamespaceSymbol symbol)
        {
            foreach (var member in symbol.GetMembers())
            {
                member.Accept(this);
            }
        }

        public override void VisitNamedType(INamedTypeSymbol symbol)
        {
            if (symbol.DeclaredAccessibility != Accessibility.Public)
                return;

            var fullName = GetFullTypeName(symbol);
            var ns = symbol.ContainingNamespace?.ToDisplayString() ?? string.Empty;

            // Apply namespace filter.
            if (_namespacePatterns is not null)
            {
                if (!_namespacePatterns.Any(p => ns.StartsWith(p, StringComparison.Ordinal)))
                    return;
            }

            // Apply type pattern filter.
            if (_typePatterns is not null)
            {
                if (!_typePatterns.Any(p =>
                    fullName.Equals(p, StringComparison.Ordinal) ||
                    MatchesWildcard(fullName, p)))
                    return;
            }

            _types.Add(ExtractType(symbol));

            // Visit nested types.
            foreach (var nested in symbol.GetTypeMembers())
            {
                nested.Accept(this);
            }
        }
    }

    private static bool MatchesWildcard(string value, string pattern)
    {
        if (pattern.EndsWith('*'))
        {
            return value.StartsWith(pattern[..^1], StringComparison.Ordinal);
        }

        return false;
    }

    private static ApiTypeNode ExtractType(INamedTypeSymbol symbol)
    {
        var stableId = BuildTypeStableId(symbol);
        var fullName = GetFullTypeName(symbol);

        return new ApiTypeNode
        {
            StableId = stableId,
            FullName = fullName,
            Name = symbol.MetadataName,
            Namespace = symbol.ContainingNamespace?.ToDisplayString() ?? string.Empty,
            Kind = ClassifyType(symbol),
            BaseType = symbol.BaseType is { } bt && bt.SpecialType != SpecialType.System_Object
                ? bt.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                : null,
            Interfaces = symbol.Interfaces
                .Select(i => i.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToList(),
            GenericParameters = ExtractGenericParameters(symbol),
            IsGenericType = symbol.IsGenericType,
            IsGenericTypeDefinition = symbol.IsUnboundGenericType || symbol.TypeParameters.Length > 0,
            IsSealed = symbol.IsSealed && symbol.TypeKind != TypeKind.Struct,
            IsAbstract = symbol.IsAbstract && symbol.TypeKind != TypeKind.Interface,
            IsStatic = symbol.IsStatic,
            Members = ExtractMembers(symbol, stableId)
                .OrderBy(m => m.StableId, StringComparer.Ordinal)
                .ToList(),
        };
    }

    private static string BuildTypeStableId(INamedTypeSymbol symbol)
    {
        if (symbol.ContainingType is not null)
        {
            return $"{BuildTypeStableId(symbol.ContainingType)}+{symbol.MetadataName}";
        }

        var ns = symbol.ContainingNamespace?.ToDisplayString() ?? string.Empty;
        return string.IsNullOrEmpty(ns) ? symbol.MetadataName : $"{ns}.{symbol.MetadataName}";
    }

    private static string GetFullTypeName(INamedTypeSymbol symbol)
    {
        return symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
            .Replace("global::", string.Empty);
    }

    private static ApiTypeKind ClassifyType(INamedTypeSymbol symbol)
    {
        return symbol.TypeKind switch
        {
            TypeKind.Enum => ApiTypeKind.Enum,
            TypeKind.Interface => ApiTypeKind.Interface,
            TypeKind.Delegate => ApiTypeKind.Delegate,
            TypeKind.Struct => ApiTypeKind.Struct,
            _ => ApiTypeKind.Class,
        };
    }

    private static List<GenericParameterInfo> ExtractGenericParameters(INamedTypeSymbol symbol)
    {
        if (symbol.TypeParameters.Length == 0)
            return [];

        return symbol.TypeParameters
            .Select((tp, i) => new GenericParameterInfo
            {
                Name = tp.Name,
                Position = i,
                Variance = tp.Variance switch
                {
                    VarianceKind.In => GenericParameterVariance.In,
                    VarianceKind.Out => GenericParameterVariance.Out,
                    _ => GenericParameterVariance.None,
                },
                Constraints = GetConstraints(tp),
            })
            .ToList();
    }

    private static List<string> GetConstraints(ITypeParameterSymbol tp)
    {
        var constraints = new List<string>();

        foreach (var ct in tp.ConstraintTypes)
        {
            constraints.Add(ct.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
        }

        if (tp.HasReferenceTypeConstraint)
            constraints.Add("class");
        if (tp.HasValueTypeConstraint)
            constraints.Add("struct");
        if (tp.HasConstructorConstraint)
            constraints.Add("new()");
        if (tp.HasUnmanagedTypeConstraint)
            constraints.Add("unmanaged");
        if (tp.HasNotNullConstraint)
            constraints.Add("notnull");

        return constraints
            .Distinct(StringComparer.Ordinal)
            .OrderBy(c => c, StringComparer.Ordinal)
            .ToList();
    }

    private static IEnumerable<ApiMemberNode> ExtractMembers(INamedTypeSymbol type, string typeStableId)
    {
        foreach (var member in type.GetMembers())
        {
            if (member.DeclaredAccessibility != Accessibility.Public)
                continue;

            switch (member)
            {
                case IMethodSymbol method:
                    if (method.MethodKind == MethodKind.Constructor)
                    {
                        var paramSig = BuildParameterSignature(method.Parameters);
                        yield return new ApiMemberNode
                        {
                            StableId = $"{typeStableId}..ctor({paramSig})",
                            Name = ".ctor",
                            Kind = ApiMemberKind.Constructor,
                            IsStatic = method.IsStatic,
                            Parameters = ExtractParameters(method.Parameters),
                        };
                    }
                    else if (method.MethodKind == MethodKind.Ordinary)
                    {
                        var paramSig = BuildParameterSignature(method.Parameters);
                        var genericSuffix = method.IsGenericMethod
                            ? $"`{method.TypeParameters.Length}"
                            : string.Empty;

                        yield return new ApiMemberNode
                        {
                            StableId = $"{typeStableId}.{method.Name}{genericSuffix}({paramSig})",
                            Name = method.Name,
                            Kind = ApiMemberKind.Method,
                            ReturnType = method.ReturnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                            IsStatic = method.IsStatic,
                            IsVirtual = method.IsVirtual && !method.IsSealed,
                            IsAbstract = method.IsAbstract,
                            Parameters = ExtractParameters(method.Parameters),
                            GenericParameters = method.TypeParameters
                                .Select((tp, i) => new GenericParameterInfo
                                {
                                    Name = tp.Name,
                                    Position = i,
                                    Constraints = GetConstraints(tp),
                                })
                                .ToList(),
                            IsGenericMethod = method.IsGenericMethod,
                            IsGenericMethodDefinition = method.IsGenericMethod,
                        };
                    }
                    else if (method.MethodKind == MethodKind.UserDefinedOperator ||
                             method.MethodKind == MethodKind.Conversion)
                    {
                        var paramSig = BuildParameterSignature(method.Parameters);
                        yield return new ApiMemberNode
                        {
                            StableId = $"{typeStableId}.{method.MetadataName}({paramSig})",
                            Name = method.MetadataName,
                            Kind = ApiMemberKind.Operator,
                            ReturnType = method.ReturnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                            IsStatic = method.IsStatic,
                            Parameters = ExtractParameters(method.Parameters),
                        };
                    }

                    break;

                case IPropertySymbol property:
                    var isIndexer = property.IsIndexer;
                    var propKind = isIndexer ? ApiMemberKind.Indexer : ApiMemberKind.Property;

                    yield return new ApiMemberNode
                    {
                        StableId = isIndexer
                            ? $"{typeStableId}.Item[{BuildParameterSignature(property.Parameters)}]"
                            : $"{typeStableId}.{property.Name}",
                        Name = property.Name,
                        Kind = propKind,
                        ReturnType = property.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                        IsStatic = property.IsStatic,
                        IsVirtual = property.IsVirtual,
                        IsAbstract = property.IsAbstract,
                        HasGetter = property.GetMethod is not null,
                        HasSetter = property.SetMethod is not null,
                        Parameters = isIndexer
                            ? ExtractParameters(property.Parameters)
                            : [],
                    };
                    break;

                case IFieldSymbol field when !field.IsImplicitlyDeclared:
                    yield return new ApiMemberNode
                    {
                        StableId = $"{typeStableId}.{field.Name}",
                        Name = field.Name,
                        Kind = ApiMemberKind.Field,
                        ReturnType = field.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                        IsStatic = field.IsStatic,
                    };
                    break;

                case IEventSymbol evt:
                    yield return new ApiMemberNode
                    {
                        StableId = $"{typeStableId}.{evt.Name}",
                        Name = evt.Name,
                        Kind = ApiMemberKind.Event,
                        ReturnType = evt.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                        IsStatic = evt.IsStatic,
                    };
                    break;
            }
        }
    }

    private static string BuildParameterSignature(ImmutableArray<IParameterSymbol> parameters)
    {
        return string.Join(", ", parameters.Select(p =>
            p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
    }

    private static List<ApiParameterInfo> ExtractParameters(ImmutableArray<IParameterSymbol> parameters)
    {
        return parameters.Select(p => new ApiParameterInfo
        {
            Name = p.Name,
            Type = p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            IsOptional = p.IsOptional,
            IsParams = p.IsParams,
            IsOut = p.RefKind == RefKind.Out,
            IsRef = p.RefKind == RefKind.Ref,
            DefaultValue = p.HasExplicitDefaultValue
                ? p.ExplicitDefaultValue?.ToString()
                : null,
        }).ToList();
    }

}
