using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace SerilogApp;

public static class SerilogOrderLogger
{
    public static IReadOnlyList<LogEventRecord> CaptureOrderWorkflowLogs(string orderId, decimal total)
    {
        var sink = new InMemorySink();

        using var logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .Enrich.WithProperty("service", "checkout")
            .WriteTo.Sink(sink)
            .CreateLogger();

        logger.Information("Order {OrderId} started", orderId);
        logger.Debug("Order {OrderId} total {Total}", orderId, total);

        try
        {
            ThrowIfInvalid(total);
            logger.Information("Order {OrderId} completed", orderId);
        }
        catch (InvalidOperationException ex)
        {
            logger.Error(ex, "Order {OrderId} failed", orderId);
        }

        return sink.Events;
    }

    private static void ThrowIfInvalid(decimal total)
    {
        if (total <= 0)
        {
            throw new InvalidOperationException("Total must be greater than zero.");
        }
    }

    private sealed class InMemorySink : ILogEventSink
    {
        private readonly List<LogEventRecord> _events = [];

        public IReadOnlyList<LogEventRecord> Events => _events;

        public void Emit(LogEvent logEvent)
        {
            var props = logEvent.Properties
                .ToDictionary(
                    kvp => kvp.Key,
                    kvp => ConvertScalar(kvp.Value));

            _events.Add(new LogEventRecord(
                logEvent.Level.ToString().ToUpperInvariant(),
                logEvent.MessageTemplate.Render(logEvent.Properties),
                props,
                logEvent.Exception));
        }

        private static object? ConvertScalar(LogEventPropertyValue value)
            => value switch
            {
                ScalarValue scalar => scalar.Value,
                SequenceValue seq => seq.Elements.Select(ConvertScalar).ToArray(),
                _ => value.ToString()
            };
    }
}
