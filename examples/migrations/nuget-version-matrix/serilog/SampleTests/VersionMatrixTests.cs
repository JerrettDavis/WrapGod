// VersionMatrixDemo.cs
// Demonstrates Serilog logging patterns that differ across v2, v3, and v4.
// This file is for illustration only — it does not compile standalone.

using Serilog;
using Serilog.Events;

namespace WrapGod.Examples.Serilog.VersionMatrix;

/// <summary>
/// Shows how the same logging scenarios are expressed differently
/// across Serilog major versions, and how WrapGod strategies handle them.
/// </summary>
public static class VersionMatrixDemo
{
    // ---------------------------------------------------------------
    // Scenario 1: Basic Configuration — identical across all versions
    // ---------------------------------------------------------------
    public static void ConfigureBasicLogging()
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .Enrich.WithProperty("Application", "VersionMatrixDemo")
            .WriteTo.Console()
            .CreateLogger();
    }

    // ---------------------------------------------------------------
    // Scenario 2: Shutdown — sync vs async
    // ---------------------------------------------------------------

    // v2.x only — synchronous flush
    public static void ShutdownV2()
    {
        Log.CloseAndFlush();
    }

    // v3+ — async flush preferred
    public static async Task ShutdownV3Plus()
    {
        await Log.CloseAndFlushAsync();
    }

    // Adaptive wrapper — works on any version
    public static async Task ShutdownAdaptive()
    {
#if SERILOG_V3_PLUS
        await Log.CloseAndFlushAsync();
#else
        Log.CloseAndFlush();
        await Task.CompletedTask;
#endif
    }

    // ---------------------------------------------------------------
    // Scenario 3: Trace Correlation — manual vs first-class
    // ---------------------------------------------------------------

    // v2.x — manual trace context via properties
    public static void LogWithTraceContextV2()
    {
        var activity = System.Diagnostics.Activity.Current;
        Log.ForContext("TraceId", activity?.TraceId.ToString())
           .ForContext("SpanId", activity?.SpanId.ToString())
           .Information("Processing request");
    }

    // v3.1+ — TraceId/SpanId auto-populated from Activity.Current
    // No code changes needed; sinks automatically receive trace context
    public static void LogWithTraceContextV3Plus()
    {
        // TraceId and SpanId are automatically captured from Activity.Current
        // and available as LogEvent.TraceId and LogEvent.SpanId
        Log.Information("Processing request");
    }

    // ---------------------------------------------------------------
    // Scenario 4: Conditional Enrichment — custom vs built-in
    // ---------------------------------------------------------------

    // v2.x — requires a custom ILogEventEnricher
    public static void ConditionalEnrichmentV2()
    {
        Log.Logger = new LoggerConfiguration()
            .Enrich.With(new AlertOnWarningEnricher())
            .WriteTo.Console()
            .CreateLogger();
    }

    // v3.1+ — built-in When() method
    public static void ConditionalEnrichmentV3Plus()
    {
        Log.Logger = new LoggerConfiguration()
            .Enrich.When(
                evt => evt.Level >= LogEventLevel.Warning,
                e => e.WithProperty("Alert", true))
            .WriteTo.Console()
            .CreateLogger();
    }

    // ---------------------------------------------------------------
    // Scenario 5: Batched Sink — three different approaches
    // ---------------------------------------------------------------

    // v2.x — requires Serilog.Sinks.PeriodicBatching NuGet package
    // public static void BatchedSinkV2()
    // {
    //     Log.Logger = new LoggerConfiguration()
    //         .WriteTo.Sink(new PeriodicBatchingSink(
    //             new MyBatchedSink(),
    //             new PeriodicBatchingSinkOptions { BatchSizeLimit = 100 }))
    //         .CreateLogger();
    // }

    // v3.x — IBatchedLogEventSink in core, no BatchingOptions yet
    // public static void BatchedSinkV3()
    // {
    //     Log.Logger = new LoggerConfiguration()
    //         .WriteTo.Sink(new MyBatchedSink())
    //         .CreateLogger();
    // }

    // v4.x — IBatchedLogEventSink with BatchingOptions
    // public static void BatchedSinkV4()
    // {
    //     Log.Logger = new LoggerConfiguration()
    //         .WriteTo.Sink(new MyBatchedSink(), new BatchingOptions
    //         {
    //             BatchSizeLimit = 1000,
    //             BufferingTimeLimit = TimeSpan.FromSeconds(2),
    //             QueueLimit = 10000,
    //             EagerlyEmitFirstEvent = true
    //         })
    //         .CreateLogger();
    // }

    // ---------------------------------------------------------------
    // Scenario 6: Level Override Case Sensitivity
    // ---------------------------------------------------------------

    public static void LevelOverrideBehavior()
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            // v2/v3: exact case match — "Microsoft.AspNetCore" only
            // v4:    case-insensitive — matches any casing variant
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
            .WriteTo.Console()
            .CreateLogger();

        // On v2/v3, a source context of "microsoft.aspnetcore.Hosting"
        // would NOT match the override above.
        // On v4, it WOULD match due to case-insensitive comparison.
    }

    // ---------------------------------------------------------------
    // Scenario 7: CreateSink Utility — v4 only
    // ---------------------------------------------------------------

    // v4+ — build a standalone sink from configuration
    // public static ILogEventSink BuildConsoleSinkV4()
    // {
    //     return LoggerSinkConfiguration.CreateSink(
    //         lsc => lsc.Console());
    // }

    // v2/v3 — no direct equivalent; must create a full logger
    // and extract the sink, or instantiate the sink class directly
}

/// <summary>
/// Custom enricher for v2.x conditional enrichment (Scenario 4).
/// In v3.1+, this is replaced by Enrich.When().
/// </summary>
public class AlertOnWarningEnricher : Serilog.Core.ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, Serilog.Core.ILogEventPropertyFactory propertyFactory)
    {
        if (logEvent.Level >= LogEventLevel.Warning)
        {
            logEvent.AddOrUpdateProperty(
                propertyFactory.CreateProperty("Alert", true));
        }
    }
}
