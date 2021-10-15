using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using LoggingTimings;
using SerilogTimings.Tests.Support;
using Xunit;

namespace SerilogTimings.Tests
{
    public class OperationTests
    {
        private const string OutcomeCompleted = "completed";
        private const string OutcomeAbandoned = "abandoned";

        private static LogEvent AssertSingleCompletionEvent(CollectingLogger logger, LogLevel expectedLevel, string expectedOutcome)
        {
            T GetScalarPropertyValue<T>(LogEvent e, string key)
            {
                var property = e.Scopes.FirstOrDefault(kvp => kvp.Key == key);
                Assert.True(!property.Equals(default(KeyValuePair<string, object>)));
                return Assert.IsType<T>(property.Value);
            }

            var ev = Assert.Single(logger.Events);
            Assert.NotNull(ev);
            Assert.Equal(expectedLevel, ev.Level);

            Assert.Equal(expectedOutcome, GetScalarPropertyValue<string>(ev, nameof(Operation.Properties.Outcome)));
            GetScalarPropertyValue<double>(ev, nameof(Operation.Properties.Elapsed));
            return ev;
        }

        [Fact]
        public void DisposeRecordsCompletionOfTimings()
        {
            var logger = new CollectingLogger();
            var op = logger.Time("Test");
            op.Dispose();
            AssertSingleCompletionEvent(logger, LogLevel.Information, OutcomeCompleted);
        }

        [Fact]
        public void CompleteRecordsCompletionOfOperations()
        {
            var logger = new CollectingLogger();
            var op = logger.BeginTime("Test");
            op.Complete();
            AssertSingleCompletionEvent(logger, LogLevel.Information, OutcomeCompleted);

            op.Dispose();
            Assert.Single(logger.Events);
        }

        [Fact]
        public void DisposeRecordsAbandonmentOfIncompleteOperations()
        {
            var logger = new CollectingLogger();
            var op = logger.BeginTime("Test");
            op.Dispose();
            AssertSingleCompletionEvent(logger, LogLevel.Warning, OutcomeAbandoned);

            op.Dispose();
            Assert.Single(logger.Events);
        }

        [Fact]
        public void AbandonRecordsAbandonmentOfBegunOperations()
        {
            var logger = new CollectingLogger();
            var op = logger.BeginTime("Test");
            op.Abandon();
            AssertSingleCompletionEvent(logger, LogLevel.Warning, OutcomeAbandoned);

            op.Dispose();
            Assert.Single(logger.Events);
        }

        // [Fact]
        // public void CompleteRecordsResultsOfOperations()
        // {
        //     var logger = new CollectingLogger();
        //     var op = logger.BeginTime("Test");
        //     op.Complete("Value", 42);
        //     Assert.Single(logger.Events);
        //     Assert.True(logger.Events.Single().Properties.ContainsKey("Value"));
        // }

        [Fact]
        public void OnceCanceledDisposeDoesNotRecordCompletionOfOperations()
        {
            var logger = new CollectingLogger();
            var op = logger.BeginTime("Test");
            op.Cancel();
            op.Dispose();
            Assert.Empty(logger.Events);
        }

        [Fact]
        public void OnceCanceledCompleteDoesNotRecordCompletionOfOperations()
        {
            var logger = new CollectingLogger();
            var op = logger.BeginTime("Test");
            op.Cancel();
            op.Complete();
            op.Dispose();
            Assert.Empty(logger.Events);
        }

        [Fact]
        public void OnceCanceledAbandonDoesNotRecordCompletionOfOperations()
        {
            var logger = new CollectingLogger();
            var op = logger.BeginTime("Test");
            op.Cancel();
            op.Abandon();
            op.Dispose();
            Assert.Empty(logger.Events);
        }

        [Fact]
        public void OnceCompletedAbandonDoesNotRecordAbandonmentOfOperations()
        {
            var logger = new CollectingLogger();
            var op = logger.BeginTime("Test");
            op.Complete();
            AssertSingleCompletionEvent(logger, LogLevel.Information, OutcomeCompleted);

            op.Abandon();
            op.Dispose();
            Assert.Single(logger.Events);
        }

        [Fact]
        public void OnceAbandonedCompleteDoesNotRecordAbandonmentOfOperations()
        {
            var logger = new CollectingLogger();
            var op = logger.BeginTime("Test");
            op.Abandon();
            AssertSingleCompletionEvent(logger, LogLevel.Warning, OutcomeAbandoned);

            op.Complete();
            op.Dispose();
            Assert.Single(logger.Events);
        }

        [Fact]
        public void CustomCompletionLevelsAreApplied()
        {
            var logger = new CollectingLogger();
            var op = logger.TimeAt(LogLevel.Error).Time("Test");
            op.Dispose();
            AssertSingleCompletionEvent(logger, LogLevel.Error, OutcomeCompleted);
        }

        [Fact]
        public void AbandonmentLevelsDefaultToCustomCompletionLevelIfApplied()
        {
            var logger = new CollectingLogger();
            var op = logger.TimeAt(LogLevel.Error).BeginTime("Test");
            op.Dispose();
            AssertSingleCompletionEvent(logger, LogLevel.Error, OutcomeAbandoned);
        }

        [Fact]
        public void CustomAbandonmentLevelsAreApplied()
        {
            var logger = new CollectingLogger();
            var op = logger.TimeAt(LogLevel.Error, LogLevel.Critical).BeginTime("Test");
            op.Dispose();
            AssertSingleCompletionEvent(logger, LogLevel.Critical, OutcomeAbandoned);
        }

        // [Fact]
        // public void IfNeitherLevelIsEnabledACachedResultIsReturned()
        // {
        //     var logger = new LoggerConfiguration().CreateLogger(); // Information
        //     var op = logger.At(LogLevel.Trace).Time("Test");
        //     var op2 = logger.At(LogLevel.Trace).Time("Test");
        //     Assert.Same(op, op2);
        // }
        //
        // [Fact]
        // public void LoggerContextIsPreserved()
        // {
        //     var logger = new CollectingLogger();
        //     var op = logger
        //         .ForContext<OperationTests>().BeginTime("Test");
        //     op.Complete();
        //
        //     var sourceContext = (logger.Events.Single().Properties["SourceContext"] as ScalarValue).Value;
        //     Assert.Equal(sourceContext, typeof(OperationTests).FullName);
        // }

        // [Fact]
        // public void CompleteRecordsOperationId()
        // {
        //     var innerLogger = new CollectingLogger();
        //     var logger = new LoggerConfiguration()
        //         .WriteTo(innerLogger)
        //         .Enrich.FromLogContext()
        //         .CreateLogger();
        //
        //     var op = logger.BeginTime("Test");
        //     op.Complete();
        //     Assert.True(
        //         Assert.Single(innerLogger.Events)
        //             .Properties.ContainsKey(nameof(Operation.Properties.OperationId))
        //     );
        // }

        // [Fact]
        // public void AbandonRecordsOperationId()
        // {
        //     var innerLogger = new CollectingLogger();
        //     var logger = new LoggerConfiguration()
        //         .WriteTo(innerLogger)
        //         .Enrich.FromLogContext()
        //         .CreateLogger();
        //
        //     var op = logger.BeginTime("Test");
        //     op.Dispose();
        //     Assert.True(
        //         Assert.Single(innerLogger.Events)
        //             .Properties.ContainsKey(nameof(Operation.Properties.OperationId))
        //     );
        // }

    }
}