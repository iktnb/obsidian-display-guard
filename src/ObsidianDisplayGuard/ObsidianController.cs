using System.Diagnostics;

namespace ObsidianDisplayGuard;

public sealed class ObsidianController
{
    private readonly WindowInspector _inspector;
    private readonly FileLogger _logger;
    private nint _protectedWindow;
    private DateTimeOffset _lastLaunchAttempt = DateTimeOffset.MinValue;

    public ObsidianController(WindowInspector inspector, FileLogger logger)
    {
        _inspector = inspector;
        _logger = logger;
    }

    public nint ProtectedWindow => _protectedWindow;

    public WindowSnapshot? FindMainWindow(string executablePath)
        => FindMainWindow(executablePath, _inspector.Enumerate());

    public WindowSnapshot? FindMainWindow(
        string executablePath,
        IReadOnlyList<WindowSnapshot> windows)
    {
        var candidates = windows
            .Where(window =>
                window.IsVisible &&
                !window.IsCloaked &&
                PathsEqual(window.ProcessPath, executablePath))
            .OrderByDescending(window => (long)window.Bounds.Width * window.Bounds.Height)
            .ToList();

        return candidates.FirstOrDefault(window => window.Owner == 0)
            ?? candidates.FirstOrDefault();
    }

    public nint EnsureProtected(GuardConfig config, MonitorDescriptor monitor)
        => EnsureProtected(config, monitor, _inspector.Enumerate());

    public nint EnsureProtected(
        GuardConfig config,
        MonitorDescriptor monitor,
        IReadOnlyList<WindowSnapshot> windows)
    {
        var mainWindow = FindMainWindow(config.ObsidianExePath, windows);
        if (mainWindow is null)
        {
            _protectedWindow = 0;
            TryLaunch(config);
            return 0;
        }

        if (_protectedWindow != 0 && _protectedWindow != mainWindow.Handle)
        {
            Restore();
        }

        _protectedWindow = mainWindow.Handle;
        if (config.BorderlessFullscreen && NeedsFullscreenUpdate(mainWindow, monitor.Bounds))
        {
            ApplyBorderlessFullscreen(mainWindow, monitor.Bounds);
        }
        else if (!config.BorderlessFullscreen &&
                 !monitor.WorkingArea.Contains(mainWindow.Bounds))
        {
            MoveAndMaximize(mainWindow.Handle, monitor.WorkingArea);
        }

        return _protectedWindow;
    }

    public bool IsAllowedObsidianWindow(WindowSnapshot window, GuardConfig config)
    {
        if (window.Handle == _protectedWindow)
        {
            return true;
        }

        return window.Owner == _protectedWindow &&
               PathsEqual(window.ProcessPath, config.ObsidianExePath);
    }

    public bool IsObsidianWindow(nint windowHandle, GuardConfig config)
    {
        var window = _inspector.TryInspect(windowHandle);
        return window is not null &&
               PathsEqual(window.ProcessPath, config.ObsidianExePath);
    }

    public void Restore()
    {
        if (_protectedWindow == 0 && !File.Exists(AppPaths.SessionPath))
        {
            return;
        }

        RecoveryManager.RestoreSavedWindow(_logger);
        _protectedWindow = 0;
    }

    private void ApplyBorderlessFullscreen(WindowSnapshot window, Rectangle bounds)
    {
        if (!File.Exists(AppPaths.SessionPath))
        {
            var placement = NativeMethods.WindowPlacement.Create();
            if (!NativeMethods.GetWindowPlacement(window.Handle, ref placement))
            {
                _logger.Warn("Could not save the original Obsidian window placement");
                return;
            }

            try
            {
                using var process = Process.GetProcessById(window.ProcessId);
                RecoveryManager.Save(new WindowSessionState
                {
                    ProcessId = window.ProcessId,
                    ProcessStartTimeUtcTicks = process.StartTime.ToUniversalTime().Ticks,
                    WindowHandle = window.Handle,
                    Style = window.Style,
                    ExtendedStyle = window.ExtendedStyle,
                    CurrentBounds = window.Bounds,
                    WasMaximized = window.IsMaximized,
                    WasMinimized = window.IsMinimized,
                    WasTopmost = (window.ExtendedStyle & NativeMethods.WsExTopmost) != 0,
                    Placement = new WindowPlacementState
                    {
                        Flags = placement.Flags,
                        ShowCommand = placement.ShowCommand,
                        NormalPosition = placement.NormalPosition.ToRectangle()
                    }
                });
            }
            catch (Exception exception)
            {
                _logger.Error("Could not save the Obsidian recovery state", exception);
                return;
            }
        }

        var fullscreenStyle = window.Style & ~NativeMethods.WsCaption & ~NativeMethods.WsThickFrame;
        NativeMethods.ShowWindowAsync(window.Handle, NativeMethods.SwRestore);
        NativeMethods.SetWindowLongPtr(window.Handle, NativeMethods.GwlStyle, (nint)fullscreenStyle);
        NativeMethods.SetWindowPos(
            window.Handle,
            NativeMethods.HwndTopmost,
            bounds.X,
            bounds.Y,
            bounds.Width,
            bounds.Height,
            NativeMethods.SwpFrameChanged |
            NativeMethods.SwpNoActivate |
            NativeMethods.SwpNoOwnerZOrder |
            NativeMethods.SwpShowWindow);
    }

    private static void MoveAndMaximize(nint handle, Rectangle workingArea)
    {
        NativeMethods.ShowWindowAsync(handle, NativeMethods.SwRestore);
        NativeMethods.SetWindowPos(
            handle,
            NativeMethods.HwndTop,
            workingArea.X,
            workingArea.Y,
            workingArea.Width,
            workingArea.Height,
            NativeMethods.SwpNoActivate |
            NativeMethods.SwpNoOwnerZOrder |
            NativeMethods.SwpAsyncWindowPos);
        NativeMethods.ShowWindowAsync(handle, NativeMethods.SwMaximize);
    }

    private void TryLaunch(GuardConfig config)
    {
        if (!config.RelaunchObsidian ||
            !File.Exists(config.ObsidianExePath) ||
            DateTimeOffset.Now - _lastLaunchAttempt < TimeSpan.FromSeconds(5))
        {
            return;
        }

        _lastLaunchAttempt = DateTimeOffset.Now;
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = config.ObsidianExePath,
                UseShellExecute = true
            };
            if (!string.IsNullOrWhiteSpace(config.VaultName))
            {
                startInfo.Arguments = $"obsidian://open?vault={Uri.EscapeDataString(config.VaultName)}";
            }

            Process.Start(startInfo);
            _logger.Info("Requested an Obsidian launch");
        }
        catch (Exception exception)
        {
            _logger.Error("Could not launch Obsidian", exception);
        }
    }

    private static bool PathsEqual(string left, string right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return false;
        }

        try
        {
            return string.Equals(
                Path.GetFullPath(left),
                Path.GetFullPath(right),
                StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static bool NeedsFullscreenUpdate(WindowSnapshot window, Rectangle target) =>
        window.Bounds != target ||
        (window.Style & (NativeMethods.WsCaption | NativeMethods.WsThickFrame)) != 0 ||
        (window.ExtendedStyle & NativeMethods.WsExTopmost) == 0;
}
