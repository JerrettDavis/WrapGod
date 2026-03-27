# RFC 0051 — Incremental Cache Strategy for `WrapGod.Extractor`

- **Issue:** #51
- **Status:** Proposed
- **Author:** OpenClaw subagent
- **Last updated:** 2026-03-27

## 1) Summary

Define a deterministic, version-aware cache for extractor outputs so repeated runs avoid re-extracting unchanged assemblies while remaining safe under extractor/version/option changes.

This RFC chooses a **hybrid cache layout**:

1. **Project-local cache index in `obj/`** for build-local determinism and easy invalidation.
2. **Content-addressed payload store in user cache** for cross-project/process reuse.

If cache reads fail or payloads are invalid/corrupt, extractor falls back to cold extraction and rewrites cache entries.

---

## 2) Goals and Non-Goals

### Goals

- Deterministic cache keys that invalidate on meaningful change.
- Fast local reuse in repeated builds.
- Optional cross-project reuse without polluting source control.
- Explicit corruption handling and safe fallback.
- Compatibility with `MultiVersionExtractor` flow.

### Non-Goals

- Generator incremental pipeline internals.
- Remote/distributed cache service.
- Binary-compatible key evolution across arbitrary future schema changes without versioning (we version keys explicitly).

---

## 3) Decisions

## 3.1 Canonical cache key fields (decided)

The extractor cache key is a normalized object with these fields:

```json
{
  "keySchema": "wg.extractor.cache.v1",
  "manifestSchema": "wrapgod.manifest.v1",
  "extractorVersion": "<WrapGod.Extractor assembly informational version>",
  "extractorAlgoVersion": "1",
  "source": {
    "sha256": "<assembly file SHA-256>",
    "mvid": "<module version id GUID>",
    "targetFramework": "<best effort; may be null>"
  },
  "options": {
    "publicOnly": true,
    "includeObsoleteDetails": false,
    "custom": {}
  }
}
```

Then:

- `options` is canonicalized as deterministic JSON (sorted keys, UTF-8, no extra whitespace).
- Full key hash = `sha256(canonicalJson)`.
- Payload filename = `<fullKeyHash>.manifest.json`.

### Rationale

- `sha256` tracks exact file content changes (already aligned with `ApiManifest.SourceHash`).
- `mvid` protects against edge cases where packaging/re-signing behavior needs stronger assembly identity correlation.
- `extractorVersion` and explicit `extractorAlgoVersion` force invalidation when extraction logic changes, even if source assembly does not.
- `manifestSchema` ensures cache entries map to the expected serialized contract.

## 3.2 Cache store location (decided)

Use a **hybrid model**:

- **L1 (project-local index):** `obj/wrapgod/cache-index/`
  - Small metadata records mapping cache key hash to payload metadata.
  - Fast local lookups tied to the current project/build graph.
- **L2 (user shared payload store):**
  - Windows: `%LOCALAPPDATA%/WrapGod/cache/extractor/`
  - Linux/macOS: `${XDG_CACHE_HOME:-~/.cache}/wrapgod/extractor/`
  - Stores manifest payloads keyed by key hash.

Lookup order:

1. Check `obj` index.
2. Resolve payload in shared store.
3. Validate payload.
4. On miss/failure, perform cold extraction, write payload, update index.

### Why not obj-only?

`obj`-only avoids cross-project coupling but duplicates payload generation and storage across repos.

### Why not user-cache-only?

Shared-only is harder to reason about in local builds and less transparent to clean operations (`dotnet clean` semantics around project-local state).

## 3.3 Invalidation and eviction policy (decided)

### Invalidation rules

Any change in the canonical key fields is a hard miss:

- Assembly bytes (`source.sha256`)
- MVID
- Extractor version
- Extractor algorithm version
- Manifest schema version
- Effective options hash/contents

### Eviction

- Shared payload store: best-effort LRU by atime + max-size cap (default 1 GB, configurable).
- Project `obj` index: naturally cleaned by build clean operations; stale records are tolerated and lazily repaired.

