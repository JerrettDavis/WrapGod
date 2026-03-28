using Xunit;

namespace ParityTests;

public class MediatorParityTests
{
    [Fact]
    public async Task Request_and_notification_paths_match_between_stacks()
    {
        var mediatr = await MediatRApp.MediatrScenario.RunAsync();
        var mt = await MassTransitMediatorApp.MassTransitMediatorScenario.RunAsync();

        Assert.Equal(mediatr.Response, mt.Response);
        Assert.Contains("request:handled", mediatr.Trace);
        Assert.Contains("request:handled", mt.Trace);
        Assert.Contains("notification:handled", mediatr.Trace);
        Assert.Contains("notification:handled", mt.Trace);
    }

    [Fact]
    public async Task Pipeline_behavior_equivalence_is_visible_in_trace()
    {
        var mediatr = await MediatRApp.MediatrScenario.RunAsync();
        var mt = await MassTransitMediatorApp.MassTransitMediatorScenario.RunAsync();

        Assert.Contains("pipeline:before:PingRequest", mediatr.Trace);
        Assert.Contains("pipeline:after:PingRequest", mediatr.Trace);
        Assert.Contains("pipeline:before:PingRequest", mt.Trace);
        Assert.Contains("pipeline:after:PingRequest", mt.Trace);
    }
}
