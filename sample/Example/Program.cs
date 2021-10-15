using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using LoggingTimings.Extensions;

namespace Example
{
    public class Program
    {
        public static void Main(string[] args)
        {
            ILogger log = LoggerFactory
                .Create(builder => builder.AddJsonConsole(options =>
                {
                    options.IncludeScopes = true;
                    options.JsonWriterOptions = new JsonWriterOptions
                    {
                        Indented = true
                    };
                }))
                .CreateLogger("Example");

            log.LogInformation("Hello, world!");

            var count = 10000;
            using (var op = log.BeginTime("Adding {Count} successive integers", count))
            {
                var sum = Enumerable.Range(0, count).Sum();
                log.LogInformation("This event is tagged with an operation id");
                // op.Abandon();
                op.Complete("Sum", sum);
            }

            log.LogInformation("Goodbye!");
        }
    }
}