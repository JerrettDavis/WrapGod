using Serilog;
using Serilog.Sinks.RollingFile;
using SerilogV2Sample.Logging;

// ── Serilog v2 bootstrap ──────────────────────────────────────────────────
// Serilog v2 ships WriteTo.RollingFile as a separate NuGet package
// (Serilog.Sinks.RollingFile). The using directive above references its
// namespace. In Serilog v3 this sink was removed; its functionality was
// merged into Serilog.Sinks.File with a rollingInterval parameter.
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console()
    .WriteTo.RollingFile("logs/app-{Date}.log")
    .CreateLogger();

try
{
    Log.Information("Application starting");

    var service = new OrderService(Log.Logger);
    service.PlaceOrder("ORD-001", 99.95m);
    service.PlaceOrder("ORD-002", 0m);   // triggers warning path

    Log.Information("Application shutting down");
}
catch (Exception ex)
{
    Log.Fatal(ex, "Unhandled exception");
}
finally
{
    Log.CloseAndFlush();
}
