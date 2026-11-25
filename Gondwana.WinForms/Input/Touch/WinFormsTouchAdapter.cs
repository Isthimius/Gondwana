using System.Runtime.InteropServices;
using Gondwana.Input.Touch;
using Gondwana.WinForms.Rendering;

namespace Gondwana.WinForms.Input.Touch;

/// <summary>
/// Captures native Windows touchscreen contacts (WM_TOUCH) from a WinForms
/// render control and its child HWNDs for the standard Gondwana touch poller.
/// </summary>
/// <remarks>
/// The UI thread records touch beginnings and endings, including contacts shorter
/// than one engine frame. <see cref="TouchEventPoller"/> dispatches them on the engine
/// thread. Mouse/pen input continues through the normal WinForms mouse adapter.
/// Dispose on the WinForms thread, or let the engine dispose the touch poller.
/// </remarks>
public sealed class WinFormsTouchAdapter : ITouchAdapter, IDisposable
{
    private const int WmTouch = 0x0240;
    private const int WmParentNotify = 0x0210;
    private const int WmCreate = 0x0001;
    private const int WmDestroy = 0x0002;
    private const int WmNcDestroy = 0x0082;
    private const uint TouchDown = 0x0002;
    private const uint TouchUp = 0x0004;

    private readonly Control _surface;
    private readonly WinFormsTouchState _state = new();
    private readonly Dictionary<Control, TouchWindowHook> _hooks = [];
    private readonly Dictionary<IntPtr, NativeChildHook> _nativeChildren = [];
    private bool _refreshingNativeChildren;
    private bool _disposed;

    /// <summary>Creates the adapter and registers existing and future child HWNDs.</summary>
    public WinFormsTouchAdapter(Control surface)
    {
        _surface = surface ?? throw new ArgumentNullException(nameof(surface));

        if (_surface.InvokeRequired)
            throw new InvalidOperationException("Initialize WinForms touch on the control's UI thread.");

        Watch(_surface);
        _surface.SizeChanged += OnSurfaceSizeChanged;
        RefreshNativeChildren();
    }

    /// <inheritdoc />
    public IReadOnlyList<TouchPoint> ActiveTouches => _state.ActiveTouches;

    /// <inheritdoc />
    public IReadOnlyList<TouchPoint> ConsumeBeganTouches() => _state.ConsumeBeginnings();

    /// <inheritdoc />
    public IReadOnlyList<TouchPoint> ConsumeEndedTouches() => _state.ConsumeEndings();

    /// <summary>Whether a native window has successfully registered for touch.</summary>
    public bool IsRegistered => !_disposed &&
                                (_hooks.Values.Any(static hook => hook.IsRegistered) ||
                                 _nativeChildren.Values.Any(static hook => hook.IsRegistered));

    /// <summary>
    /// Reports whether the WinForms mouse adapter should ignore a touch-origin
    /// compatibility mouse event on the same render control.
    /// </summary>
    internal static bool IsNativeTouchMouseMessage(Control surface)
    {
        if (Engine.Instance.Input.TouchAdapter is not WinFormsTouchAdapter adapter ||
            !adapter.IsRegistered || !ReferenceEquals(adapter._surface, surface))
            return false;

        return WinFormsTouchState.IsPromotedTouchMouseMessage(GetMessageExtraInfo().ToInt64());
    }

    private void Watch(Control control)
    {
        if (_hooks.ContainsKey(control))
            return;

        var hook = new TouchWindowHook(this, control);
        _hooks.Add(control, hook);
        control.ControlAdded += OnControlAdded;
        control.ControlRemoved += OnControlRemoved;

        foreach (Control child in control.Controls)
            Watch(child);
    }

    private void Unwatch(Control control)
    {
        foreach (Control child in control.Controls.Cast<Control>().ToArray())
            Unwatch(child);

        control.ControlAdded -= OnControlAdded;
        control.ControlRemoved -= OnControlRemoved;

        if (_hooks.Remove(control, out TouchWindowHook? hook))
            hook.Dispose();
    }

    // OpenTK may recreate its drawable independently of managed Control events.
    // The native parent-notify hooks are primary; resize provides a fallback
    // for hosts that suppress native child creation notifications.
    private void OnSurfaceSizeChanged(object? sender, EventArgs e)
        => RefreshNativeChildren();

