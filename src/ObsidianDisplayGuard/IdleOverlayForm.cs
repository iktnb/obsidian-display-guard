using System.Drawing.Drawing2D;

namespace ObsidianDisplayGuard;

internal sealed class IdleOverlayForm : Form
{
    private readonly Action _dismissed;
    private readonly int _delayMinutes;
    private Bitmap? _blurredBackground;
    private bool _dismissStarted;

    internal IdleOverlayForm(MonitorDescriptor monitor, int delayMinutes, Action dismissed)
    {
        _dismissed = dismissed;
        _delayMinutes = Math.Clamp(delayMinutes, 1, 120);

        Text = "Obsidian idle screen";
        Name = nameof(IdleOverlayForm);
        AccessibleName = "Obsidian idle screen";
        AccessibleDescription = "Click to return to Obsidian";
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Bounds = monitor.Bounds;
        TopMost = true;
        ShowInTaskbar = false;
        BackColor = Color.FromArgb(24, 25, 28);
        Cursor = Cursors.Hand;
        KeyPreview = true;
        DoubleBuffered = true;

        try
        {
            _blurredBackground = BlurredScreenCapture.Capture(monitor.Bounds);
        }
        catch
        {
            _blurredBackground = null;
        }

        Click += (_, _) => Dismiss();
        KeyDown += (_, eventArgs) =>
        {
            if (eventArgs.KeyCode is Keys.Enter or Keys.Space or Keys.Escape)
            {
                eventArgs.Handled = true;
                Dismiss();
            }
        };
        Shown += (_, _) =>
        {
            BringToFront();
            Activate();
        };
    }

    protected override void OnPaint(PaintEventArgs eventArgs)
    {
        base.OnPaint(eventArgs);

        var graphics = eventArgs.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        if (_blurredBackground is not null)
        {
            graphics.DrawImage(_blurredBackground, ClientRectangle);
        }

        using (var tint = new SolidBrush(Color.FromArgb(132, 20, 21, 24)))
        {
            graphics.FillRectangle(tint, ClientRectangle);
        }

        DrawCenteredContent(graphics);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _blurredBackground?.Dispose();
            _blurredBackground = null;
        }

        base.Dispose(disposing);
    }

    private void DrawCenteredContent(Graphics graphics)
    {
        const int iconSize = 92;
        const int gapAfterIcon = 28;
        const int gapAfterTitle = 12;

        using var titleFont = new Font("Segoe UI Semibold", 24F, FontStyle.Regular, GraphicsUnit.Point);
        using var detailFont = new Font("Segoe UI", 11.5F, FontStyle.Regular, GraphicsUnit.Point);

        const string title = "Клікни, щоб повернутися в Obsidian";
        var detail = $"Екран приглушено після {_delayMinutes} хв без фокуса";

        var titleSize = graphics.MeasureString(title, titleFont);
        var detailSize = graphics.MeasureString(detail, detailFont);
        var contentHeight = iconSize + gapAfterIcon + titleSize.Height + gapAfterTitle + detailSize.Height;
        var top = (ClientSize.Height - contentHeight) / 2F;

        var iconBounds = new RectangleF(
            (ClientSize.Width - iconSize) / 2F,
            top,
            iconSize,
            iconSize);
        DrawClickIcon(graphics, iconBounds);

        using var titleBrush = new SolidBrush(Color.FromArgb(246, 246, 248));
        using var detailBrush = new SolidBrush(Color.FromArgb(188, 190, 198));
        using var centered = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };

        var titleTop = iconBounds.Bottom + gapAfterIcon;
        graphics.DrawString(
            title,
            titleFont,
            titleBrush,
            new RectangleF(40, titleTop, ClientSize.Width - 80, titleSize.Height + 4),
            centered);
        graphics.DrawString(
            detail,
            detailFont,
            detailBrush,
            new RectangleF(
                40,
                titleTop + titleSize.Height + gapAfterTitle,
                ClientSize.Width - 80,
                detailSize.Height + 4),
            centered);
    }

    private static void DrawClickIcon(Graphics graphics, RectangleF bounds)
    {
        using var haloBrush = new SolidBrush(Color.FromArgb(46, 166, 137, 255));
        using var haloPen = new Pen(Color.FromArgb(150, 186, 164, 255), 1.5F);
        graphics.FillEllipse(haloBrush, bounds);
        graphics.DrawEllipse(haloPen, bounds);

        var center = new PointF(bounds.Left + (bounds.Width * 0.5F), bounds.Top + (bounds.Height * 0.48F));
        using var rayPen = new Pen(Color.FromArgb(225, 242, 238, 255), 2.4F)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };
        graphics.DrawLine(rayPen, center.X, center.Y - 29, center.X, center.Y - 20);
        graphics.DrawLine(rayPen, center.X - 27, center.Y - 15, center.X - 20, center.Y - 10);
        graphics.DrawLine(rayPen, center.X + 27, center.Y - 15, center.X + 20, center.Y - 10);

        var pointer = new[]
        {
            new PointF(center.X - 9, center.Y - 13),
            new PointF(center.X + 20, center.Y + 15),
            new PointF(center.X + 7, center.Y + 17),
            new PointF(center.X + 14, center.Y + 31),
            new PointF(center.X + 5, center.Y + 35),
            new PointF(center.X - 2, center.Y + 21),
            new PointF(center.X - 11, center.Y + 30)
        };
        using var pointerPath = new GraphicsPath();
        pointerPath.AddPolygon(pointer);
        using var pointerBrush = new SolidBrush(Color.FromArgb(246, 246, 250));
        using var pointerPen = new Pen(Color.FromArgb(215, 172, 146, 255), 1.4F);
        graphics.FillPath(pointerBrush, pointerPath);
        graphics.DrawPath(pointerPen, pointerPath);
    }

    private void Dismiss()
    {
        if (_dismissStarted)
        {
            return;
        }

        _dismissStarted = true;
        Close();
        _dismissed();
    }
}
