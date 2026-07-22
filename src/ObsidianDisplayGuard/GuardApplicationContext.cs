using Microsoft.Win32;

namespace ObsidianDisplayGuard;

internal sealed class GuardApplicationContext : ApplicationContext
{
    private readonly LaunchOptions _options;
    private readonly FileLogger _logger;
    private readonly ConfigStore _configStore;
    private readonly MonitorCatalog _monitorCatalog;
    private readonly GuardEngine _engine;
    private readonly WinEventHookService _hookService;
    private readonly HotkeyWindow _hotkeyWindow;
    private readonly EventWaitHandle _showSettingsSignal;
    private readonly System.Windows.Forms.Timer _timer;
    private readonly NotifyIcon _trayIcon;
    private readonly ToolStripMenuItem _protectionItem;
    private readonly ToolStripMenuItem _resumeItem;
    private readonly ToolStripMenuItem _startupItem;
    private readonly Control _dispatcher;
    private GuardConfig _config;
    private SettingsForm? _settingsForm;
    private bool _disposed;

    internal GuardApplicationContext(LaunchOptions options, FileLogger logger)
    {
        _options = options;
        _logger = logger;
        _configStore = new ConfigStore(logger);
        _monitorCatalog = new MonitorCatalog(logger);

        RecoveryManager.RestoreSavedWindow(logger);

        _config = _configStore.Load();
        var hadConfirmedConfig = _config.IsConfigured;
        if (!hadConfirmedConfig)
        {
            _config = DefaultConfigDiscovery.Create(_monitorCatalog, logger);
        }

        var inspector = new WindowInspector();
        var mover = new WindowMover(logger);
        var obsidian = new ObsidianController(inspector, logger);
        _engine = new GuardEngine(_config, _monitorCatalog, inspector, mover, obsidian, logger);
        _engine.StatusChanged += OnStatusChanged;

        _dispatcher = new Control();
        _ = _dispatcher.Handle;
        _hookService = new WinEventHookService(_dispatcher);
        _hookService.WindowChanged += _engine.EvaluateWindow;

        _hotkeyWindow = new HotkeyWindow();
        _hotkeyWindow.Pressed += TogglePause;
        if (!_hotkeyWindow.Registered)
        {
            logger.Warn("The emergency hotkey could not be registered");
        }

        _showSettingsSignal = new EventWaitHandle(
            false,
            EventResetMode.AutoReset,
            AppIdentity.ShowSettingsEventName,
            out _);

        _protectionItem = new ToolStripMenuItem("Protection on") { CheckOnClick = false };
        _protectionItem.Click += (_, _) => ToggleProtection();
        _resumeItem = new ToolStripMenuItem("Resume now");
        _resumeItem.Click += (_, _) => ResumeProtection();
        _startupItem = new ToolStripMenuItem("Start with Windows") { CheckOnClick = false };
        _startupItem.Click += (_, _) => ToggleStartup();

        var menu = BuildMenu();
        _trayIcon = new NotifyIcon
        {
            Text = AppIdentity.ProductName,
            Icon = TrayIconFactory.Create(GuardStatus.Paused),
            ContextMenuStrip = menu,
            Visible = true
        };
        _trayIcon.DoubleClick += (_, _) => ShowSettings();

        _timer = new System.Windows.Forms.Timer { Interval = 2000 };
        _timer.Tick += (_, _) => OnTimerTick();
        _timer.Start();

        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;

        if (!_hookService.Start())
        {
            logger.Warn("One or more WinEvent hooks could not be registered; fallback checks remain active");
        }

        _engine.Start(options.SafeMode || !hadConfirmedConfig);
        RefreshMenu();

        if (!hadConfirmedConfig || !options.Background)
        {
            ShowSettings();
        }
    }

    internal void EmergencyRestore() => _engine.EmergencyRestore();

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add(_protectionItem);
        menu.Items.Add(_resumeItem);
        menu.Items.Add(new ToolStripSeparator());

        var pauseMenu = new ToolStripMenuItem("Pause protection");
        pauseMenu.DropDownItems.Add("5 minutes", null, (_, _) => _engine.Pause(TimeSpan.FromMinutes(5)));
        pauseMenu.DropDownItems.Add("15 minutes", null, (_, _) => _engine.Pause(TimeSpan.FromMinutes(15)));
        pauseMenu.DropDownItems.Add("60 minutes", null, (_, _) => _engine.Pause(TimeSpan.FromMinutes(60)));
        pauseMenu.DropDownItems.Add("Until resumed", null, (_, _) => _engine.Pause(null));
        menu.Items.Add(pauseMenu);

