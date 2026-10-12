using System;
using System.Windows.Threading;
using StrongLocker.Interop;
using StrongLocker.Models;

namespace StrongLocker.Services;

/// <summary>Fires <see cref="IdleReached"/> when the user has been idle long enough.</summary>
public sealed class IdleMonitor : IDisposable
{
    private readonly DispatcherTimer _timer;
    private AppSettings _settings = new();

    /// <summary>Set by the owner so we never re-trigger while already locked.</summary>
    public Func<bool>? IsLocked { get; set; }

    public event Action? IdleReached;

    public IdleMonitor()
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _timer.Tick += OnTick;
    }

    public void Apply(AppSettings settings)
    {
        _settings = settings;
        _timer.Stop();
        if (settings.IdleLockEnabled && settings.IdleMinutes > 0)
            _timer.Start();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        if (IsLocked?.Invoke() == true) return;
        if (GetIdle() >= TimeSpan.FromMinutes(_settings.IdleMinutes))
            IdleReached?.Invoke();
    }

    private static TimeSpan GetIdle()
    {
        var info = new NativeMethods.LASTINPUTINFO { cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.LASTINPUTINFO>() };
        if (!NativeMethods.GetLastInputInfo(ref info)) return TimeSpan.Zero;
        // Both values are unsigned tick counts that wrap ~every 49.7 days.
        uint idleMs = unchecked((uint)Environment.TickCount - info.dwTime);
        return TimeSpan.FromMilliseconds(idleMs);
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Tick -= OnTick;
    }
}
