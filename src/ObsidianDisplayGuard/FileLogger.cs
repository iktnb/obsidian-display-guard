namespace ObsidianDisplayGuard;

public sealed class FileLogger : IDisposable
{
    private const long MaximumBytes = 1024 * 1024;
    private readonly object _gate = new();
    private readonly string _path;
    private bool _disposed;

    public FileLogger(string path)
    {
        _path = path;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        RotateIfNeeded();
    }

    public void Info(string message) => Write("INFO", message, null);
    public void Warn(string message) => Write("WARN", message, null);
    public void Error(string message, Exception? exception = null) => Write("ERROR", message, exception);

    private void Write(string level, string message, Exception? exception)
    {
        if (_disposed)
        {
            return;
        }

        var safeMessage = message.Replace('\r', ' ').Replace('\n', ' ');
        var line = $"{DateTimeOffset.Now:O} [{level}] {safeMessage}";
        if (exception is not null)
        {
            line += $" | {exception.GetType().Name}: {exception.Message.Replace('\r', ' ').Replace('\n', ' ')}";
        }

        lock (_gate)
        {
            RotateIfNeeded();
            File.AppendAllText(_path, line + Environment.NewLine);
        }
    }

    private void RotateIfNeeded()
    {
        var file = new FileInfo(_path);
        if (!file.Exists || file.Length < MaximumBytes)
        {
            return;
        }

        var previous = _path + ".1";
        File.Move(_path, previous, true);
    }

    public void Dispose() => _disposed = true;
}
