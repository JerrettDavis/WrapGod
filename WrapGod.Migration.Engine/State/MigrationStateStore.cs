using System.Security.Cryptography;
using System.Text;

namespace WrapGod.Migration.Engine.State;

/// <summary>
/// Provides static helpers for locating, loading, saving, and hashing migration state files.
/// </summary>
/// <remarks>
/// <para>
/// The state file sits next to the schema file and is named
/// <c>{schemaFilename}.state.json</c>.
/// </para>
/// <para>
/// Writes are atomic: content is first written to a <c>.tmp</c> file, then renamed
/// over the target path so no partial state is ever visible on disk.
/// </para>
/// </remarks>
public static class MigrationStateStore
{
    // ── Path helpers ─────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the state file path for the given schema file path.
    /// The state file sits in the same directory as the schema with the suffix
    /// <c>.state.json</c> appended to the schema filename.
    /// </summary>
    /// <example>
    /// <c>GetStatePath("/migrations/myschema.json")</c>
    /// returns <c>"/migrations/myschema.json.state.json"</c>.
    /// </example>
    public static string GetStatePath(string schemaPath)
    {
        ArgumentNullException.ThrowIfNull(schemaPath);
        return schemaPath + ".state.json";
    }

    // ── Load ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Loads the <see cref="MigrationState"/> associated with <paramref name="schemaPath"/>.
    /// Returns <see langword="null"/> when the state file does not exist, is empty,
    /// or contains invalid JSON (the corrupt file is left in place; callers such as
    /// <see cref="StatefulMigrationEngine"/> may archive it before proceeding).
    /// </summary>
    public static MigrationState? Load(string schemaPath)
    {
        ArgumentNullException.ThrowIfNull(schemaPath);

        var statePath = GetStatePath(schemaPath);
        if (!File.Exists(statePath))
            return null;

        try
        {
            var json = File.ReadAllText(statePath, Encoding.UTF8);
            return MigrationStateSerializer.Deserialize(json); // returns null on bad JSON
        }
        catch (IOException)
        {
            return null;
        }
    }

    // ── Save ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Persists <paramref name="state"/> next to <paramref name="schemaPath"/>.
    /// Creates the parent directory if it does not exist.
    /// Uses an atomic write (temp file + rename) so no partial state appears on disk.
    /// </summary>
    /// <exception cref="IOException">
    /// Propagated from the underlying file system on write failure.
    /// </exception>
    public static void Save(string schemaPath, MigrationState state)
    {
        ArgumentNullException.ThrowIfNull(schemaPath);
        ArgumentNullException.ThrowIfNull(state);

        var statePath = GetStatePath(schemaPath);
        var dir = Path.GetDirectoryName(statePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var json = MigrationStateSerializer.Serialize(state);
        var tmp  = statePath + ".tmp";

        File.WriteAllText(tmp, json, Encoding.UTF8);
        File.Move(tmp, statePath, overwrite: true);
    }

    // ── Hashing ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Computes a normalised SHA-256 hash of <paramref name="schemaJson"/>.
    /// </summary>
    /// <remarks>
    /// Before hashing the content is normalised:
    /// <list type="number">
    ///   <item><description>CRLF (<c>\r\n</c>) and bare CR (<c>\r</c>) are replaced with LF (<c>\n</c>).</description></item>
    ///   <item><description>Trailing whitespace is trimmed from each line.</description></item>
    /// </list>
    /// This makes the hash insensitive to git's <c>autocrlf</c> setting and to trailing
    /// space differences introduced by editors, but it still changes when the schema
    /// rules are reordered (content-sensitive, not schema-semantic).
    /// </remarks>
    /// <returns>The hash in the format <c>sha256:&lt;lowercase-hex&gt;</c>.</returns>
    public static string ComputeSchemaHash(string schemaJson)
    {
        ArgumentNullException.ThrowIfNull(schemaJson);

        // Normalise line endings and trim trailing whitespace per line.
        var normalised = Normalise(schemaJson);
        var bytes = Encoding.UTF8.GetBytes(normalised);
        var hash  = SHA256.HashData(bytes);
        return "sha256:" + Convert.ToHexStringLower(hash);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Normalises line endings to LF and trims trailing whitespace from each line.
    /// </summary>
    private static string Normalise(string input)
    {
        // Replace CRLF then lone CR with LF.
        var unified = input.Replace("\r\n", "\n", StringComparison.Ordinal)
                           .Replace('\r', '\n');

        // Trim trailing whitespace per line.
        var lines = unified.Split('\n');
        for (var i = 0; i < lines.Length; i++)
            lines[i] = lines[i].TrimEnd();

        return string.Join('\n', lines);
    }
}
