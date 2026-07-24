namespace ObsidianDisplayGuard.Tests;

public sealed class ObsidianIdleTrackerTests
{
    private static readonly DateTimeOffset Start = new(2026, 7, 24, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void OverlayIsRequestedOnlyAfterTheFullUnfocusedDelay()
    {
        var tracker = new ObsidianIdleTracker();
        var delay = TimeSpan.FromMinutes(5);

        Assert.False(tracker.Update(true, false, delay, Start));
        Assert.False(tracker.Update(true, false, delay, Start.AddMinutes(4).AddSeconds(59)));
        Assert.True(tracker.Update(true, false, delay, Start.AddMinutes(5)));
    }

    [Fact]
    public void ObsidianFocusRestartsTheDelay()
    {
        var tracker = new ObsidianIdleTracker();
        var delay = TimeSpan.FromMinutes(5);

        Assert.False(tracker.Update(true, false, delay, Start));
        Assert.False(tracker.Update(true, true, delay, Start.AddMinutes(4)));
        Assert.False(tracker.Update(true, false, delay, Start.AddMinutes(8)));
        Assert.True(tracker.Update(true, false, delay, Start.AddMinutes(13)));
    }

    [Fact]
    public void DisablingTheFeatureClearsPendingIdleTime()
    {
        var tracker = new ObsidianIdleTracker();
        var delay = TimeSpan.FromMinutes(5);

        Assert.False(tracker.Update(true, false, delay, Start));
        Assert.False(tracker.Update(false, false, delay, Start.AddMinutes(6)));
        Assert.False(tracker.Update(true, false, delay, Start.AddMinutes(7)));
    }

    [Fact]
    public void ExplicitResetRequiresAFullNewDelay()
    {
        var tracker = new ObsidianIdleTracker();
        var delay = TimeSpan.FromMinutes(5);

        Assert.False(tracker.Update(true, false, delay, Start));
        tracker.Reset();

        Assert.False(tracker.Update(true, false, delay, Start.AddMinutes(6)));
        Assert.True(tracker.Update(true, false, delay, Start.AddMinutes(11)));
    }
}
