namespace NLogApp;

public sealed record LogEventRecord(
    string Level,
    string Message,
    IReadOnlyDictionary<string, object?> Properties,
    Exception? Exception = null);
