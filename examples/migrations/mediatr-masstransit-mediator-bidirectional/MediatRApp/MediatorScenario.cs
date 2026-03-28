using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace MediatRApp;

public sealed record ScenarioResult(string Response, IReadOnlyList<string> Trace);

public static class MediatrScenario
{
    public static async Task<ScenarioResult> RunAsync()
    {
        var services = new ServiceCollection();
        services.AddSingleton<TraceLog>();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<PingRequest>());
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(TracingBehavior<,>));

        await using var provider = services.BuildServiceProvider();

        var mediator = provider.GetRequiredService<IMediator>();
        var trace = provider.GetRequiredService<TraceLog>();

        var response = await mediator.Send(new PingRequest("hello"));
        await mediator.Publish(new OrderCreated("ORD-100"));

        return new ScenarioResult(response, trace.Entries.ToList());
    }

    public sealed class TraceLog
    {
        public List<string> Entries { get; } = [];
    }

    public sealed record PingRequest(string Text) : IRequest<string>;
    public sealed record OrderCreated(string OrderId) : INotification;

    public sealed class PingHandler(TraceLog trace) : IRequestHandler<PingRequest, string>
    {
        public Task<string> Handle(PingRequest request, CancellationToken cancellationToken)
        {
            trace.Entries.Add("request:handled");
            return Task.FromResult($"pong:{request.Text}");
        }
    }

    public sealed class OrderCreatedHandler(TraceLog trace) : INotificationHandler<OrderCreated>
    {
        public Task Handle(OrderCreated notification, CancellationToken cancellationToken)
        {
            trace.Entries.Add("notification:handled");
            return Task.CompletedTask;
        }
    }

    public sealed class TracingBehavior<TRequest, TResponse>(TraceLog trace)
        : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            trace.Entries.Add($"pipeline:before:{typeof(TRequest).Name}");
            var response = await next(cancellationToken);
            trace.Entries.Add($"pipeline:after:{typeof(TRequest).Name}");
            return response;
        }
    }
}
