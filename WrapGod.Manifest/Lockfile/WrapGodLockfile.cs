using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WrapGod.Manifest.Lockfile;

/// <summary>
/// Root schema for the wrapgod.lock.json reproducibility lockfile.
/// Captures the exact resolution state so builds are deterministic.
/// </summary>
public sealed class WrapGodLockfile
{
    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    [JsonPropertyName("generatedAt")]
    public DateTimeOffset GeneratedAt { get; set; } = DateTimeOffset.UtcNow;

    [JsonPropertyName("toolchainVersion")]
    public string ToolchainVersion { get; set; } = string.Empty;

    [JsonPropertyName("sources")]
    public List<LockfileSource> Sources { get; set; } = new List<LockfileSource>();
}

/// <summary>
/// A single resolved source entry in the lockfile.
/// </summary>
public sealed class LockfileSource
{
    [JsonPropertyName("packageId")]
    public string PackageId { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("feed")]
    public string Feed { get; set; } = string.Empty;

    [JsonPropertyName("fileHash")]
    public string FileHash { get; set; } = string.Empty;

    [JsonPropertyName("tfm")]
    public string Tfm { get; set; } = string.Empty;

    [JsonPropertyName("resolvedPath")]
    public string ResolvedPath { get; set; } = string.Empty;
}

/// <summary>
/// Serializes a <see cref="WrapGodLockfile"/> to JSON.
/// </summary>
public static class WrapGodLockfileWriter
{
    private static readonly JsonSerializerOptions s_options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// Serializes the lockfile to a JSON string.
    /// </summary>
    public static string Serialize(WrapGodLockfile lockfile)
    {
        if (lockfile is null) throw new ArgumentNullException(nameof(lockfile));
        return JsonSerializer.Serialize(lockfile, s_options);
    }

    /// <summary>
    /// Writes the lockfile to the specified file path.
    /// </summary>
    public static void WriteToFile(WrapGodLockfile lockfile, string path)
    {
        if (lockfile is null) throw new ArgumentNullException(nameof(lockfile));
        if (path is null) throw new ArgumentNullException(nameof(path));
        File.WriteAllText(path, Serialize(lockfile));
    }
}

/// <summary>
/// Deserializes and validates a <see cref="WrapGodLockfile"/> from JSON.
/// </summary>
public static class WrapGodLockfileReader
{
    /// <summary>
    /// Deserializes a lockfile from a JSON string.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the JSON is invalid or the lockfile version is unsupported.</exception>
    public static WrapGodLockfile Deserialize(string json)
    {
        if (json is null) throw new ArgumentNullException(nameof(json));

        var lockfile = JsonSerializer.Deserialize<WrapGodLockfile>(json)
            ?? throw new InvalidOperationException("Failed to deserialize lockfile: result was null.");

        Validate(lockfile);
        return lockfile;
    }

    /// <summary>
    /// Reads and deserializes a lockfile from the specified file path.
    /// </summary>
    public static WrapGodLockfile ReadFromFile(string path)
    {
        if (path is null) throw new ArgumentNullException(nameof(path));
        var json = File.ReadAllText(path);
        return Deserialize(json);
    }

    private static void Validate(WrapGodLockfile lockfile)
    {
        if (lockfile.Version < 1)
        {
            throw new InvalidOperationException(
                $"Unsupported lockfile version: {lockfile.Version}. Expected >= 1.");
        }

        foreach (var source in lockfile.Sources)
        {
            if (string.IsNullOrWhiteSpace(source.PackageId))
            {
                throw new InvalidOperationException(
                    "Lockfile source has an empty packageId.");
            }

            if (string.IsNullOrWhiteSpace(source.Version))
            {
                throw new InvalidOperationException(
                    $"Lockfile source '{source.PackageId}' has an empty version.");
            }
        }
    }
}

/// <summary>
/// Represents a single difference detected between the current resolution and a lockfile.
/// </summary>
public sealed class LockfileDrift
{
    /// <summary>The package that drifted.</summary>
    public string PackageId { get; set; } = string.Empty;

    /// <summary>Human-readable description of the drift.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>The field that changed (e.g. "version", "fileHash").</summary>
    public string Field { get; set; } = string.Empty;

    /// <summary>The expected (lockfile) value.</summary>
    public string Expected { get; set; } = string.Empty;

    /// <summary>The actual (current resolution) value.</summary>
    public string Actual { get; set; } = string.Empty;
}

/// <summary>
/// Compares a current resolution against a persisted lockfile and reports drifts.
/// </summary>
public static class DriftDetector
{
    /// <summary>
    /// Detects differences between the locked sources and the current sources.
    /// </summary>
    /// <param name="locked">The persisted lockfile (source of truth).</param>
    /// <param name="current">The current resolution result.</param>
    /// <returns>A list of drifts. Empty means no drift.</returns>
    public static IReadOnlyList<LockfileDrift> Detect(
        WrapGodLockfile locked,
        WrapGodLockfile current)
    {
        if (locked is null) throw new ArgumentNullException(nameof(locked));
        if (current is null) throw new ArgumentNullException(nameof(current));

        var drifts = new List<LockfileDrift>();

        var lockedByPackage = new Dictionary<string, LockfileSource>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in locked.Sources)
        {
            lockedByPackage[s.PackageId] = s;
        }

        var currentByPackage = new Dictionary<string, LockfileSource>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in current.Sources)
        {
            currentByPackage[s.PackageId] = s;
        }

        // Check for changed or removed packages.
        foreach (var kvp in lockedByPackage)
        {
            var packageId = kvp.Key;
            var lockedSource = kvp.Value;

            LockfileSource currentSource;
            if (!currentByPackage.TryGetValue(packageId, out currentSource))
            {
                drifts.Add(new LockfileDrift
                {
                    PackageId = packageId,
                    Description = $"Package '{packageId}' was removed from resolution.",
                    Field = "presence",
                    Expected = "present",
                    Actual = "absent",
                });
                continue;
            }

            CompareField(drifts, packageId, "version", lockedSource.Version, currentSource.Version);
            CompareField(drifts, packageId, "fileHash", lockedSource.FileHash, currentSource.FileHash);
            CompareField(drifts, packageId, "feed", lockedSource.Feed, currentSource.Feed);
            CompareField(drifts, packageId, "tfm", lockedSource.Tfm, currentSource.Tfm);
            CompareField(drifts, packageId, "resolvedPath", lockedSource.ResolvedPath, currentSource.ResolvedPath);
        }

        // Check for added packages.
        foreach (var packageId in currentByPackage.Keys)
        {
            if (!lockedByPackage.ContainsKey(packageId))
            {
                drifts.Add(new LockfileDrift
                {
                    PackageId = packageId,
                    Description = $"Package '{packageId}' was added (not in lockfile).",
                    Field = "presence",
                    Expected = "absent",
                    Actual = "present",
                });
            }
        }

        return drifts;
    }

    private static void CompareField(
        List<LockfileDrift> drifts,
        string packageId,
        string field,
        string expected,
        string actual)
    {
        if (!string.Equals(expected, actual, StringComparison.Ordinal))
        {
            drifts.Add(new LockfileDrift
            {
                PackageId = packageId,
                Description = $"Package '{packageId}' has {field} drift: expected '{expected}', got '{actual}'.",
                Field = field,
                Expected = expected,
                Actual = actual,
            });
        }
    }
}
