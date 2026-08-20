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

[CollectionDefinition("Global engine state", DisableParallelization = true)]
public sealed class GlobalEngineStateCollection
{
}

[Collection("Global engine state")]
public sealed class LoggingIntegrationRegressionTests
{
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
        public void AddProvider(ILoggerProvider provider)
        {
        }

        public ILogger CreateLogger(string categoryName) =>
            TestLogger.Instance;

        public void Dispose()
        {
        }
    }

    private sealed class TestLogger : ILogger
    {
        public static TestLogger Instance { get; } = new();

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull =>
            NoOpScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => false;

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
        public static NoOpScope Instance { get; } = new();

        public void Dispose()
        {
        }
    }
}

[Collection("Global engine state")]
public sealed class RenderSurfacePresentationRegressionTests
{
    [Fact]
    public void PresentBackbufferRect_ClampsDirtyRectangleToBackbufferBounds()
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

            // Dirty tracking inflates by the supplied rectangle's dimensions,
            // producing (70, 50, 60, 60), which extends beyond the 100x80 backbuffer.
            host.Backbuffer.AddToBackbufferDirtyRectangle(
                new Rectangle(90, 70, 20, 20));

            host.PresentBackbufferToAdapter();

            Assert.Equal(1, adapter.PresentCount);
            Assert.Equal(
                new SKRectI(70, 50, 100, 80),
                adapter.BufferRect!.Value);
            Assert.Equal(
                SKRect.Create(70, 50, 30, 30),
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
        public int PresentCount { get; private set; }
        public SKRectI? BufferRect { get; private set; }
        public SKRect? DestinationRect { get; private set; }

        public override void Present(
            SKImage bufferImage,
            SKRectI bufferRect,
            SKRect destRect)
        {
            PresentCount++;
            BufferRect = bufferRect;
            DestinationRect = destRect;
        }
    }

    private sealed class ImmediateUiDispatcher : IUiDispatcher
    {
        public bool IsOnUIThread => true;

        public void Post(Action action) => action();

        public void Send(Action action) => action();
    }
}
