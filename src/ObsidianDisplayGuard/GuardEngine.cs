using System.Collections.Concurrent;

namespace ObsidianDisplayGuard;

public sealed class GuardEngine : IDisposable
{
    private readonly MonitorCatalog _monitorCatalog;
    private readonly WindowInspector _inspector;
    private readonly WindowMover _mover;
    private readonly ObsidianController _obsidian;
    private readonly IdleOverlayController _idleOverlay;
    private readonly FileLogger _logger;
    private readonly int _ownProcessId = Environment.ProcessId;
    private readonly ConcurrentDictionary<nint, DateTimeOffset> _lastWindowEvaluation = new();

    private GuardConfig _config;
    private MonitorDescriptor? _protectedMonitor;
    private MonitorDescriptor? _destinationMonitor;
    private bool _enabled;
    private DateTimeOffset? _resumeAt;
    private GuardStatusInfo _status = new(GuardStatus.Paused, "Protection is paused");
    private bool _disposed;

    public GuardEngine(
        GuardConfig config,
        MonitorCatalog monitorCatalog,
        WindowInspector inspector,
        WindowMover mover,
        ObsidianController obsidian,
        IdleOverlayController idleOverlay,
        FileLogger logger)
    {
        _config = config;
        _monitorCatalog = monitorCatalog;
        _inspector = inspector;
        _mover = mover;
        _obsidian = obsidian;
        _idleOverlay = idleOverlay;
        _logger = logger;
        _enabled = config.ProtectionEnabled;
    }

    public event Action<GuardStatusInfo>? StatusChanged;
    public GuardStatusInfo Status => _status;
    public bool IsEnabled => _enabled;
    public bool IsPaused => !_enabled || _resumeAt is not null;

    public void ApplyConfig(GuardConfig config)
    {
        _idleOverlay.Reset();
        _obsidian.Restore();
        _config = config;
        _enabled = config.ProtectionEnabled;
        _resumeAt = null;
        RefreshTopology();
        Tick();
    }

    public void Start(bool safeMode)
    {
        _enabled = _config.ProtectionEnabled && !safeMode;
        _resumeAt = null;
        RefreshTopology();
        if (safeMode)
        {
            SetStatus(GuardStatus.Paused, "Safe mode: protection is paused");
        }
        else
        {
            Tick();
        }
    }

    public void SetEnabled(bool enabled)
    {
        _enabled = enabled;
        _resumeAt = null;
        if (!enabled)
        {
            _idleOverlay.Reset();
            _obsidian.Restore();
            SetStatus(GuardStatus.Paused, "Protection is off");
            return;
        }

        RefreshTopology();
        Tick();
    }

    public void Pause(TimeSpan? duration)
    {
        if (!_enabled)
        {
            return;
        }

        _resumeAt = duration.HasValue ? DateTimeOffset.Now.Add(duration.Value) : DateTimeOffset.MaxValue;
        _idleOverlay.Reset();
        _obsidian.Restore();
        SetStatus(
            GuardStatus.Paused,
            duration.HasValue ? $"Paused for {duration.Value.TotalMinutes:0} minutes" : "Protection is paused");
    }

    public void Resume()
    {
        if (!_enabled)
        {
            _enabled = true;
        }

        _resumeAt = null;
        RefreshTopology();
        Tick();
    }

    public void OnDisplaySettingsChanged()
    {
        _idleOverlay.Reset();
        _obsidian.Restore();
        _protectedMonitor = null;
        _destinationMonitor = null;
        SetStatus(GuardStatus.Error, "Display layout changed; waiting for a stable topology");
    }

