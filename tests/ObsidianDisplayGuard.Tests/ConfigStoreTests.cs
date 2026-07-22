namespace ObsidianDisplayGuard.Tests;

public sealed class ConfigStoreTests : IDisposable
{
    private readonly string _temporaryDirectory = Path.Combine(Path.GetTempPath(), "odg-config-" + Guid.NewGuid());
    private readonly string _configPath;
    private readonly FileLogger _logger;

    public ConfigStoreTests()
    {
        Directory.CreateDirectory(_temporaryDirectory);
        _configPath = Path.Combine(_temporaryDirectory, "config.json");
        _logger = new FileLogger(Path.Combine(_temporaryDirectory, "test.log"));
    }

    [Fact]
    public void ConfigurationRoundTripsWithoutChangingUnicodeVaultName()
    {
        var store = new ConfigStore(_logger, _configPath);
        var config = new GuardConfig
        {
            ObsidianExePath = @"C:\Apps\Obsidian\Obsidian.exe",
            VaultName = "Приклад сховища",
            ProtectedMonitor = new MonitorIdentity
            {
                DeviceId = "MONITOR\\EXAMPLE1234",
                HardwareIdHash = "ABC",
                FriendlyName = "Example Display",
                Width = 1920,
                Height = 1080
            },
            LaunchAtSignIn = true,
            ProtectionEnabled = true
        };

        store.Save(config);
        var loaded = store.Load();

        Assert.Equal(config.VaultName, loaded.VaultName);
        Assert.Equal(config.ProtectedMonitor.HardwareIdHash, loaded.ProtectedMonitor.HardwareIdHash);
        Assert.True(loaded.ProtectionEnabled);
    }

    [Fact]
    public void UnsupportedSchemaReturnsSafeUnconfiguredDefaults()
    {
        File.WriteAllText(_configPath, "{\"schemaVersion\":999,\"protectionEnabled\":true}");
        var store = new ConfigStore(_logger, _configPath);

        var loaded = store.Load();

        Assert.False(loaded.IsConfigured);
        Assert.False(loaded.ProtectionEnabled);
    }

    public void Dispose()
    {
        _logger.Dispose();
        Directory.Delete(_temporaryDirectory, true);
    }
}
