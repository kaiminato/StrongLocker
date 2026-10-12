using System;
using System.Windows.Interop;
using StrongLocker.Interop;
using StrongLocker.Models;

namespace StrongLocker.Services;

/// <summary>
/// Registers a single global "lock now" hotkey using a message-only window to
/// receive WM_HOTKEY (the app has no main window).
/// </summary>
public sealed class HotkeyService : IDisposable
{
    private const int HotkeyId = 0xBEEF;
    private HwndSource? _source;
    private bool _registered;

    public event Action? HotkeyPressed;

    public void Apply(AppSettings settings)
    {
        EnsureSource();
        Unregister();
        uint mods = (uint)settings.HotkeyModifiers | NativeMethods.MOD_NOREPEAT;
        if (settings.HotkeyKey == 0) return;
        _registered = NativeMethods.RegisterHotKey(_source!.Handle, HotkeyId, mods, (uint)settings.HotkeyKey);
    }

    private void EnsureSource()
    {
        if (_source is not null) return;
        var parameters = new HwndSourceParameters("StrongLockerHotkeySink")
        {
            Width = 0,
            Height = 0,
            ParentWindow = NativeMethods.HWND_MESSAGE
        };
        _source = new HwndSource(parameters);
        _source.AddHook(WndProc);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY && wParam.ToInt32() == HotkeyId)
        {
            handled = true;
            HotkeyPressed?.Invoke();
        }
        return IntPtr.Zero;
    }

    private void Unregister()
    {
        if (_registered && _source is not null)
        {
            NativeMethods.UnregisterHotKey(_source.Handle, HotkeyId);
            _registered = false;
        }
    }

    public void Dispose()
    {
        Unregister();
        _source?.RemoveHook(WndProc);
        _source?.Dispose();
        _source = null;
    }
}
