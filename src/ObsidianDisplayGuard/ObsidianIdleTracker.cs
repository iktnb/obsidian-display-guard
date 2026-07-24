namespace ObsidianDisplayGuard;

public sealed class ObsidianIdleTracker
{
    private DateTimeOffset? _unfocusedSince;

    public bool Update(
        bool enabled,
        bool isObsidianFocused,
        TimeSpan delay,
        DateTimeOffset now)
    {
        if (!enabled || isObsidianFocused)
        {
            Reset();
            return false;
        }

        _unfocusedSince ??= now;
        return now - _unfocusedSince.Value >= delay;
    }

    public void Reset()
    {
        _unfocusedSince = null;
    }
}
