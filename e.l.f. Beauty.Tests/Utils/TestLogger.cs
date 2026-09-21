using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace e.l.f._Beauty.Tests.Utils
{
    public class TestLogger<T> : ILogger<T>
    {
        public List<LogEntry> Entries { get; } = new List<LogEntry>();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var message = formatter != null ? formatter(state, exception) : state?.ToString();
            Entries.Add(new LogEntry { Level = logLevel, EventId = eventId, Exception = exception, Message = message });
        }

        public class LogEntry
        {
            public LogLevel Level { get; set; }
            public EventId EventId { get; set; }
            public Exception? Exception { get; set; }
            public string? Message { get; set; }
        }

        private class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new NullScope();
            public void Dispose() { }
        }
    }
}