    private void OnControlAdded(object? sender, ControlEventArgs e)
    {
        Watch(e.Control);
        RefreshNativeChildren();
    }

    private void OnControlRemoved(object? sender, ControlEventArgs e)
    {
        Unwatch(e.Control);
        RefreshNativeChildren();
        _state.CancelAll();
    }

    /// <summary>
    /// SKGLControl hosts an OpenTK/GLFW native child HWND that is not exposed
    /// through Control.Controls. Discover Win32 descendants and refresh after
    /// native parent notifications or window recreation.
    /// </summary>
    private void RefreshNativeChildren()
    {
        if (_disposed || !_surface.IsHandleCreated || _refreshingNativeChildren)
            return;

        _refreshingNativeChildren = true;

        try
        {
            var descendants = new HashSet<IntPtr>();

            EnumChildWindows(
                _surface.Handle,
                (hwnd, _) =>
                {
                    descendants.Add(hwnd);
                    return true;
                },
                IntPtr.Zero);

            var managedHandles = _hooks.Values
                .Where(static hook => hook.Handle != IntPtr.Zero)
                .Select(static hook => hook.Handle)
                .ToHashSet();

            foreach (IntPtr hwnd in _nativeChildren.Keys.ToArray())
            {
                if (descendants.Contains(hwnd) && !managedHandles.Contains(hwnd))
                    continue;

                _nativeChildren.Remove(hwnd, out NativeChildHook? stale);
                stale?.Dispose();
            }

            foreach (IntPtr hwnd in descendants)
            {
                if (managedHandles.Contains(hwnd) || _nativeChildren.ContainsKey(hwnd))
                    continue;

                _nativeChildren.Add(hwnd, new NativeChildHook(this, hwnd));
            }
        }
        finally
        {
            _refreshingNativeChildren = false;
        }
    }

    private void ForgetNativeChild(IntPtr hwnd)
    {
        if (_nativeChildren.Remove(hwnd, out NativeChildHook? hook))
            hook.Dispose();

        _state.CancelAll();
    }

    private bool HandleTouchMessage(IntPtr wParam, IntPtr lParam)
    {
        int count = unchecked((ushort)(wParam.ToInt64() & 0xFFFF));

        try
        {
            if (count == 0)
                return true;

            var inputs = new TouchInput[count];

            if (!GetTouchInputInfo(
                    lParam,
                    (uint)count,
                    inputs,
                    Marshal.SizeOf<TouchInput>()))
                return true;

            foreach (TouchInput input in inputs)
            {
                // TOUCHINPUT coordinates are physical screen pixels in hundredths.
                // Convert through the surface's WinForms client space, and then
                // through the existing presentation transform to logical ScreenPx.
                var physical = new Point(
                    (int)Math.Round(input.X / 100d),
                    (int)Math.Round(input.Y / 100d));

                Point local = _surface.PointToClient(physical);

                Point position = _surface switch
                {
                    WinFormGpuRenderSurfaceControl gpu when gpu.Host is not null =>
                        gpu.Adapter.AdapterPxToScreenPx(local),
                    WinFormBitmapRenderSurfaceControl bitmap when bitmap.Host is not null =>
                        bitmap.Adapter.AdapterPxToScreenPx(local),
                    _ => local
                };

                int id = unchecked((int)input.Id);

                if ((input.Flags & TouchDown) != 0)
                    _state.Begin(id, position);
                else if ((input.Flags & TouchUp) != 0)
                    _state.End(id, position);
                else
                    _state.Move(id, position);
            }

            return true;
        }
        finally
        {
            // Once handled, WM_TOUCH must not be passed to DefWindowProc.
            CloseTouchInputHandle(lParam);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _surface.SizeChanged -= OnSurfaceSizeChanged;
        Unwatch(_surface);

        foreach (NativeChildHook hook in _nativeChildren.Values.ToArray())
            hook.Dispose();

        _nativeChildren.Clear();
        _state.CancelAll();
    }

    private sealed class TouchWindowHook : NativeWindow, IDisposable
    {
        private readonly WinFormsTouchAdapter _owner;
        private readonly Control _control;

        internal bool IsRegistered { get; private set; }

        internal TouchWindowHook(WinFormsTouchAdapter owner, Control control)
        {
            _owner = owner;
            _control = control;
            _control.HandleCreated += OnHandleCreated;
            _control.HandleDestroyed += OnHandleDestroyed;

            if (_control.IsHandleCreated)
                Attach();
        }

        private void OnHandleCreated(object? sender, EventArgs e)
        {
            Attach();
            _owner.RefreshNativeChildren();
        }

        private void OnHandleDestroyed(object? sender, EventArgs e)
        {
            Detach();
            _owner.RefreshNativeChildren();
            _owner._state.CancelAll();
        }

        private void Attach()
        {
            if (Handle != IntPtr.Zero || !_control.IsHandleCreated)
                return;

            AssignHandle(_control.Handle);
            IsRegistered = RegisterTouchWindow(Handle, 0);
        }

        private void Detach()
        {
            if (Handle == IntPtr.Zero)
                return;

            if (IsRegistered)
                UnregisterTouchWindow(Handle);

            IsRegistered = false;
            ReleaseHandle();
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WmTouch && IsRegistered &&
                _owner.HandleTouchMessage(m.WParam, m.LParam))
            {
                m.Result = IntPtr.Zero;
                return;
            }

            base.WndProc(ref m);

            // Native children may be created by OpenTK rather than Control.Controls.
            if (m.Msg == WmParentNotify &&
                ((m.WParam.ToInt64() & 0xFFFF) is WmCreate or WmDestroy))
                _owner.RefreshNativeChildren();
        }

        public void Dispose()
        {
            _control.HandleCreated -= OnHandleCreated;
            _control.HandleDestroyed -= OnHandleDestroyed;
            Detach();
        }
    }

