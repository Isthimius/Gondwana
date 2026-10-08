using System.Runtime.CompilerServices;
using Gondwana.Drawing.Direct;
using Gondwana.Rendering.Backbuffers;

namespace Gondwana.Tests.Drawing.Direct;

/// <summary>
/// Contains regression tests for direct drawing movable base.
/// </summary>
public sealed class DirectDrawingMovableBaseTests
{
    /// <summary>
    /// Verifies update before movement initialization does not throw.
    /// </summary>
    [Fact]
    public void Update_BeforeMovementInitialization_DoesNotThrow()
    {
        var drawing = (UninitializedMovableDrawing)
            RuntimeHelpers.GetUninitializedObject(typeof(UninitializedMovableDrawing));

        Exception? exception = Record.Exception(() => drawing.Update(1));

        Assert.Null(exception);
    }

    private sealed class UninitializedMovableDrawing : DirectDrawingMovableBase
    {
        private UninitializedMovableDrawing()
            : base(
                null!,
                DirectDrawingMode.View,
                null,
                null,
                null,
                null)
        {
        }

        /// <inheritdoc/>
        protected override void OnDraw(
            BackbufferBase backbuffer,
            System.Drawing.RectangleF destRectScreen)
        {
        }
    }
}
