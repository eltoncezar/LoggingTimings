using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace SerilogTimings.Tests.Support
{
    public class LogEvent
    {
        public LogLevel Level { get; }
        public EventId EventId { get; }
        public string Content { get; }
        public Exception Exception { get; }
        public List<KeyValuePair<string, object>> Scopes { get; }

        public LogEvent(LogLevel level, EventId eventId, string content, Exception exception, List<KeyValuePair<string, object>> scopes)
        {
            Level = level;
            EventId = eventId;
            Content = content;
            Exception = exception;
            Scopes = scopes;
        }
    }
}