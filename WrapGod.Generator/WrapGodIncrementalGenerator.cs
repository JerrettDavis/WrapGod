using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace WrapGod.Generator;

[Generator]
public sealed class WrapGodIncrementalGenerator : IIncrementalGenerator
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Step 1a: Filter AdditionalFiles to *.wrapgod.json manifests
        //          (but NOT *.wrapgod.config.json -- those are config files)
        var manifestTexts = context.AdditionalTextsProvider
            .Where(static file =>
                file.Path.EndsWith(".wrapgod.json", StringComparison.OrdinalIgnoreCase)
                && !file.Path.EndsWith(".wrapgod.config.json", StringComparison.OrdinalIgnoreCase))
            .Select(static (file, ct) => file.GetText(ct)?.ToString() ?? string.Empty)
            .Where(static text => text.Length > 0);

        // Step 1b: Filter AdditionalFiles to *.wrapgod.config.json config files
        var configTexts = context.AdditionalTextsProvider
            .Where(static file => file.Path.EndsWith(".wrapgod.config.json", StringComparison.OrdinalIgnoreCase))
            .Select(static (file, ct) => file.GetText(ct)?.ToString() ?? string.Empty)
            .Where(static text => text.Length > 0);

        // Step 2: Parse each manifest into a GenerationPlan (cached via Equals)
        var plans = manifestTexts
            .Select(static (text, _) => ParseManifest(text))
            .Where(static plan => plan is not null)
            .Select(static (plan, _) => plan!);

        // Step 3: Parse config files into ConfigPlan models
        var configs = configTexts
            .Select(static (text, _) => ParseConfig(text))
            .Where(static config => config is not null)
            .Select(static (config, _) => config!);

        // Step 4: Collect plans + configs and apply config rules before emission
        var collectedPlans = plans.Collect();
        var collectedConfigs = configs.Collect();

        var combined = collectedPlans.Combine(collectedConfigs);

        context.RegisterSourceOutput(combined, static (spc, pair) =>
        {
            var filteredPlans = ApplyConfig(pair.Left, pair.Right);
            EmitSources(spc, filteredPlans);
        });
    }

    /// <summary>
    /// Parses a wrapgod.json manifest into a <see cref="GenerationPlan"/>.
    /// Returns null if the JSON is invalid or contains no types.
    /// </summary>
    internal static GenerationPlan? ParseManifest(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            string assemblyName = "Unknown";
            if (root.TryGetProperty("assembly", out var assemblyEl) &&
                assemblyEl.TryGetProperty("name", out var nameEl))
            {
                assemblyName = nameEl.GetString() ?? "Unknown";
            }

            var types = new List<TypePlan>();

            if (root.TryGetProperty("types", out var typesEl) && typesEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var typeEl in typesEl.EnumerateArray())
                {
                    var typePlan = ParseTypePlan(typeEl);
                    if (typePlan is not null)
                    {
                        types.Add(typePlan);
                    }
                }
            }

            return new GenerationPlan(assemblyName, types);
        }
#pragma warning disable CA1031 // Do not catch general exception types -- generator must not crash
        catch
