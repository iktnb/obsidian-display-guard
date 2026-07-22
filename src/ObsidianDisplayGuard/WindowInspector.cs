using System.Diagnostics;
using System.Collections.Concurrent;
using System.Text;

namespace ObsidianDisplayGuard;

public static class WindowPolicy
{
    private static readonly HashSet<string> IgnoredClasses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Progman",
        "WorkerW",
        "Shell_TrayWnd",
        "Shell_SecondaryTrayWnd",
        "NotifyIconOverflowWindow",
        "DV2ControlHost",
        "Windows.UI.Core.CoreWindow",
        "XamlExplorerHostIslandWindow"
    };

    public static bool IsEligible(WindowSnapshot window, int ownProcessId, bool isRootWindow)
    {
        if (!window.IsVisible || window.IsMinimized || window.IsCloaked ||
            window.ProcessId == ownProcessId ||
            window.Bounds.Width < 80 || window.Bounds.Height < 60 ||
            IgnoredClasses.Contains(window.ClassName) ||
            !isRootWindow)
        {
            return false;
        }

        var isToolWindow = (window.ExtendedStyle & NativeMethods.WsExToolWindow) != 0;
        var isAppWindow = (window.ExtendedStyle & NativeMethods.WsExAppWindow) != 0;
        return !isToolWindow || isAppWindow;
    }
}

public sealed class WindowInspector
{
    private sealed record ProcessCacheEntry(long StartTimeUtcTicks, string Path, string Name);

    private readonly ConcurrentDictionary<int, ProcessCacheEntry> _processCache = new();


    public IReadOnlyList<WindowSnapshot> Enumerate()
    {
        var windows = new List<WindowSnapshot>();
        NativeMethods.EnumWindows((windowHandle, _) =>
        {
            var snapshot = TryInspect(windowHandle);
            if (snapshot is not null)
            {
                windows.Add(snapshot);
            }

            return true;
        }, 0);
        return windows;
    }

    public WindowSnapshot? TryInspect(nint windowHandle)
    {
        if (windowHandle == 0 || !NativeMethods.IsWindow(windowHandle))
        {
            return null;
        }

        var visible = NativeMethods.IsWindowVisible(windowHandle);
        if (!visible)
        {
            return null;
        }
        if (!NativeMethods.GetWindowRect(windowHandle, out var nativeRect))
        {
            return null;
        }

        NativeMethods.GetWindowThreadProcessId(windowHandle, out var processIdValue);
        if (processIdValue == 0)
        {
            return null;
        }

        var classNameBuffer = new StringBuilder(256);
        NativeMethods.GetClassName(windowHandle, classNameBuffer, classNameBuffer.Capacity);
        var className = classNameBuffer.ToString();
        var processInfo = GetProcessInfo((int)processIdValue);

        var cloaked = false;
        if (NativeMethods.DwmGetWindowAttribute(
                windowHandle,
                NativeMethods.DwmwaCloaked,
                out var cloakedValue,
                sizeof(int)) == 0)
        {
            cloaked = cloakedValue != 0;
        }

        return new WindowSnapshot(
            windowHandle,
            (int)processIdValue,
            processInfo.Path,
            processInfo.Name,
            className,
            nativeRect.ToRectangle(),
            visible,
            NativeMethods.IsIconic(windowHandle),
            NativeMethods.IsZoomed(windowHandle),
            cloaked,
            NativeMethods.GetWindowLongPtr(windowHandle, NativeMethods.GwlStyle).ToInt64(),
            NativeMethods.GetWindowLongPtr(windowHandle, NativeMethods.GwlExStyle).ToInt64(),
            NativeMethods.GetWindow(windowHandle, NativeMethods.GwOwner));
    }

    public bool IsEligible(WindowSnapshot window, int ownProcessId)
    {
        return WindowPolicy.IsEligible(
            window,
            ownProcessId,
            NativeMethods.GetAncestor(window.Handle, NativeMethods.GaRoot) == window.Handle);
    }

    private (string Path, string Name) GetProcessInfo(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            var startTime = process.StartTime.ToUniversalTime().Ticks;
            if (_processCache.TryGetValue(processId, out var cached) &&
                cached.StartTimeUtcTicks == startTime)
            {
                return (cached.Path, cached.Name);
            }

            var value = new ProcessCacheEntry(
                startTime,
                process.MainModule?.FileName ?? string.Empty,
                process.ProcessName);
            _processCache[processId] = value;
            return (value.Path, value.Name);
        }
        catch
        {
            // Elevated or protected processes cannot always be inspected without elevation.
            return (string.Empty, string.Empty);
        }
    }
}
