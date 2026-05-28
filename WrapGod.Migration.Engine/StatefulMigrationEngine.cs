using WrapGod.Migration;
using WrapGod.Migration.Engine.State;

namespace WrapGod.Migration.Engine;

/// <summary>
/// Wraps a <see cref="MigrationEngine"/> with state-tracking behaviour so that
/// <c>apply</c> runs are idempotent across multiple invocations.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Idempotence protocol:</strong>
/// <list type="number">
///   <item><description>Load the state file from <c>{schemaPath}.state.json</c>.</description></item>
///   <item><description>Compute the SHA-256 hash of the current schema JSON.</description></item>
///   <item><description>
///     If the state exists AND the hash matches: skip <c>(ruleId, file)</c> pairs already in
///     <see cref="State.MigrationState.Applied"/>.
///   </description></item>
///   <item><description>
///     If the state exists BUT the hash differs: re-run all rules (schema has changed);
///     old applied entries that reappear in the new run are de-duplicated.
///   </description></item>
///   <item><description>If no state file: full run.</description></item>
/// </list>
/// After the run the merged state is persisted (unless this is a dry run).
/// </para>
/// <para>
/// <strong>State file ownership:</strong> <see cref="StatefulMigrationEngine"/> owns the
/// read/write lifecycle. The inner <see cref="MigrationEngine"/> is unaware of state.
/// </para>
/// </remarks>
public sealed class StatefulMigrationEngine
{
    private readonly MigrationEngine _inner;

    /// <summary>
    /// Initializes a new <see cref="StatefulMigrationEngine"/> wrapping
    /// <paramref name="inner"/>.
    /// </summary>
    /// <param name="inner">The inner stateless engine to delegate to.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="inner"/> is <see langword="null"/>.
    /// </exception>
    public StatefulMigrationEngine(MigrationEngine inner)
    {
        ArgumentNullException.ThrowIfNull(inner);
        _inner = inner;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Applies <paramref name="schema"/> rules to <paramref name="files"/>, skipping
    /// rules already recorded in the persisted state, and writes the merged state to
    /// disk when complete.
    /// </summary>
    /// <param name="schemaPath">
    /// Path to the schema file on disk. Used to locate the state file and compute the
    /// current schema hash.
    /// </param>
    /// <param name="schema">The migration schema to apply.</param>
    /// <param name="files">Source file paths to process.</param>
    /// <returns>
    /// The cumulative <see cref="MigrationResult"/> (new applied entries from this run).
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="schemaPath"/>, <paramref name="schema"/>,
    /// or <paramref name="files"/> is <see langword="null"/>.
    /// </exception>
    public MigrationResult ApplyWithState(
        string schemaPath,
        MigrationSchema schema,
        IEnumerable<string> files)
    {
        ArgumentNullException.ThrowIfNull(schemaPath);
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(files);

        var (priorState, currentHash) = LoadAndHash(schemaPath, schema);
        var alreadyApplied = BuildAlreadyAppliedSet(priorState, currentHash);

        var result = _inner.Apply(schema, files, alreadyApplied);

        var baseState = priorState ?? NewState(schemaPath);
        var merged = baseState.Merge(result, currentHash);

        MigrationStateStore.Save(schemaPath, merged);
        return result;
    }

    /// <summary>
    /// Performs a dry run — evaluates rules (honouring prior state) but writes neither
    /// source files nor the state file to disk.
    /// </summary>
    /// <param name="schemaPath">Path to the schema file (used for state lookup and hash).</param>
    /// <param name="schema">The migration schema to evaluate.</param>
    /// <param name="files">Source file paths to process.</param>
    /// <returns>
    /// A <see cref="MigrationResult"/> with <see cref="MigrationResult.DryRun"/> = <see langword="true"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="schemaPath"/>, <paramref name="schema"/>,
    /// or <paramref name="files"/> is <see langword="null"/>.
    /// </exception>
    public MigrationResult DryRunWithState(
        string schemaPath,
        MigrationSchema schema,
        IEnumerable<string> files)
    {
        ArgumentNullException.ThrowIfNull(schemaPath);
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(files);

        var (priorState, currentHash) = LoadAndHash(schemaPath, schema);
        var alreadyApplied = BuildAlreadyAppliedSet(priorState, currentHash);

        return _inner.DryRun(schema, files, alreadyApplied);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Loads the prior state (gracefully handling corruption) and computes the current
    /// schema hash from the schema file on disk (or from the serialized schema when the
    /// file cannot be read).
    /// </summary>
    private static (State.MigrationState? priorState, string currentHash) LoadAndHash(
        string schemaPath,
        MigrationSchema schema)
    {
        // Compute current hash. Prefer reading the actual file on disk so the hash
        // matches what was written; fall back to serializing the in-memory schema.
        string schemaJson;
        try
        {
            schemaJson = File.ReadAllText(schemaPath, System.Text.Encoding.UTF8);
        }
        catch (IOException)
        {
            schemaJson = WrapGod.Migration.MigrationSchemaSerializer.Serialize(schema);
        }
        var currentHash = MigrationStateStore.ComputeSchemaHash(schemaJson);

        // Load prior state — null if missing or corrupt.
        var priorState = MigrationStateStore.Load(schemaPath);
        return (priorState, currentHash);
    }

    /// <summary>
    /// Builds the set of <c>(ruleId, file)</c> pairs that should be skipped because
    /// they are already recorded in the prior state AND the schema has not changed.
    /// When the schema has changed all rules are re-evaluated (returns an empty set).
    /// </summary>
    private static HashSet<(string RuleId, string File)> BuildAlreadyAppliedSet(
        State.MigrationState? priorState,
        string currentHash)
    {
        if (priorState is null || priorState.SchemaHasChanged(currentHash))
            return [];

        return new HashSet<(string, string)>(
            priorState.Applied.Select(a => (a.RuleId, a.File)),
            EqualityComparer<(string, string)>.Default);
    }

    /// <summary>Creates a fresh <see cref="State.MigrationState"/> for a first run.</summary>
    private static State.MigrationState NewState(string schemaPath) => new()
    {
        Schema    = schemaPath,
        StartedAt = DateTimeOffset.UtcNow,
        LastRunAt = DateTimeOffset.UtcNow,
    };
}
