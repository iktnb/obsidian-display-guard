using System.Text.Json;

namespace ObsidianDisplayGuard;

public sealed class ConfigStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly FileLogger _logger;
    private readonly string _configPath;

    public ConfigStore(FileLogger logger, string? configPath = null)
    {
        _logger = logger;
        _configPath = configPath ?? AppPaths.ConfigPath;
    }

    public GuardConfig Load()
    {
        if (!File.Exists(_configPath))
        {
            return new GuardConfig();
        }

        try
        {
            var config = JsonSerializer.Deserialize<GuardConfig>(
                File.ReadAllText(_configPath),
                JsonOptions);

            if (config is null || config.SchemaVersion != 1)
            {
                _logger.Warn("Unsupported or empty configuration; starting unconfigured");
                return new GuardConfig();
            }

            return config;
        }
        catch (Exception exception)
        {
            _logger.Error("Could not read configuration; starting unconfigured", exception);
            return new GuardConfig();
        }
    }

    public void Save(GuardConfig config)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_configPath)!);
        var temporaryPath = _configPath + ".tmp";
        var json = JsonSerializer.Serialize(config, JsonOptions);
        File.WriteAllText(temporaryPath, json);
        File.Move(temporaryPath, _configPath, true);
    }

    internal static JsonSerializerOptions SerializerOptions => JsonOptions;
}
