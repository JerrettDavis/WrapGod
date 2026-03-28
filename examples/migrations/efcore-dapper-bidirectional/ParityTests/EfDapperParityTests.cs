using Xunit;

namespace ParityTests;

public class EfDapperParityTests
{
    [Fact]
    public void EfCore_and_Dapper_scenarios_emit_equivalent_repository_outputs()
    {
        var ef = EfCoreApp.EfOrderService.RunScenario();
        var dapper = DapperApp.DapperOrderService.RunScenario();

        Assert.Equal(ef.Count, dapper.Count);

        for (var i = 0; i < ef.Count; i++)
        {
            Assert.Equal(ef[i].Id, dapper[i].Id);
            Assert.Equal(ef[i].CustomerName, dapper[i].CustomerName);
            Assert.Equal(ef[i].Total, dapper[i].Total);
            Assert.Equal(ef[i].IsPaid, dapper[i].IsPaid);
        }
    }

    [Fact]
    public void Service_boundary_transition_pattern_is_staged_and_deterministic()
    {
        var ef = EfCoreApp.EfOrderService.RunScenario();
        Assert.All(ef, o => Assert.True(o.IsPaid));

        var dapper = DapperApp.DapperOrderService.RunScenario();
        Assert.All(dapper, o => Assert.True(o.IsPaid));

        // common service boundary expectation:
        Assert.Equal(ef.Select(o => o.Id), dapper.Select(o => o.Id));
    }
}
