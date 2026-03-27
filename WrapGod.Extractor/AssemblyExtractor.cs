using System.Reflection;
using System.Security.Cryptography;
using WrapGod.Manifest;

namespace WrapGod.Extractor;

/// <summary>
/// Extracts the public API surface of a .NET assembly into an <see cref="ApiManifest"/>.
/// Uses <see cref="MetadataLoadContext"/> for safe, reflection-only loading.
/// </summary>
public static class AssemblyExtractor
{
    /// <summary>
    /// Extracts an <see cref="ApiManifest"/> from the assembly at <paramref name="assemblyPath"/>.
    /// </summary>
    /// <param name="assemblyPath">Absolute path to the .NET assembly DLL.</param>
    /// <returns>A fully-populated, deterministically-sorted manifest.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the assembly file does not exist.</exception>
    public static ApiManifest Extract(string assemblyPath)
    {
        if (!File.Exists(assemblyPath))
        {
            throw new FileNotFoundException("Assembly file not found.", assemblyPath);
        }

        var runtimeDir = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        var resolver = new PathAssemblyResolver(
            Directory.GetFiles(runtimeDir, "*.dll")
                .Append(assemblyPath));

        using var mlc = new MetadataLoadContext(resolver);
        var assembly = mlc.LoadFromAssemblyPath(assemblyPath);

        var manifest = new ApiManifest
        {
            SchemaVersion = "1.0",
            GeneratedAt = DateTimeOffset.UtcNow,
            SourceHash = ComputeFileHash(assemblyPath),
            Assembly = ExtractIdentity(assembly),
            Types = ExtractTypes(assembly)
                .OrderBy(t => t.StableId, StringComparer.Ordinal)
                .ToList(),
        };

        return manifest;
    }

    private static string ComputeFileHash(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        var hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static AssemblyIdentity ExtractIdentity(Assembly assembly)
    {
        var name = assembly.GetName();
        var token = name.GetPublicKeyToken();

        return new AssemblyIdentity
        {
            Name = name.Name ?? string.Empty,
            Version = name.Version?.ToString() ?? string.Empty,
            Culture = string.IsNullOrEmpty(name.CultureName) ? null : name.CultureName,
            PublicKeyToken = token is { Length: > 0 }
                ? Convert.ToHexString(token).ToLowerInvariant()
                : null,
            TargetFramework = assembly.CustomAttributes
                .Where(a => a.AttributeType.FullName ==
                    "System.Runtime.Versioning.TargetFrameworkAttribute")
                .SelectMany(a => a.ConstructorArguments)
                .Select(a => a.Value?.ToString())
                .FirstOrDefault(),
        };
    }

    private static IEnumerable<ApiTypeNode> ExtractTypes(Assembly assembly)
    {
        Type[] types;
        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            types = ex.Types.Where(t => t is not null).ToArray()!;
        }

        foreach (var type in types)
        {
            if (!type.IsPublic && !type.IsNestedPublic)
                continue;

            yield return ExtractType(type);
        }
    }

    private static ApiTypeNode ExtractType(Type type)
    {
        var stableId = BuildTypeStableId(type);

        var node = new ApiTypeNode
        {
            StableId = stableId,
            FullName = FormatTypeName(type),
            Name = type.Name,
            Namespace = type.Namespace ?? string.Empty,
            Kind = ClassifyType(type),
            BaseType = type.BaseType is { } bt && bt.FullName != "System.Object"
                ? FormatTypeName(bt)
                : null,
            Interfaces = type.GetInterfaces()
                .Select(FormatTypeName)
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToList(),
            GenericParameters = ExtractGenericParameters(type),
            IsSealed = type.IsSealed && !type.IsValueType,
            IsAbstract = type.IsAbstract && !type.IsInterface,
            IsStatic = type.IsAbstract && type.IsSealed,
            Members = ExtractMembers(type, stableId)
                .OrderBy(m => m.StableId, StringComparer.Ordinal)
                .ToList(),
        };

        return node;
    }

