using WrapGod.Cli;

namespace WrapGod.Tests;

public class InitScaffolderTests
{
    [Fact]
    public void Scaffold_CreatesBaselineFiles()
    {
        var tempDir = CreateTempDirectory();

        var result = InitScaffolder.Scaffold(tempDir, new InitScaffoldOptions(
            DryRun: false,
            IncludeSamples: false,
            IncludeCi: false));

        Assert.Equal(4, result.CreatedCount);
        Assert.Equal(0, result.SkippedCount);
        Assert.Contains("wrapgod.root.json", result.CreatedFiles);
        Assert.Contains("wrapgod.project.json", result.CreatedFiles);
        Assert.Contains("wrapgod-types.txt", result.CreatedFiles);
        Assert.Contains("docs/wrapgod-init.md", result.CreatedFiles);

        Assert.True(File.Exists(Path.Combine(tempDir, "wrapgod.root.json")));
        Assert.True(File.Exists(Path.Combine(tempDir, "wrapgod.project.json")));
        Assert.True(File.Exists(Path.Combine(tempDir, "wrapgod-types.txt")));
        Assert.True(File.Exists(Path.Combine(tempDir, "docs", "wrapgod-init.md")));
    }

    [Fact]
    public void Scaffold_IsIdempotent_WhenRunTwice()
    {
        var tempDir = CreateTempDirectory();
        var options = new InitScaffoldOptions(DryRun: false, IncludeSamples: true, IncludeCi: true);

        var first = InitScaffolder.Scaffold(tempDir, options);
        var second = InitScaffolder.Scaffold(tempDir, options);

        Assert.True(first.CreatedCount > 0);
        Assert.Equal(0, second.CreatedCount);
        Assert.Equal(first.CreatedCount, second.SkippedCount);
    }

    [Fact]
    public void Scaffold_DryRun_DoesNotWriteFiles()
    {
        var tempDir = CreateTempDirectory();

        var result = InitScaffolder.Scaffold(tempDir, new InitScaffoldOptions(
            DryRun: true,
            IncludeSamples: true,
            IncludeCi: true));

        Assert.Equal(6, result.CreatedCount);
        Assert.Equal(0, result.SkippedCount);
        Assert.False(File.Exists(Path.Combine(tempDir, "wrapgod.root.json")));
        Assert.False(File.Exists(Path.Combine(tempDir, "wrapgod.project.json")));
        Assert.False(File.Exists(Path.Combine(tempDir, "wrapgod-types.txt")));
        Assert.False(File.Exists(Path.Combine(tempDir, "wrapgod-types.sample.txt")));
        Assert.False(File.Exists(Path.Combine(tempDir, ".github", "workflows", "wrapgod-starter.yml")));
    }

    [Fact]
    public void Scaffold_OptionalFlags_CreateOptionalFiles()
    {
        var tempDir = CreateTempDirectory();

        var result = InitScaffolder.Scaffold(tempDir, new InitScaffoldOptions(
            DryRun: false,
            IncludeSamples: true,
            IncludeCi: true));

        Assert.Equal(6, result.CreatedCount);
        Assert.Contains("wrapgod-types.sample.txt", result.CreatedFiles);
        Assert.Contains(".github/workflows/wrapgod-starter.yml", result.CreatedFiles);

        Assert.True(File.Exists(Path.Combine(tempDir, "wrapgod-types.sample.txt")));
        Assert.True(File.Exists(Path.Combine(tempDir, ".github", "workflows", "wrapgod-starter.yml")));
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"wrapgod-init-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
