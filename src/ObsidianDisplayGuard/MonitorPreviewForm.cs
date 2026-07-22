namespace ObsidianDisplayGuard;

internal sealed class MonitorPreviewForm : Form
{
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 1800 };

    internal MonitorPreviewForm(MonitorDescriptor monitor)
    {
        Text = "Monitor preview";
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Bounds = monitor.Bounds;
        TopMost = true;
        ShowInTaskbar = false;
        BackColor = Color.FromArgb(16, 18, 24);
        Opacity = 0.94;

        var title = new Label
        {
            Text = "OBSIDIAN DISPLAY GUARD",
            ForeColor = Color.FromArgb(146, 114, 255),
            Font = new Font("Segoe UI", 26F, FontStyle.Bold),
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleCenter
        };
        var detail = new Label
        {
            Text = $"This monitor will be protected\n{monitor.FriendlyName} · {monitor.Bounds.Width}×{monitor.Bounds.Height}",
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 14F),
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleCenter,
            Margin = new Padding(0, 16, 0, 0)
        };
        var stack = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            Anchor = AnchorStyles.None,
            BackColor = Color.Transparent
        };
        stack.Controls.Add(title);
        stack.Controls.Add(detail);

        var host = new TableLayoutPanel { Dock = DockStyle.Fill };
        host.Controls.Add(stack, 0, 0);
        Controls.Add(host);

        _timer.Tick += (_, _) => Close();
        Shown += (_, _) => _timer.Start();
        FormClosed += (_, _) => _timer.Dispose();
        KeyDown += (_, eventArgs) =>
        {
            if (eventArgs.KeyCode == Keys.Escape)
            {
                Close();
            }
        };
    }
}