    private static string BuildTypeStableId(Type type)
    {
        // For nested types, include declaring type chain to ensure uniqueness.
        if (type.DeclaringType is not null)
        {
            return $"{BuildTypeStableId(type.DeclaringType)}+{type.Name}";
        }

        var ns = type.Namespace ?? string.Empty;
        var name = type.Name;

        return string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
    }

    private static string FormatTypeName(Type type)
    {
        if (type.IsByRef)
            return FormatTypeName(type.GetElementType()!) + "&";

        if (type.IsArray)
        {
            var rank = type.GetArrayRank();
            var suffix = rank == 1 ? "[]" : "[" + new string(',', rank - 1) + "]";
            return FormatTypeName(type.GetElementType()!) + suffix;
        }

        if (type.IsPointer)
            return FormatTypeName(type.GetElementType()!) + "*";

        if (type.IsGenericParameter)
            return type.Name;

        if (type.IsGenericType)
        {
            var def = type.GetGenericTypeDefinition();
            var baseName = (def.FullName ?? def.Name).Split('`')[0];
            var args = type.GetGenericArguments().Select(FormatTypeName);
            return $"{baseName}<{string.Join(", ", args)}>";
        }

        return type.FullName ?? type.Name;
    }

    private static ApiTypeKind ClassifyType(Type type)
    {
        if (type.IsEnum) return ApiTypeKind.Enum;
        if (type.IsInterface) return ApiTypeKind.Interface;

        if (type.BaseType?.FullName == "System.MulticastDelegate")
            return ApiTypeKind.Delegate;

        if (type.IsValueType) return ApiTypeKind.Struct;

        return ApiTypeKind.Class;
    }

    private static List<GenericParameterInfo> ExtractGenericParameters(Type type)
    {
        if (!type.IsGenericTypeDefinition)
            return [];

        return type.GetGenericArguments()
            .Select(g => new GenericParameterInfo
            {
                Name = g.Name,
                Constraints = g.GetGenericParameterConstraints()
                    .Select(FormatTypeName)
                    .OrderBy(c => c, StringComparer.Ordinal)
                    .ToList(),
            })
            .ToList();
    }

    private static List<GenericParameterInfo> ExtractGenericParameters(MethodInfo method)
    {
        if (!method.IsGenericMethodDefinition)
            return [];

        return method.GetGenericArguments()
            .Select(g => new GenericParameterInfo
            {
                Name = g.Name,
                Constraints = g.GetGenericParameterConstraints()
                    .Select(FormatTypeName)
                    .OrderBy(c => c, StringComparer.Ordinal)
                    .ToList(),
            })
            .ToList();
    }

    private static IEnumerable<ApiMemberNode> ExtractMembers(Type type, string typeStableId)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance |
                                   BindingFlags.Static | BindingFlags.DeclaredOnly;

        // Constructors
        foreach (var ctor in type.GetConstructors(flags))
        {
            var paramSig = BuildParameterSignature(ctor.GetParameters());
            yield return new ApiMemberNode
            {
                StableId = $"{typeStableId}..ctor({paramSig})",
                Name = ".ctor",
                Kind = ApiMemberKind.Constructor,
                IsStatic = ctor.IsStatic,
                Parameters = ExtractParameters(ctor.GetParameters()),
            };
        }

