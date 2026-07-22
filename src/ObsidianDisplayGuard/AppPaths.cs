namespace ObsidianDisplayGuard;

public static class AppPaths
{
    public static string DataDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ObsidianDisplayGuard");

    public static string ConfigPath { get; } = Path.Combine(DataDirectory, "config.json");
    public static string SessionPath { get; } = Path.Combine(DataDirectory, "session.json");
    public static string LogPath { get; } = Path.Combine(DataDirectory, "logs", "guard.log");
}