    /// <summary>
    /// Subclasses a Win32 child window not represented by a managed Control.
    /// In particular, OpenTK owns its GLFW drawable; we only intercept touch.
    /// </summary>
    private sealed class NativeChildHook : NativeWindow, IDisposable
    {
        private readonly WinFormsTouchAdapter _owner;

        internal bool IsRegistered { get; private set; }

        internal NativeChildHook(WinFormsTouchAdapter owner, IntPtr hwnd)
        {
            _owner = owner;
            AssignHandle(hwnd);
            IsRegistered = RegisterTouchWindow(hwnd, 0);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WmTouch && IsRegistered &&
                _owner.HandleTouchMessage(m.WParam, m.LParam))
            {
                m.Result = IntPtr.Zero;
                return;
            }

            IntPtr hwnd = Handle;
            base.WndProc(ref m);

            if (m.Msg == WmParentNotify &&
                ((m.WParam.ToInt64() & 0xFFFF) is WmCreate or WmDestroy))
                _owner.RefreshNativeChildren();

            if (m.Msg == WmNcDestroy)
                _owner.ForgetNativeChild(hwnd);
        }

        public void Dispose()
        {
            if (Handle == IntPtr.Zero)
                return;

            if (IsRegistered)
                UnregisterTouchWindow(Handle);

            IsRegistered = false;
            ReleaseHandle();
        }
    }

    // Win32 TOUCHINPUT, including pointer-sized HANDLE/ULONG_PTR fields.
    [StructLayout(LayoutKind.Sequential)]
    private struct TouchInput
    {
        internal int X;
        internal int Y;
        internal IntPtr Source;
        internal uint Id;
        internal uint Flags;
        internal uint Mask;
        internal uint Time;
        internal UIntPtr ExtraInfo;
        internal uint ContactWidth;
        internal uint ContactHeight;
    }

    private delegate bool EnumWindowsCallback(IntPtr hwnd, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumChildWindows(
        IntPtr parent,
        EnumWindowsCallback callback,
        IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterTouchWindow(IntPtr hwnd, uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterTouchWindow(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetTouchInputInfo(
        IntPtr handle,
        uint count,
        [Out] TouchInput[] inputs,
        int size);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseTouchInputHandle(IntPtr handle);

    [DllImport("user32.dll")]
    private static extern IntPtr GetMessageExtraInfo();
}
