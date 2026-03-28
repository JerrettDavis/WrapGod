using NLogApp;
using SerilogApp;
using Xunit;

namespace ParityTests;

public class LogParityTests
{
    [Fact]
    public void Serilog_and_NLog_emit_equivalent_events_for_happy_path()
    {
        var serilogEvents = SerilogOrderLogger.CaptureOrderWorkflowLogs("A-100", 42.5m);
        var nlogEvents = NLogOrderLogger.CaptureOrderWorkflowLogs("A-100", 42.5m);

        AssertParity(serilogEvents, nlogEvents);
    }

    [Fact]
    public void Serilog_and_NLog_emit_equivalent_events_for_error_path()
    {
        var serilogEvents = SerilogOrderLogger.CaptureOrderWorkflowLogs("A-101", 0m);
        var nlogEvents = NLogOrderLogger.CaptureOrderWorkflowLogs("A-101", 0m);

        AssertParity(serilogEvents, nlogEvents, expectError: true);
    }

    private static void AssertParity(
        IReadOnlyList<SerilogApp.LogEventRecord> serilogEvents,
        IReadOnlyList<NLogApp.LogEventRecord> nlogEvents,
        bool expectError = false)
    {
        Assert.Equal(serilogEvents.Count, nlogEvents.Count);

        for (var i = 0; i < serilogEvents.Count; i++)
        {
            var s = serilogEvents[i];
            var n = nlogEvents[i];

            Assert.Equal(NormalizeLevel(s.Level), NormalizeLevel(n.Level));
            Assert.Equal(NormalizeMessage(s.Message), NormalizeMessage(n.Message));
            Assert.Equal(s.Properties["service"], n.Properties["service"]);
        }

        if (expectError)
        {
            Assert.NotNull(serilogEvents[^1].Exception);
            Assert.NotNull(nlogEvents[^1].Exception);
        }
    }

    private static string NormalizeLevel(string level)
        => level.ToUpperInvariant() switch
        {
            "INFORMATION" => "INFO",
            _ => level.ToUpperInvariant()
        };

    private static string NormalizeMessage(string message)
        => message.Replace("\"", string.Empty, StringComparison.Ordinal);
}
