using Serilog;
using Serilog.Sinks.RollingFile;

namespace SerilogV2Sample.Logging;

/// <summary>
/// Application service that demonstrates Serilog v2 usage patterns.
/// Specifically: structured logging with an ILogger and RollingFile sink wired
/// in Program.cs via WriteTo.RollingFile().
/// </summary>
public sealed class OrderService
{
    private readonly ILogger _logger;

    public OrderService(ILogger logger)
    {
        // ILogger is the same type in both v2 and v3.
        _logger = logger.ForContext<OrderService>();
    }

    public void PlaceOrder(string orderId, decimal amount)
    {
        if (amount <= 0)
        {
            _logger.Warning("Order {OrderId} has zero or negative amount {Amount}", orderId, amount);
            return;
        }

        _logger.Information("Placing order {OrderId} for {Amount:C}", orderId, amount);
        _logger.Debug("Order {OrderId} persisted to database", orderId);
    }
}
