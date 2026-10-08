using System.Drawing;
using System.Reflection;
using Gondwana.Extensibility;
using Gondwana.Logging;
using Gondwana.Rendering;
using Gondwana.Rendering.Backbuffers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SkiaSharp;

namespace Gondwana.Tests;

/// <summary>
/// Represents global engine state collection.
/// </summary>
[CollectionDefinition("Global engine state", DisableParallelization = true)]
public sealed class GlobalEngineStateCollection
{
}

/// <summary>
/// Contains regression tests for logging integration regression.
/// </summary>
[Collection("Global engine state")]
public sealed class LoggingIntegrationRegressionTests
{
    /// <summary>
    /// Verifies add engine logging uses registered factory without circular resolution.
    /// </summary>
    [Fact]
    public void AddEngineLogging_UsesRegisteredFactoryWithoutCircularResolution()
    {
        using var state = new EngineLoggerStateScope();

        var services = new ServiceCollection();
        services.AddEngineLogging();

        using var provider = services.BuildServiceProvider();
        var resolvedFactory = provider.GetRequiredService<ILoggerFactory>();

        Assert.Same(resolvedFactory, EngineLogger.EngineLoggerFactory);
    }

    /// <summary>
    /// Verifies set log level does not replace externally provided factory.
    /// </summary>
    [Fact]
    public void SetLogLevel_DoesNotReplaceExternallyProvidedFactory()
    {
        using var state = new EngineLoggerStateScope();
        var externalFactory = new TestLoggerFactory();

        EngineLogger.Initialize(externalFactory);
        EngineLogger.SetLogLevel(LogLevel.Trace);

        Assert.Same(externalFactory, EngineLogger.EngineLoggerFactory);
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

        private readonly object? _originalFactory =
            LoggerFactoryField.GetValue(null);

        private readonly bool _originalExternalFactory =
            (bool)ExternalFactoryField.GetValue(null)!;

        /// <inheritdoc/>
        public void Dispose()
        {
            LoggerFactoryField.SetValue(null, _originalFactory);
            ExternalFactoryField.SetValue(null, _originalExternalFactory);

            var cache = LoggerCacheField.GetValue(null)!;
            cache.GetType()
                .GetMethod(nameof(System.Collections.IDictionary.Clear))!
                .Invoke(cache, null);
        }
    }

    private sealed class TestLoggerFactory : ILoggerFactory
    {
        /// <inheritdoc/>
        public void AddProvider(ILoggerProvider provider)
        {
        }

        /// <inheritdoc/>
        public ILogger CreateLogger(string categoryName) =>
            TestLogger.Instance;

        /// <inheritdoc/>
        public void Dispose()
        {
        }
    }

    private sealed class TestLogger : ILogger
    {
        /// <summary>
        /// Gets the shared instance.
        /// </summary>
        public static TestLogger Instance { get; } = new();

        /// <inheritdoc/>
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull =>
            NoOpScope.Instance;

        /// <inheritdoc/>
        public bool IsEnabled(LogLevel logLevel) => false;

        /// <inheritdoc/>
        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
        }
    }

    private sealed class NoOpScope : IDisposable
    {
        /// <summary>
        /// Gets the shared instance.
        /// </summary>
        public static NoOpScope Instance { get; } = new();

        /// <inheritdoc/>
        public void Dispose()
        {
        }
    }
}

/// <summary>
/// Contains regression tests for render surface presentation regression.
/// </summary>
[Collection("Global engine state")]
public sealed class RenderSurfacePresentationRegressionTests
{
    /// <summary>
    /// Verifies present backbuffer rect clamps dirty rectangle to backbuffer bounds.
    /// </summary>
    /// <param name="width">The width value for this test case.</param>
    /// <param name="height">The height value for this test case.</param>
    /// <param name="left">The left value for this test case.</param>
    /// <param name="top">The top value for this test case.</param>
    /// <param name="destWidth">The dest width value for this test case.</param>
    /// <param name="destHeight">The dest height value for this test case.</param>
    [Theory]
    [InlineData(100, 80, 70, 50, 30, 30)]
    [InlineData(200, 200, 140, 120, 60, 60)]
    public void PresentBackbufferRect_ClampsDirtyRectangleToBackbufferBounds(int width, int height, int left, int top, int destWidth, int destHeight)
    {
        var engine = Engine.Instance;
        var uiDispatcherProperty = typeof(Engine).GetProperty(
            nameof(Engine.UiDispatcher),
            BindingFlags.Instance | BindingFlags.Public)!;
        var uiDispatcherSetter = uiDispatcherProperty.GetSetMethod(nonPublic: true)!;
        var originalDispatcher = (IUiDispatcher?)uiDispatcherProperty.GetValue(engine);

        try
        {
            uiDispatcherSetter.Invoke(
                engine,
                new object?[] { new ImmediateUiDispatcher() });

            var adapter = new RecordingAdapter(100, 80);
            using var host = new RenderSurfaceHost<BitmapBackbuffer>(adapter);

            adapter.Resize(width, height);
            // First presentation establishes the complete retained image, including margins.
            host.PresentBackbufferToAdapter();

            // Dirty tracking inflates by the supplied rectangle's dimensions,
            // producing (70, 50, 60, 60), which extends beyond the 100x80 backbuffer.
            host.Backbuffer.AddToBackbufferDirtyRectangle(
                new Rectangle(90, 70, 20, 20));

            host.PresentBackbufferToAdapter();

            Assert.Equal(2, adapter.PresentCount);
            Assert.Equal(
                new SKRectI(70, 50, 100, 80),
                adapter.BufferRect!.Value);
            Assert.Equal(
                SKRect.Create(left, top, destWidth, destHeight),
                adapter.DestinationRect!.Value);
        }
        finally
        {
            uiDispatcherSetter.Invoke(
                engine,
                new object?[] { originalDispatcher });
        }
    }

    private sealed class RecordingAdapter(int width, int height)
        : RenderSurfaceAdapterBase(width, height)
    {
        internal void Resize(int width, int height) => SetDestinationSize(width, height);
        /// <summary>
        /// Gets the present count.
        /// </summary>
        public int PresentCount { get; private set; }
        /// <summary>
        /// Gets the buffer rect.
        /// </summary>
        public SKRectI? BufferRect { get; private set; }
        /// <summary>
        /// Gets the destination rect.
        /// </summary>
        public SKRect? DestinationRect { get; private set; }

        /// <inheritdoc/>
        public override void Present(
            SKImage bufferImage,
            SKRectI bufferRect,
            SKRect destRect)
        {
            PresentCount++;
            BufferRect = bufferRect;
            DestinationRect = destRect;
            bufferImage.Dispose();
        }
    }

    private sealed class ImmediateUiDispatcher : IUiDispatcher
    {
        /// <inheritdoc/>
        public bool IsOnUIThread => true;

        /// <inheritdoc/>
        public void Post(Action action) => action();

        /// <inheritdoc/>
        public void Send(Action action) => action();
    }
}
