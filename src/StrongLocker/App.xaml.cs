using System;
using System.Windows;
using StrongLocker.Models;
using StrongLocker.Services;
using StrongLocker.Views;

namespace StrongLocker;

public partial class App : Application
{
    private SingleInstance? _single;
    private SettingsStore _store = null!;
    private AppSettings _settings = null!;
    private AppearanceService _appearance = null!;
    private InputBlocker _input = null!;
    private LockController _lock = null!;
    private HotkeyService _hotkey = null!;
    private IdleMonitor _idle = null!;
    private TrayIconService _tray = null!;
    private bool _settingsOpen;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _single = new SingleInstance();
        if (!_single.IsFirstInstance)
        {
            MessageBox.Show("StrongLocker is already running (see the system tray).",
                "StrongLocker", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        _store = new SettingsStore();
        _settings = _store.Load();

        _appearance = new AppearanceService();
        _input = new InputBlocker();
        _lock = new LockController(() => _settings, _appearance, _input);

        _hotkey = new HotkeyService();
        _hotkey.HotkeyPressed += () => _lock.Lock();

        _idle = new IdleMonitor { IsLocked = () => _lock.IsLocked };
        _idle.IdleReached += () => _lock.Lock();

        _tray = new TrayIconService();
        _tray.LockRequested += OnLockRequested;
        _tray.SettingsRequested += OpenSettings;
        _tray.QuitRequested += () => Shutdown(); // quit immediately, no password (#5)

        if (!_settings.HasPassword)
        {
            OpenSettings(); // force first-run password setup
            _tray.ShowBalloon("StrongLocker", "Running in the tray. Set a password to enable locking.");
        }

        ApplyServices();

        if (_settings.LockOnStartup && _settings.HasPassword)
            _lock.Lock();
    }

    private void ApplyServices()
    {
        _hotkey.Apply(_settings);
        _idle.Apply(_settings);
        StartupService.Apply(_settings.RunOnStartup); // keep the Run key in sync (e.g. if the exe moved)
    }

    private void OnLockRequested()
    {
        if (_settings.HasPassword) _lock.Lock();
        else OpenSettings();
    }

    private void OpenSettings()
    {
        if (_settingsOpen) return;
        _settingsOpen = true;
        try
        {
            var w = new SettingsWindow(_settings, _store, snapshot => _lock.Preview(snapshot));
            w.Saved += ApplyServices;
            w.ShowDialog();
        }
        finally
        {
            _settingsOpen = false;
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        _hotkey?.Dispose();
        _idle?.Dispose();
        _input?.Dispose();
        _single?.Dispose();
        base.OnExit(e);
    }
}
