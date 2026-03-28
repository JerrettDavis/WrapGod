using TinyBDD;
using TinyBDD.Xunit;
using WrapGod.Extractor;
using Xunit.Abstractions;

namespace WrapGod.Tests;

[Feature("NuGet source hardening — error handling and validation")]
public sealed class NuGetHardeningTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    private readonly string _cacheRoot =
        Path.Combine(Path.GetTempPath(), "wrapgod-hardening-" + Guid.NewGuid().ToString("N")[..8]);

    private NuGetPackageResolver CreateResolver() => new(_cacheRoot);

    // --- Package not found ---

    [Scenario("Resolve non-existent package produces clear error")]
    [Fact]
    public Task PackageNotFound_ClearError() =>
        Given("a resolver targeting a non-existent package", () => CreateResolver())
        .Then("resolve throws with descriptive message",
            (Func<NuGetPackageResolver, Task<bool>>)(async resolver =>
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => resolver.ResolveAsync("NonExistent.Package.ZZZ999", "1.0.0"));
            return ex.Message.Contains("NonExistent.Package.ZZZ999")
                && ex.Message.Contains("not found");
        }))
        .AssertPassed();

    // --- Version not found ---

    [Scenario("Resolve existing package with non-existent version produces clear error")]
    [Fact]
    public Task VersionNotFound_ClearError() =>
        Given("a resolver targeting a real package with bad version", () => CreateResolver())
        .Then("resolve throws with version in message",
            (Func<NuGetPackageResolver, Task<bool>>)(async resolver =>
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => resolver.ResolveAsync("Newtonsoft.Json", "999.999.999"));
            return ex.Message.Contains("999.999.999")
                && ex.Message.Contains("not found");
        }))
        .AssertPassed();

    // --- TFM not available ---

    [Scenario("Resolve with unavailable TFM produces clear error listing available TFMs")]
    [Fact]
    public Task TfmNotAvailable_ClearError() =>
        Given("a resolver targeting a TFM that does not exist in the package", () => CreateResolver())
        .Then("resolve throws listing available TFMs",
            (Func<NuGetPackageResolver, Task<bool>>)(async resolver =>
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => resolver.ResolveAsync("Newtonsoft.Json", "13.0.3", "net1.0"));
            return ex.Message.Contains("net1.0")
                && ex.Message.Contains("Available");
        }))
        .AssertPassed();

    // --- Invalid package ID ---

    [Scenario("Resolve with empty package ID throws argument error")]
    [Fact]
    public Task InvalidPackageId_Empty() =>
        Given("a resolver", () => CreateResolver())
        .Then("empty packageId throws ArgumentException",
            (Func<NuGetPackageResolver, Task<bool>>)(async resolver =>
        {
            await Assert.ThrowsAsync<ArgumentException>(
                () => resolver.ResolveAsync("", "1.0.0"));
            return true;
        }))
        .AssertPassed();

    [Scenario("Resolve with whitespace package ID throws argument error")]
    [Fact]
    public Task InvalidPackageId_Whitespace() =>
        Given("a resolver", () => CreateResolver())
        .Then("whitespace packageId throws ArgumentException",
            (Func<NuGetPackageResolver, Task<bool>>)(async resolver =>
        {
            await Assert.ThrowsAsync<ArgumentException>(
                () => resolver.ResolveAsync("   ", "1.0.0"));
            return true;
        }))
        .AssertPassed();

    [Scenario("Resolve with empty version throws argument error")]
    [Fact]
    public Task InvalidVersion_Empty() =>
        Given("a resolver", () => CreateResolver())
        .Then("empty version throws ArgumentException",
            (Func<NuGetPackageResolver, Task<bool>>)(async resolver =>
        {
            await Assert.ThrowsAsync<ArgumentException>(
                () => resolver.ResolveAsync("Newtonsoft.Json", ""));
            return true;
        }))
        .AssertPassed();

    // --- No secrets in error messages ---

    [Scenario("Error messages do not leak feed URLs as secrets")]
    [Fact]
    public Task NoSecretsInErrors() =>
        Given("a resolver targeting a non-existent package on the default feed", () => CreateResolver())
        .Then("error message contains feed URL but no auth tokens",
            (Func<NuGetPackageResolver, Task<bool>>)(async resolver =>
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => resolver.ResolveAsync("NonExistent.Package.ZZZ999", "1.0.0"));
            return !ex.Message.Contains("password", StringComparison.OrdinalIgnoreCase)
                && !ex.Message.Contains("token", StringComparison.OrdinalIgnoreCase)
                && !ex.Message.Contains("secret", StringComparison.OrdinalIgnoreCase)
                && !ex.Message.Contains("apikey", StringComparison.OrdinalIgnoreCase);
        }))
        .AssertPassed();

    // --- Network timeout handling ---

    [Scenario("Cancellation token is respected during resolve")]
    [Fact]
    public Task CancellationTokenRespected() =>
        Given("a resolver with an already-cancelled token", () =>
            (Resolver: CreateResolver(), Token: new CancellationToken(canceled: true)))
        .Then("resolve throws OperationCanceledException",
            (Func<(NuGetPackageResolver Resolver, CancellationToken Token), Task<bool>>)(async ctx =>
        {
            try
            {
                await ctx.Resolver.ResolveAsync("Newtonsoft.Json", "13.0.3",
                    cancellationToken: ctx.Token);
                return false;
            }
            catch (OperationCanceledException)
            {
                return true;
            }
        }))
        .AssertPassed();
}
