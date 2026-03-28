# Serilog <-> NLog Mapping Matrix

| Concern | Serilog API | NLog API | Direction | Safety | Notes |
|---|---|---|---|---|---|
| Logger acquisition | `new LoggerConfiguration().CreateLogger()` | `LogManager.GetLogger(name)` | both | safe | Prefer explicit logger name in NLog |
| Information event | `logger.Information(template, args)` | `logger.Info(template, args)` | both | safe | Template interpolation semantics differ slightly |
| Debug event | `logger.Debug(template, args)` | `logger.Debug(template, args)` | both | safe | Equivalent intent |
| Error with exception | `logger.Error(ex, template, args)` | `logger.Error(ex, template, args)` | both | safe | Preserve exception as first-class field |
| Global enrichment/context | `.Enrich.WithProperty(key, value)` | `LogEventInfo.Properties[key]=value`/`ScopeContext` | both | review | NLog context propagation APIs vary by integration |
| Structured arguments | `{OrderId}` / `{Total}` tokens | message template + `LogEventInfo.Parameters` | both | review | Positional vs named fidelity can vary by renderer |
| Sink/target registration | `.WriteTo.Sink(...)` | `LoggingConfiguration.AddRuleForAllLevels(target)` | both | review | Routing/filter semantics are not 1:1 |
| Minimum level | `.MinimumLevel.Debug()` | `LoggingRule` + `LogLevel` | both | safe | Keep threshold behavior explicit |
| Async pipeline wrappers | `WriteTo.Async(...)` | `AsyncTargetWrapper` | both | manual | Buffering and flush semantics differ |
| JSON rendering | `RenderedCompactJsonFormatter` | `JsonLayout` | both | manual | Property naming/order and exception schema differ |
