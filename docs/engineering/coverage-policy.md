# Coverage Policy

## Thresholds

| Scope | Metric | Minimum |
|-------|--------|---------|
| Per-package | Line rate | 90% |

The CI workflow enforces these thresholds on every PR and push to `main`.
If any package drops below the minimum, the build fails.

## How It Works

1. **Collection**: `dotnet test` runs with `coverlet.collector` producing
   a Cobertura XML report (`coverage.cobertura.xml`).
2. **Parsing**: The CI workflow parses each `<package>` element's `line-rate`
   attribute from the Cobertura XML.
3. **Gate**: Each package's line rate is compared against the threshold.
   If any package falls below 90%, the workflow fails with an error annotation.
4. **Reporting**: A coverage summary table is posted to the GitHub Actions
   job summary, showing per-package line rates and pass/fail status.

## Adjusting Thresholds

To change the threshold, update the `THRESHOLD` variable in
`.github/workflows/ci.yml` under the "Coverage Gate" step.

Threshold changes require team review and should be accompanied by a rationale
in the PR description.

## Exceptions

If a package legitimately cannot meet the threshold (e.g., generated code,
platform-specific branches), document the exception here with a justification:

| Package | Actual | Justification |
|---------|--------|---------------|
| *(none currently)* | | |

## Adding Coverage to New Packages

New packages are automatically included in coverage collection. Ensure the test
project references the new package and that `coverlet.collector` is included
as a test dependency.
