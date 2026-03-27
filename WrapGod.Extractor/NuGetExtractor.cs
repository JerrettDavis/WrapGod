using WrapGod.Manifest;

namespace WrapGod.Extractor;

/// <summary>
/// High-level extractor that resolves a NuGet package, downloads it if needed,
/// and delegates to <see cref="AssemblyExtractor"/> to produce an <see cref="ApiManifest"/>.
/// </summary>
public sealed class NuGetExtractor
{
    private readonly NuGetPackageResolver _resolver;

    /// <summary>
    /// Creates a new NuGet extractor using the given resolver.
    /// When <c>null</c>, a default resolver is created.
    /// </summary>
    public NuGetExtractor(NuGetPackageResolver? resolver = null)
    {
        _resolver = resolver ?? new NuGetPackageResolver();
    }

    /// <summary>
    /// Resolves a NuGet package, downloads it, and extracts an <see cref="ApiManifest"/>
    /// from its primary DLL.
    /// </summary>
    /// <param name="packageId">NuGet package identifier.</param>
    /// <param name="version">Exact package version.</param>
    /// <param name="targetFramework">Optional explicit TFM override.</param>
    /// <param name="sourceFeed">Optional private feed URL.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An <see cref="ApiManifest"/> extracted from the package assembly.</returns>
    public async Task<ApiManifest> ExtractFromPackageAsync(
        string packageId,
        string version,
        string? targetFramework = null,
        string? sourceFeed = null,
        CancellationToken cancellationToken = default)
    {
        var dllPath = await _resolver.ResolveAsync(
            packageId, version, targetFramework, sourceFeed, cancellationToken);

        return AssemblyExtractor.Extract(dllPath);
    }

    /// <summary>
    /// Result of a multi-version NuGet extraction.
    /// </summary>
    /// <param name="MergedManifest">The merged manifest with version presence metadata.</param>
    /// <param name="Diff">Compatibility diff report across versions.</param>
    public sealed record MultiVersionResult(ApiManifest MergedManifest, VersionDiff Diff);

    /// <summary>
    /// Extracts multiple versions of the same NuGet package and produces a merged manifest
    /// with version presence metadata and a compatibility diff.
    /// </summary>
    /// <param name="packageId">NuGet package identifier.</param>
    /// <param name="versions">List of version strings to extract and compare.</param>
    /// <param name="targetFramework">Optional explicit TFM override (applied to all versions).</param>
    /// <param name="sourceFeed">Optional private feed URL.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Merged manifest and diff report.</returns>
    public async Task<MultiVersionResult> ExtractMultiVersionAsync(
        string packageId,
        IReadOnlyList<string> versions,
        string? targetFramework = null,
        string? sourceFeed = null,
        CancellationToken cancellationToken = default)
    {
        if (versions.Count == 0)
            throw new ArgumentException("At least one version is required.", nameof(versions));

        var versionInputs = new List<MultiVersionExtractor.VersionInput>();

        foreach (var version in versions)
        {
            var dllPath = await _resolver.ResolveAsync(
                packageId, version, targetFramework, sourceFeed, cancellationToken);

            versionInputs.Add(new MultiVersionExtractor.VersionInput(version, dllPath));
        }

        var result = MultiVersionExtractor.Extract(versionInputs);
        return new MultiVersionResult(result.MergedManifest, result.Diff);
    }
}
