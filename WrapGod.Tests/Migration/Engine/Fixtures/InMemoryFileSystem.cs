using WrapGod.Migration.Engine;

namespace WrapGod.Tests.Migration.Engine.Fixtures;

/// <summary>
/// In-memory <see cref="IMigrationFileSystem"/> used in unit tests.
/// Simulates a virtual disk without touching the real file system.
/// </summary>
internal sealed class InMemoryFileSystem : IMigrationFileSystem
{
    private readonly Dictionary<string, string> _files =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// When set to a non-null path, reading that path will throw an <see cref="IOException"/>.
    /// Useful for testing IO-error handling.
    /// </summary>
    public string? ThrowOnRead { get; set; }

    /// <summary>All files currently stored in the virtual file system.</summary>
    public IReadOnlyDictionary<string, string> Files => _files;

    /// <summary>Adds or replaces a virtual file.</summary>
    public InMemoryFileSystem WithFile(string path, string content)
    {
        _files[path] = content;
        return this;
    }

    // ── IMigrationFileSystem ──────────────────────────────────────────────────

    public bool FileExists(string path) => _files.ContainsKey(path);

    public string ReadAllText(string path)
    {
        if (ThrowOnRead is not null &&
            string.Equals(ThrowOnRead, path, StringComparison.OrdinalIgnoreCase))
        {
            throw new IOException($"Simulated IO error reading '{path}'.");
        }

        if (_files.TryGetValue(path, out var content))
            return content;

        throw new IOException($"Virtual file not found: '{path}'.");
    }

    public void WriteAllTextAtomic(string path, string content) => _files[path] = content;
}
