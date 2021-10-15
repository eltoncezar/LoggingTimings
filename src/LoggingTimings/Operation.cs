// Copyright 2016 LoggingTimings Contributors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using LoggingTimings.Configuration;
using Microsoft.Extensions.Logging;
using LoggingTimings.Extensions;

namespace LoggingTimings
{
    /// <summary>
    /// Records operation timings to the Serilog log.
    /// </summary>
    /// <remarks>
    /// Static members on this class are thread-safe. Instances
    /// of <see cref="Operation"/> are designed for use on a single thread only.
    /// </remarks>
    public class Operation : IDisposable
    {
        /// <summary>
        /// Property names attached to events by <see cref="Operation"/>s.
        /// </summary>
        public enum Properties
        {
            /// <summary>
            /// The timing, in milliseconds.
            /// </summary>
            Elapsed,

            /// <summary>
            /// Completion status, either <em>completed</em> or <em>discarded</em>.
            /// </summary>
            Outcome,

            /// <summary>
            /// A unique identifier added to the log context during
            /// the operation.
            /// </summary>
            OperationId
        };

        private const string OutcomeCompleted = "completed", OutcomeAbandoned = "abandoned";

        private ILogger _target;
        private IDisposable _scope;
        private readonly string _messageTemplate;
        private readonly object[] _args;
        private readonly Stopwatch _stopwatch;

        private Guid? _operationId;
        private CompletionBehaviour _completionBehaviour;
        private readonly LogLevel _completionLevel;
        private readonly LogLevel _abandonmentLevel;
        private Exception _exception;

        internal Operation(ILogger target, string messageTemplate, object[] args, CompletionBehaviour completionBehaviour, LogLevel completionLevel, LogLevel abandonmentLevel)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (messageTemplate == null) throw new ArgumentNullException(nameof(messageTemplate));
            if (args == null) throw new ArgumentNullException(nameof(args));
            _target = target;
            _messageTemplate = messageTemplate;
            _args = args;
            _completionBehaviour = completionBehaviour;
            _completionLevel = completionLevel;
            _abandonmentLevel = abandonmentLevel;
            _operationId = Guid.NewGuid();
            _stopwatch = Stopwatch.StartNew();

            _scope = _target.BeginScope(new Dictionary<string, object> { ["OperationId"] = _operationId });
        }

        /// <summary>
        /// Returns the elapsed time of the operation. This will update during the operation, and be frozen once the
        /// operation is completed or canceled.
        /// </summary>
        public TimeSpan Elapsed => _stopwatch.Elapsed;

        /// <summary>
        /// Begin a new timed operation. The return value must be completed using <see cref="Complete()"/>,
        /// or disposed to record abandonment.
        /// </summary>
        /// <param name="messageTemplate">A log message describing the operation, in message template format.</param>
        /// <param name="args">Arguments to the log message. These will be stored and captured only when the
        /// operation completes, so do not pass arguments that are mutated during the operation.</param>
        /// <returns>An <see cref="Operation"/> object.</returns>
        public Operation BeginTime(string messageTemplate, params object[] args)
        {
            return _target.BeginTime(messageTemplate, args);
        }

        /// <summary>
        /// Begin a new timed operation. The return value must be disposed to complete the operation.
        /// </summary>
        /// <param name="messageTemplate">A log message describing the operation, in message template format.</param>
        /// <param name="args">Arguments to the log message. These will be stored and captured only when the
        /// operation completes, so do not pass arguments that are mutated during the operation.</param>
        /// <returns>An <see cref="Operation"/> object.</returns>
        public IDisposable Time(string messageTemplate, params object[] args)
        {
            return _target.Time(messageTemplate, args);
        }

        /// <summary>
        /// Configure the logging levels used for completion and abandonment events.
        /// </summary>
        /// <param name="completion">The level of the event to write on operation completion.</param>
        /// <param name="abandonment">The level of the event to write on operation abandonment; if not
        /// specified, the <paramref name="completion"/> level will be used.</param>
        /// <returns>An object from which timings with the configured levels can be made.</returns>
        /// <remarks>If neither <paramref name="completion"/> nor <paramref name="abandonment"/> is enabled
        /// on the logger at the time of the call, a no-op result is returned.</remarks>
        public LevelledOperation TimeAt(LogLevel completion, LogLevel? abandonment = null)
        {
            return _target.TimeAt(completion, abandonment);
        }

