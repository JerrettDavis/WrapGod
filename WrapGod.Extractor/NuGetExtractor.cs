using WrapGod.Manifest;

namespace WrapGod.Extractor;

public sealed class NuGetExtractor
{
    private readonly NuGetPackageResolver _resolver;

    public NuGetExtractor(NuGetPackageResolver? resolver = null)
    {
        _resolver = resolver ?? new NuGetPackageResolver();
    }

    public sealed record LockedExtractionResult(ApiManifest Manifest, NuGetPackageResolver.ResolutionResult Resolution);

    public async Task<ApiManifest> ExtractFromPackageAsync(string packageId, string version, string? targetFramework = null, string? sourceFeed = null, CancellationToken cancellationToken = default)
    {
        var dllPath = await _resolver.ResolveAsync(packageId, version, targetFramework, sourceFeed, cancellationToken);
        return AssemblyExtractor.Extract(dllPath);
    }

    public async Task<LockedExtractionResult> ExtractFromPackageWithLockAsync(string packageId, string version, string? targetFramework = null, string? sourceFeed = null, CancellationToken cancellationToken = default)
    {
        var resolution = await _resolver.ResolveWithMetadataAsync(packageId, version, targetFramework, sourceFeed, cancellationToken);
        var manifest = AssemblyExtractor.Extract(resolution.DllPath);
        return new LockedExtractionResult(manifest, resolution);
    }

    public async Task<ApiManifest> ExtractFromLockAsync(LockSourceEntry lockEntry, CancellationToken cancellationToken = default)
    {
        var dllPath = await _resolver.ResolveFromLockAsync(
            lockEntry.PackageId,
            lockEntry.Version,
            lockEntry.SourceFeed,
            lockEntry.PackageSha256,
            lockEntry.TargetFramework,
            lockEntry.DllRelativePath,
            lockEntry.DllSha256,
            cancellationToken);

        return AssemblyExtractor.Extract(dllPath);
    }

    public sealed record MultiVersionResult(ApiManifest MergedManifest, VersionDiff Diff);

    public async Task<MultiVersionResult> ExtractMultiVersionAsync(string packageId, IReadOnlyList<string> versions, string? targetFramework = null, string? sourceFeed = null, CancellationToken cancellationToken = default)
    {
        if (versions.Count == 0)
            throw new ArgumentException("At least one version is required.", nameof(versions));

        var versionInputs = new List<MultiVersionExtractor.VersionInput>();
        foreach (var version in versions)
        {
            var dllPath = await _resolver.ResolveAsync(packageId, version, targetFramework, sourceFeed, cancellationToken);
            versionInputs.Add(new MultiVersionExtractor.VersionInput(version, dllPath));
        }

        var result = MultiVersionExtractor.Extract(versionInputs);
        return new MultiVersionResult(result.MergedManifest, result.Diff);
    }
}