        menu.Items.Add("Pause && restore Obsidian now", null, (_, _) =>
        {
            _engine.Pause(null);
        });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Settings…", null, (_, _) => ShowSettings());
        menu.Items.Add(_startupItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit && restore", null, (_, _) => ExitSafely());
        return menu;
    }

    private void OnTimerTick()
    {
        if (_showSettingsSignal.WaitOne(0))
        {
            ShowSettings();
        }

        _engine.Tick();
        RefreshMenu();
    }

    private void ToggleProtection()
    {
        _config.ProtectionEnabled = !_engine.IsEnabled;
        _configStore.Save(_config);
        _engine.SetEnabled(_config.ProtectionEnabled);
        RefreshMenu();
    }

    private void TogglePause()
    {
        if (!_engine.IsEnabled || _engine.IsPaused)
        {
            ResumeProtection();
        }
        else
        {
            _engine.Pause(null);
        }

        RefreshMenu();
    }

    private void ResumeProtection()
    {
        _config.ProtectionEnabled = true;
        _configStore.Save(_config);
        _engine.Resume();
        RefreshMenu();
    }

    private void ToggleStartup()
    {
        var enabled = !StartupManager.IsEnabled();
        try
        {
            StartupManager.SetEnabled(enabled);
            _config.LaunchAtSignIn = enabled;
            _configStore.Save(_config);
        }
        catch (Exception exception)
        {
            _logger.Error("Could not update per-user startup", exception);
            MessageBox.Show(
                "Windows startup could not be updated. No other setting was changed.",
                AppIdentity.ProductName,
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }

        RefreshMenu();
    }

    private void ShowSettings()
    {
        if (_settingsForm is not null && !_settingsForm.IsDisposed)
        {
            _settingsForm.Activate();
            return;
        }

        var monitors = _monitorCatalog.GetMonitors();
        _settingsForm = new SettingsForm(_config, monitors, _monitorCatalog);
        var result = _settingsForm.ShowDialog();
        if (result == DialogResult.OK)
        {
            _config = _settingsForm.ResultConfig;
            _configStore.Save(_config);
            try
            {
                StartupManager.SetEnabled(_config.LaunchAtSignIn);
            }
            catch (Exception exception)
            {
                _logger.Error("Could not apply the startup setting", exception);
            }

            _engine.ApplyConfig(_config);
        }

        _settingsForm.Dispose();
        _settingsForm = null;
        RefreshMenu();
    }

    private void OnStatusChanged(GuardStatusInfo status)
    {
        var oldIcon = _trayIcon.Icon;
        _trayIcon.Icon = TrayIconFactory.Create(status.Status);
        oldIcon?.Dispose();
        _trayIcon.Text = status.Message.Length <= 63 ? status.Message : status.Message[..63];
        if (status.Status == GuardStatus.Error)
        {
            _trayIcon.BalloonTipTitle = AppIdentity.ProductName;
            _trayIcon.BalloonTipText = status.Message;
            _trayIcon.BalloonTipIcon = ToolTipIcon.Warning;
            _trayIcon.ShowBalloonTip(3000);
        }

        RefreshMenu();
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs eventArgs)
    {
        try
        {
            _dispatcher.BeginInvoke(_engine.OnDisplaySettingsChanged);
        }
        catch (InvalidOperationException)
        {
            // The application is shutting down.
        }
    }

    private void RefreshMenu()
    {
        _protectionItem.Checked = _engine.IsEnabled;
        _protectionItem.Text = _engine.IsEnabled ? "Protection on" : "Protection off";
        _resumeItem.Enabled = _engine.IsPaused;
        _startupItem.Checked = StartupManager.IsEnabled();
    }

    private void ExitSafely()
    {
        EmergencyRestore();
        ExitThread();
    }

    protected override void ExitThreadCore()
    {
        EmergencyRestore();
        base.ExitThreadCore();
    }

    protected override void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (disposing)
        {
            SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
            _timer.Stop();
            _timer.Dispose();
            _hookService.Dispose();
            _hotkeyWindow.Dispose();
            _showSettingsSignal.Dispose();
            _engine.Dispose();
            _dispatcher.Dispose();
            _settingsForm?.Dispose();
            _trayIcon.Visible = false;
            _trayIcon.Icon?.Dispose();
            _trayIcon.Dispose();
        }

        base.Dispose(disposing);
    }
}
