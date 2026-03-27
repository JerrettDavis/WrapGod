using System.Reflection;
using System.Text.Json;

namespace WrapGod.TypeMap;

public static class TypeMappingPlanBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static TypeMappingPlan FromJson(string json)
    {
        var plan = JsonSerializer.Deserialize<TypeMappingPlan>(json, JsonOptions);

        return plan ?? new TypeMappingPlan();
    }

    public static TypeMappingPlan FromAssemblyAttributes(Assembly assembly)
    {
        var plan = new TypeMappingPlan();

        foreach (var type in assembly.GetTypes())
        {
            foreach (var typeMap in type.GetCustomAttributes<MapTypeAttribute>())
            {
                var map = new TypeMapDefinition
                {
                    SourceType = typeMap.SourceType,
                    DestinationType = typeMap.DestinationType,
                    Bidirectional = typeMap.Bidirectional,
                };

                foreach (var member in type.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
                {
                    foreach (var memberMap in member.GetCustomAttributes<MapMemberAttribute>())
                    {
                        map.Members.Add(new MemberMapDefinition
                        {
                            SourceMember = memberMap.SourceMember,
                            DestinationMember = memberMap.DestinationMember,
                            Converter = memberMap.Converter,
                        });
                    }
                }

                plan.Mappings.Add(map);
            }
        }

        return plan;
    }
}
