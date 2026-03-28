using NLog;
using NLog.Config;
using NLog.Targets;

namespace NLogApp;

public static class NLogOrderLogger
{
    public static IReadOnlyList<LogEventRecord> CaptureOrderWorkflowLogs(string orderId, decimal total)
    {
        var sink = new InMemoryTarget();
        var config = new LoggingConfiguration();
        config.AddRuleForAllLevels(sink);

        LogManager.Configuration = config;
        var logger = LogManager.GetLogger("checkout");

        var contextProperties = new Dictionary<string, object?> { ["service"] = "checkout" };

        LogWithProperties(logger, LogLevel.Info, "Order {OrderId} started", contextProperties, orderId);
        LogWithProperties(logger, LogLevel.Debug, "Order {OrderId} total {Total}", contextProperties, orderId, total);

        try
        {
            ThrowIfInvalid(total);
            LogWithProperties(logger, LogLevel.Info, "Order {OrderId} completed", contextProperties, orderId);
        }
        catch (InvalidOperationException ex)
        {
            LogWithProperties(logger, LogLevel.Error, "Order {OrderId} failed", contextProperties, orderId, exception: ex);
        }

        LogManager.Shutdown();
        return sink.Events;
    }

    private static void LogWithProperties(
        Logger logger,
        LogLevel level,
        string template,
        IReadOnlyDictionary<string, object?> baseProperties,
        object? arg1,
        object? arg2 = null,
        Exception? exception = null)
    {
        var evt = new LogEventInfo
        {
            Level = level,
            LoggerName = logger.Name,
            Message = template,
            Parameters = new[] { arg1, arg2 }.Where(v => v is not null).ToArray(),
            Exception = exception
        };
        foreach (var (key, value) in baseProperties)
        {
            evt.Properties[key] = value;
        }

        logger.Log(evt);
    }

    private static void ThrowIfInvalid(decimal total)
    {
        if (total <= 0)
        {
            throw new InvalidOperationException("Total must be greater than zero.");
        }
    }

    private sealed class InMemoryTarget : TargetWithLayout
    {
        private readonly List<LogEventRecord> _events = [];

        public IReadOnlyList<LogEventRecord> Events => _events;

        protected override void Write(LogEventInfo logEvent)
        {
            var properties = logEvent.Properties.ToDictionary(
                kvp => kvp.Key.ToString()!,
                kvp => kvp.Value);

            _events.Add(new LogEventRecord(
                logEvent.Level.Name.ToUpperInvariant(),
                logEvent.FormattedMessage,
                properties,
                logEvent.Exception));
        }
    }
}
