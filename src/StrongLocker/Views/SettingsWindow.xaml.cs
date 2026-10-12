using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using StrongLocker.Models;
using StrongLocker.Services;

namespace StrongLocker.Views;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _settings;
    private readonly SettingsStore _store;
    private readonly Action<AppSettings> _preview;
    private readonly bool _firstRun;

    private HotkeyModifiers _capturedMods;
    private int _capturedVk;
    private bool _capturingHotkey;

    /// <summary>Raised after a successful save so the host can re-apply services.</summary>
    public event Action? Saved;

    public SettingsWindow(AppSettings settings, SettingsStore store, Action<AppSettings> preview)
    {
        InitializeComponent();
        _settings = settings;
        _store = store;
        _preview = preview;
        _firstRun = !settings.HasPassword;

        TitleBar.MouseLeftButtonDown += (_, _) => DragMove();
        BgCombo.ItemsSource = Enum.GetValues(typeof(BackgroundMode));
        BlurSlider.ValueChanged += (_, _) => BlurValue.Text = $"{(int)BlurSlider.Value}";
        TintSlider.ValueChanged += (_, _) => TintValue.Text = $"{(int)TintSlider.Value}%";

        LoadFromSettings();
    }

    private void LoadFromSettings()
    {
        PwHint.Text = _firstRun
            ? "Set a password to enable locking."
            : "Leave blank to keep your current password.";
        if (_firstRun)
        {
            CurrentPwLabel.Visibility = Visibility.Collapsed;
            CurrentPw.Visibility = Visibility.Collapsed;
        }

        _capturedMods = _settings.HotkeyModifiers;
        _capturedVk = _settings.HotkeyKey;
        HotkeyText.Text = FormatHotkey(_capturedMods, _capturedVk);

        IdleEnabled.IsChecked = _settings.IdleLockEnabled;
        IdleMinutes.Text = _settings.IdleMinutes.ToString();

        BgCombo.SelectedItem = _settings.BackgroundMode;
        BlurSlider.Value = _settings.BlurStrength;
        BlurValue.Text = $"{_settings.BlurStrength}";
        SolidColor.Text = _settings.SolidColor;
        ImagePath.Text = _settings.ImagePath ?? string.Empty;
        TintSlider.Value = _settings.TintPercent;
        TintValue.Text = $"{_settings.TintPercent}%";
        ShowClock.IsChecked = _settings.ShowClock;
        Clock24.IsChecked = _settings.Clock24Hour;

        RunOnStartup.IsChecked = _settings.RunOnStartup;
        LockOnStartup.IsChecked = _settings.LockOnStartup;

        UpdateAppearanceRows();
    }

    // ---- Appearance: show only the rows that apply (#8) ----

    private void BgCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        => UpdateAppearanceRows();

    private void UpdateAppearanceRows()
    {
        if (BlurRow is null) return; // not loaded yet
        var mode = BgCombo.SelectedItem is BackgroundMode m ? m : BackgroundMode.BlurDesktop;
        BlurRow.Visibility = mode == BackgroundMode.BlurDesktop ? Visibility.Visible : Visibility.Collapsed;
        ColorRow.Visibility = mode == BackgroundMode.SolidColor ? Visibility.Visible : Visibility.Collapsed;
        ImageRow.Visibility = mode == BackgroundMode.Image ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SolidColor_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (ColorSwatch is null) return;
        ColorSwatch.Color = AppearanceService.ParseColor(SolidColor.Text, "#1E1E24");
    }

    // ---- Hotkey capture (#6) ----

    private void HotkeyBox_GotFocus(object sender, RoutedEventArgs e)
    {
        _capturingHotkey = true;
        HotkeyText.Text = "Press your shortcut…";
    }

    private void HotkeyBox_LostFocus(object sender, RoutedEventArgs e)
    {
        _capturingHotkey = false;
        HotkeyText.Text = FormatHotkey(_capturedMods, _capturedVk);
    }

    private void HotkeyBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!_capturingHotkey) return;
        e.Handled = true;

        Key key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (IsModifierKey(key)) return; // wait for the non-modifier key

        HotkeyModifiers mods = HotkeyModifiers.None;
        var m = Keyboard.Modifiers;
        if (m.HasFlag(ModifierKeys.Control)) mods |= HotkeyModifiers.Control;
        if (m.HasFlag(ModifierKeys.Alt)) mods |= HotkeyModifiers.Alt;
        if (m.HasFlag(ModifierKeys.Shift)) mods |= HotkeyModifiers.Shift;
        if (m.HasFlag(ModifierKeys.Windows)) mods |= HotkeyModifiers.Win;

        _capturedMods = mods;
        _capturedVk = KeyInterop.VirtualKeyFromKey(key);
        HotkeyText.Text = FormatHotkey(_capturedMods, _capturedVk);
    }

    private static bool IsModifierKey(Key k) => k is Key.LeftCtrl or Key.RightCtrl
        or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift
        or Key.LWin or Key.RWin or Key.System;

    private static string FormatHotkey(HotkeyModifiers mods, int vk)
    {
        var parts = new System.Collections.Generic.List<string>();
        if (mods.HasFlag(HotkeyModifiers.Control)) parts.Add("Ctrl");
        if (mods.HasFlag(HotkeyModifiers.Alt)) parts.Add("Alt");
        if (mods.HasFlag(HotkeyModifiers.Shift)) parts.Add("Shift");
        if (mods.HasFlag(HotkeyModifiers.Win)) parts.Add("Win");
        parts.Add(KeyName(vk));
        return string.Join("+", parts);
    }

    private static string KeyName(int vk)
    {
        if (vk >= 0x41 && vk <= 0x5A) return ((char)vk).ToString();        // A-Z
        if (vk >= 0x30 && vk <= 0x39) return ((char)vk).ToString();        // 0-9
        if (vk >= 0x70 && vk <= 0x7B) return "F" + (vk - 0x6F);            // F1-F12
        try { return KeyInterop.KeyFromVirtualKey(vk).ToString(); } catch { return "?"; }
    }

    // ---- Preview (#12): no password; any key/click exits ----

    private void Preview_Click(object sender, RoutedEventArgs e) => _preview(BuildAppearanceSnapshot());

    private AppSettings BuildAppearanceSnapshot() => new()
    {
        BackgroundMode = BgCombo.SelectedItem is BackgroundMode m ? m : BackgroundMode.BlurDesktop,
        BlurStrength = (int)BlurSlider.Value,
        SolidColor = SolidColor.Text,
        ImagePath = string.IsNullOrWhiteSpace(ImagePath.Text) ? null : ImagePath.Text,
        TintPercent = (int)TintSlider.Value,
        ShowClock = ShowClock.IsChecked == true,
        Clock24Hour = Clock24.IsChecked == true
    };

    // ---- Save / Cancel ----

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        StatusText.Text = string.Empty;

        string newPw = NewPw.Password;
        if (_firstRun)
        {
            if (string.IsNullOrEmpty(newPw)) { Fail("Please set a password."); return; }
            if (newPw != ConfirmPw.Password) { Fail("Passwords do not match."); return; }
            PasswordStore.SetPassword(_settings, newPw);
        }
        else if (!string.IsNullOrEmpty(newPw))
        {
            if (!PasswordStore.Verify(_settings, CurrentPw.Password)) { Fail("Current password is incorrect."); return; }
            if (newPw != ConfirmPw.Password) { Fail("New passwords do not match."); return; }
            PasswordStore.SetPassword(_settings, newPw);
        }

        if (_capturedVk == 0) { Fail("Please set a lock hotkey."); return; }
        _settings.HotkeyModifiers = _capturedMods;
        _settings.HotkeyKey = _capturedVk;

        _settings.IdleLockEnabled = IdleEnabled.IsChecked == true;
        // Always persist the minutes value when it's valid, whether or not idle-lock
        // is currently enabled. Only block the save if the box is on and the value is bad.
        if (int.TryParse(IdleMinutes.Text, out int mins) && mins > 0)
            _settings.IdleMinutes = mins;
        else if (_settings.IdleLockEnabled)
        {
            Fail("Idle minutes must be a positive number.");
            return;
        }

        if (BgCombo.SelectedItem is BackgroundMode bg) _settings.BackgroundMode = bg;
        _settings.BlurStrength = (int)BlurSlider.Value;
        _settings.SolidColor = SolidColor.Text;
        _settings.ImagePath = string.IsNullOrWhiteSpace(ImagePath.Text) ? null : ImagePath.Text;
        _settings.TintPercent = (int)TintSlider.Value;
        _settings.ShowClock = ShowClock.IsChecked == true;
        _settings.Clock24Hour = Clock24.IsChecked == true;
        _settings.RunOnStartup = RunOnStartup.IsChecked == true;
        _settings.LockOnStartup = LockOnStartup.IsChecked == true;
        StartupService.Apply(_settings.RunOnStartup);

        try { _store.Save(_settings); }
        catch (Exception ex) { Fail("Could not save: " + ex.Message); return; }

        Saved?.Invoke();
        DialogResult = true;
        Close();
    }

    private void Fail(string message) => StatusText.Text = message;

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void BrowseImage_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp|All files|*.*", CheckFileExists = true };
        if (dlg.ShowDialog() == true) ImagePath.Text = dlg.FileName;
    }
}
