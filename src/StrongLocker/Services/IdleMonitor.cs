using System;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using StrongLocker.Interop;
using StrongLocker.Models;

namespace StrongLocker.Services;

/// <summary>
/// Fires <see cref="IdleReached"/> after the user has been idle long enough.
/// <para>
/// Idle is measured from the last <b>real</b> hardware keyboard/mouse event via
/// low-level hooks that <b>ignore injected (synthetic) input</b> - so software
/// "mouse jigglers" / stay-awake tools don't keep the machine from locking.
/// Falls back to GetLastInputInfo if the hooks can't be installed.
/// </para>
/// </summary>
public sealed class IdleMonitor : IDisposable
{
    private readonly DispatcherTimer _timer;
    private AppSettings _settings = new();

    private readonly NativeMethods.LowLevelKeyboardProc _kbProc; // kept alive vs GC
    private readonly NativeMethods.LowLevelKeyboardProc _msProc;
    private IntPtr _kbHook, _msHook;
    private long _lastRealInput = Environment.TickCount64;

    // A mouse MOVE only counts as activity if the cursor hops more than this many
    // pixels between events; smaller moves are treated as idle jitter/drift.
    private const int MoveThresholdSq = 10 * 10;
    private NativeMethods.POINT _lastMousePos;
    private bool _mousePosInit;

    public Func<bool>? IsLocked { get; set; }
    public event Action? IdleReached;

    public IdleMonitor()
    {
        _kbProc = KeyboardCallback;
        _msProc = MouseCallback;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _timer.Tick += OnTick;
    }

    public void Apply(AppSettings settings)
    {
        _settings = settings;
        _timer.Stop();
        UninstallHooks();

        if (settings.IdleLockEnabled && settings.IdleMinutes > 0)
        {
            _mousePosInit = false;
            InstallHooks();
            _lastRealInput = Environment.TickCount64;
            _timer.Start();
        }
    }

    private void OnTick(object? sender, EventArgs e)
    {
        if (IsLocked?.Invoke() == true) return;
        if (GetIdle() >= TimeSpan.FromMinutes(_settings.IdleMinutes))
            IdleReached?.Invoke();
    }

    private TimeSpan GetIdle()
    {
        // Prefer the hook-based real-input clock; fall back to the OS idle timer.
        if (_kbHook != IntPtr.Zero || _msHook != IntPtr.Zero)
            return TimeSpan.FromMilliseconds(Environment.TickCount64 - _lastRealInput);

        var info = new NativeMethods.LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<NativeMethods.LASTINPUTINFO>() };
        if (!NativeMethods.GetLastInputInfo(ref info)) return TimeSpan.Zero;
        return TimeSpan.FromMilliseconds(unchecked((uint)Environment.TickCount - info.dwTime));
    }

    // ---- Hooks ----

    private void InstallHooks()
    {
        IntPtr hMod = NativeMethods.GetModuleHandle(null);
        if (_kbHook == IntPtr.Zero)
            _kbHook = NativeMethods.SetWindowsHookEx(NativeMethods.WH_KEYBOARD_LL, _kbProc, hMod, 0);
        if (_msHook == IntPtr.Zero)
            _msHook = NativeMethods.SetWindowsHookEx(NativeMethods.WH_MOUSE_LL, _msProc, hMod, 0);
    }

    private void UninstallHooks()
    {
        if (_kbHook != IntPtr.Zero) { NativeMethods.UnhookWindowsHookEx(_kbHook); _kbHook = IntPtr.Zero; }
        if (_msHook != IntPtr.Zero) { NativeMethods.UnhookWindowsHookEx(_msHook); _msHook = IntPtr.Zero; }
    }

    private IntPtr KeyboardCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var data = Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);
            if ((data.flags & NativeMethods.LLKHF_INJECTED) == 0)
                _lastRealInput = Environment.TickCount64;
        }
        return NativeMethods.CallNextHookEx(_kbHook, nCode, wParam, lParam);
    }

    private IntPtr MouseCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var data = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);
            if ((data.flags & NativeMethods.LLMHF_INJECTED) == 0)
            {
                if (wParam.ToInt32() == NativeMethods.WM_MOUSEMOVE)
                {
                    // Count as activity only if the cursor hopped more than the threshold.
                    if (!_mousePosInit)
                    {
                        _lastMousePos = data.pt;
                        _mousePosInit = true;
                    }
                    else
                    {
                        long dx = data.pt.x - _lastMousePos.x;
                        long dy = data.pt.y - _lastMousePos.y;
                        _lastMousePos = data.pt;
                        if (dx * dx + dy * dy > MoveThresholdSq)
                            _lastRealInput = Environment.TickCount64;
                    }
                }
                else
                {
                    // Clicks, wheel, and any non-move mouse event are real activity.
                    _lastRealInput = Environment.TickCount64;
                }
            }
        }
        return NativeMethods.CallNextHookEx(_msHook, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Tick -= OnTick;
        UninstallHooks();
    }
}
