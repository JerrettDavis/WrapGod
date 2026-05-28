using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;

namespace WrapGod.Cli.Globbing;

/// <summary>
/// Wraps <see cref="Matcher"/> to resolve include/exclude glob patterns against a
/// project directory and return matching absolute file paths.
/// </summary>
internal static class FileMatcherHelper
{
    /// <summary>
    /// Default include patterns applied when no explicit <c>--include</c> globs are given.
    /// </summary>
    internal static readonly IReadOnlyList<string> DefaultIncludes = ["**/*.cs"];

    /// <summary>
    /// Default exclude patterns applied when no explicit <c>--exclude</c> globs are given.
    /// </summary>
    internal static readonly IReadOnlyList<string> DefaultExcludes =
    [
        "**/bin/**",
        "**/obj/**",
        "**/.wrapgod/**",
    ];

    /// <summary>
    /// Enumerates all files under <paramref name="projectDir"/> that satisfy
    /// <paramref name="includes"/> after subtracting <paramref name="excludes"/>.
    /// </summary>
    /// <param name="projectDir">Root directory for the glob walk.</param>
    /// <param name="includes">
    /// Include patterns (e.g. <c>**/*.cs</c>). When empty, <see cref="DefaultIncludes"/>
    /// is used.
    /// </param>
    /// <param name="excludes">
    /// Exclude patterns (e.g. <c>**/bin/**</c>). When empty, <see cref="DefaultExcludes"/>
    /// is used.
    /// </param>
    /// <returns>Absolute paths of matched files, sorted for deterministic ordering.</returns>
    public static IReadOnlyList<string> GetMatchingFiles(
        string projectDir,
        IEnumerable<string> includes,
        IEnumerable<string> excludes)
    {
        ArgumentNullException.ThrowIfNull(projectDir);

        var includeList = includes?.ToList() ?? [];
        var excludeList = excludes?.ToList() ?? [];

        if (includeList.Count == 0) includeList = [.. DefaultIncludes];
        if (excludeList.Count == 0) excludeList = [.. DefaultExcludes];

        var matcher = new Matcher(StringComparison.OrdinalIgnoreCase);
        foreach (var pattern in includeList)
            matcher.AddInclude(pattern);
        foreach (var pattern in excludeList)
            matcher.AddExclude(pattern);

        var dirWrapper = new DirectoryInfoWrapper(new DirectoryInfo(projectDir));
        var result = matcher.Execute(dirWrapper);

        return result.Files
            .Select(f => Path.GetFullPath(Path.Combine(projectDir, f.Path)))
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
