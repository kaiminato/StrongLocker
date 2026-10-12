using System;
using System.Windows.Media;
using Drawing = System.Drawing;

namespace StrongLocker.Services;

/// <summary>Everything one overlay window needs to render and (on the primary) unlock.</summary>
public sealed class OverlayOptions
{
    public required Drawing.Rectangle ScreenBounds { get; init; } // device pixels
    public required Brush Background { get; init; }
    public double TintOpacity { get; init; }
    /// <summary>Gaussian blur radius applied to the background (0 = none).</summary>
    public double BlurRadius { get; init; }
    public bool IsPrimary { get; init; }
    public bool ShowClock { get; init; }
    public bool Clock24Hour { get; init; }

    /// <summary>Preview mode: no password; any key or click dismisses it.</summary>
    public bool IsPreview { get; init; }

    /// <summary>Password length, so the overlay tests the last N keystrokes.</summary>
    public int PasswordLength { get; init; }

    /// <summary>Returns true if the candidate matches the password. Primary overlay only.</summary>
    public Func<string, bool>? VerifyPassword { get; init; }

    /// <summary>Raised when the overlay should be dismissed (unlocked or preview exit).</summary>
    public Action? Unlocked { get; init; }
}
