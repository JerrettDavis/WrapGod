namespace MassTransitMediatorApp;

public sealed record ScenarioResult(string Response, IReadOnlyList<string> Trace);

public static class MassTransitMediatorScenario
{
    public static Task<ScenarioResult> RunAsync()
    {
        var trace = new TraceLog();

        var response = HandleRequest(new PingRequest("hello"), trace);
        HandleNotification(new OrderCreated("ORD-100"), trace);

        return Task.FromResult(new ScenarioResult(response.Text, trace.Entries.ToList()));
    }

    private static PongResponse HandleRequest(PingRequest request, TraceLog trace)
    {
        trace.Entries.Add("pipeline:before:PingRequest");
        trace.Entries.Add("request:handled");
        var response = new PongResponse($"pong:{request.Text}");
        trace.Entries.Add("pipeline:after:PingRequest");
        return response;
    }

    private static void HandleNotification(OrderCreated notification, TraceLog trace)
    {
        _ = notification;
        trace.Entries.Add("notification:handled");
    }

    public sealed class TraceLog
    {
        public List<string> Entries { get; } = [];
    }

    public sealed record PingRequest(string Text);
    public sealed record PongResponse(string Text);
    public sealed record OrderCreated(string OrderId);
}
