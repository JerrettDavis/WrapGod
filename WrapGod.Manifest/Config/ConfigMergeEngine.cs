using System;
using System.Collections.Generic;
using System.Linq;
using WrapGod.Abstractions.Config;

namespace WrapGod.Manifest.Config;

public static class ConfigMergeEngine
{
    public static ConfigMergeResult Merge(WrapGodConfig jsonConfig, WrapGodConfig attributeConfig, ConfigMergeOptions? options = null)
    {
        options ??= new ConfigMergeOptions();
        var result = new ConfigMergeResult();

        var allTypeKeys = jsonConfig.Types.Select(t => t.SourceType)
            .Concat(attributeConfig.Types.Select(t => t.SourceType))
            .Distinct(StringComparer.Ordinal);

        foreach (var typeKey in allTypeKeys)
        {
            var fromJson = FindMatchingType(jsonConfig.Types, typeKey);
            var fromAttr = FindMatchingType(attributeConfig.Types, typeKey);

            var mergedType = MergeType(typeKey, fromJson, fromAttr, options, result.Diagnostics);

            // Preserve generic pattern metadata from whichever source had it.
            var sourceWithPattern = fromJson?.IsGenericPattern == true ? fromJson
                : fromAttr?.IsGenericPattern == true ? fromAttr
                : null;
            if (sourceWithPattern != null)
            {
                mergedType.IsGenericPattern = true;
                mergedType.GenericArity = sourceWithPattern.GenericArity;
            }

            result.Config.Types.Add(mergedType);
        }

        return result;
    }

    /// <summary>
    /// Finds a type config matching the given key. First tries an exact match,
    /// then falls back to matching generic patterns by base name.
    /// </summary>
    private static TypeConfig? FindMatchingType(List<TypeConfig> types, string key)
    {
        // Exact match first.
        var exact = types.FirstOrDefault(t => string.Equals(t.SourceType, key, StringComparison.Ordinal));
        if (exact != null)
            return exact;

        // Try matching open generic patterns by base name.
        var baseName = GetGenericBaseName(key);
        if (baseName != null)
        {
            return types.FirstOrDefault(t =>
                t.IsGenericPattern &&
                string.Equals(GetGenericBaseName(t.SourceType) ?? t.SourceType, baseName, StringComparison.Ordinal));
        }

        return null;
    }

    /// <summary>
    /// Extracts the base type name from a generic type string.
    /// <c>"Dictionary&lt;,&gt;"</c> becomes <c>"Dictionary"</c>.
    /// Returns <c>null</c> for non-generic types.
    /// </summary>
    internal static string? GetGenericBaseName(string sourceType)
    {
        var openAngle = sourceType.IndexOf('<');
        return openAngle > 0 ? sourceType.Substring(0, openAngle) : null;
    }

    private static TypeConfig MergeType(
        string key,
        TypeConfig? fromJson,
        TypeConfig? fromAttr,
        ConfigMergeOptions options,
        List<ConfigDiagnostic> diagnostics)
    {
        var merged = new TypeConfig { SourceType = key };

        merged.Include = ResolveBool(
            fromJson?.Include,
            fromAttr?.Include,
            options,
            diagnostics,
            "WG6001",
            $"Type include conflict for '{key}'.");

        merged.TargetName = ResolveString(
            fromJson?.TargetName,
            fromAttr?.TargetName,
            options,
            diagnostics,
            "WG6002",
            $"Type rename conflict for '{key}'.");

        var allMemberKeys = (fromJson?.Members.Select(m => m.SourceMember) ?? Enumerable.Empty<string>())
            .Concat(fromAttr?.Members.Select(m => m.SourceMember) ?? Enumerable.Empty<string>())
            .Distinct(StringComparer.Ordinal);

        foreach (var memberKey in allMemberKeys)
        {
            var jsonMember = fromJson?.Members.FirstOrDefault(m => string.Equals(m.SourceMember, memberKey, StringComparison.Ordinal));
            var attrMember = fromAttr?.Members.FirstOrDefault(m => string.Equals(m.SourceMember, memberKey, StringComparison.Ordinal));

            var mergedMember = new MemberConfig
            {
                SourceMember = memberKey,
                Include = ResolveBool(
                    jsonMember?.Include,
                    attrMember?.Include,
                    options,
                    diagnostics,
                    "WG6003",
                    $"Member include conflict for '{key}.{memberKey}'."),
                TargetName = ResolveString(
                    jsonMember?.TargetName,
                    attrMember?.TargetName,
                    options,
                    diagnostics,
                    "WG6004",
                    $"Member rename conflict for '{key}.{memberKey}'."),
            };

            merged.Members.Add(mergedMember);
        }

        return merged;
    }

    private static bool? ResolveBool(
        bool? json,
        bool? attributes,
        ConfigMergeOptions options,
        List<ConfigDiagnostic> diagnostics,
        string conflictCode,
        string conflictMessage)
    {
        if (json.HasValue && attributes.HasValue && json.Value != attributes.Value)
        {
            diagnostics.Add(new ConfigDiagnostic { Code = conflictCode, Message = conflictMessage });
        }

        return options.HigherPrecedence == ConfigSource.Attributes
            ? attributes ?? json
            : json ?? attributes;
    }

    private static string? ResolveString(
        string? json,
        string? attributes,
        ConfigMergeOptions options,
        List<ConfigDiagnostic> diagnostics,
        string conflictCode,
        string conflictMessage)
    {
        if (!string.IsNullOrWhiteSpace(json)
            && !string.IsNullOrWhiteSpace(attributes)
            && !string.Equals(json, attributes, StringComparison.Ordinal))
        {
            diagnostics.Add(new ConfigDiagnostic { Code = conflictCode, Message = conflictMessage });
        }

        return options.HigherPrecedence == ConfigSource.Attributes
            ? attributes ?? json
            : json ?? attributes;
    }
}
