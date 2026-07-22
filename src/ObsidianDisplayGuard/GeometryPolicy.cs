using System.Drawing;

namespace ObsidianDisplayGuard;

public static class GeometryPolicy
{
    public static bool ShouldEvict(Rectangle window, Rectangle protectedBounds, double minimumOverlapRatio = 0.20)
    {
        if (window.Width <= 0 || window.Height <= 0 || !window.IntersectsWith(protectedBounds))
        {
            return false;
        }

        var center = new Point(window.Left + window.Width / 2, window.Top + window.Height / 2);
        if (protectedBounds.Contains(center))
        {
            return true;
        }

        var intersection = Rectangle.Intersect(window, protectedBounds);
        var windowArea = (double)window.Width * window.Height;
        var overlapArea = (double)intersection.Width * intersection.Height;
        return windowArea > 0 && overlapArea / windowArea >= minimumOverlapRatio;
    }

    public static Rectangle FitInside(Rectangle desired, Rectangle workingArea)
    {
        var width = Math.Min(Math.Max(desired.Width, 120), workingArea.Width);
        var height = Math.Min(Math.Max(desired.Height, 80), workingArea.Height);
        var x = Math.Clamp(desired.X, workingArea.Left, workingArea.Right - width);
        var y = Math.Clamp(desired.Y, workingArea.Top, workingArea.Bottom - height);
        return new Rectangle(x, y, width, height);
    }

    public static Rectangle CenterInside(Rectangle current, Rectangle workingArea)
    {
        var width = Math.Min(Math.Max(current.Width, 640), workingArea.Width);
        var height = Math.Min(Math.Max(current.Height, 480), workingArea.Height);
        return new Rectangle(
            workingArea.Left + (workingArea.Width - width) / 2,
            workingArea.Top + (workingArea.Height - height) / 2,
            width,
            height);
    }
}
