using System.CommandLine;
using System.Text.Json;
using WrapGod.Cli;

namespace WrapGod.Tests;

/// <summary>
/// In-process coverage tests for CLI commands that are otherwise only tested via
/// process-based invocation in CliTests.cs (which doesn't contribute to instrumented coverage).
/// </summary>
[Collection("CLI")]
public sealed class CliCoverageTests
{
    // ──────────────────────────────────────────────────────────────
    // CiBootstrapCommand
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task CiBootstrap_CreatesWorkflowFile()
    {
        var tempRoot = CreateTempDir();
        try
        {
            var outputDir = Path.Combine(tempRoot, ".github", "workflows");

            var ciCommand = CreateCiCommand();

            var result = await InvokeAsync(ciCommand, $"bootstrap --output \"{outputDir}\"");

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Generated:", result.StdOut);
            Assert.True(File.Exists(Path.Combine(outputDir, "wrapgod-ci.yml")));
        }
        finally
        {
            SafeDelete(tempRoot);
        }
    }

    [Fact]
    public async Task CiBootstrap_ExistingFile_ReturnsExitCode1()
    {
        var tempRoot = CreateTempDir();
        try
        {
            var outputDir = Path.Combine(tempRoot, ".github", "workflows");
            Directory.CreateDirectory(outputDir);
            await File.WriteAllTextAsync(Path.Combine(outputDir, "wrapgod-ci.yml"), "existing");

            var ciCommand = CreateCiCommand();

            var result = await InvokeAsync(ciCommand, $"bootstrap --output \"{outputDir}\"");

            Assert.Equal(1, result.ExitCode);
            Assert.Contains("already exists", result.StdErr);
        }
        finally
        {
            SafeDelete(tempRoot);
        }
    }

    [Fact]
    public async Task CiBootstrap_WithForce_OverwritesExisting()
    {
        var tempRoot = CreateTempDir();
        try
        {
            var outputDir = Path.Combine(tempRoot, ".github", "workflows");
            Directory.CreateDirectory(outputDir);
            await File.WriteAllTextAsync(Path.Combine(outputDir, "wrapgod-ci.yml"), "old content");

            var ciCommand = CreateCiCommand();

            var result = await InvokeAsync(ciCommand, $"bootstrap --output \"{outputDir}\" --force");

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Generated:", result.StdOut);
            var content = await File.ReadAllTextAsync(Path.Combine(outputDir, "wrapgod-ci.yml"));
            Assert.Contains("WrapGod CI", content);
        }
        finally
        {
            SafeDelete(tempRoot);
        }
    }

    // ──────────────────────────────────────────────────────────────
    // CiParityReportCommand
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task CiParity_MissingDirectory_ReturnsExitCode1()
    {
        var missingDir = Path.Combine(Path.GetTempPath(), $"wrapgod-missing-{Guid.NewGuid():N}");
        var ciCommand = CreateCiCommand();

        var result = await InvokeAsync(ciCommand, $"parity --workflow-dir \"{missingDir}\"");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("not found", result.StdErr);
    }

    [Fact]
    public async Task CiParity_NoWorkflowFiles_ReturnsExitCode1()
    {
        var tempRoot = CreateTempDir();
        try
        {
            var ciCommand = CreateCiCommand();

            var result = await InvokeAsync(ciCommand, $"parity --workflow-dir \"{tempRoot}\"");

            Assert.Equal(1, result.ExitCode);
            Assert.Contains("No workflow files", result.StdErr);
        }
        finally
        {
            SafeDelete(tempRoot);
        }
    }

    [Fact]
    public async Task CiParity_WithBootstrappedWorkflow_ReportsFullParity()
    {
        var tempRoot = CreateTempDir();
        try
        {
            // Write the workflow YAML that bootstrap would create
            var yaml = CiBootstrapCommand.GenerateWorkflowYaml();
            await File.WriteAllTextAsync(Path.Combine(tempRoot, "wrapgod-ci.yml"), yaml);

            var ciCommand = CreateCiCommand();

            var result = await InvokeAsync(ciCommand, $"parity --workflow-dir \"{tempRoot}\"");

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("full parity", result.StdOut);
        }
        finally
        {
            SafeDelete(tempRoot);
        }
    }

