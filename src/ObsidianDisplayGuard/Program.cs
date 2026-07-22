namespace ObsidianDisplayGuard;

static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        using var mutex = new Mutex(true, AppIdentity.MutexName, out var ownsMutex);
        if (!ownsMutex)
        {
            SingleInstanceSignal.SignalExistingInstance();
            return 0;
        }

        using var logger = new FileLogger(AppPaths.LogPath);
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

        GuardApplicationContext? context = null;
        Application.ThreadException += (_, eventArgs) =>
        {
            logger.Error("Unhandled UI exception", eventArgs.Exception);
            context?.EmergencyRestore();
        };
        AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
        {
            logger.Error("Unhandled process exception", eventArgs.ExceptionObject as Exception);
            context?.EmergencyRestore();
        };

        try
        {
            var options = LaunchOptions.Parse(args);
            if (options.RestoreOnly)
            {
                RecoveryManager.RestoreSavedWindow(logger);
                return 0;
            }

            context = new GuardApplicationContext(options, logger);
            Application.Run(context);
            return 0;
        }
        catch (Exception exception)
        {
            logger.Error("Fatal startup exception", exception);
            context?.EmergencyRestore();
            MessageBox.Show(
                "Obsidian Display Guard could not start safely. Protection was not enabled.\n\n" +
                "See the local log for details:\n" + AppPaths.LogPath,
                AppIdentity.ProductName,
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return 1;
        }
        finally
        {
            context?.Dispose();
        }
    }
}
