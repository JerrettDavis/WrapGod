using System.Collections.Generic;
using System.Linq;
using WrapGod.Abstractions.Config;

namespace WrapGod.Manifest.Config;

/// <summary>
/// Convention-first source discovery for zero-config flows.
/// Discovery order: WrapGodPackage -&gt; package refs -&gt; @self -&gt; explicit.
/// </summary>
public static class SourceDiscoveryEngine
{
    public static SourceDiscoveryResult Discover(SourceDiscoveryInput input)
    {
        if (!string.IsNullOrWhiteSpace(input.WrapGodPackage))
        {
            return new SourceDiscoveryResult
            {
                Source = input.WrapGodPackage,
                Strategy = "WrapGodPackage"
            };
        }

        var packageRef = input.PackageReferences
            .FirstOrDefault(static p => !string.IsNullOrWhiteSpace(p));

        if (!string.IsNullOrWhiteSpace(packageRef))
        {
            return new SourceDiscoveryResult
            {
                Source = packageRef,
                Strategy = "PackageReference"
            };
        }

        if (input.HasSelfSource)
        {
            return new SourceDiscoveryResult
            {
                Source = "@self",
                Strategy = "@self"
            };
        }

        if (!string.IsNullOrWhiteSpace(input.ExplicitSource))
        {
            return new SourceDiscoveryResult
            {
                Source = input.ExplicitSource,
                Strategy = "Explicit"
            };
        }

        return new SourceDiscoveryResult
        {
            Strategy = "None",
            Diagnostics = new List<ConfigDiagnostic>
            {
                new()
                {
                    Code = "WG6011",
                    Target = "source.discovery",
                    Message = "No source was discovered. Set WrapGodPackage, add a package reference, annotate a wrapper with [WrapType(\"@self\")], or provide an explicit source."
                }
            }
        };
    }
}