        // Methods (excluding property accessors, event accessors, operator overloads handled separately)
        foreach (var method in type.GetMethods(flags))
        {
            if (method.IsSpecialName)
            {
                // Handle operators
                if (method.Name.StartsWith("op_", StringComparison.Ordinal))
                {
                    var paramSig = BuildParameterSignature(method.GetParameters());
                    yield return new ApiMemberNode
                    {
                        StableId = $"{typeStableId}.{method.Name}({paramSig})",
                        Name = method.Name,
                        Kind = ApiMemberKind.Operator,
                        ReturnType = FormatTypeName(method.ReturnType),
                        IsStatic = method.IsStatic,
                        IsVirtual = method.IsVirtual && !method.IsFinal,
                        IsAbstract = method.IsAbstract,
                        Parameters = ExtractParameters(method.GetParameters()),
                        GenericParameters = ExtractGenericParameters(method),
                    };
                }

                continue;
            }

            var mParamSig = BuildParameterSignature(method.GetParameters());
            var genericSuffix = method.IsGenericMethodDefinition
                ? $"`{method.GetGenericArguments().Length}"
                : string.Empty;

            yield return new ApiMemberNode
            {
                StableId = $"{typeStableId}.{method.Name}{genericSuffix}({mParamSig})",
                Name = method.Name,
                Kind = ApiMemberKind.Method,
                ReturnType = FormatTypeName(method.ReturnType),
                IsStatic = method.IsStatic,
                IsVirtual = method.IsVirtual && !method.IsFinal,
                IsAbstract = method.IsAbstract,
                Parameters = ExtractParameters(method.GetParameters()),
                GenericParameters = ExtractGenericParameters(method),
            };
        }

        // Properties
        foreach (var prop in type.GetProperties(flags))
        {
            var indexParams = prop.GetIndexParameters();
            var kind = indexParams.Length > 0 ? ApiMemberKind.Indexer : ApiMemberKind.Property;
            var paramSig = indexParams.Length > 0
                ? BuildParameterSignature(indexParams)
                : string.Empty;

            var getter = prop.GetGetMethod();
            var setter = prop.GetSetMethod();

            yield return new ApiMemberNode
            {
                StableId = kind == ApiMemberKind.Indexer
                    ? $"{typeStableId}.Item[{paramSig}]"
                    : $"{typeStableId}.{prop.Name}",
                Name = prop.Name,
                Kind = kind,
                ReturnType = FormatTypeName(prop.PropertyType),
                IsStatic = (getter?.IsStatic ?? setter?.IsStatic) == true,
                IsVirtual = (getter?.IsVirtual ?? setter?.IsVirtual) == true,
                IsAbstract = (getter?.IsAbstract ?? setter?.IsAbstract) == true,
                HasGetter = getter is not null,
                HasSetter = setter is not null,
                Parameters = kind == ApiMemberKind.Indexer
                    ? ExtractParameters(indexParams)
                    : [],
            };
        }

        // Fields
        foreach (var field in type.GetFields(flags))
        {
            yield return new ApiMemberNode
            {
                StableId = $"{typeStableId}.{field.Name}",
                Name = field.Name,
                Kind = ApiMemberKind.Field,
                ReturnType = FormatTypeName(field.FieldType),
                IsStatic = field.IsStatic,
            };
        }

        // Events
        foreach (var evt in type.GetEvents(flags))
        {
            yield return new ApiMemberNode
            {
                StableId = $"{typeStableId}.{evt.Name}",
                Name = evt.Name,
                Kind = ApiMemberKind.Event,
                ReturnType = evt.EventHandlerType is { } eht ? FormatTypeName(eht) : null,
                IsStatic = evt.AddMethod?.IsStatic == true,
            };
        }
    }

    private static string BuildParameterSignature(ParameterInfo[] parameters)
    {
        return string.Join(", ", parameters.Select(p => FormatTypeName(p.ParameterType)));
    }

    private static List<ApiParameterInfo> ExtractParameters(ParameterInfo[] parameters)
    {
        return parameters.Select(p => new ApiParameterInfo
        {
            Name = p.Name ?? string.Empty,
            Type = FormatTypeName(p.ParameterType),
            IsOptional = p.IsOptional,
            IsParams = p.CustomAttributes.Any(a =>
                a.AttributeType.FullName == "System.ParamArrayAttribute"),
            IsOut = p.IsOut,
            IsRef = p.ParameterType.IsByRef && !p.IsOut,
            DefaultValue = p.HasDefaultValue
                ? p.RawDefaultValue?.ToString()
                : null,
        }).ToList();
    }
}
