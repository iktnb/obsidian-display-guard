namespace ObsidianDisplayGuard;

public sealed class IdleOverlayController : IDisposable
{
    private readonly ObsidianIdleTracker _tracker = new();
    private readonly FileLogger _logger;
    private IdleOverlayForm? _overlay;
    private nint _protectedWindow;
    private bool _disposed;

    internal IdleOverlayController(FileLogger logger)
    {
        _logger = logger;
    }

    internal void Update(
        GuardConfig config,
        MonitorDescriptor monitor,
        nint protectedWindow,
        bool isObsidianFocused,
        DateTimeOffset now)
    {
        if (_disposed)
        {
            return;
        }

        _protectedWindow = protectedWindow;
        var shouldShow = _tracker.Update(
            config.IdleOverlayEnabled,
            isObsidianFocused,
            config.IdleOverlayDelay,
            now);

        if (!config.IdleOverlayEnabled || isObsidianFocused || protectedWindow == 0)
        {
            HideOverlay();
            return;
        }

        if (!shouldShow || _overlay is { IsDisposed: false })
        {
            return;
        }

        try
        {
            _overlay = new IdleOverlayForm(
                monitor,
                config.IdleOverlayDelayMinutes,
                DismissAndActivateObsidian);
            _overlay.FormClosed += OnOverlayClosed;
            _overlay.Show();
            _logger.Info("Idle screen shown");
        }
        catch (Exception exception)
        {
            HideOverlay();
            _tracker.Reset();
            _logger.Error("Could not show the idle screen", exception);
        }
    }

    internal void Reset()
    {
        _tracker.Reset();
        HideOverlay();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Reset();
    }

    private void DismissAndActivateObsidian()
    {
        var protectedWindow = _protectedWindow;
        _tracker.Reset();
        HideOverlay();

        if (protectedWindow == 0 || !NativeMethods.IsWindow(protectedWindow))
        {
            return;
        }

        if (NativeMethods.IsIconic(protectedWindow))
        {
            NativeMethods.ShowWindowAsync(protectedWindow, NativeMethods.SwRestore);
        }

        NativeMethods.SetForegroundWindow(protectedWindow);
        _logger.Info("Idle screen dismissed");
    }

    private void HideOverlay()
    {
        if (_overlay is null)
        {
            return;
        }

        var overlay = _overlay;
        _overlay = null;
        overlay.FormClosed -= OnOverlayClosed;
        if (!overlay.IsDisposed)
        {
            overlay.Close();
            overlay.Dispose();
        }
    }

    private void OnOverlayClosed(object? sender, FormClosedEventArgs eventArgs)
    {
        if (sender is not IdleOverlayForm overlay)
        {
            return;
        }

        overlay.FormClosed -= OnOverlayClosed;
        if (ReferenceEquals(_overlay, overlay))
        {
            _overlay = null;
        }

        _tracker.Reset();
        overlay.Dispose();
    }
}
