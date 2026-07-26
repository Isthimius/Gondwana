using Gondwana.Drawing.Direct;
using Gondwana.Rendering.Views;
using System.Drawing;
using System.Numerics;

namespace Gondwana.Widgets;

/// <summary>
/// Contains the shared drag state machine used by draggable leaf and container widgets.
/// </summary>
internal sealed class WidgetDragBehavior
{
    private readonly WidgetBase _owner;

    private bool _isPointerDown;
    private bool _suppressNextPointerClick;
    private float _dragThresholdPx = 3f;
    private PointF _dragStartScreenPositionPx;
    private Vector2 _dragStartWidgetPositionPx;
    private WidgetPointerButtonEnum _dragButton = WidgetPointerButtonEnum.None;
    private int _dragPointerId;
    private View? _dragView;
    private Vector2 _dragStartPointerPositionPx;

    internal WidgetDragBehavior(WidgetBase owner)
    {
        _owner =
            owner ??
            throw new ArgumentNullException(nameof(owner));
    }

    internal bool IsDragEnabled { get; set; } = true;

    internal bool IsDragging { get; private set; }

    internal float DragThresholdPx
    {
        get => _dragThresholdPx;
        set
        {
            if (value < 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    "The drag threshold cannot be negative.");
            }

            _dragThresholdPx = value;
        }
    }

    internal bool CanStartDrag(
        WidgetPointerEventArgs args)
    {
        return IsDragEnabled &&
               args.IsPrimaryButton;
    }

    internal void ProcessPointerDown(
        WidgetPointerEventArgs args,
        Func<WidgetPointerEventArgs, bool> canStartDrag)
    {
        if (_isPointerDown ||
            !canStartDrag(args))
        {
            return;
        }

        _isPointerDown = true;
        _suppressNextPointerClick = false;
        IsDragging = false;

        _dragStartScreenPositionPx =
            args.ScreenPositionPx;

        _dragStartWidgetPositionPx =
            _owner.GetPosition();

        _dragButton = args.Button;
        _dragPointerId = args.PointerId;
        _dragView = args.View;

        _dragStartPointerPositionPx =
            GetPointerPositionInWidgetSpace(
                args.ScreenPositionPx,
                args.View);
    }

    internal void ProcessPointerMove(
        WidgetPointerEventArgs args,
        Func<Vector2, View, Vector2> convertDelta,
        Func<Vector2, Vector2> constrainPosition,
        Action<WidgetDragEventArgs> dragStarted,
        Action<WidgetDragEventArgs> dragged)
    {
        if (!_isPointerDown ||
            args.PointerId != _dragPointerId)
        {
            return;
        }

        Vector2 totalScreenDeltaPx =
            GetTotalScreenDelta(
                args.ScreenPositionPx);

        if (!IsDragging)
        {
            float thresholdSquared =
                DragThresholdPx *
                DragThresholdPx;

            if (totalScreenDeltaPx.LengthSquared() <
                thresholdSquared)
            {
                return;
            }

            IsDragging = true;
            _suppressNextPointerClick = true;

            dragStarted(
                CreateDragEventArgs(args));

            // The callback may cancel or hide the widget, which resets this state.
            if (!IsDragging)
                return;
        }

        ApplyDragPosition(
            totalScreenDeltaPx,
            _dragView ?? args.View,
            convertDelta,
            constrainPosition);

        dragged(
            CreateDragEventArgs(args));
    }

    internal void ProcessPointerUp(
        WidgetPointerEventArgs args,
        Func<Vector2, View, Vector2> convertDelta,
        Func<Vector2, Vector2> constrainPosition,
        Action<WidgetDragEventArgs> dragEnded)
    {
        if (!_isPointerDown ||
            !IsMatchingRelease(args))
        {
            return;
        }

        WidgetDragEventArgs? dragArgs = null;

        if (IsDragging)
        {
            Vector2 totalScreenDeltaPx =
                GetTotalScreenDelta(
                    args.ScreenPositionPx);

            ApplyDragPosition(
                totalScreenDeltaPx,
                _dragView ?? args.View,
                convertDelta,
                constrainPosition);

            dragArgs =
                CreateDragEventArgs(args);
        }

        bool completedDrag = IsDragging;

        ResetPointerState();

        if (completedDrag &&
            dragArgs is not null)
        {
            _suppressNextPointerClick = true;
            dragEnded(dragArgs);
        }
    }

    internal bool ShouldDispatchPointerClick(
        WidgetPointerEventArgs args)
    {
        if (!_suppressNextPointerClick)
            return true;

        _suppressNextPointerClick = false;
        args.Handled = true;

        return false;
    }

    internal Vector2 ConvertScreenDeltaToPositionDelta(
        Vector2 totalScreenDeltaPx,
        View view)
    {
        ArgumentNullException.ThrowIfNull(view);

        if (_owner.Mode == DirectDrawingMode.View)
            return totalScreenDeltaPx;

        var currentScreenPositionPx = new PointF(
            _dragStartScreenPositionPx.X +
            totalScreenDeltaPx.X,
            _dragStartScreenPositionPx.Y +
            totalScreenDeltaPx.Y);

        Vector2 currentPointerPositionPx =
            GetPointerPositionInWidgetSpace(
                currentScreenPositionPx,
                view);

        return currentPointerPositionPx -
               _dragStartPointerPositionPx;
    }

    internal void ResetPointerState(
        bool clearClickSuppression = false)
    {
        _isPointerDown = false;
        IsDragging = false;
        _dragStartScreenPositionPx = PointF.Empty;
        _dragStartWidgetPositionPx = Vector2.Zero;
        _dragButton = WidgetPointerButtonEnum.None;
        _dragPointerId = 0;
        _dragView = null;
        _dragStartPointerPositionPx = Vector2.Zero;

        if (clearClickSuppression)
            _suppressNextPointerClick = false;
    }

    private Vector2 GetPointerPositionInWidgetSpace(
        PointF screenPositionPx,
        View view)
    {
        if (_owner.Mode ==
            DirectDrawingMode.View)
        {
            return new Vector2(
                screenPositionPx.X,
                screenPositionPx.Y);
        }

        var sceneLayer =
            _owner.SceneLayer ??
            throw new InvalidOperationException(
                "A scene-layer draggable widget must contain children attached to a SceneLayer.");

        PointF worldPositionPx =
            view.ScreenPxToWorldPx(
                sceneLayer,
                screenPositionPx);

        return new Vector2(
            worldPositionPx.X,
            worldPositionPx.Y);
    }

    private Vector2 GetTotalScreenDelta(
        PointF currentScreenPositionPx)
    {
        return new Vector2(
            currentScreenPositionPx.X -
            _dragStartScreenPositionPx.X,
            currentScreenPositionPx.Y -
            _dragStartScreenPositionPx.Y);
    }

    private void ApplyDragPosition(
        Vector2 totalScreenDeltaPx,
        View view,
        Func<Vector2, View, Vector2> convertDelta,
        Func<Vector2, Vector2> constrainPosition)
    {
        Vector2 positionDeltaPx =
            convertDelta(
                totalScreenDeltaPx,
                view);

        Vector2 proposedPositionPx =
            _dragStartWidgetPositionPx +
            positionDeltaPx;

        _owner.SetPosition(
            constrainPosition(
                proposedPositionPx));
    }

    private bool IsMatchingRelease(
        WidgetPointerEventArgs args)
    {
        return args.PointerId ==
               _dragPointerId &&
               (args.Button ==
                    WidgetPointerButtonEnum.None ||
                args.Button ==
                    _dragButton);
    }

    private WidgetDragEventArgs CreateDragEventArgs(
        WidgetPointerEventArgs pointerArgs)
    {
        return new WidgetDragEventArgs(
            _owner,
            _dragStartScreenPositionPx,
            pointerArgs.ScreenPositionPx,
            _dragButton,
            pointerArgs.Tick,
            pointerArgs.PointerId);
    }
}
