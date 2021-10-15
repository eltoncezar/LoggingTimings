# Logging Timings [![NuGet Release](https://img.shields.io/nuget/v/SerilogTimings.svg)](https://nuget.org/packages/serilogtimings)

This project is a port of [Serilog Timings](https://github.com/nblumhardt/serilog-timings) to the [`Microsoft.Extensions.Logging`](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/logging) framework.



Serilog's support for structured data makes it a great way to collect timing information. However, the Microsoft default logging solution doesn't provide the same features and easy of use. So I decided to port this great project, so we can use it without Serilog dependency. 

**Serilog Timings** was built with some specific requirements in mind, and **Logging Timings** try to keep them:

 * One operation produces exactly one log event (events are raised at the completion of an operation)
 * Natural and fully-templated messages
 * Events for a single operation have a single event type, across both success and failure cases (only the logging level and `Outcome` properties change)

This keeps noise in the log to a minimum, and makes it easy to extract and manipulate timing 
information on a per-operation basis.

### Installation

The library is published as _LoggingTimings_ on NuGet.

```powershell
Install-Package LoggingTimings -DependencyVersion Highest
```

**.NET 4.6.1+** and **.NET Standard 2.0+** are supported. The package uses `Microsoft.Extensions.Logging` 5.0, which is compatible with both platforms.

### Getting started

Before your timings will go anywhere, [install and configure Microsoft.Extensions.Logging](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/logging).

Types are in the `LoggingTimings` namespace.

```csharp
using LoggingTimings;
```

### Usage
The simplest use case is to time an operation, without explicitly recording success/failure:

```csharp
using (logger.Time("Submitting payment for {OrderId}", order.Id))
{
    // Timed block of code goes here
}
```

At the completion of the `using` block, a message will be written to the log like:

```
[INF] Submitting payment for order-12345 completed in 456.7 ms
```

The operation description passed to `Time()` is a message template; the event written to the log
extends it with `" {Outcome} in {Elapsed} ms"`.

 * All events raised by LoggingTimings carry an `Elapsed` property in milliseconds
 * `Outcome` will always be `"completed"` when the `Time()` method is used

All of the properties from the description, plus the outcome and timing, will be recorded as
first-class properties on the log event.

Operations that can either _succeed or fail_, or _that produce a result_, can be created with
`logger.BeginTime()`:

```csharp
using (var op = logger.BeginTime("Retrieving orders for {CustomerId}", customer.Id))
{
	// Timed block of code goes here

	op.Complete();
}
```

Using `op.Complete()` will produce the same kind of result as in the first example:

```
[INF] Retrieving orders for customer-67890 completed in 7.8 ms
```

Additional methods on `Operation` allow more detailed results to be captured:

```csharp
    op.Complete("Rows", orders.Rows.Length);
```

This will not change the text of the log message, but the property `Rows` will be attached to it for
later filtering and analysis.

If the operation is not completed by calling `Complete()`, it is assumed to have failed and a
warning-level event will be written to the log instead:

```
[WRN] Retrieving orders for customer-67890 abandoned in 1234.5 ms
```

In this case the `Outcome` property will be `"abandoned"`.

To suppress this message, for example when an operation turns out to be inapplicable, use
`op.Cancel()`. Once `Cancel()` has been called, no event will be written by the operation on
either completion or abandonment.



### `OperationId` Context

_LoggingTimings_ will automatically add an `OperationId` property to all events inside
timing blocks.

This is **highly recommended**, because it makes it much easier to trace from a timing result back 
through the operation that raised it.

### Levelling

Timings are most useful in production, so timing events are recorded at the `Information` level and
higher, which should generally be collected all the time.

If you truly need `Trace`- or `Debug`-level timings, you can trigger them with `logger.TimeAt()`:

```csharp
using (logger.TimerAt(LogEventLevel.Debug).Time("Preparing zip archive"))
{
    // ...
```

When a level is specified, both completion and abandonment events will use it. To configure a different
abandonment level, pass the second optional parameter to the `TimerAt()` method.

### Caveats

One important usage note: because the event is not written until the completion of the `using` block
(or call to `Complete()`), arguments to `BeginTime()` or `Time()` are not captured until then; don't
pass parameters to these methods that mutate during the operation.