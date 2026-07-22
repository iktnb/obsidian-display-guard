namespace ObsidianDisplayGuard.Tests;

public sealed class RecoveryManagerTests
{
    [Fact]
    public void RecoveryRequiresPidStartTimeAndHandleToMatch()
    {
        var state = new WindowSessionState
        {
            ProcessId = 100,
            ProcessStartTimeUtcTicks = 200,
            WindowHandle = 300
        };

        Assert.True(RecoveryManager.MatchesRunningWindow(state, 100, 200, 300));
        Assert.False(RecoveryManager.MatchesRunningWindow(state, 101, 200, 300));
        Assert.False(RecoveryManager.MatchesRunningWindow(state, 100, 201, 300));
        Assert.False(RecoveryManager.MatchesRunningWindow(state, 100, 200, 301));
    }
}
