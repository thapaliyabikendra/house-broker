using Microsoft.Extensions.Logging;

namespace HouseBroker.TestSupport;

/// <summary>Captures log entries for assertions.</summary>
public sealed class RecordingLogger<T> : ILogger<T>
{
    private readonly List<LogEntry> _entries = new();

    /// <summary>Gets the captured log entries.</summary>
    public IReadOnlyList<LogEntry> Entries => _entries;

    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel) => true;

    /// <inheritdoc />
    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
        => _entries.Add(new LogEntry(logLevel, formatter(state, exception), state));

    /// <summary>A captured log entry.</summary>
    /// <param name="Level">The log level.</param>
    /// <param name="Message">The formatted message.</param>
    /// <param name="State">The structured log state.</param>
    public sealed record LogEntry(LogLevel Level, string Message, object? State);
}
