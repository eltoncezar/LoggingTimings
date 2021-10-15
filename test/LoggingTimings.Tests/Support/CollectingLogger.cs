using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace SerilogTimings.Tests.Support
{
    public class CollectingLogger : ILogger
    {
        public List<LogEvent> Events { get; } = new List<LogEvent>();

        internal IExternalScopeProvider ScopeProvider { get; set; }

        public CollectingLogger()
        {
            ScopeProvider = new LoggerExternalScopeProvider();
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;

            Events.Add(new LogEvent(logLevel, eventId, formatter(state, exception), exception, GetScopes()));
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public IDisposable BeginScope<TState>(TState state) => ScopeProvider?.Push(state) ?? NullScope.Instance;

        private List<KeyValuePair<string, object>> GetScopes()
        {
            var scopeProvider = ScopeProvider;
            if (scopeProvider == null) return null;

            var result = new List<KeyValuePair<string, object>>();

            scopeProvider.ForEachScope((scope, state) =>
            {
                if (scope is Dictionary<string, object>)
                {
                    foreach (var (key, value) in (Dictionary<string, object>)scope)
                    {
                        result.Add(new KeyValuePair<string, object>(key, value));
                    }
                }
                else
                {
                    result.Add(new KeyValuePair<string, object>(scope.ToString()!, null));
                }
            }, "");

            return result;
        }
    }
}