    public void Tick()
    {
        if (_disposed || !_enabled)
        {
            _idleOverlay.Reset();
            return;
        }

        if (_resumeAt is not null)
        {
            if (DateTimeOffset.Now < _resumeAt.Value)
            {
                _idleOverlay.Reset();
                return;
            }

            _resumeAt = null;
        }

        if (!ResolveTopology())
        {
            _idleOverlay.Reset();
            return;
        }

        var windows = _inspector.Enumerate();
        var protectedWindow = _obsidian.EnsureProtected(_config, _protectedMonitor!, windows);
        if (protectedWindow == 0)
        {
            _idleOverlay.Reset();
            SetStatus(GuardStatus.Paused, "Opening Obsidian…");
            return;
        }

        foreach (var window in windows)
        {
            Evaluate(window);
        }

        _mover.Prune(windows.Select(window => window.Handle).ToArray());
        var foregroundWindow = NativeMethods.GetForegroundWindow();
        _idleOverlay.Update(
            _config,
            _protectedMonitor!,
            protectedWindow,
            _obsidian.IsObsidianWindow(foregroundWindow, _config),
            DateTimeOffset.Now);
        SetStatus(GuardStatus.Protected, "The secondary monitor is protected");
    }

    public void EvaluateWindow(nint windowHandle)
    {
        if (_disposed || !_enabled || _resumeAt is not null || !ResolveTopology())
        {
            return;
        }

        if (windowHandle == _obsidian.ProtectedWindow)
        {
            return;
        }

        var now = DateTimeOffset.Now;
        if (_lastWindowEvaluation.TryGetValue(windowHandle, out var last) &&
            now - last < TimeSpan.FromMilliseconds(80))
        {
            return;
        }

        _lastWindowEvaluation[windowHandle] = now;
        var window = _inspector.TryInspect(windowHandle);
        if (window is not null)
        {
            Evaluate(window);
        }
    }

    public void EmergencyRestore()
    {
        _enabled = false;
        _resumeAt = null;
        _idleOverlay.Reset();
        _obsidian.Restore();
        SetStatus(GuardStatus.Paused, "Emergency restore completed");
    }

    private void Evaluate(WindowSnapshot window)
    {
        if (_protectedMonitor is null || _destinationMonitor is null ||
            !_inspector.IsEligible(window, _ownProcessId) ||
            _obsidian.IsAllowedObsidianWindow(window, _config))
        {
            return;
        }

        if (!GeometryPolicy.ShouldEvict(window.Bounds, _protectedMonitor.Bounds))
        {
            _mover.RememberSafePosition(window, _protectedMonitor.Bounds);
            return;
        }

        if (!_mover.MoveToMonitor(window, _destinationMonitor))
        {
            _logger.Warn($"A window from {SafeProcessName(window)} could not be moved");
        }
    }

    private bool ResolveTopology()
    {
        if (_protectedMonitor is not null && _destinationMonitor is not null)
        {
            return true;
        }

        return RefreshTopology();
    }

    private bool RefreshTopology()
    {
        var monitors = _monitorCatalog.GetMonitors();
        _protectedMonitor = _monitorCatalog.Find(monitors, _config.ProtectedMonitor);
        _destinationMonitor = monitors.FirstOrDefault(monitor => monitor.Primary);

        if (_protectedMonitor is null)
        {
            _obsidian.Restore();
            SetStatus(GuardStatus.Error, "Protected monitor was not found; nothing will be moved");
            return false;
        }

        if (_destinationMonitor is null || _destinationMonitor.DeviceName == _protectedMonitor.DeviceName)
        {
            _obsidian.Restore();
            SetStatus(GuardStatus.Error, "A separate primary monitor is required; nothing will be moved");
            return false;
        }

        return true;
    }

    private void SetStatus(GuardStatus status, string message)
    {
        var next = new GuardStatusInfo(status, message);
        if (next == _status)
        {
            return;
        }

        _status = next;
        _logger.Info($"Status: {status}");
        StatusChanged?.Invoke(next);
    }

    private static string SafeProcessName(WindowSnapshot window) =>
        string.IsNullOrWhiteSpace(window.ProcessName) ? "an inaccessible process" : window.ProcessName;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _idleOverlay.Dispose();
        _obsidian.Restore();
    }
}
