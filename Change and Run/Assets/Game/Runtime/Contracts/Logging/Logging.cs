using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Game.Contracts.Logging
{
    public enum LogLevel
    {
        Debug,
        Information,
        Warning,
        Error
    }

    public sealed class LogContext
    {
        private static readonly IReadOnlyDictionary<string, string> EmptyValues =
            new ReadOnlyDictionary<string, string>(new Dictionary<string, string>());

        public static readonly LogContext Empty = new LogContext(EmptyValues);

        public IReadOnlyDictionary<string, string> Values { get; }

        public LogContext(IReadOnlyDictionary<string, string> values)
        {
            if (values == null)
                throw new ArgumentNullException(nameof(values));

            var copy = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, string> pair in values)
                copy[pair.Key] = pair.Value;

            Values = new ReadOnlyDictionary<string, string>(copy);
        }

        public LogContext With(string key, string value)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("A context key is required.", nameof(key));

            var copy = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, string> pair in Values)
                copy[pair.Key] = pair.Value;
            copy[key] = value ?? string.Empty;
            return new LogContext(copy);
        }

        public LogContext Merge(LogContext other)
        {
            if (other == null || other.Values.Count == 0)
                return this;

            var copy = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, string> pair in Values)
                copy[pair.Key] = pair.Value;
            foreach (KeyValuePair<string, string> pair in other.Values)
                copy[pair.Key] = pair.Value;

            return new LogContext(copy);
        }
    }

    public interface IGameLogger
    {
        void Write(LogLevel level, string message, LogContext context = null,
            Exception exception = null);
    }

    public sealed class NullGameLogger : IGameLogger
    {
        public static readonly NullGameLogger Instance = new NullGameLogger();

        private NullGameLogger()
        {
        }

        public void Write(LogLevel level, string message, LogContext context = null,
            Exception exception = null)
        {
        }
    }

    public sealed class ContextualGameLogger : IGameLogger
    {
        private readonly IGameLogger _inner;
        private readonly LogContext _baseContext;

        public ContextualGameLogger(IGameLogger inner, LogContext baseContext)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _baseContext = baseContext ?? LogContext.Empty;
        }

        public void Write(LogLevel level, string message, LogContext context = null,
            Exception exception = null)
        {
            _inner.Write(level, message, _baseContext.Merge(context), exception);
        }
    }
}
