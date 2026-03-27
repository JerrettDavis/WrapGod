using System;
using System.Collections.Generic;
using System.Collections.Immutable;
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
        // Step 1: Filter AdditionalFiles to *.wrapgod.json manifests
        var manifestTexts = context.AdditionalTextsProvider
            .Where(static file => file.Path.EndsWith(".wrapgod.json", StringComparison.OrdinalIgnoreCase))
            .Select(static (file, ct) => file.GetText(ct)?.ToString() ?? string.Empty)
            .Where(static text => text.Length > 0);

        // Step 2: Parse each manifest into a GenerationPlan (cached via Equals)
        var plans = manifestTexts
            .Select(static (text, _) => ParseManifest(text))
            .Where(static plan => plan is not null)
            .Select(static (plan, _) => plan!);

        // Step 3: Collect all plans and register output
        var collectedPlans = plans.Collect();

        context.RegisterSourceOutput(collectedPlans, static (spc, plans) =>
        {
            EmitSources(spc, plans);
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

        var parameters = new List<ParameterPlan>();

        if (memberEl.TryGetProperty("parameters", out var paramsEl) && paramsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var paramEl in paramsEl.EnumerateArray())
            {
                string paramName = GetStringProperty(paramEl, "name");
                string paramType = GetStringProperty(paramEl, "type");
                if (!string.IsNullOrEmpty(paramName) && !string.IsNullOrEmpty(paramType))
                {
                    parameters.Add(new ParameterPlan(paramName, paramType));
                }
            }
        }

        return new MemberPlan(name, kind, returnType, parameters, hasGetter, hasSetter);
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
                    "IWrapped" + type.Name + ".g.cs",
                    SourceText.From(interfaceSource, Encoding.UTF8));

                string facadeSource = SourceEmitter.EmitFacade(type);
                spc.AddSource(
                    type.Name + "Facade.g.cs",
                    SourceText.From(facadeSource, Encoding.UTF8));
            }
        }
    }
}
