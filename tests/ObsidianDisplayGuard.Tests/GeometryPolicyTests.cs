using System.Drawing;

namespace ObsidianDisplayGuard.Tests;

public sealed class GeometryPolicyTests
{
    private static readonly Rectangle Protected = new(-1920, 357, 1920, 1080);

    [Fact]
    public void WindowCenteredOnProtectedMonitorIsEvicted()
    {
        Assert.True(GeometryPolicy.ShouldEvict(new Rectangle(-1500, 500, 900, 600), Protected));
    }

    [Fact]
    public void TinyShadowOverlapFromPrimaryMonitorIsIgnored()
    {
        Assert.False(GeometryPolicy.ShouldEvict(new Rectangle(-8, -8, 2576, 1408), Protected));
    }

    [Fact]
    public void SignificantPartialOverlapIsEvicted()
    {
        Assert.True(GeometryPolicy.ShouldEvict(new Rectangle(-400, 600, 800, 600), Protected));
    }

    [Fact]
    public void FitInsideHandlesNegativeCoordinatesAndOversizedWindows()
    {
        var workArea = new Rectangle(-1920, 357, 1920, 1032);
        var result = GeometryPolicy.FitInside(new Rectangle(-3000, -500, 3000, 2000), workArea);

        Assert.Equal(workArea, result);
    }

    [Fact]
    public void CenterInsidePreservesUsefulMinimumSize()
    {
        var workArea = new Rectangle(0, 0, 2560, 1392);
        var result = GeometryPolicy.CenterInside(new Rectangle(-1500, 500, 200, 100), workArea);

        Assert.Equal(new Size(640, 480), result.Size);
        Assert.True(workArea.Contains(result));
    }
}
