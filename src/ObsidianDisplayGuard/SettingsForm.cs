namespace ObsidianDisplayGuard;

internal sealed class SettingsForm : Form
{
    private readonly IReadOnlyList<MonitorDescriptor> _monitors;
    private readonly ComboBox _monitor = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    private readonly TextBox _obsidianPath = new() { Dock = DockStyle.Fill };
    private readonly TextBox _vaultName = new() { Dock = DockStyle.Fill };
    private readonly CheckBox _launchAtSignIn = new() { Text = "Start Guard and protect after Windows sign-in", AutoSize = true };
    private readonly CheckBox _relaunch = new() { Text = "Relaunch Obsidian if it closes", AutoSize = true };
    private readonly CheckBox _fullscreen = new() { Text = "Borderless fullscreen over the secondary taskbar", AutoSize = true };
    private readonly CheckBox _protection = new() { Text = "Enable protection after saving", AutoSize = true };

    internal SettingsForm(GuardConfig current, IReadOnlyList<MonitorDescriptor> monitors, MonitorCatalog catalog)
    {
        _monitors = monitors;
        ResultConfig = current;

        Text = "Obsidian Display Guard — Settings";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(680, 540);
        Size = new Size(760, 590);
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = false;
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 10F);

        var heading = new Label
        {
            Text = "Turn one monitor into a dedicated Obsidian display",
            Font = new Font(Font, FontStyle.Bold),
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 6)
        };
        var explanation = new Label
        {
            Text = "Other normal windows will be returned to the primary monitor. " +
                   "Ctrl+Alt+Shift+F12 immediately pauses or resumes protection.",
            AutoSize = true,
            MaximumSize = new Size(680, 0),
            ForeColor = Color.DimGray,
            Margin = new Padding(0, 0, 0, 18)
        };

        foreach (var monitor in monitors)
        {
            _monitor.Items.Add(monitor);
        }
        _monitor.DisplayMember = nameof(MonitorDescriptor.DisplayLabel);

        var selected = catalog.Find(monitors, current.ProtectedMonitor)
            ?? MonitorCatalog.ChooseDefault(monitors);
        if (selected is not null)
        {
            _monitor.SelectedItem = selected;
        }

        _obsidianPath.Text = current.ObsidianExePath;
        _vaultName.Text = current.VaultName;
        _launchAtSignIn.Checked = current.LaunchAtSignIn;
        _relaunch.Checked = current.RelaunchObsidian;
        _fullscreen.Checked = current.BorderlessFullscreen;
        _protection.Checked = current.ProtectionEnabled || !File.Exists(AppPaths.ConfigPath);

        var previewButton = new Button { Text = "Preview monitor", AutoSize = true };
        previewButton.Click += (_, _) => PreviewSelectedMonitor();
        var browseButton = new Button { Text = "Browse…", AutoSize = true };
        browseButton.Click += (_, _) => BrowseForObsidian();

        var monitorRow = CreateFieldRow(_monitor, previewButton);
        var executableRow = CreateFieldRow(_obsidianPath, browseButton);

        var options = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            Dock = DockStyle.Fill,
            Margin = new Padding(0)
        };
        options.Controls.AddRange(new Control[] { _launchAtSignIn, _relaunch, _fullscreen, _protection });

        var saveButton = new Button { Text = "Save and apply", AutoSize = true, Padding = new Padding(8, 3, 8, 3) };
        saveButton.Click += (_, _) => SaveAndClose();
        var cancelButton = new Button { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel };
        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 16, 0, 0)
        };
        buttons.Controls.Add(saveButton);
        buttons.Controls.Add(cancelButton);

        var layout = new TableLayoutPanel
        {
            ColumnCount = 1,
            RowCount = 11,
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(28)
        };
        layout.RowStyles.Clear();
        layout.Controls.Add(heading);
        layout.Controls.Add(explanation);
        layout.Controls.Add(CreateLabel("Protected monitor"));
        layout.Controls.Add(monitorRow);
        layout.Controls.Add(CreateLabel("Obsidian executable"));
        layout.Controls.Add(executableRow);
        layout.Controls.Add(CreateLabel("Vault name (kept only on this PC)"));
        layout.Controls.Add(_vaultName);
        layout.Controls.Add(CreateLabel("Behavior"));
        layout.Controls.Add(options);
        layout.Controls.Add(buttons);

        Controls.Add(layout);
        AcceptButton = saveButton;
        CancelButton = cancelButton;
    }

    internal GuardConfig ResultConfig { get; private set; }

    private static Label CreateLabel(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Font = new Font("Segoe UI", 9F, FontStyle.Bold),
        Margin = new Padding(0, 12, 0, 4)
    };

    private static TableLayoutPanel CreateFieldRow(Control primary, Control secondary)
    {
        var row = new TableLayoutPanel
        {
            ColumnCount = 2,
            RowCount = 1,
            Dock = DockStyle.Fill,
            AutoSize = true,
            Margin = new Padding(0)
        };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        primary.Margin = new Padding(0, 0, 8, 0);
        row.Controls.Add(primary, 0, 0);
        row.Controls.Add(secondary, 1, 0);
        return row;
    }

    private void PreviewSelectedMonitor()
    {
        if (_monitor.SelectedItem is not MonitorDescriptor selected)
        {
            return;
        }

        using var preview = new MonitorPreviewForm(selected);
        preview.ShowDialog(this);
    }

    private void BrowseForObsidian()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Choose Obsidian.exe",
            Filter = "Obsidian executable (Obsidian.exe)|Obsidian.exe|Windows executable (*.exe)|*.exe",
            CheckFileExists = true,
            InitialDirectory = Path.GetDirectoryName(_obsidianPath.Text)
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _obsidianPath.Text = dialog.FileName;
        }
    }

    private void SaveAndClose()
    {
        if (_monitor.SelectedItem is not MonitorDescriptor selectedMonitor)
        {
            MessageBox.Show(this, "Choose a protected monitor.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var executable = _obsidianPath.Text.Trim();
        if (!File.Exists(executable) || !string.Equals(Path.GetFileName(executable), "Obsidian.exe", StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show(this, "Choose the installed Obsidian.exe file.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        ResultConfig = new GuardConfig
        {
            SchemaVersion = 1,
            ObsidianExePath = executable,
            VaultName = _vaultName.Text.Trim(),
            ProtectedMonitor = MonitorIdentity.FromDescriptor(selectedMonitor),
            LaunchAtSignIn = _launchAtSignIn.Checked,
            RelaunchObsidian = _relaunch.Checked,
            BorderlessFullscreen = _fullscreen.Checked,
            ProtectionEnabled = _protection.Checked,
            Hotkey = "Ctrl+Alt+Shift+F12"
        };

        DialogResult = DialogResult.OK;
        Close();
    }
}
