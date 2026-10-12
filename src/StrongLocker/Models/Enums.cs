namespace StrongLocker.Models;

/// <summary>How the lock overlay paints its background.</summary>
public enum BackgroundMode
{
    /// <summary>Capture the desktop and blur it.</summary>
    BlurDesktop,
    /// <summary>Capture the desktop and darken it (no blur).</summary>
    DimDesktop,
    /// <summary>Fill with a single solid color.</summary>
    SolidColor,
    /// <summary>Stretch a user-chosen image.</summary>
    Image
}

/// <summary>Hotkey modifier flags, matching Win32 MOD_* values used by RegisterHotKey.</summary>
[System.Flags]
public enum HotkeyModifiers
{
    None = 0,
    Alt = 0x0001,
    Control = 0x0002,
    Shift = 0x0004,
    Win = 0x0008
}
