using System.Drawing;

namespace ObsidianDisplayGuard.Tests;

public sealed class WindowPolicyTests
{
    [Fact]
    public void NormalTopLevelWindowIsEligible()
    {
        Assert.True(WindowPolicy.IsEligible(CreateWindow(), ownProcessId: 999, isRootWindow: true));
    }

    [Theory]
    [InlineData("Progman")]
    [InlineData("WorkerW")]
    [InlineData("Shell_TrayWnd")]
    [InlineData("Shell_SecondaryTrayWnd")]
    public void ShellWindowsAreIgnored(string className)
    {
        Assert.False(WindowPolicy.IsEligible(CreateWindow(className: className), 999, true));
    }

    [Fact]
    public void CloakedVirtualDesktopWindowIsIgnored()
    {
        Assert.False(WindowPolicy.IsEligible(CreateWindow(cloaked: true), 999, true));
    }

    [Fact]
    public void ToolWindowWithoutAppWindowStyleIsIgnored()
    {
        Assert.False(WindowPolicy.IsEligible(CreateWindow(extendedStyle: 0x80), 999, true));
    }

    [Fact]
    public void OwnWindowIsIgnored()
    {
        Assert.False(WindowPolicy.IsEligible(CreateWindow(processId: 42), 42, true));
    }

    private static WindowSnapshot CreateWindow(
        int processId = 42,
        string className = "TestWindow",
        bool cloaked = false,
        long extendedStyle = 0) => new(
            123,
            processId,
            @"C:\Apps\Test.exe",
            "Test",
            className,
            new Rectangle(20, 20, 800, 600),
            true,
            false,
            false,
            cloaked,
            0,
            extendedStyle,
            0);
}
