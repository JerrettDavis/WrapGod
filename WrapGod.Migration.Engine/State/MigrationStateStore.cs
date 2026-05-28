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
    /// or contains invalid JSON. Corrupt files are <strong>not</strong> archived by
    /// this overload — use <see cref="Load(string, out bool, out string?)"/> when
    /// the caller needs to surface recovery to the user (e.g.
    /// <see cref="StatefulMigrationEngine"/>).
    /// </summary>
    public static MigrationState? Load(string schemaPath) =>
        Load(schemaPath, out _, out _);

    /// <summary>
    /// Loads the <see cref="MigrationState"/> associated with <paramref name="schemaPath"/>
    /// and reports whether a corrupt state file was archived during the load.
    /// </summary>
    /// <param name="schemaPath">Path to the schema file (the state file is its sibling).</param>
    /// <param name="wasCorrupt">
    /// Set to <see langword="true"/> when the state file existed but contained invalid JSON
    /// (or was empty). In that case the corrupt file is moved to
    /// <paramref name="backupPath"/> and the method returns <see langword="null"/>.
    /// </param>
    /// <param name="backupPath">
    /// When <paramref name="wasCorrupt"/> is <see langword="true"/>, the absolute path of
    /// the archived <c>.state.json.bak</c> file. Otherwise <see langword="null"/>.
    /// </param>
    /// <returns>
    /// The loaded state, or <see langword="null"/> when the state file is missing, empty,
    /// or corrupt. When corrupt, the caller can inspect <paramref name="wasCorrupt"/>
    /// to surface recovery in its audit trail.
    /// </returns>
    public static MigrationState? Load(string schemaPath, out bool wasCorrupt, out string? backupPath)
    {
        ArgumentNullException.ThrowIfNull(schemaPath);

        wasCorrupt = false;
        backupPath = null;

        var statePath = GetStatePath(schemaPath);
        if (!File.Exists(statePath))
            return null;

        string json;
        try
        {
            json = File.ReadAllText(statePath, Encoding.UTF8);
        }
        catch (IOException)
        {
            return null;
        }

        // Empty file is treated like a missing state file (not "corrupt").
        if (string.IsNullOrWhiteSpace(json))
            return null;

        var state = MigrationStateSerializer.Deserialize(json);
        if (state is not null)
            return state;

        // Corrupt JSON: archive the offending file and return null so the engine
        // can re-run from scratch. The caller surfaces a SkippedRewrite to the
        // user (see StatefulMigrationEngine.ApplyWithState).
        var bak = statePath + ".bak";
        try
        {
            File.Move(statePath, bak, overwrite: true);
            wasCorrupt = true;
            backupPath = bak;
        }
        catch (IOException)
        {
            // Best-effort archive. If the move fails (e.g. file locked), leave the
            // corrupt file in place — the next save will overwrite it atomically.
            wasCorrupt = true;
            backupPath = null;
        }

        return null;
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
        try
        {
            File.Move(tmp, statePath, overwrite: true);
        }
        catch
        {
            // Move failed — destination locked, parent removed, etc.
            // Best-effort cleanup of the .tmp file so we don't leave orphans.
            try { File.Delete(tmp); } catch { /* best-effort */ }
            throw;
        }
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
