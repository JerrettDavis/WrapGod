# Flake Policy

## Identifying Flaky Tests

A test is considered flaky when it produces non-deterministic results across runs
without code changes. Common root causes:

- **Timing dependence**: Assertions on wall-clock time (`DateTime.Now`,
  `DateTimeOffset.UtcNow`) or tests that rely on `Task.Delay`/`Thread.Sleep`
  for synchronization.
- **Order dependence**: Static mutable state shared between test methods, or
  reliance on test execution order.
- **Shared file system state**: Temp directories reused across tests without
  per-test isolation (missing `Guid.NewGuid()` scoping).
- **Network dependence**: Tests that call live NuGet feeds, HTTP endpoints, or
  DNS without mocking or stubbing.
- **Platform dependence**: Path separators, line endings, or locale-sensitive
  string comparisons.

## Prevention Guidelines

1. **Deterministic timestamps**: Use fixed `DateTimeOffset` values in test setup
   rather than `DateTimeOffset.UtcNow`.
2. **Per-test isolation**: Temp directories must use `Guid.NewGuid()` per test
   instance, not per class (`static readonly`).
3. **No static mutable state**: Test helper methods that create resources should
   be instance methods, not static, to prevent cross-test contamination.
4. **Deterministic seeds**: If randomness is needed, use a fixed seed and log it
   so failures are reproducible.
5. **Generous timeouts**: Integration tests touching the file system or NuGet
   should use timeouts at least 2x the expected duration.

## Quarantine Rules

When a test is identified as flaky:

1. Apply the `[Trait("Category", "Quarantined")]` attribute to the test method.
2. Add a comment referencing the tracking issue: `// Quarantined: see #NNN`.
3. Quarantined tests are excluded from the CI gate via filter:
   `--filter "Category!=Quarantined"`.
4. The quarantined test must have a GitHub issue assigned within 48 hours.

## Re-entry Criteria

A quarantined test may be restored to the main suite when:

1. The root cause is identified and fixed.
2. The fix is verified by running the test at least 50 times locally without failure:
   ```bash
   for i in $(seq 1 50); do dotnet test --filter "FullyQualifiedName~TheTest" --nologo -v q || break; done
   ```
3. The `[Trait("Category", "Quarantined")]` attribute and comment are removed.
4. The associated GitHub issue is closed.

## Audit Cadence

- On each PR that modifies test files, reviewers should check for new timing or
  order dependencies.
- Monthly: run the full suite 10x in CI to surface intermittent failures.
