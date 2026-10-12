using System;
using System.Collections.Generic;
using StrongLocker.Models;
using StrongLocker.Views;
using Forms = System.Windows.Forms;

namespace StrongLocker.Services;

/// <summary>
/// Owns the lock lifecycle: paints one overlay per monitor, turns on input
/// blocking, and tears everything down once the typed password matches.
/// Also drives the password-free appearance preview.
/// </summary>
public sealed class LockController
{
    private readonly Func<AppSettings> _settings;
    private readonly AppearanceService _appearance;
    private readonly InputBlocker _input;
    private readonly List<OverlayWindow> _overlays = new();
    private readonly List<OverlayWindow> _previewOverlays = new();

    public bool IsLocked { get; private set; }
    public event Action? LockStateChanged;

    public LockController(Func<AppSettings> settings, AppearanceService appearance, InputBlocker input)
    {
        _settings = settings;
        _appearance = appearance;
        _input = input;
    }

    public void Lock()
    {
        if (IsLocked || _previewOverlays.Count > 0) return;
        var s = _settings();
        if (!s.HasPassword) return; // never lock without a way to unlock

        foreach (var opts in BuildPlans(s, preview: false))
        {
            var w = new OverlayWindow(opts);
            _overlays.Add(w);
            w.Show();
        }

        _input.Activate();
        IsLocked = true;
        LockStateChanged?.Invoke();
    }

    public void Unlock()
    {
        if (!IsLocked) return;
        _input.Deactivate();
        foreach (var w in _overlays) w.ForceClose();
        _overlays.Clear();
        IsLocked = false;
        LockStateChanged?.Invoke();
    }

    /// <summary>
    /// Shows the lock appearance using the supplied (possibly unsaved) settings.
    /// No password and no input blocking - any key or click exits.
    /// </summary>
    public void Preview(AppSettings snapshot)
    {
        if (IsLocked || _previewOverlays.Count > 0) return;

        foreach (var opts in BuildPlans(snapshot, preview: true))
        {
            var w = new OverlayWindow(opts);
            _previewOverlays.Add(w);
            w.Show();
        }
    }

    private void ClosePreview()
    {
        foreach (var w in _previewOverlays) w.ForceClose();
        _previewOverlays.Clear();
    }

    private List<OverlayOptions> BuildPlans(AppSettings s, bool preview)
    {
        // Capture every monitor BEFORE any overlay is shown, otherwise a later
        // capture would grab an overlay that's already on screen.
        var plans = new List<OverlayOptions>();
        foreach (var screen in Forms.Screen.AllScreens)
        {
            bool isPrimary = screen.Primary;
            var bg = _appearance.Build(s, screen.Bounds);
            plans.Add(new OverlayOptions
            {
                ScreenBounds = screen.Bounds,
                Background = bg.Brush,
                TintOpacity = bg.TintOpacity,
                BlurRadius = bg.BlurRadius,
                IsPrimary = isPrimary,
                ShowClock = s.ShowClock,
                Clock24Hour = s.Clock24Hour,
                IsPreview = preview,
                PasswordLength = s.PasswordLength,
                VerifyPassword = (!preview && isPrimary) ? (pw => PasswordStore.Verify(s, pw)) : null,
                Unlocked = preview ? ClosePreview : (isPrimary ? Unlock : null)
            });
        }
        return plans;
    }
}
