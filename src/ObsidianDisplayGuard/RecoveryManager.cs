using System.Diagnostics;
using System.Text.Json;

namespace ObsidianDisplayGuard;

public static class RecoveryManager
{
    public static void Save(WindowSessionState state)
    {
        Directory.CreateDirectory(AppPaths.DataDirectory);
        var temporaryPath = AppPaths.SessionPath + ".tmp";
        File.WriteAllText(
            temporaryPath,
            JsonSerializer.Serialize(state, ConfigStore.SerializerOptions));
        File.Move(temporaryPath, AppPaths.SessionPath, true);
    }

    public static WindowSessionState? Load(FileLogger logger)
    {
        if (!File.Exists(AppPaths.SessionPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<WindowSessionState>(
                File.ReadAllText(AppPaths.SessionPath),
                ConfigStore.SerializerOptions);
        }
        catch (Exception exception)
        {
            logger.Error("Could not read the recovery session", exception);
            return null;
        }
    }

    public static bool RestoreSavedWindow(FileLogger logger)
    {
        var state = Load(logger);
        if (state is null)
        {
            return true;
        }

        try
        {
            var handle = (nint)state.WindowHandle;
            if (!NativeMethods.IsWindow(handle))
            {
                Clear();
                return true;
            }

            NativeMethods.GetWindowThreadProcessId(handle, out var processId);
            using var process = Process.GetProcessById((int)processId);
            if (!MatchesRunningWindow(
                    state,
                    (int)processId,
                    process.StartTime.ToUniversalTime().Ticks,
                    handle.ToInt64()))
            {
                Clear();
                return true;
            }

            NativeMethods.SetWindowLongPtr(handle, NativeMethods.GwlStyle, (nint)state.Style);
            NativeMethods.SetWindowLongPtr(handle, NativeMethods.GwlExStyle, (nint)state.ExtendedStyle);

            var placement = NativeMethods.WindowPlacement.Create();
            placement.Flags = state.Placement.Flags;
            placement.ShowCommand = 1;
            placement.NormalPosition = NativeMethods.Rect.FromRectangle(state.Placement.NormalPosition);
            NativeMethods.SetWindowPlacement(handle, ref placement);

            var originalBounds = state.CurrentBounds.Width > 0 && state.CurrentBounds.Height > 0
                ? state.CurrentBounds
                : state.Placement.NormalPosition;
            NativeMethods.ShowWindowAsync(handle, NativeMethods.SwRestore);
            NativeMethods.SetWindowPos(
                handle,
                state.WasTopmost ? NativeMethods.HwndTopmost : NativeMethods.HwndNotTopmost,
                originalBounds.X,
                originalBounds.Y,
                originalBounds.Width,
                originalBounds.Height,
                NativeMethods.SwpFrameChanged |
                NativeMethods.SwpNoActivate |
                NativeMethods.SwpNoOwnerZOrder |
                NativeMethods.SwpShowWindow);

            if (state.WasMaximized)
            {
                NativeMethods.ShowWindowAsync(handle, NativeMethods.SwMaximize);
            }
            else if (state.WasMinimized)
            {
                NativeMethods.ShowWindowAsync(handle, 6);
            }

            Clear();
            logger.Info("Recovered the original Obsidian window state");
            return true;
        }
        catch (Exception exception)
        {
            logger.Error("Could not recover the original Obsidian window state", exception);
            return false;
        }
    }

    public static void Clear()
    {
        try
        {
            File.Delete(AppPaths.SessionPath);
        }
        catch
        {
            // A stale recovery file is safe; it is validated by PID and process start time.
        }
    }

    public static bool MatchesRunningWindow(
        WindowSessionState state,
        int processId,
        long processStartTimeUtcTicks,
        long windowHandle) =>
        state.ProcessId == processId &&
        state.ProcessStartTimeUtcTicks == processStartTimeUtcTicks &&
        state.WindowHandle == windowHandle;
}
