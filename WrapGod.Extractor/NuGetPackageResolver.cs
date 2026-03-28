using System.Security.Cryptography;
using NuGet.Common;
using NuGet.Packaging;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace WrapGod.Extractor;

public sealed class NuGetPackageResolver
{
    public sealed record ResolutionResult(
        string DllPath,
        string PackageId,
        string Version,
        string SourceFeed,
        string PackageSha256,
        string TargetFramework,
        string DllRelativePath,
        string DllSha256);

    public const string DefaultSourceFeed = "https://api.nuget.org/v3/index.json";

    private static readonly string[] PreferredTfms =
    [
        "net8.0", "net7.0", "net6.0", "netstandard2.1", "netstandard2.0", "netstandard1.6", "net472", "net462"
    ];

    private readonly string _cacheRoot;

    public NuGetPackageResolver(string? cacheRoot = null)
    {
        _cacheRoot = cacheRoot ?? Path.Combine(Directory.GetCurrentDirectory(), ".wrapgod-cache", "packages");
    }

    public async Task<string> ResolveAsync(string packageId, string version, string? targetFramework = null, string? sourceFeed = null, CancellationToken cancellationToken = default)
        => (await ResolveWithMetadataAsync(packageId, version, targetFramework, sourceFeed, cancellationToken)).DllPath;

    public async Task<ResolutionResult> ResolveWithMetadataAsync(string packageId, string version, string? targetFramework = null, string? sourceFeed = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageId);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);

        var feed = sourceFeed ?? DefaultSourceFeed;
        var packageDir = GetPackageDirectory(packageId, version);
        var markerPath = Path.Combine(packageDir, ".extracted");
        if (!File.Exists(markerPath))
        {
            await DownloadAndExtractAsync(packageId, version, feed, packageDir, cancellationToken);
        }

        var selection = FindDllSelection(packageId, packageDir, targetFramework);
        var nupkgPath = Path.Combine(packageDir, $"{packageId}.{version}.nupkg");

        return new ResolutionResult(
            selection.DllPath,
            packageId,
            version,
            feed,
            ComputeSha256Hex(nupkgPath),
            selection.ChosenTfm,
            selection.DllRelativePath,
            ComputeSha256Hex(selection.DllPath));
    }

    public async Task<string> ResolveFromLockAsync(
        string packageId,
        string version,
        string sourceFeed,
        string packageSha256,
        string targetFramework,
        string dllRelativePath,
        string dllSha256,
        CancellationToken cancellationToken = default)
    {
        var result = await ResolveWithMetadataAsync(packageId, version, targetFramework, sourceFeed, cancellationToken);

        if (!string.Equals(result.PackageSha256, packageSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Lock drift detected for {packageId}@{version}: package hash mismatch. Expected {packageSha256}, actual {result.PackageSha256}.");

        if (!string.Equals(result.TargetFramework, targetFramework, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Lock drift detected for {packageId}@{version}: TFM mismatch. Expected {targetFramework}, actual {result.TargetFramework}.");

        if (!string.Equals(result.DllRelativePath, dllRelativePath, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Lock drift detected for {packageId}@{version}: selected DLL mismatch. Expected {dllRelativePath}, actual {result.DllRelativePath}.");

        if (!string.Equals(result.DllSha256, dllSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Lock drift detected for {packageId}@{version}: assembly hash mismatch. Expected {dllSha256}, actual {result.DllSha256}.");

        return result.DllPath;
    }

    public string GetPackageDirectory(string packageId, string version)
        => Path.Combine(_cacheRoot, packageId.ToLowerInvariant(), version);

    private static async Task DownloadAndExtractAsync(string packageId, string version, string sourceFeed, string packageDir, CancellationToken cancellationToken)
    {
        var cache = new SourceCacheContext();
        var repository = Repository.Factory.GetCoreV3(sourceFeed);
        var resource = await repository.GetResourceAsync<FindPackageByIdResource>(cancellationToken);
        var nugetVersion = new NuGetVersion(version);

        var versions = await resource.GetAllVersionsAsync(packageId, cache, NullLogger.Instance, cancellationToken);
        if (!versions.Contains(nugetVersion))
            throw new InvalidOperationException($"Package '{packageId}' version '{version}' was not found on feed '{sourceFeed}'.");

        Directory.CreateDirectory(packageDir);
        var nupkgPath = Path.Combine(packageDir, $"{packageId}.{version}.nupkg");

        using (var fileStream = File.Create(nupkgPath))
        {
            var downloaded = await resource.CopyNupkgToStreamAsync(packageId, nugetVersion, fileStream, cache, NullLogger.Instance, cancellationToken);
            if (!downloaded)
                throw new InvalidOperationException($"Failed to download package '{packageId}' version '{version}'.");
        }

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

        await File.WriteAllTextAsync(Path.Combine(packageDir, ".extracted"), DateTimeOffset.UtcNow.ToString("O"), cancellationToken);
    }

    private static (string DllPath, string ChosenTfm, string DllRelativePath) FindDllSelection(string packageId, string packageDir, string? targetFramework)
    {
        var libDir = Path.Combine(packageDir, "lib");
        if (!Directory.Exists(libDir))
            throw new InvalidOperationException($"Package '{packageId}' does not contain a lib/ folder with any DLLs.");

        var availableTfms = Directory.GetDirectories(libDir).Select(Path.GetFileName).Where(d => d is not null).Cast<string>().ToList();
        if (availableTfms.Count == 0)
            throw new InvalidOperationException($"Package '{packageId}' lib/ folder contains no TFM subfolders.");

        string chosenTfm;
        if (targetFramework is not null)
        {
            chosenTfm = availableTfms.FirstOrDefault(t => string.Equals(t, targetFramework, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException($"Package '{packageId}' does not contain TFM '{targetFramework}'. Available: {string.Join(", ", availableTfms)}");
        }
        else
        {
            chosenTfm = PreferredTfms.FirstOrDefault(pref => availableTfms.Any(t => string.Equals(t, pref, StringComparison.OrdinalIgnoreCase))) ?? availableTfms[0];
        }

        var tfmPath = Path.Combine(libDir, chosenTfm);
        var dlls = Directory.GetFiles(tfmPath, "*.dll");
        if (dlls.Length == 0)
            throw new InvalidOperationException($"No DLLs found in '{tfmPath}' for package '{packageId}'.");

        var primaryDll = dlls.FirstOrDefault(d => Path.GetFileNameWithoutExtension(d).Equals(packageId, StringComparison.OrdinalIgnoreCase)) ?? dlls[0];
        var relativePath = Path.GetRelativePath(packageDir, primaryDll).Replace('\\', '/');
        return (primaryDll, chosenTfm, relativePath);
    }

    private static string ComputeSha256Hex(string path)
    {
        using var stream = File.OpenRead(path);
        var hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
