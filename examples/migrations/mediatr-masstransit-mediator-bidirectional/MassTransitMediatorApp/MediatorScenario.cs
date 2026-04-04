using MassTransit;
using MassTransit.Mediator;
using Microsoft.Extensions.DependencyInjection;

namespace MassTransitMediatorApp;

public sealed record ScenarioResult(string Response, IReadOnlyList<string> Trace);

public static class MassTransitMediatorScenario
{
    public static async Task<ScenarioResult> RunAsync()
    {
        var services = new ServiceCollection();
        services.AddSingleton<TraceLog>();
        services.AddMediator(cfg =>
        {
            cfg.AddConsumer<PingConsumer>();
            cfg.AddConsumer<OrderCreatedConsumer>();
            cfg.ConfigureMediator((context, mediator) =>
            {
                mediator.UseConsumeFilter(typeof(TracingFilter<>), context);
            });
        });

        await using var provider = services.BuildServiceProvider();
        var trace = provider.GetRequiredService<TraceLog>();
        var mediator = provider.GetRequiredService<IMediator>();
        var requestClient = provider.GetRequiredService<IRequestClient<PingRequest>>();

        var response = await requestClient.GetResponse<PongResponse>(new PingRequest("hello"));
        await mediator.Publish(new OrderCreated("ORD-100"));

        return new ScenarioResult(response.Message.Text, trace.Entries.ToList());
    }

    public sealed class TraceLog
    {
        public List<string> Entries { get; } = [];
    }

    public sealed record PingRequest(string Text);
    public sealed record PongResponse(string Text);
    public sealed record OrderCreated(string OrderId);

    public sealed class PingConsumer(TraceLog trace) : IConsumer<PingRequest>
    {
        public Task Consume(ConsumeContext<PingRequest> context)
        {
            trace.Entries.Add("request:handled");
            return context.RespondAsync(new PongResponse($"pong:{context.Message.Text}"));
        }
    }

    public sealed class OrderCreatedConsumer(TraceLog trace) : IConsumer<OrderCreated>
    {
        public Task Consume(ConsumeContext<OrderCreated> context)
        {
            _ = context.Message;
            trace.Entries.Add("notification:handled");
            return Task.CompletedTask;
        }
    }

    public sealed class TracingFilter<TMessage>(TraceLog trace) : IFilter<ConsumeContext<TMessage>>
        where TMessage : class
    {
        public async Task Send(ConsumeContext<TMessage> context, IPipe<ConsumeContext<TMessage>> next)
        {
            trace.Entries.Add($"pipeline:before:{typeof(TMessage).Name}");
            await next.Send(context);
            trace.Entries.Add($"pipeline:after:{typeof(TMessage).Name}");
        }

        public void Probe(ProbeContext context)
        {
            context.CreateFilterScope("tracing");
        }
    }
}
