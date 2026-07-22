namespace ObsidianDisplayGuard;

internal sealed record LaunchOptions(bool Background, bool SafeMode, bool RestoreOnly)
{
    internal static LaunchOptions Parse(IEnumerable<string> arguments)
    {
        var values = new HashSet<string>(arguments, StringComparer.OrdinalIgnoreCase);
        return new LaunchOptions(
            values.Contains("--background"),
            values.Contains("--safe"),
            values.Contains("--restore"));
    }
}
