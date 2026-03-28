# Diagnostics and Manual Migration Guidance

The following patterns should be flagged as **review/manual** during automated migration.

## Unsupported or risky sink/target semantics

1. **Custom Serilog sinks with side effects**
   - Example: sink writes to external service with custom batching logic
   - NLog equivalent may require custom target implementation
   - Recommendation: emit diagnostic `WG3XXX` (manual target port required)

2. **NLog target wrappers (Buffering/Fallback/Async)**
   - Wrapper stacks can alter delivery guarantees and retry behavior
   - Serilog pipeline composition is similar but not identical
   - Recommendation: emit diagnostic `WG3XXX` (delivery semantics review)

3. **Template/rendering assumptions in dashboards**
   - Serilog message-template rendering differs from NLog layout rendering
   - Potential drift in structured fields and search queries
   - Recommendation: emit diagnostic `WG3XXX` (dashboard query update required)

4. **Scope/context propagation across async boundaries**
   - Serilog `LogContext` and NLog `ScopeContext` can diverge by host integration
   - Recommendation: emit diagnostic `WG3XXX` (trace correlation validation required)

## Manual path checklist

- Build inventory of existing sinks/targets and wrapper stacks
- Migrate baseline level + template APIs first
- Preserve structured property keys used by alerting dashboards
- Add parity tests over representative log scenarios (happy + failure paths)
- Validate throughput/flush behavior under load before production rollout
