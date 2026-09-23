using Microsoft.Extensions.Logging;

namespace Zenmanage.Tests.Helpers;

/// <summary>
/// Minimal <see cref="ILogger"/> that records formatted log messages so tests can assert
/// on them, instead of relying on <c>NullLogger</c> (which discards everything and can't
/// verify a warning was actually logged).
/// </summary>
internal sealed class TestLogger : ILogger
{
    public List<(LogLevel Level, string Message)> Entries { get; } = new();

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        => Entries.Add((logLevel, formatter(state, exception)));
}
