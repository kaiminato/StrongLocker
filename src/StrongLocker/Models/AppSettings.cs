using System;

namespace StrongLocker.Models;

/// <summary>
/// All persisted configuration. Serialized as JSON to
/// %AppData%\StrongLocker\settings.json. Contains only a salted password
/// <b>hash</b> - never the password itself.
/// </summary>
public sealed class AppSettings
{
    // ---- Password (PBKDF2) ----
    public string? PasswordSalt { get; set; }   // base64, 16 bytes
    public string? PasswordHash { get; set; }   // base64, 32 bytes
    public int PasswordIterations { get; set; } = 100_000;
    /// <summary>Character length of the password, so the lock screen knows how
    /// many trailing keystrokes to test against the hash.</summary>
    public int PasswordLength { get; set; }

    /// <summary>True once a password has been set up.</summary>
    public bool HasPassword =>
        !string.IsNullOrEmpty(PasswordSalt) && !string.IsNullOrEmpty(PasswordHash);

    // ---- Lock hotkey ----
    public HotkeyModifiers HotkeyModifiers { get; set; } =
        Models.HotkeyModifiers.Control | Models.HotkeyModifiers.Alt;
    /// <summary>Virtual key code. Default 'L' (0x4C).</summary>
    public int HotkeyKey { get; set; } = 0x4C;

    // ---- Auto-lock on idle ----
    public bool IdleLockEnabled { get; set; } = false;
    public int IdleMinutes { get; set; } = 15;

    // ---- Appearance ----
    public BackgroundMode BackgroundMode { get; set; } = BackgroundMode.BlurDesktop;
    /// <summary>Blur strength for BlurDesktop mode, 1 (subtle) .. 40 (heavy).</summary>
    public int BlurStrength { get; set; } = 10;
    /// <summary>Solid color as #AARRGGBB or #RRGGBB.</summary>
    public string SolidColor { get; set; } = "#1E1E24";
    public string? ImagePath { get; set; }
    /// <summary>Darkening tint over the background, 0..100 (% black).</summary>
    public int TintPercent { get; set; } = 35;
    public bool ShowClock { get; set; } = true;
    public bool Clock24Hour { get; set; } = false;

    // ---- Behavior ----
    /// <summary>Launch StrongLocker automatically at Windows login (HKCU Run key).</summary>
    public bool RunOnStartup { get; set; } = false;
    /// <summary>Lock immediately when StrongLocker itself starts.</summary>
    public bool LockOnStartup { get; set; } = false;
}
