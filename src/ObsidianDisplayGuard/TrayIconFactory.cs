using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace ObsidianDisplayGuard;

internal static class TrayIconFactory
{
    internal static Icon Create(GuardStatus status)
    {
        var color = status switch
        {
            GuardStatus.Protected => Color.FromArgb(46, 204, 113),
            GuardStatus.Paused => Color.FromArgb(241, 196, 15),
            _ => Color.FromArgb(231, 76, 60)
        };

        using var bitmap = new Bitmap(32, 32);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.Clear(Color.Transparent);
            using var background = new SolidBrush(Color.FromArgb(34, 36, 45));
            using var accent = new SolidBrush(color);
            graphics.FillEllipse(background, 1, 1, 30, 30);
            graphics.FillEllipse(accent, 7, 7, 18, 18);
            using var pen = new Pen(Color.White, 2.2F);
            graphics.DrawRectangle(pen, 10, 10, 12, 9);
        }

        var handle = bitmap.GetHicon();
        try
        {
            using var temporary = Icon.FromHandle(handle);
            return (Icon)temporary.Clone();
        }
        finally
        {
            NativeMethods.DestroyIcon(handle);
        }
    }
}
