using System.Text.Json;

namespace WrapGod.Manifest;

public static class LockFileSerializer
{
    public static string Serialize(WrapGodLockFile lockFile)
        => JsonSerializer.Serialize(lockFile, ManifestSerializer.GetOptions());

    public static WrapGodLockFile? Deserialize(string json)
        => JsonSerializer.Deserialize<WrapGodLockFile>(json, ManifestSerializer.GetOptions());
}