#pragma warning restore CA1031
        {
            return null;
        }
    }

    /// <summary>
    /// Parses a <c>*.wrapgod.config.json</c> file into a lightweight <see cref="ConfigPlan"/>.
    /// Returns null if the JSON is invalid.
    /// </summary>
    internal static ConfigPlan? ParseConfig(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var types = new List<ConfigTypePlan>();

            if (root.TryGetProperty("types", out var typesEl) && typesEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var typeEl in typesEl.EnumerateArray())
                {
                    string sourceType = GetStringProperty(typeEl, "sourceType");
                    if (string.IsNullOrEmpty(sourceType))
                        continue;

                    bool include = true;
                    if (typeEl.TryGetProperty("include", out var inclEl) &&
                        (inclEl.ValueKind == JsonValueKind.True || inclEl.ValueKind == JsonValueKind.False))
                    {
                        include = inclEl.GetBoolean();
                    }

                    string? targetName = null;
                    if (typeEl.TryGetProperty("targetName", out var tnEl) && tnEl.ValueKind == JsonValueKind.String)
                    {
                        targetName = tnEl.GetString();
                    }

                    var members = new List<ConfigMemberPlan>();
                    if (typeEl.TryGetProperty("members", out var membersEl) && membersEl.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var memberEl in membersEl.EnumerateArray())
                        {
                            string sourceMember = GetStringProperty(memberEl, "sourceMember");
                            if (string.IsNullOrEmpty(sourceMember))
                                continue;

                            bool memberInclude = true;
                            if (memberEl.TryGetProperty("include", out var mInclEl) &&
                                (mInclEl.ValueKind == JsonValueKind.True || mInclEl.ValueKind == JsonValueKind.False))
                            {
                                memberInclude = mInclEl.GetBoolean();
                            }

                            string? memberTargetName = null;
                            if (memberEl.TryGetProperty("targetName", out var mTnEl) && mTnEl.ValueKind == JsonValueKind.String)
                            {
                                memberTargetName = mTnEl.GetString();
                            }

                            members.Add(new ConfigMemberPlan(sourceMember, memberInclude, memberTargetName));
                        }
                    }

                    types.Add(new ConfigTypePlan(sourceType, include, targetName, members));
                }
            }

            return new ConfigPlan(types);
        }
#pragma warning disable CA1031 // Do not catch general exception types -- generator must not crash
        catch
