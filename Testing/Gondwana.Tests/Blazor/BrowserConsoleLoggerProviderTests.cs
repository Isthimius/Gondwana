using System.Reflection;
using Gondwana.Blazor.Logging;
using Gondwana.Logging;
using Microsoft.Extensions.Logging;

namespace Gondwana.Tests.Blazor;

/// <summary>
/// Contains regression tests for browser console logger provider.
/// </summary>
[Collection("BrowserConsoleLogging")]
public sealed class BrowserConsoleLoggerProviderTests
{
    /// <summary>
    /// Verifies information writes formatted message to standard output.
    /// </summary>
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

    /// <summary>
    /// Verifies warning writes to standard error.
    /// </summary>
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

    /// <summary>
    /// Verifies attach to engine logger persists across set log level and ignores repeated attach.
    /// </summary>
    [Fact]
    public void AttachToEngineLogger_PersistsAcrossSetLogLevel_AndIgnoresRepeatedAttach()
    {
        using var state = new EngineLoggerStateScope();
        var originalOut = Console.Out;
        using var output = new StringWriter();

        try
        {
            Console.SetOut(output);

            BrowserConsoleLogging.AttachToEngineLogger();
            BrowserConsoleLogging.AttachToEngineLogger();
            EngineLogger.SetLogLevel(LogLevel.Information);
            EngineLogger.Mode = EngineLoggingMode.Asynchronous;

            var logger = EngineLogger.GetLogger<BrowserConsoleLoggerProviderTests>();
            logger.LogInformation("Queued browser log {Value}", 7);
            EngineLogger.SwitchToSyncAndFlush(TimeSpan.FromSeconds(1));

            const string expectedLine =
                "[info] Gondwana.Tests.Blazor.BrowserConsoleLoggerProviderTests: Queued browser log 7";
            string console = output.ToString();
            Assert.Contains(expectedLine, console, StringComparison.Ordinal);
            Assert.Equal(1, CountOccurrences(console, expectedLine));
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    private static int CountOccurrences(string content, string value)
    {
        int count = 0;
        int index = 0;

        while ((index = content.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }

    private sealed class EngineLoggerStateScope : IDisposable
    {
        private static readonly FieldInfo LoggerFactoryField =
            typeof(EngineLogger).GetField(
                "_loggerFactory",
                BindingFlags.Static | BindingFlags.NonPublic)!;

        private static readonly FieldInfo ExternalFactoryField =
            typeof(EngineLogger).GetField(
                "_usingExternalLoggerFactory",
                BindingFlags.Static | BindingFlags.NonPublic)!;

        private static readonly FieldInfo LoggerCacheField =
            typeof(EngineLogger).GetField(
                "_loggerCache",
                BindingFlags.Static | BindingFlags.NonPublic)!;

        private static readonly FieldInfo PersistentProvidersField =
            typeof(EngineLogger).GetField(
                "_persistentProviderFactories",
                BindingFlags.Static | BindingFlags.NonPublic)!;

        private static readonly FieldInfo ModeField =
            typeof(EngineLogger).GetField(
                "_mode",
                BindingFlags.Static | BindingFlags.NonPublic)!;

        private static readonly FieldInfo CapacityField =
            typeof(EngineLogger).GetField(
                "_capacity",
                BindingFlags.Static | BindingFlags.NonPublic)!;

        private readonly ILoggerFactory? _originalFactory =
            (ILoggerFactory?)LoggerFactoryField.GetValue(null);

        private readonly bool _originalExternalFactory =
            (bool)ExternalFactoryField.GetValue(null)!;

        private readonly EngineLoggingMode _originalMode =
            (EngineLoggingMode)ModeField.GetValue(null)!;

        private readonly int _originalCapacity =
            (int)CapacityField.GetValue(null)!;

        private readonly Dictionary<string, Func<ILoggerProvider>> _originalProviders =
            new(
                (Dictionary<string, Func<ILoggerProvider>>)PersistentProvidersField.GetValue(null)!,
                StringComparer.Ordinal);

        /// <inheritdoc/>
        public void Dispose()
        {
            EngineLogger.StopAsyncLogging(flush: true, flushTimeout: TimeSpan.FromSeconds(1));

            LoggerFactoryField.SetValue(null, _originalFactory);
            ExternalFactoryField.SetValue(null, _originalExternalFactory);
            ModeField.SetValue(null, _originalMode);
            CapacityField.SetValue(null, _originalCapacity);

            var providers =
                (Dictionary<string, Func<ILoggerProvider>>)PersistentProvidersField.GetValue(null)!;
            providers.Clear();
            foreach (KeyValuePair<string, Func<ILoggerProvider>> provider in _originalProviders)
                providers.Add(provider.Key, provider.Value);

            var cache = LoggerCacheField.GetValue(null)!;
            cache.GetType()
                .GetMethod(nameof(System.Collections.IDictionary.Clear))!
                .Invoke(cache, null);
        }
    }
}

/// <summary>
/// Represents browser console logging collection.
/// </summary>
[CollectionDefinition("BrowserConsoleLogging", DisableParallelization = true)]
public sealed class BrowserConsoleLoggingCollection
{
}
