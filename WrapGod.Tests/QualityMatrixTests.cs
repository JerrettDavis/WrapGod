using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace WrapGod.Tests;

[Feature("Quality matrix baseline")]
public sealed class QualityMatrixTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("quality matrix document exists")]
    [Fact]
    public Task QualityMatrixDocumentExists()
        => Given("the quality matrix path", () => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../docs/QUALITY-MATRIX.md")))
            .Then("the file exists", File.Exists)
            .AssertPassed();
}