        /// <summary>
        /// Complete the timed operation. This will write the event and elapsed time to the log.
        /// </summary>
        public void Complete()
        {
            _stopwatch.Stop();

            if (_completionBehaviour == CompletionBehaviour.Silent)
                return;

            Write(_target, _completionLevel, OutcomeCompleted);
        }

        /// <summary>
        /// Complete the timed operation with an included result value.
        /// </summary>
        /// <param name="resultPropertyName">The name for the property to attach to the event.</param>
        /// <param name="result">The result value.</param>
        /// <param name="serializeObjects">If true, the property value will be serialized in JSON.</param>
        public void Complete(string resultPropertyName, object result, bool serializeObjects = false)
        {
            if (resultPropertyName == null) throw new ArgumentNullException(nameof(resultPropertyName));
            if (result == null) throw new ArgumentNullException(nameof(result));

            _stopwatch.Stop();

            if (_completionBehaviour == CompletionBehaviour.Silent) return;

            if (serializeObjects) result = JsonSerializer.Serialize(result);

            using (_target.BeginScope(new Dictionary<string, object> { { resultPropertyName, result } }))
            {
                Write(_target, _completionLevel, OutcomeCompleted);
            }
        }

        /// <summary>
        /// Abandon the timed operation. This will write the event and elapsed time to the log.
        /// </summary>
        public void Abandon()
        {
            if (_completionBehaviour == CompletionBehaviour.Silent)
                return;

            Write(_target, _abandonmentLevel, OutcomeAbandoned);
        }

        /// <summary>
        /// Abandon the timed operation. This will write the event and elapsed time to the log.
        /// </summary>
        /// <param name="resultPropertyName">The name for the property to attach to the event.</param>
        /// <param name="result">The result value.</param>
        /// <param name="serializeObjects">If true, the property value will be serialized in JSON.</param>
        public void Abandon(string resultPropertyName, object result, bool serializeObjects = false)
        {
            if (resultPropertyName == null) throw new ArgumentNullException(nameof(resultPropertyName));
            if (result == null) throw new ArgumentNullException(nameof(result));

            if (_completionBehaviour == CompletionBehaviour.Silent) return;

            if (serializeObjects) result = JsonSerializer.Serialize(result);

            using (_target.BeginScope(new Dictionary<string, object> { { resultPropertyName, result } }))
            {
                Write(_target, _abandonmentLevel, OutcomeAbandoned);
            }
        }

        /// <summary>
        /// Cancel the timed operation. After calling, no event will be recorded either through
        /// completion or disposal.
        /// </summary>
        public void Cancel()
        {
            _stopwatch.Stop();
            _completionBehaviour = CompletionBehaviour.Silent;
            PopLogContext();
        }

        /// <summary>
        /// Dispose the operation. If not already completed or canceled, an event will be written
        /// with timing information. Operations started with <see cref="Time"/> will be completed through
        /// disposal. Operations started with <see cref="BeginTime"/> will be recorded as abandoned.
        /// </summary>
        public void Dispose()
        {
            switch (_completionBehaviour)
            {
                case CompletionBehaviour.Silent:
                    break;

                case CompletionBehaviour.Abandon:
                    Write(_target, _abandonmentLevel, OutcomeAbandoned);
                    break;

                case CompletionBehaviour.Complete:
                    Write(_target, _completionLevel, OutcomeCompleted);
                    break;

                default:
                    throw new InvalidOperationException("Unknown underlying state value");
            }

            PopLogContext();
        }

        private void PopLogContext()
        {
            // _operationId = Guid.NewGuid();
            _operationId = null;
            _scope.Dispose();
        }

        private void Write(ILogger target, LogLevel level, string outcome)
        {
            _completionBehaviour = CompletionBehaviour.Silent;

            var elapsed = _stopwatch.Elapsed.TotalMilliseconds;

            using (_target.BeginScope(new Dictionary<string, object>
                {
                    {"Outcome", outcome},
                    {"Elapsed", elapsed}
                }))
            {
                target.Log(level, _exception, $"{_messageTemplate} {{{nameof(Properties.Outcome)}}} in {{{nameof(Properties.Elapsed)}:0.0}} ms", _args.Concat(new object[] { outcome, elapsed }).ToArray());
            }

            PopLogContext();
        }

        /// <summary>
        /// Enriches resulting log event with the given exception.
        /// </summary>
        /// <param name="exception">Exception related to the event.</param>
        /// <returns>Same <see cref="Operation"/>.</returns>
        /// <seealso cref="Exception"/>
        public Operation SetException(Exception exception)
        {
            _exception = exception;
            return this;
        }
    }
}