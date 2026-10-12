using System;
using Drawing = System.Drawing;
using Drawing2D = System.Drawing.Drawing2D;
using Forms = System.Windows.Forms;

namespace StrongLocker.Services;

/// <summary>System-tray presence and menu. Uses the WinForms NotifyIcon (no extra deps).</summary>
public sealed class TrayIconService : IDisposable
{
    private readonly Forms.NotifyIcon _icon;
    private Drawing.Icon? _generatedIcon;

    public event Action? LockRequested;
    public event Action? SettingsRequested;
    public event Action? QuitRequested;

    public TrayIconService()
    {
        _generatedIcon = LoadAppIcon() ?? CreateLockIcon();

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Lock now", null, (_, _) => LockRequested?.Invoke());
        menu.Items.Add("Settings…", null, (_, _) => SettingsRequested?.Invoke());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Quit", null, (_, _) => QuitRequested?.Invoke());

        _icon = new Forms.NotifyIcon
        {
            Icon = _generatedIcon,
            Text = "StrongLocker",
            Visible = true,
            ContextMenuStrip = menu
        };
        // Single left-click locks immediately; right-click opens the menu.
        _icon.MouseClick += (_, e) =>
        {
            if (e.Button == Forms.MouseButtons.Left) LockRequested?.Invoke();
        };
    }

    public void ShowBalloon(string title, string text)
    {
        try
        {
            _icon.BalloonTipTitle = title;
            _icon.BalloonTipText = text;
            _icon.ShowBalloonTip(3000);
        }
        catch { }
    }

    private static Drawing.Icon? LoadAppIcon()
    {
        try
        {
            string? exe = Environment.ProcessPath;
            if (exe is not null)
            {
                var icon = Drawing.Icon.ExtractAssociatedIcon(exe);
                if (icon is not null) return (Drawing.Icon)icon.Clone();
            }
        }
        catch { }
        return null;
    }

    private static Drawing.Icon CreateLockIcon()
    {
        using var bmp = new Drawing.Bitmap(32, 32, Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var g = Drawing.Graphics.FromImage(bmp))
        {
            g.SmoothingMode = Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Drawing.Color.Transparent);

            using var shackle = new Drawing.Pen(Drawing.Color.FromArgb(230, 230, 235), 3f);
            g.DrawArc(shackle, 10, 6, 12, 14, 180, 180); // top loop

            using var body = new Drawing.SolidBrush(Drawing.Color.FromArgb(76, 111, 255));
            g.FillRectangle(body, 8, 15, 16, 12);

            using var keyhole = new Drawing.SolidBrush(Drawing.Color.FromArgb(20, 20, 26));
            g.FillEllipse(keyhole, 14, 18, 4, 4);
        }

        IntPtr hIcon = bmp.GetHicon();
        return (Drawing.Icon)Drawing.Icon.FromHandle(hIcon).Clone();
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
        _generatedIcon?.Dispose();
        _generatedIcon = null;
    }
}
