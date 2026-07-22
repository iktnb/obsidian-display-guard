using System.Text.Json;

namespace WindowHarness;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        var options = Parse(args);
        Application.Run(new HarnessForm(options));
    }

    private static HarnessOptions Parse(string[] args)
    {
        var values = args
            .Chunk(2)
            .Where(pair => pair.Length == 2)
            .ToDictionary(pair => pair[0], pair => pair[1], StringComparer.OrdinalIgnoreCase);
        return new HarnessOptions(
            int.Parse(values.GetValueOrDefault("--x", "100")),
            int.Parse(values.GetValueOrDefault("--y", "100")),
            int.Parse(values.GetValueOrDefault("--width", "900")),
            int.Parse(values.GetValueOrDefault("--height", "600")),
            int.Parse(values.GetValueOrDefault("--duration", "3500")),
            values.GetValueOrDefault("--result", string.Empty));
    }
}

internal sealed record HarnessOptions(int X, int Y, int Width, int Height, int Duration, string ResultPath);

internal sealed class HarnessForm : Form
{
    private readonly HarnessOptions _options;
    private readonly System.Windows.Forms.Timer _timer;

    internal HarnessForm(HarnessOptions options)
    {
        _options = options;
        Text = "Obsidian Display Guard integration harness";
        StartPosition = FormStartPosition.Manual;
        Bounds = new Rectangle(options.X, options.Y, options.Width, options.Height);
        Controls.Add(new Label
        {
            Text = "This test window should be evicted from the protected monitor.",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 14F)
        });

        _timer = new System.Windows.Forms.Timer { Interval = options.Duration };
        _timer.Tick += (_, _) => Finish();
        Shown += (_, _) => _timer.Start();
    }

    private void Finish()
    {
        _timer.Stop();
        if (!string.IsNullOrWhiteSpace(_options.ResultPath))
        {
            var result = new { Bounds.X, Bounds.Y, Bounds.Width, Bounds.Height };
            File.WriteAllText(_options.ResultPath, JsonSerializer.Serialize(result));
        }

        Close();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Dispose();
        }

        base.Dispose(disposing);
    }
}
