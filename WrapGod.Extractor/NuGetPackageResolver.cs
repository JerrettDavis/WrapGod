using NuGet.Common;
using NuGet.Configuration;
using NuGet.Packaging;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace WrapGod.Extractor;

/// <summary>
/// Resolves a NuGet package by downloading the .nupkg and extracting the best-match DLL
/// for a given target framework. Results are cached locally to avoid repeated downloads.
/// </summary>
public sealed class NuGetPackageResolver
{
    /// <summary>Default NuGet v3 feed URL (nuget.org).</summary>
    public const string DefaultSourceFeed = "https://api.nuget.org/v3/index.json";

    /// <summary>
    /// Ordered list of preferred TFMs when auto-selecting the best match.
    /// First match wins.
    /// </summary>
    private static readonly string[] PreferredTfms =
    [
        "net8.0",
        "net7.0",
        "net6.0",
        "netstandard2.1",
        "netstandard2.0",
        "netstandard1.6",
        "net472",
        "net462",
    ];

    private readonly string _cacheRoot;

    /// <summary>
    /// Creates a new resolver with the given local cache root.
    /// Defaults to <c>.wrapgod-cache/packages</c> under the current directory.
    /// </summary>
    public NuGetPackageResolver(string? cacheRoot = null)
    {
        _cacheRoot = cacheRoot ?? Path.Combine(Directory.GetCurrentDirectory(), ".wrapgod-cache", "packages");
    }

    /// <summary>
    /// Resolves a NuGet package to a local DLL path. Downloads and extracts
    /// the package when not already cached.
    /// </summary>
    /// <param name="packageId">NuGet package identifier (e.g. <c>Newtonsoft.Json</c>).</param>
    /// <param name="version">Exact package version (e.g. <c>13.0.3</c>).</param>
    /// <param name="targetFramework">
    /// Explicit TFM to extract (e.g. <c>net8.0</c>). When <c>null</c>, the best
    /// match is auto-selected using <see cref="PreferredTfms"/>.
    /// </param>
    /// <param name="sourceFeed">NuGet v3 feed URL. Defaults to nuget.org.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Absolute path to the extracted DLL inside the local cache.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the package cannot be found, contains no compatible DLLs,
    /// or the specified TFM does not exist in the package.
    /// </exception>
    public async Task<string> ResolveAsync(
        string packageId,
        string version,
        string? targetFramework = null,
        string? sourceFeed = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageId);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);

        var packageDir = GetPackageDirectory(packageId, version);

        // Check if we already have a cached extraction marker.
        var markerPath = Path.Combine(packageDir, ".extracted");
        if (!File.Exists(markerPath))
        {
            await DownloadAndExtractAsync(packageId, version, sourceFeed ?? DefaultSourceFeed, packageDir, cancellationToken);
        }

        return FindDll(packageId, packageDir, targetFramework);
    }

    /// <summary>
    /// Returns the local cache directory for a given package id and version.
    /// </summary>
    public string GetPackageDirectory(string packageId, string version)
        => Path.Combine(_cacheRoot, packageId.ToLowerInvariant(), version);

    private static async Task DownloadAndExtractAsync(
        string packageId,
        string version,
        string sourceFeed,
        string packageDir,
        CancellationToken cancellationToken)
    {
        var cache = new SourceCacheContext();
        var repository = Repository.Factory.GetCoreV3(sourceFeed);
        var resource = await repository.GetResourceAsync<FindPackageByIdResource>(cancellationToken);

        var nugetVersion = new NuGetVersion(version);

        // Verify the package exists.
        var versions = await resource.GetAllVersionsAsync(packageId, cache, NullLogger.Instance, cancellationToken);
        if (!versions.Contains(nugetVersion))
        {
            throw new InvalidOperationException(
                $"Package '{packageId}' version '{version}' was not found on feed '{sourceFeed}'.");
        }

        Directory.CreateDirectory(packageDir);

        var nupkgPath = Path.Combine(packageDir, $"{packageId}.{version}.nupkg");

        // Download the .nupkg.
        using (var fileStream = File.Create(nupkgPath))
        {
            var downloaded = await resource.CopyNupkgToStreamAsync(
                packageId, nugetVersion, fileStream, cache, NullLogger.Instance, cancellationToken);

            if (!downloaded)
            {
                throw new InvalidOperationException(
                    $"Failed to download package '{packageId}' version '{version}'.");
            }
        }

        // Extract lib/ DLLs from the nupkg.
        using var packageReader = new PackageArchiveReader(nupkgPath);
        var libItems = (await packageReader.GetLibItemsAsync(cancellationToken)).ToList();

        foreach (var group in libItems)
        {
            var tfm = group.TargetFramework.GetShortFolderName();
            var tfmDir = Path.Combine(packageDir, "lib", tfm);
            Directory.CreateDirectory(tfmDir);

            foreach (var item in group.Items)
            {
                var fileName = Path.GetFileName(item);
                if (!fileName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                    continue;

                using var entryStream = await packageReader.GetStreamAsync(item, cancellationToken);
                var targetPath = Path.Combine(tfmDir, fileName);
                using var output = File.Create(targetPath);
                await entryStream.CopyToAsync(output, cancellationToken);
            }
        }

        // Write extraction marker.
        await File.WriteAllTextAsync(Path.Combine(packageDir, ".extracted"), DateTimeOffset.UtcNow.ToString("O"), cancellationToken);
    }

    private static string FindDll(string packageId, string packageDir, string? targetFramework)
    {
        var libDir = Path.Combine(packageDir, "lib");

        if (!Directory.Exists(libDir))
        {
            throw new InvalidOperationException(
                $"Package '{packageId}' does not contain a lib/ folder with any DLLs.");
        }

        var availableTfms = Directory.GetDirectories(libDir)
            .Select(Path.GetFileName)
            .Where(d => d is not null)
            .Cast<string>()
            .ToList();

        if (availableTfms.Count == 0)
        {
            throw new InvalidOperationException(
                $"Package '{packageId}' lib/ folder contains no TFM subfolders.");
        }

        string chosenTfm;

        if (targetFramework is not null)
        {
            // Exact match required.
            chosenTfm = availableTfms.FirstOrDefault(
                t => string.Equals(t, targetFramework, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException(
                    $"Package '{packageId}' does not contain TFM '{targetFramework}'. " +
                    $"Available: {string.Join(", ", availableTfms)}");
        }
        else
        {
            // Auto-select best match from preference order.
            chosenTfm = PreferredTfms
                .FirstOrDefault(pref => availableTfms.Any(
                    t => string.Equals(t, pref, StringComparison.OrdinalIgnoreCase)))
                ?? availableTfms[0]; // Fallback to first available.
        }

        var tfmPath = Path.Combine(libDir, chosenTfm);
        var dlls = Directory.GetFiles(tfmPath, "*.dll");

        if (dlls.Length == 0)
        {
            throw new InvalidOperationException(
                $"No DLLs found in '{tfmPath}' for package '{packageId}'.");
        }

        // Prefer the DLL matching the package name, otherwise return the first.
        var primaryDll = dlls.FirstOrDefault(
            d => Path.GetFileNameWithoutExtension(d)
                .Equals(packageId, StringComparison.OrdinalIgnoreCase))
            ?? dlls[0];

        return primaryDll;
    }
}
