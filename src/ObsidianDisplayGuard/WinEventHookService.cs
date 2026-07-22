namespace ObsidianDisplayGuard;

internal sealed class WinEventHookService : IDisposable
{
    private readonly Control _dispatcher;
    private readonly NativeMethods.WinEventDelegate _callback;
    private readonly List<nint> _hooks = new();
    private bool _disposed;

    internal WinEventHookService(Control dispatcher)
    {
        _dispatcher = dispatcher;
        _callback = OnWinEvent;
    }

    internal event Action<nint>? WindowChanged;

    internal bool Start()
    {
        if (_hooks.Count > 0)
        {
            return true;
        }

        var events = new[]
        {
            NativeMethods.EventSystemForeground,
            NativeMethods.EventSystemMoveSizeEnd,
            NativeMethods.EventObjectShow,
            NativeMethods.EventObjectLocationChange
        };

        foreach (var eventType in events)
        {
            var hook = NativeMethods.SetWinEventHook(
                eventType,
                eventType,
                0,
                _callback,
                0,
                0,
                NativeMethods.WineventOutOfContext | NativeMethods.WineventSkipOwnProcess);
            if (hook != 0)
            {
                _hooks.Add(hook);
            }
        }

        return _hooks.Count == events.Length;
    }

    private void OnWinEvent(
        nint hook,
        uint eventType,
        nint windowHandle,
        int objectId,
        int childId,
        uint eventThread,
        uint eventTime)
    {
        if (_disposed || windowHandle == 0)
        {
            return;
        }

        if (eventType >= NativeMethods.EventObjectShow && objectId != NativeMethods.ObjidWindow)
        {
            return;
        }

        try
        {
            _dispatcher.BeginInvoke(() => WindowChanged?.Invoke(windowHandle));
        }
        catch (InvalidOperationException)
        {
            // The UI message loop is shutting down.
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (var hook in _hooks)
        {
            NativeMethods.UnhookWinEvent(hook);
        }

        _hooks.Clear();
    }
}
