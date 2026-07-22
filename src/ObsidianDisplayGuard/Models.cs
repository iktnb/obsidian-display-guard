using System.Drawing;
using System.Text.Json.Serialization;

namespace ObsidianDisplayGuard;

public sealed class GuardConfig
{
    public int SchemaVersion { get; set; } = 1;
    public string ObsidianExePath { get; set; } = string.Empty;
    public string VaultName { get; set; } = string.Empty;
    public MonitorIdentity ProtectedMonitor { get; set; } = new();
    public bool LaunchAtSignIn { get; set; } = true;
    public bool RelaunchObsidian { get; set; } = true;
    public bool BorderlessFullscreen { get; set; } = true;
    public bool ProtectionEnabled { get; set; }
    public string Hotkey { get; set; } = "Ctrl+Alt+Shift+F12";

    [JsonIgnore]
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ObsidianExePath) &&
        ProtectedMonitor.IsConfigured;
}

public sealed class MonitorIdentity
{
    public string DeviceId { get; set; } = string.Empty;
    public string HardwareIdHash { get; set; } = string.Empty;
    public string FriendlyName { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }

    [JsonIgnore]
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(DeviceId) ||
        !string.IsNullOrWhiteSpace(HardwareIdHash);

    public static MonitorIdentity FromDescriptor(MonitorDescriptor descriptor) => new()
    {
        DeviceId = descriptor.DeviceId,
        HardwareIdHash = descriptor.HardwareIdHash,
        FriendlyName = descriptor.FriendlyName,
        Width = descriptor.Bounds.Width,
        Height = descriptor.Bounds.Height
    };
}

public sealed record MonitorDescriptor(
    string DeviceName,
    string DeviceId,
    string HardwareIdHash,
    string FriendlyName,
    Rectangle Bounds,
    Rectangle WorkingArea,
    bool Primary)
{
    public string DisplayLabel =>
        $"{FriendlyName} — {Bounds.Width}×{Bounds.Height}" +
        (Primary ? " (Primary)" : string.Empty);
}

public enum GuardStatus
{
    Protected,
    Paused,
    Error
}

public sealed record GuardStatusInfo(GuardStatus Status, string Message);

public sealed class WindowSessionState
{
    public int ProcessId { get; set; }
    public long ProcessStartTimeUtcTicks { get; set; }
    public long WindowHandle { get; set; }
    public long Style { get; set; }
    public long ExtendedStyle { get; set; }
    public WindowPlacementState Placement { get; set; } = new();
    public Rectangle CurrentBounds { get; set; }
    public bool WasMaximized { get; set; }
    public bool WasMinimized { get; set; }
    public bool WasTopmost { get; set; }
}

public sealed class WindowPlacementState
{
    public int Flags { get; set; }
    public int ShowCommand { get; set; }
    public Rectangle NormalPosition { get; set; }
}

public sealed record WindowSnapshot(
    nint Handle,
    int ProcessId,
    string ProcessPath,
    string ProcessName,
    string ClassName,
    Rectangle Bounds,
    bool IsVisible,
    bool IsMinimized,
    bool IsMaximized,
    bool IsCloaked,
    long Style,
    long ExtendedStyle,
    nint Owner);
