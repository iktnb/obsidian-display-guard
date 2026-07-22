using System.Collections.Concurrent;

namespace ObsidianDisplayGuard;

public sealed class WindowMover
{
    private readonly ConcurrentDictionary<nint, Rectangle> _lastSafePositions = new();
    private readonly FileLogger _logger;

    public WindowMover(FileLogger logger)
    {
        _logger = logger;
    }

    public void RememberSafePosition(WindowSnapshot window, Rectangle protectedBounds)
    {
        if (!window.IsMaximized && !GeometryPolicy.ShouldEvict(window.Bounds, protectedBounds))
        {
            _lastSafePositions[window.Handle] = window.Bounds;
        }
    }

    public bool MoveToMonitor(WindowSnapshot window, MonitorDescriptor destination)
    {
        try
        {
            var wasMaximized = window.IsMaximized;
            if (wasMaximized)
            {
                NativeMethods.ShowWindowAsync(window.Handle, NativeMethods.SwRestore);
            }

            var target = _lastSafePositions.TryGetValue(window.Handle, out var remembered)
                ? GeometryPolicy.FitInside(remembered, destination.WorkingArea)
                : GeometryPolicy.CenterInside(window.Bounds, destination.WorkingArea);

            var moved = NativeMethods.SetWindowPos(
                window.Handle,
                NativeMethods.HwndTop,
                target.X,
                target.Y,
                target.Width,
                target.Height,
                NativeMethods.SwpNoActivate |
                NativeMethods.SwpNoOwnerZOrder |
                NativeMethods.SwpAsyncWindowPos);

            if (wasMaximized)
            {
                NativeMethods.ShowWindowAsync(window.Handle, NativeMethods.SwMaximize);
            }

            if (moved)
            {
                _lastSafePositions[window.Handle] = target;
            }

            return moved;
        }
        catch (Exception exception)
        {
            _logger.Warn($"Could not move a protected-display intruder: {exception.GetType().Name}");
            return false;
        }
    }

    public void Prune(IReadOnlyCollection<nint> activeHandles)
    {
        var active = activeHandles.ToHashSet();
        foreach (var handle in _lastSafePositions.Keys.Where(handle => !active.Contains(handle)).ToList())
        {
            _lastSafePositions.TryRemove(handle, out _);
        }
    }
}
