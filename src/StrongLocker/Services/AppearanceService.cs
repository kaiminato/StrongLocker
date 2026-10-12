using System;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Drawing = System.Drawing;
using DrawingImaging = System.Drawing.Imaging;
using StrongLocker.Interop;
using StrongLocker.Models;

namespace StrongLocker.Services;

/// <summary>
/// Builds the per-monitor background brush shown by the lock overlay.
/// Blur is approximated cheaply by downscaling a screen capture and letting
/// bilinear upscaling soften it - fast and good enough for a lock screen.
/// </summary>
public sealed class AppearanceService
{
    /// <summary>Result for one monitor: the base brush, a black tint opacity (0..1),
    /// and a Gaussian blur radius applied by the overlay.</summary>
    public readonly record struct Background(Brush Brush, double TintOpacity, double BlurRadius);

    public Background Build(AppSettings settings, Drawing.Rectangle screenPx)
    {
        double tint = Math.Clamp(settings.TintPercent, 0, 100) / 100.0;

        switch (settings.BackgroundMode)
        {
            case BackgroundMode.SolidColor:
                return new Background(new SolidColorBrush(ParseColor(settings.SolidColor, "#1E1E24")), tint, 0);

            case BackgroundMode.Image:
                Brush imageBrush = TryLoadImageBrush(settings.ImagePath)
                                   ?? (Brush)new SolidColorBrush(ParseColor(settings.SolidColor, "#1E1E24"));
                return new Background(imageBrush, tint, 0);

            case BackgroundMode.DimDesktop:
                return new Background(CaptureBrush(screenPx, downscale: 1), tint, 0);

            case BackgroundMode.BlurDesktop:
            default:
                // Radical blur: heavy downscale for a frosted base, PLUS a strong
                // Gaussian blur applied by the overlay (GPU) to smooth it out.
                int strength = Math.Clamp(settings.BlurStrength, 1, 40);
                double radius = Math.Clamp(strength * 2.0, 12, 80);
                return new Background(CaptureBrush(screenPx, downscale: strength), tint, radius);
        }
    }

    private static Brush CaptureBrush(Drawing.Rectangle bounds, int downscale)
    {
        try
        {
            using var full = new Drawing.Bitmap(bounds.Width, bounds.Height, DrawingImaging.PixelFormat.Format32bppArgb);
            using (var g = Drawing.Graphics.FromImage(full))
            {
                g.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bounds.Size, Drawing.CopyPixelOperation.SourceCopy);
            }

            Drawing.Bitmap source = full;
            Drawing.Bitmap? scaled = null;
            if (downscale > 1)
            {
                int w = Math.Max(1, bounds.Width / downscale);
                int h = Math.Max(1, bounds.Height / downscale);
                scaled = new Drawing.Bitmap(w, h, DrawingImaging.PixelFormat.Format32bppArgb);
                using (var g = Drawing.Graphics.FromImage(scaled))
                {
                    g.InterpolationMode = Drawing.Drawing2D.InterpolationMode.HighQualityBilinear;
                    g.DrawImage(full, 0, 0, w, h);
                }
                source = scaled;
            }

            BitmapSource bmp = ToBitmapSource(source);
            scaled?.Dispose();
            var brush = new ImageBrush(bmp) { Stretch = Stretch.Fill };
            brush.Freeze();
            return brush;
        }
        catch
        {
            // If capture fails (e.g. secure desktop), fall back to a solid dark fill.
            var fallback = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x24));
            fallback.Freeze();
            return fallback;
        }
    }

    private static BitmapSource ToBitmapSource(Drawing.Bitmap bitmap)
    {
        IntPtr hBitmap = bitmap.GetHbitmap();
        try
        {
            var src = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                hBitmap, IntPtr.Zero, System.Windows.Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            src.Freeze();
            return src;
        }
        finally
        {
            NativeMethods.DeleteObject(hBitmap);
        }
    }

    private static ImageBrush? TryLoadImageBrush(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path)) return null;
        try
        {
            // Load via a stream so the file isn't locked and paths with spaces or
            // odd characters always resolve (no URI parsing surprises).
            var img = new BitmapImage();
            using (var stream = System.IO.File.OpenRead(path))
            {
                img.BeginInit();
                img.CacheOption = BitmapCacheOption.OnLoad;
                img.StreamSource = stream;
                img.EndInit();
            }
            img.Freeze();
            var brush = new ImageBrush(img) { Stretch = Stretch.UniformToFill };
            brush.Freeze();
            return brush;
        }
        catch
        {
            return null;
        }
    }

    public static Color ParseColor(string? value, string fallback)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(value))
                return (Color)ColorConverter.ConvertFromString(value);
        }
        catch { }
        return (Color)ColorConverter.ConvertFromString(fallback);
    }
}
