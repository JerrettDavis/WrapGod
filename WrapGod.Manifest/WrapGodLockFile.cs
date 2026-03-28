using System;
using System.Collections.Generic;

namespace WrapGod.Manifest;

public sealed class WrapGodLockFile
{
    public string SchemaVersion { get; set; } = "1.0";
    public DateTimeOffset GeneratedAt { get; set; } = DateTimeOffset.UtcNow;
    public LockToolchain Toolchain { get; set; } = new();
    public List<LockSourceEntry> Sources { get; set; } = [];
}

public sealed class LockToolchain
{
    public string Tool { get; set; } = "wrap-god";
    public string? Version { get; set; }
    public string? Runtime { get; set; }
}

public sealed class LockSourceEntry
{
    public string PackageId { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string SourceFeed { get; set; } = string.Empty;
    public string PackageSha256 { get; set; } = string.Empty;
    public string TargetFramework { get; set; } = string.Empty;
    public string DllRelativePath { get; set; } = string.Empty;
    public string DllSha256 { get; set; } = string.Empty;
}