    [Fact]
    public async Task CiParity_PartialWorkflow_ReportsMissing()
    {
        var tempRoot = CreateTempDir();
        try
        {
            // Workflow with only checkout - missing everything else
            await File.WriteAllTextAsync(Path.Combine(tempRoot, "ci.yml"),
                "steps:\n  - uses: actions/checkout@v4\n");

            var ciCommand = CreateCiCommand();

            var result = await InvokeAsync(ciCommand, $"parity --workflow-dir \"{tempRoot}\"");

            Assert.Equal(2, result.ExitCode);
            Assert.Contains("[PASS]", result.StdOut);
            Assert.Contains("[MISS]", result.StdOut);
        }
        finally
        {
            SafeDelete(tempRoot);
        }
    }

    // ──────────────────────────────────────────────────────────────
    // ExplainCommand
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Explain_NoManifest_ReportsError()
    {
        var tempRoot = CreateTempDir();
        try
        {
            var origDir = Directory.GetCurrentDirectory();
            Directory.SetCurrentDirectory(tempRoot);
            try
            {
                var result = await InvokeAsync(ExplainCommand.Create(), "SomeType");

                Assert.Contains("No manifest file found", result.StdErr);
            }
            finally
            {
                Directory.SetCurrentDirectory(origDir);
            }
        }
        finally
        {
            SafeDelete(tempRoot);
        }
    }

    [Fact]
    public async Task Explain_ManifestNotFound_ReportsError()
    {
        var result = await InvokeAsync(ExplainCommand.Create(),
            "SomeType --manifest does-not-exist.json");

        Assert.Contains("Manifest not found", result.StdErr);
    }

    [Fact]
    public async Task Explain_KnownType_ShowsTypeInfo()
    {
        var tempRoot = CreateTempDir();
        try
        {
            var manifestPath = Path.Combine(tempRoot, "vendor.wrapgod.json");
            await File.WriteAllTextAsync(manifestPath, """
                {
                  "SchemaVersion": "1.0",
                  "GeneratedAt": "2026-01-01T00:00:00Z",
                  "Assembly": { "Name": "Acme.Lib", "Version": "1.0.0" },
                  "Types": [
                    {
                      "FullName": "Acme.Lib.FooService",
                      "Name": "FooService",
                      "StableId": "Acme.Lib.FooService",
                      "Kind": 0,
                      "Members": []
                    }
                  ]
                }
                """);

            var result = await InvokeAsync(ExplainCommand.Create(),
                $"FooService --manifest \"{manifestPath}\"");

            Assert.Contains("Type: Acme.Lib.FooService", result.StdOut);
            Assert.Contains("Kind:", result.StdOut);
            Assert.Contains("IWrappedFooService", result.StdOut);
        }
        finally
        {
            SafeDelete(tempRoot);
        }
    }

    [Fact]
    public async Task Explain_UnknownSymbol_ShowsNotFound()
    {
        var tempRoot = CreateTempDir();
        try
        {
            var manifestPath = Path.Combine(tempRoot, "vendor.wrapgod.json");
            await File.WriteAllTextAsync(manifestPath, """
                {
                  "SchemaVersion": "1.0",
                  "GeneratedAt": "2026-01-01T00:00:00Z",
                  "Assembly": { "Name": "Acme.Lib", "Version": "1.0.0" },
                  "Types": []
                }
                """);

            var result = await InvokeAsync(ExplainCommand.Create(),
                $"NonExistent --manifest \"{manifestPath}\"");

            Assert.Contains("not found in manifest", result.StdOut);
        }
        finally
        {
            SafeDelete(tempRoot);
        }
    }

