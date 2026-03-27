namespace VendorLib;

/// <summary>
/// A simulated third-party logger. Wrapping it lets you swap
/// implementations without touching consuming code.
/// </summary>
public class Logger
{
    /// <summary>The minimum log level.</summary>
    public LogLevel MinLevel { get; set; } = LogLevel.Info;

    /// <summary>Log an informational message.</summary>
    public void Info(string message) => Console.WriteLine($"[INFO] {message}");

    /// <summary>Log a warning message.</summary>
    public void Warn(string message) => Console.WriteLine($"[WARN] {message}");

    /// <summary>Log an error message.</summary>
    public void Error(string message) => Console.WriteLine($"[ERROR] {message}");
}

/// <summary>
/// Log severity levels used by the vendor logger.
/// </summary>
public enum LogLevel
{
    Debug,
    Info,
    Warn,
    Error,
}
