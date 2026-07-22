using System.Diagnostics;
using System.Text.Json;

namespace ObsidianDisplayGuard;

internal static class DefaultConfigDiscovery
{
    internal static GuardConfig Create(MonitorCatalog monitorCatalog, FileLogger logger)
    {
        var monitors = monitorCatalog.GetMonitors();
        var selectedMonitor = MonitorCatalog.ChooseDefault(monitors);
        return new GuardConfig
        {
            ObsidianExePath = FindObsidianPath(),
            VaultName = FindOpenVaultName(logger),
            ProtectedMonitor = selectedMonitor is null
                ? new MonitorIdentity()
                : MonitorIdentity.FromDescriptor(selectedMonitor),
            LaunchAtSignIn = true,
            RelaunchObsidian = true,
            BorderlessFullscreen = true,
            ProtectionEnabled = false
        };
    }

    private static string FindObsidianPath()
    {
        try
        {
            var running = Process.GetProcessesByName("Obsidian")
                .Select(process =>
                {
                    try
                    {
                        return process.MainModule?.FileName;
                    }
                    catch
                    {
                        return null;
                    }
                    finally
                    {
                        process.Dispose();
                    }
                })
                .FirstOrDefault(path => !string.IsNullOrWhiteSpace(path));
            if (!string.IsNullOrWhiteSpace(running))
            {
                return running;
            }
        }
        catch
        {
            // Continue with the conventional per-user installation path.
        }

        var conventionalPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs",
            "Obsidian",
            "Obsidian.exe");
        return File.Exists(conventionalPath) ? conventionalPath : string.Empty;
    }

    private static string FindOpenVaultName(FileLogger logger)
    {
        var obsidianConfiguration = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "obsidian",
            "obsidian.json");
        if (!File.Exists(obsidianConfiguration))
        {
            return string.Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(obsidianConfiguration));
            if (!document.RootElement.TryGetProperty("vaults", out var vaults))
            {
                return string.Empty;
            }

            foreach (var vault in vaults.EnumerateObject())
            {
                var value = vault.Value;
                if (value.TryGetProperty("open", out var open) &&
                    open.ValueKind == JsonValueKind.True &&
                    value.TryGetProperty("path", out var path))
                {
                    return Path.GetFileName(path.GetString()?.TrimEnd('\\', '/') ?? string.Empty);
                }
            }
        }
        catch (Exception exception)
        {
            logger.Warn($"Could not discover the active Obsidian vault: {exception.GetType().Name}");
        }

        return string.Empty;
    }
}
