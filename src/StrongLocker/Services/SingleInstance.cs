using System;
using System.Threading;

namespace StrongLocker.Services;

/// <summary>Guards against a second copy of StrongLocker running at once.</summary>
public sealed class SingleInstance : IDisposable
{
    private readonly Mutex _mutex;
    public bool IsFirstInstance { get; }

    public SingleInstance()
    {
        _mutex = new Mutex(initiallyOwned: true, @"Global\StrongLocker_SingleInstance", out bool created);
        IsFirstInstance = created;
    }

    public void Dispose()
    {
        try { if (IsFirstInstance) _mutex.ReleaseMutex(); } catch { }
        _mutex.Dispose();
    }
}