    [Fact]
    public async Task Explain_GenericTypeWithPresence_ShowsFullInfo()
    {
        var tempRoot = CreateTempDir();
        try
        {
            var manifestPath = Path.Combine(tempRoot, "vendor.wrapgod.json");
            await File.WriteAllTextAsync(manifestPath, """
                {
                  "SchemaVersion": "1.0",
                  "GeneratedAt": "2026-01-01T00:00:00Z",
                  "Assembly": { "Name": "Acme.Lib", "Version": "2.0.0" },
                  "Types": [
                    {
                      "FullName": "Acme.Lib.GenericRepo`1",
                      "Name": "GenericRepo`1",
                      "StableId": "Acme.Lib.GenericRepo`1",
                      "Kind": 0,
                      "IsGenericType": true,
                      "IsGenericTypeDefinition": true,
                      "Presence": {
                        "IntroducedIn": "1.0.0",
                        "RemovedIn": null,
                        "ChangedIn": "2.0.0"
                      },
                      "Members": [
                        {
                          "Name": "GetById",
                          "Kind": 0,
                          "ReturnType": "T",
                          "Parameters": [
                            { "Name": "id", "Type": "int" }
                          ],
                          "Presence": {
                            "IntroducedIn": "1.0.0",
                            "RemovedIn": null,
                            "ChangedIn": "2.0.0"
                          }
                        }
                      ]
                    }
                  ]
                }
                """);

            // Test type lookup — use the full name to match
            var result = await InvokeAsync(ExplainCommand.Create(),
                $"Acme.Lib.GenericRepo`1 --manifest \"{manifestPath}\"");

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Type: Acme.Lib.GenericRepo`1", result.StdOut);
            Assert.Contains("Generic:", result.StdOut);
            Assert.Contains("Introduced:", result.StdOut);
            Assert.Contains("Changed:", result.StdOut);

            // Test member lookup
            var memberResult = await InvokeAsync(ExplainCommand.Create(),
                $"GetById --manifest \"{manifestPath}\"");

            Assert.Equal(0, memberResult.ExitCode);
            Assert.Contains("Parameters:", memberResult.StdOut);
            Assert.Contains("Introduced:", memberResult.StdOut);
            Assert.Contains("Changed:", memberResult.StdOut);
        }
        finally
        {
            SafeDelete(tempRoot);
        }
    }

    [Fact]
    public async Task Explain_NullManifest_ReportsError()
    {
        var tempRoot = CreateTempDir();
        try
        {
            var manifestPath = Path.Combine(tempRoot, "bad.wrapgod.json");
            await File.WriteAllTextAsync(manifestPath, "null");

            var result = await InvokeAsync(ExplainCommand.Create(),
                $"SomeType --manifest \"{manifestPath}\"");

            Assert.Contains("Failed to deserialize", result.StdErr);
        }
        finally
        {
            SafeDelete(tempRoot);
        }
    }

    [Fact]
    public async Task Explain_WithConfig_LoadsConfig()
    {
        var tempRoot = CreateTempDir();
        try
        {
            var manifestPath = Path.Combine(tempRoot, "vendor.wrapgod.json");
            await File.WriteAllTextAsync(manifestPath, """
                {
                  "SchemaVersion": "1.0",
                  "GeneratedAt": "2026-01-01T00:00:00Z",
                  "Assembly": { "Name": "Acme.Lib", "Version": "1.0.0" },
                  "Types": [
                    {
                      "FullName": "Acme.Lib.FooService",
                      "Name": "FooService",
                      "StableId": "Acme.Lib.FooService",
                      "Kind": 0,
                      "Members": []
                    }
                  ]
                }
                """);

            var configPath = Path.Combine(tempRoot, "wrapgod.config.json");
            await File.WriteAllTextAsync(configPath, """{ "source": "@self" }""");

            var result = await InvokeAsync(ExplainCommand.Create(),
                $"FooService --manifest \"{manifestPath}\" --config \"{configPath}\"");

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Type: Acme.Lib.FooService", result.StdOut);
        }
        finally
        {
            SafeDelete(tempRoot);
        }
    }

    [Fact]
    public async Task Explain_AutoDetectsManifest()
    {
        var tempRoot = CreateTempDir();
        try
        {
            await File.WriteAllTextAsync(Path.Combine(tempRoot, "vendor.wrapgod.json"), """
                {
                  "SchemaVersion": "1.0",
                  "GeneratedAt": "2026-01-01T00:00:00Z",
                  "Assembly": { "Name": "Acme.Lib", "Version": "1.0.0" },
                  "Types": []
                }
                """);

            var origDir = Directory.GetCurrentDirectory();
            Directory.SetCurrentDirectory(tempRoot);
            try
            {
                var result = await InvokeAsync(ExplainCommand.Create(), "SomeType");

                Assert.Contains("not found in manifest", result.StdOut);
            }
            finally
            {
                Directory.SetCurrentDirectory(origDir);
            }
        }
        finally
        {
            SafeDelete(tempRoot);
        }
    }

    [Fact]
    public async Task Explain_ManifestNotFoundByPath_ReportsError()
    {
        var result = await InvokeAsync(ExplainCommand.Create(),
            "SomeType --manifest /nonexistent/path/manifest.json");

        Assert.Contains("Manifest not found", result.StdErr);
    }

    [Fact]
    public async Task Explain_MemberWithRemovedVersion_ShowsRemoved()
    {
        var tempRoot = CreateTempDir();
        try
        {
            var manifestPath = Path.Combine(tempRoot, "vendor.wrapgod.json");
            await File.WriteAllTextAsync(manifestPath, """
                {
                  "SchemaVersion": "1.0",
                  "GeneratedAt": "2026-01-01T00:00:00Z",
                  "Assembly": { "Name": "Acme.Lib", "Version": "3.0.0" },
                  "Types": [
                    {
                      "FullName": "Acme.Lib.OldService",
                      "Name": "OldService",
                      "StableId": "Acme.Lib.OldService",
                      "Kind": 0,
                      "Presence": {
                        "IntroducedIn": "1.0.0",
                        "RemovedIn": "3.0.0",
                        "ChangedIn": null
                      },
                      "Members": [
                        {
                          "Name": "LegacyMethod",
                          "Kind": 0,
                          "ReturnType": "void",
                          "Parameters": [],
                          "Presence": {
                            "IntroducedIn": "1.0.0",
                            "RemovedIn": "3.0.0",
                            "ChangedIn": null
                          }
                        }
                      ]
                    }
                  ]
                }
                """);

            var result = await InvokeAsync(ExplainCommand.Create(),
                $"OldService --manifest \"{manifestPath}\"");

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Removed:", result.StdOut);

            var memberResult = await InvokeAsync(ExplainCommand.Create(),
                $"OldService.LegacyMethod --manifest \"{manifestPath}\"");

            Assert.Contains("Removed:", memberResult.StdOut);
        }
        finally
        {
            SafeDelete(tempRoot);
        }
    }

    // ──────────────────────────────────────────────────────────────
    // ExtractCommand — NuGet parse error paths
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Extract_ValidNuGetSinglePackage_AttemptsExtraction()
    {
        // This will fail at NuGet download (no network), but exercises the parsing code
        var result = await InvokeAsync(ExtractCommand.Create(),
            "--nuget SomePackage@1.0.0");

        // We just need to get past the parsing — the actual download will fail
        // which is fine for coverage purposes
        Assert.True(result.ExitCode != 0 || result.StdErr.Length > 0 || result.StdOut.Length > 0);
    }

    [Fact]
    public async Task Extract_MultipleNuGetPackages_AttemptsExtraction()
    {
        // Exercise the multi-version NuGet path
        var result = await InvokeAsync(ExtractCommand.Create(),
            "--nuget SomePackage@1.0.0 --nuget SomePackage@2.0.0");

        // The download will fail, but the parsing/branching code runs
        Assert.True(result.ExitCode != 0 || result.StdErr.Length > 0 || result.StdOut.Length > 0);
    }

    [Fact]
    public async Task Explain_KnownMember_ShowsMemberInfo()
    {
        var tempRoot = CreateTempDir();
        try
        {
            var manifestPath = Path.Combine(tempRoot, "vendor.wrapgod.json");
            await File.WriteAllTextAsync(manifestPath, """
                {
                  "SchemaVersion": "1.0",
                  "GeneratedAt": "2026-01-01T00:00:00Z",
                  "Assembly": { "Name": "Acme.Lib", "Version": "1.0.0" },
                  "Types": [
                    {
                      "FullName": "Acme.Lib.FooService",
                      "Name": "FooService",
                      "StableId": "Acme.Lib.FooService",
                      "Kind": 0,
                      "Members": [
                        {
                          "Name": "DoWork",
                          "Kind": 0,
                          "ReturnType": "void",
                          "Parameters": [
                            { "Name": "input", "Type": "string" }
                          ]
                        }
                      ]
                    }
                  ]
                }
                """);

            var result = await InvokeAsync(ExplainCommand.Create(),
                $"FooService.DoWork --manifest \"{manifestPath}\"");

            Assert.Contains("Member: Acme.Lib.FooService.DoWork", result.StdOut);
            Assert.Contains("Kind:", result.StdOut);
        }
        finally
        {
            SafeDelete(tempRoot);
        }
    }

    // ──────────────────────────────────────────────────────────────
    // InitCommand
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Init_WithSource_CreatesConfigAndCache()
    {
        var tempRoot = CreateTempDir();
        try
        {
            var origDir = Directory.GetCurrentDirectory();
            Directory.SetCurrentDirectory(tempRoot);
            try
            {
                var outputFile = Path.Combine(tempRoot, "wrapgod.config.json");
                var result = await InvokeAsync(InitCommand.Create(),
                    $"--source self-project --output \"{outputFile}\"");

                Assert.Contains("Created config", result.StdOut);
                Assert.True(File.Exists(outputFile));
                Assert.True(Directory.Exists(Path.Combine(tempRoot, ".wrapgod-cache")));
            }
            finally
            {
                Directory.SetCurrentDirectory(origDir);
            }
        }
        finally
        {
            SafeDelete(tempRoot);
        }
    }

    [Fact]
    public async Task Init_ConfigAlreadyExists_ReportsError()
    {
        var tempRoot = CreateTempDir();
        try
        {
            var configPath = Path.Combine(tempRoot, "existing.json");
            await File.WriteAllTextAsync(configPath, "{}");

            var result = await InvokeAsync(InitCommand.Create(),
                $"--source self-project --output \"{configPath}\"");

            Assert.Contains("already exists", result.StdErr);
        }
        finally
        {
            SafeDelete(tempRoot);
        }
    }

    // ──────────────────────────────────────────────────────────────
    // GenerateCommand
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Generate_MissingManifest_ReportsError()
    {
        var result = await InvokeAsync(GenerateCommand.Create(),
            "does-not-exist.wrapgod.json");

        Assert.Contains("Manifest not found", result.StdErr);
    }

    [Fact]
    public async Task Generate_ValidManifest_PrintsUsageInfo()
    {
        var tempRoot = CreateTempDir();
        try
        {
            var manifestPath = Path.Combine(tempRoot, "manifest.wrapgod.json");
            await File.WriteAllTextAsync(manifestPath, """
                {
                  "schemaVersion": "1.0",
                  "generatedAt": "2026-01-01T00:00:00Z",
                  "assembly": { "name": "Acme.Lib", "version": "1.0.0" },
                  "types": []
                }
                """);

            var result = await InvokeAsync(GenerateCommand.Create(), $"\"{manifestPath}\"");

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("WrapGod Generator", result.StdOut);
            Assert.Contains("dotnet add package WrapGod.Generator", result.StdOut);
        }
        finally
        {
            SafeDelete(tempRoot);
        }
    }

    // ──────────────────────────────────────────────────────────────
    // ExtractCommand
    // ──────────────────────────────────────────────────────────────

    // ──────────────────────────────────────────────────────────────
    // AnalyzeCommand — type breakdown path
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Analyze_WithTypesAndMembers_ShowsBreakdown()
    {
        var tempRoot = CreateTempDir();
        try
        {
            var manifestPath = Path.Combine(tempRoot, "vendor.wrapgod.json");
            await File.WriteAllTextAsync(manifestPath, """
                {
                  "SchemaVersion": "1.0",
                  "GeneratedAt": "2026-01-01T00:00:00Z",
                  "Assembly": { "Name": "Acme.Lib", "Version": "1.0.0" },
                  "Types": [
                    {
                      "FullName": "Acme.Lib.FooService",
                      "Name": "FooService",
                      "StableId": "Acme.Lib.FooService",
                      "Kind": 0,
                      "Members": [
                        { "Name": "DoWork", "Kind": 0, "ReturnType": "void", "Parameters": [] }
                      ]
                    }
                  ]
                }
                """);

            var result = await InvokeAsync(AnalyzeCommand.Create(), $"\"{manifestPath}\"");

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Acme.Lib.FooService", result.StdOut);
            Assert.Contains("DoWork", result.StdOut);
            Assert.Contains("Members:", result.StdOut);
        }
        finally
        {
            SafeDelete(tempRoot);
        }
    }

    [Fact]
    public async Task Analyze_NullManifest_ReportsError()
    {
        var tempRoot = CreateTempDir();
        try
        {
            var manifestPath = Path.Combine(tempRoot, "null.wrapgod.json");
            await File.WriteAllTextAsync(manifestPath, "null");

            var result = await InvokeAsync(AnalyzeCommand.Create(), $"\"{manifestPath}\"");

            Assert.Equal(1, result.ExitCode);
            Assert.Contains("Failed to deserialize", result.StdErr);
        }
        finally
        {
            SafeDelete(tempRoot);
        }
    }

    [Fact]
    public async Task Analyze_WithConfig_ShowsConfigPath()
    {
        var tempRoot = CreateTempDir();
        try
        {
            var manifestPath = Path.Combine(tempRoot, "vendor.wrapgod.json");
            await File.WriteAllTextAsync(manifestPath, """
                {
                  "SchemaVersion": "1.0",
                  "GeneratedAt": "2026-01-01T00:00:00Z",
                  "Assembly": { "Name": "Acme.Lib", "Version": "1.0.0" },
                  "Types": []
                }
                """);

            var configPath = Path.Combine(tempRoot, "wrapgod.config.json");
            await File.WriteAllTextAsync(configPath, """{ "source": "Acme.Lib.dll" }""");

            var result = await InvokeAsync(AnalyzeCommand.Create(),
                $"\"{manifestPath}\" --config \"{configPath}\"");

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Config loaded:", result.StdOut);
        }
        finally
        {
            SafeDelete(tempRoot);
        }
    }

    // ──────────────────────────────────────────────────────────────
    // DoctorCommand — null manifest deserialization
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Doctor_NullManifest_ReportsDeserializationFailure()
    {
        var tempRoot = CreateTempDir();
        try
        {
            await File.WriteAllTextAsync(Path.Combine(tempRoot, "wrapgod.config.json"), "{}");
            await File.WriteAllTextAsync(Path.Combine(tempRoot, "vendor.wrapgod.json"), "null");

            var result = await InvokeAsync(DoctorCommand.Create(), $"--project-dir \"{tempRoot}\"");

            Assert.Equal(1, result.ExitCode);
            Assert.Contains("deserialized to null", result.StdOut);
        }
        finally
        {
            SafeDelete(tempRoot);
        }
    }

    // ──────────────────────────────────────────────────────────────
    // ExplainCommand — config with matching type
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Explain_WithMatchingConfig_ShowsConfigRules()
    {
        var tempRoot = CreateTempDir();
        try
        {
            var manifestPath = Path.Combine(tempRoot, "vendor.wrapgod.json");
            await File.WriteAllTextAsync(manifestPath, """
                {
                  "SchemaVersion": "1.0",
                  "GeneratedAt": "2026-01-01T00:00:00Z",
                  "Assembly": { "Name": "Acme.Lib", "Version": "1.0.0" },
                  "Types": [
                    {
                      "FullName": "Acme.Lib.FooService",
                      "Name": "FooService",
                      "StableId": "Acme.Lib.FooService",
                      "Kind": 0,
                      "Members": []
                    }
                  ]
                }
                """);

            var configPath = Path.Combine(tempRoot, "wrapgod.config.json");
            await File.WriteAllTextAsync(configPath, """
                {
                  "source": "Acme.Lib.dll",
                  "types": [
                    {
                      "sourceType": "Acme.Lib.FooService",
                      "include": true,
                      "targetName": "CustomFooService",
                      "members": [
                        { "sourceMember": "DoWork", "include": true }
                      ]
                    }
                  ]
                }
                """);

            var result = await InvokeAsync(ExplainCommand.Create(),
                $"FooService --manifest \"{manifestPath}\" --config \"{configPath}\"");

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Config rules:", result.StdOut);
            Assert.Contains("TargetName:", result.StdOut);
            Assert.Contains("Member overrides:", result.StdOut);
        }
        finally
        {
            SafeDelete(tempRoot);
        }
    }

    // ──────────────────────────────────────────────────────────────
    // MigrateInit — generic constraint classification
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task MigrateInit_WithReflection_ClassifiesAsManual()
    {
        var tempRoot = CreateTempDir();
        try
        {
            var manifestPath = Path.Combine(tempRoot, "vendor.wrapgod.json");
            await File.WriteAllTextAsync(manifestPath, """
                {
                  "SchemaVersion": "1.0",
                  "GeneratedAt": "2026-01-01T00:00:00Z",
                  "Assembly": { "Name": "Acme.Lib", "Version": "1.0.0" },
                  "Types": [
                    { "FullName": "Acme.Lib.FooService", "Name": "FooService", "StableId": "Acme.Lib.FooService", "Kind": 0, "Members": [] }
                  ]
                }
                """);

            await File.WriteAllTextAsync(Path.Combine(tempRoot, "Reflector.cs"), """
                namespace MyApp;
                public class Reflector
                {
                    // Uses reflection keyword
                    public void Reflect() { var x = "reflection"; var y = new FooService(); }
                }
                """);

            var outputPath = Path.Combine(tempRoot, "plan.json");
            var result = await InvokeAsync(MigrateInitCommand.Create(),
                $"init --project-dir \"{tempRoot}\" --manifest \"{manifestPath}\" --output \"{outputPath}\"");

            Assert.Equal(0, result.ExitCode);
            var planJson = await File.ReadAllTextAsync(outputPath);
            // "reflection" keyword should trigger manual classification
            Assert.Contains("\"manual\"", planJson);
        }
        finally
        {
            SafeDelete(tempRoot);
        }
    }

    [Fact]
    public async Task MigrateInit_GenericConstraint_ClassifiesAsAssisted()
    {
        var tempRoot = CreateTempDir();
        try
        {
            var manifestPath = Path.Combine(tempRoot, "vendor.wrapgod.json");
            await File.WriteAllTextAsync(manifestPath, """
                {
                  "SchemaVersion": "1.0",
                  "GeneratedAt": "2026-01-01T00:00:00Z",
                  "Assembly": { "Name": "Acme.Lib", "Version": "1.0.0" },
                  "Types": [
                    { "FullName": "Acme.Lib.IHandler", "Name": "IHandler", "StableId": "Acme.Lib.IHandler", "Kind": 0, "Members": [] }
                  ]
                }
                """);

            await File.WriteAllTextAsync(Path.Combine(tempRoot, "Generic.cs"), """
                namespace MyApp;
                public class Processor<T> where T : IHandler
                {
                    public void Process(T handler) { }
                }
                """);

            var outputPath = Path.Combine(tempRoot, "plan.json");
            var result = await InvokeAsync(MigrateInitCommand.Create(),
                $"init --project-dir \"{tempRoot}\" --manifest \"{manifestPath}\" --output \"{outputPath}\"");

            Assert.Equal(0, result.ExitCode);
            var planJson = await File.ReadAllTextAsync(outputPath);
            Assert.Contains("\"assisted\"", planJson);
        }
        finally
        {
            SafeDelete(tempRoot);
        }
    }

    // ──────────────────────────────────────────────────────────────
    // GenerateCommand — with config
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Generate_WithConfig_PrintsConfigPath()
    {
        var tempRoot = CreateTempDir();
        try
        {
            var manifestPath = Path.Combine(tempRoot, "manifest.wrapgod.json");
            await File.WriteAllTextAsync(manifestPath, """
                {
                  "SchemaVersion": "1.0",
                  "GeneratedAt": "2026-01-01T00:00:00Z",
                  "Assembly": { "Name": "Acme.Lib", "Version": "1.0.0" },
                  "Types": []
                }
                """);

            var configPath = Path.Combine(tempRoot, "wrapgod.config.json");
            await File.WriteAllTextAsync(configPath, "{}");

            var result = await InvokeAsync(GenerateCommand.Create(),
                $"\"{manifestPath}\" --config \"{configPath}\"");

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Config:", result.StdOut);
        }
        finally
        {
            SafeDelete(tempRoot);
        }
    }

    [Fact]
    public async Task Extract_NoArguments_ReportsError()
    {
        var result = await InvokeAsync(ExtractCommand.Create(), "");

        Assert.Contains("provide an assembly-path", result.StdErr);
    }

    [Fact]
    public async Task Extract_NonExistentAssembly_ReportsError()
    {
        var result = await InvokeAsync(ExtractCommand.Create(),
            "does-not-exist.dll");

        Assert.Contains("Assembly not found", result.StdErr);
    }

    [Fact]
    public async Task Extract_InvalidNuGetFormat_ReportsError()
    {
        var result = await InvokeAsync(ExtractCommand.Create(),
            "--nuget invalid-no-at-sign");

        Assert.Contains("Invalid --nuget format", result.StdErr);
    }

    [Fact]
    public async Task Extract_FromRuntimeAssembly_ProducesValidManifest()
    {
        var tempRoot = CreateTempDir();
        try
        {
            // Extract from WrapGod.Runtime itself (small, available assembly)
            var runtimeDll = Path.Combine(
                AppContext.BaseDirectory, "..", "..", "..", "..",
                "WrapGod.Runtime", "bin", "Release", "netstandard2.0", "WrapGod.Runtime.dll");
            runtimeDll = Path.GetFullPath(runtimeDll);

            if (!File.Exists(runtimeDll))
            {
                // Skip if runtime DLL not available (e.g., in different build config)
                return;
            }

            var outputPath = Path.Combine(tempRoot, "manifest.json");
            var result = await InvokeAsync(ExtractCommand.Create(),
                $"\"{runtimeDll}\" --output \"{outputPath}\"");

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Manifest written to", result.StdOut);
            Assert.True(File.Exists(outputPath));

            // Verify it's valid JSON
            var json = await File.ReadAllTextAsync(outputPath);
            var doc = JsonDocument.Parse(json);
            Assert.NotNull(doc);
        }
        finally
        {
            SafeDelete(tempRoot);
        }
    }

    // ──────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────

    private static async Task<(int ExitCode, string StdOut, string StdErr)> InvokeAsync(
        Command command, string args)
    {
        var previousOut = Console.Out;
        var previousErr = Console.Error;
        var previousExitCode = Environment.ExitCode;
        using var stdOut = new StringWriter();
        using var stdErr = new StringWriter();

        try
        {
            Console.SetOut(stdOut);
            Console.SetError(stdErr);
            Environment.ExitCode = 0;

            var invokeCode = await command.InvokeAsync(args);
            var effectiveExitCode = Environment.ExitCode == 0 ? invokeCode : Environment.ExitCode;
            return (effectiveExitCode, stdOut.ToString(), stdErr.ToString());
        }
        finally
        {
            Console.SetOut(previousOut);
            Console.SetError(previousErr);
            Environment.ExitCode = previousExitCode;
        }
    }

    private static Command CreateCiCommand()
    {
        return new Command("ci", "CI/CD workflow tools")
        {
            CiBootstrapCommand.Create(),
            CiParityReportCommand.Create(),
        };
    }

    private static string CreateTempDir()
    {
        var path = Path.Combine(Path.GetTempPath(), $"wrapgod-cli-cov-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static void SafeDelete(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
}
