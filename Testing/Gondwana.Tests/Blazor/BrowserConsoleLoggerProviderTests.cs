using Gondwana.Blazor.Logging;
using Microsoft.Extensions.Logging;

namespace Gondwana.Tests.Blazor;

[Collection("BrowserConsoleLogging")]
public sealed class BrowserConsoleLoggerProviderTests
{
    [Fact]
    public void Information_WritesFormattedMessageToStandardOutput()
    {
        var originalOut = Console.Out;
        using var output = new StringWriter();

        try
        {
            Console.SetOut(output);
            using var provider = new BrowserConsoleLoggerProvider();
            var logger = provider.CreateLogger("Gondwana.Tests.Browser");

            logger.LogInformation("MSAA actual {Actual}", 4);

            Assert.Contains(
                "[info] Gondwana.Tests.Browser: MSAA actual 4",
                output.ToString(),
                StringComparison.Ordinal);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public void Warning_WritesToStandardError()
    {
        var originalError = Console.Error;
        using var output = new StringWriter();

        try
        {
            Console.SetError(output);
            using var provider = new BrowserConsoleLoggerProvider();
            var logger = provider.CreateLogger("Gondwana.Tests.Browser");

            logger.LogWarning("Fallback to {Samples} sample", 1);

            Assert.Contains(
                "[warn] Gondwana.Tests.Browser: Fallback to 1 sample",
                output.ToString(),
                StringComparison.Ordinal);
        }
        finally
        {
            Console.SetError(originalError);
        }
    }
}

[CollectionDefinition("BrowserConsoleLogging", DisableParallelization = true)]
public sealed class BrowserConsoleLoggingCollection;
