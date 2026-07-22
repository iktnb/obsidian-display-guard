using System.Drawing;

namespace ObsidianDisplayGuard.Tests;

public sealed class MonitorCatalogTests : IDisposable
{
    private readonly string _temporaryDirectory = Path.Combine(Path.GetTempPath(), "odg-tests-" + Guid.NewGuid());
    private readonly FileLogger _logger;

    public MonitorCatalogTests()
    {
        Directory.CreateDirectory(_temporaryDirectory);
        _logger = new FileLogger(Path.Combine(_temporaryDirectory, "test.log"));
    }

    [Fact]
    public void ExactDeviceIdentityWins()
    {
        var catalog = new MonitorCatalog(_logger);
        var monitors = CreateMonitors();
        var identity = MonitorIdentity.FromDescriptor(monitors[1]);

        Assert.Same(monitors[1], catalog.Find(monitors, identity));
    }

    [Fact]
    public void HardwareHashSurvivesDeviceNameChange()
    {
        var catalog = new MonitorCatalog(_logger);
        var monitors = CreateMonitors();
        var identity = new MonitorIdentity
        {
            DeviceId = "stale-device-id",
            HardwareIdHash = monitors[1].HardwareIdHash,
            FriendlyName = monitors[1].FriendlyName,
            Width = 1920,
            Height = 1080
        };

        Assert.Same(monitors[1], catalog.Find(monitors, identity));
    }

    [Fact]
    public void MissingProtectedMonitorFailsClosed()
    {
        var catalog = new MonitorCatalog(_logger);
        var identity = new MonitorIdentity
        {
            DeviceId = "missing",
            HardwareIdHash = "missing",
            FriendlyName = "Missing",
            Width = 1280,
            Height = 720
        };

        Assert.Null(catalog.Find(CreateMonitors(), identity));
    }

    [Fact]
    public void SmallestNonPrimaryMonitorIsDefault()
    {
        var monitors = CreateMonitors();
        Assert.Same(monitors[1], MonitorCatalog.ChooseDefault(monitors));
    }

    private static IReadOnlyList<MonitorDescriptor> CreateMonitors() =>
    [
        new("DISPLAY1", "BIG", "HASH-BIG", "Big", new Rectangle(0, 0, 2560, 1440), new Rectangle(0, 0, 2560, 1392), true),
        new("DISPLAY2", "SMALL", "HASH-SMALL", "Small", new Rectangle(-1920, 357, 1920, 1080), new Rectangle(-1920, 357, 1920, 1032), false)
    ];

    public void Dispose()
    {
        _logger.Dispose();
        Directory.Delete(_temporaryDirectory, true);
    }
}