#pragma warning restore CA1031
        {
            return null;
        }
    }

    /// <summary>
    /// Applies all config rules from every loaded config file to the generation plans.
    /// Types excluded by config are removed; surviving types/members get their target names applied.
    /// </summary>
    internal static ImmutableArray<GenerationPlan> ApplyConfig(
        ImmutableArray<GenerationPlan> plans,
        ImmutableArray<ConfigPlan> configs)
    {
        if (configs.Length == 0)
            return plans;

        // Merge all config files into a single lookup by source type (full name).
        // Later config files override earlier ones for the same key.
        var typeRules = new Dictionary<string, ConfigTypePlan>(StringComparer.Ordinal);
        foreach (var config in configs)
        {
            foreach (var typeRule in config.Types)
            {
                typeRules[typeRule.SourceType] = typeRule;
            }
        }

        var result = ImmutableArray.CreateBuilder<GenerationPlan>(plans.Length);

        foreach (var plan in plans)
        {
            var filteredTypes = new List<TypePlan>();

            foreach (var type in plan.Types)
            {
                if (typeRules.TryGetValue(type.FullName, out var rule))
                {
                    // Type excluded
                    if (!rule.Include)
                        continue;

                    // Build member lookup
                    var memberRules = new Dictionary<string, ConfigMemberPlan>(StringComparer.Ordinal);
                    foreach (var mr in rule.Members)
                    {
                        memberRules[mr.SourceMember] = mr;
                    }

                    // Filter and rename members
                    var filteredMembers = new List<MemberPlan>();
                    foreach (var member in type.Members)
                    {
                        if (memberRules.TryGetValue(member.Name, out var memberRule))
                        {
                            if (!memberRule.Include)
                                continue;

                            // Apply member rename
                            if (!string.IsNullOrEmpty(memberRule.TargetName))
                            {
                                filteredMembers.Add(new MemberPlan(
                                    member.Name,
                                    member.Kind,
                                    member.ReturnType,
                                    member.Parameters,
                                    member.HasGetter,
                                    member.HasSetter,
                                    member.IsStatic,
                                    member.GenericParameters,
                                    targetName: memberRule.TargetName,
                                    introducedIn: member.IntroducedIn,
                                    removedIn: member.RemovedIn));
                            }
                            else
                            {
                                filteredMembers.Add(member);
                            }
                        }
                        else
                        {
                            filteredMembers.Add(member);
                        }
                    }

                    // Apply type rename
                    filteredTypes.Add(new TypePlan(
                        type.FullName,
                        type.Name,
                        type.Namespace,
                        filteredMembers,
                        targetName: !string.IsNullOrEmpty(rule.TargetName) ? rule.TargetName : type.TargetName,
                        introducedIn: type.IntroducedIn,
                        removedIn: type.RemovedIn));
                }
                else
                {
                    // No config rule for this type -- include as-is
                    filteredTypes.Add(type);
                }
            }

            result.Add(new GenerationPlan(plan.AssemblyName, filteredTypes));
        }

        return result.ToImmutable();
    }

    private static TypePlan? ParseTypePlan(JsonElement typeEl)
    {
        string fullName = GetStringProperty(typeEl, "fullName");
        string name = GetStringProperty(typeEl, "name");
        string ns = GetStringProperty(typeEl, "namespace");

        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        var members = new List<MemberPlan>();

        if (typeEl.TryGetProperty("members", out var membersEl) && membersEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var memberEl in membersEl.EnumerateArray())
            {
                var memberPlan = ParseMemberPlan(memberEl);
                if (memberPlan is not null)
                {
                    members.Add(memberPlan);
                }
            }
        }

        return new TypePlan(fullName, name, ns, members);
    }

    private static MemberPlan? ParseMemberPlan(JsonElement memberEl)
    {
        string name = GetStringProperty(memberEl, "name");
        string kind = GetStringProperty(memberEl, "kind");
        string returnType = GetStringProperty(memberEl, "returnType", "void");

        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        bool hasGetter = GetBoolProperty(memberEl, "hasGetter");
        bool hasSetter = GetBoolProperty(memberEl, "hasSetter");
        bool isStatic = GetBoolProperty(memberEl, "isStatic");

        var genericParameters = new List<string>();
        if (memberEl.TryGetProperty("genericParameters", out var gpEl) && gpEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var gp in gpEl.EnumerateArray())
            {
                string gpName = GetStringProperty(gp, "name");
                if (!string.IsNullOrEmpty(gpName))
                {
                    genericParameters.Add(gpName);
                }
            }
        }

        var parameters = new List<ParameterPlan>();

        if (memberEl.TryGetProperty("parameters", out var paramsEl) && paramsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var paramEl in paramsEl.EnumerateArray())
            {
                string paramName = GetStringProperty(paramEl, "name");
                string paramType = GetStringProperty(paramEl, "type");
                if (!string.IsNullOrEmpty(paramName) && !string.IsNullOrEmpty(paramType))
                {
                    string modifier = "";
                    if (GetBoolProperty(paramEl, "isOut"))
                        modifier = "out";
                    else if (GetBoolProperty(paramEl, "isRef"))
                        modifier = "ref";
                    else if (GetBoolProperty(paramEl, "isParams"))
                        modifier = "params";

                    parameters.Add(new ParameterPlan(paramName, paramType, modifier));
                }
            }
        }

        return new MemberPlan(name, kind, returnType, parameters, hasGetter, hasSetter, isStatic, genericParameters);
    }

    private static string GetStringProperty(JsonElement el, string name, string defaultValue = "")
    {
        return el.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String
            ? prop.GetString() ?? defaultValue
            : defaultValue;
    }

    private static bool GetBoolProperty(JsonElement el, string name)
    {
        return el.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.True;
    }

    private static void EmitSources(SourceProductionContext spc, ImmutableArray<GenerationPlan> plans)
    {
        foreach (var plan in plans)
        {
            foreach (var type in plan.Types)
            {
                string interfaceSource = SourceEmitter.EmitInterface(type);
                spc.AddSource(
                    "IWrapped" + type.EffectiveName + ".g.cs",
                    SourceText.From(interfaceSource, Encoding.UTF8));

                string facadeSource = SourceEmitter.EmitFacade(type);
                spc.AddSource(
                    type.EffectiveName + "Facade.g.cs",
                    SourceText.From(facadeSource, Encoding.UTF8));
            }
        }
    }
}