## 3.4 Corruption and fallback behavior (decided)

If any of the following occurs:

- JSON parse failure
- Schema mismatch
- Required fields missing
- Key mismatch against embedded metadata

Then:

1. Log a warning diagnostic (non-fatal).
2. Delete or quarantine bad payload.
3. Perform cold extraction.
4. Rewrite cache entry.

Build must continue unless cold extraction itself fails.

---

## 4) Multi-Version Compatibility

`MultiVersionExtractor` gets two cache layers:

1. **Per-version extraction cache** (above key).
2. **Merged-result cache** keyed by ordered vector of version labels + per-version manifest cache key hashes + merge algorithm version.

Merged key example (conceptual):

```json
{
  "keySchema": "wg.extractor.multiversion.cache.v1",
  "mergeAlgoVersion": "1",
  "inputs": [
    { "label": "1.0.0", "extractKey": "<hash-a>" },
    { "label": "2.0.0", "extractKey": "<hash-b>" }
  ]
}
```

Order is significant and must match input order.

---

## 5) Alternatives Considered

1. **No cache (status quo)**
   - Rejected: repeated extraction cost scales poorly in CI and local loops.

2. **Obj-only cache**
   - Rejected: misses cross-project reuse and duplicates large manifest payloads.

3. **Shared-only cache**
   - Rejected: weaker project-local traceability and harder clean semantics.

4. **Timestamp/length-based keys**
   - Rejected: unsafe vs content hash; susceptible to false hits.

---

## 6) Cache key schema and metadata

Each cached payload includes metadata envelope:

```json
{
  "cacheKeyHash": "<sha256>",
  "cacheKey": { "...": "canonical key object" },
  "createdAtUtc": "2026-03-27T22:00:00Z",
  "manifest": { "...": "ApiManifest" }
}
```

This enables integrity checks and future migrations.

---

## 7) Invalidation matrix

| Scenario | Expected Result | Notes |
|---|---|---|
| Same assembly bytes, same extractor/options/schema | HIT | Baseline repeat-run case |
| Assembly bytes changed | MISS | `source.sha256` change |
| MVID changed with same hash impossible/rare | MISS if observed | Defensive identity guard |
| Extractor package version changed | MISS | `extractorVersion` invalidates |
| Extraction algorithm bump only | MISS | `extractorAlgoVersion` invalidates |
| Manifest schema changed (v1 -> v2) | MISS | `manifestSchema` invalidates |
| Option value changed | MISS | Canonical options changed |
| Corrupt cached JSON | MISS + rebuild | Non-fatal warning |
| Shared cache payload missing, obj index exists | MISS + repair | Self-healing |
| Multi-version input order changed | MISS (merge cache) | Order-sensitive by design |
| One input version changed among many | Partial miss (one extract + merge miss) | Reuses unchanged per-version entries |

---

## 8) Implementation follow-up checklist

- [ ] Add `ExtractorCacheKey` + canonical JSON serializer in `WrapGod.Extractor`.
- [ ] Add cache interfaces (`IExtractorCacheStore`, `IExtractorCacheIndex`) and default filesystem implementation.
- [ ] Wrap `AssemblyExtractor.Extract` with cache-aware entrypoint (`ExtractWithCache`).
- [ ] Add metadata envelope type + validation path.
- [ ] Add merge cache path for `MultiVersionExtractor`.
- [ ] Add diagnostics for cache corruption/fallback.
- [ ] Add config knobs (cache enabled, max size, cache root override).
- [ ] Add tests for matrix above (hit/miss/invalidate/corruption).

---

## 9) Rollout plan

1. Implement behind opt-in flag (phase 1).
2. Validate determinism + correctness with integration tests.
3. Enable by default after stability window.
4. Monitor cache hit rate and extraction latency improvements.

---

## 10) Follow-up work items

- [x] Tracking implementation issue: #76
- [ ] Optional telemetry counters for cache hit/miss/corruption.
- [ ] Optional cache pruning command/tooling.
