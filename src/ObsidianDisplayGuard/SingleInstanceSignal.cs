namespace ObsidianDisplayGuard;

internal static class SingleInstanceSignal
{
    internal static void SignalExistingInstance()
    {
        try
        {
            using var signal = EventWaitHandle.OpenExisting(AppIdentity.ShowSettingsEventName);
            signal.Set();
        }
        catch (WaitHandleCannotBeOpenedException)
        {
            // The first process is still starting. The second process exits safely.
        }
    }
}
