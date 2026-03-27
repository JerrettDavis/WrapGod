using System;
using System.Linq;
using System.Reflection;
using WrapGod.Abstractions.Config;

namespace WrapGod.Manifest.Config;

public static class AttributeConfigReader
{
    public static WrapGodConfig ReadFromAssembly(Assembly assembly)
    {
        var config = new WrapGodConfig();

        var wrappedTypes = assembly
            .GetTypes()
            .Where(t => t.GetCustomAttribute<WrapTypeAttribute>() is not null);

        foreach (var type in wrappedTypes)
        {
            var typeAttribute = type.GetCustomAttribute<WrapTypeAttribute>()!;
            var typeConfig = new TypeConfig
            {
                SourceType = typeAttribute.SourceType,
                Include = typeAttribute.Include,
                TargetName = typeAttribute.TargetName,
            };

            var members = type.GetMembers(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (var member in members)
            {
                var memberAttribute = member.GetCustomAttribute<WrapMemberAttribute>();
                if (memberAttribute is null)
                {
                    continue;
                }

                typeConfig.Members.Add(new MemberConfig
                {
                    SourceMember = memberAttribute.SourceMember,
                    Include = memberAttribute.Include,
                    TargetName = memberAttribute.TargetName,
                });
            }

            config.Types.Add(typeConfig);
        }

        return config;
    }
}
