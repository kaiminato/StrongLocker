using System;
using StrongLocker.Interop;

namespace StrongLocker.Services;

/// <summary>
/// While active, a low-level keyboard hook swallows the common "escape" key
/// combinations that would let someone leave the lock overlay:
/// Win, Alt+Tab, Alt+Esc, Ctrl+Esc, Alt+F4. Ordinary keys pass through so the
/// focused password box still works.
/// <para>
/// LIMITATION: Ctrl+Alt+Del (the Secure Attention Sequence) and Win+L cannot be
/// intercepted from user mode. This is a soft cover, not a security boundary -
/// see ARCHITECTURE.md.
/// </para>
/// </summary>
public sealed class InputBlocker : IDisposable
{
    private readonly NativeMethods.LowLevelKeyboardProc _proc; // kept alive against GC
    private IntPtr _hook = IntPtr.Zero;
    private bool _active;

    public InputBlocker()
    {
        _proc = HookCallback;
    }

    public void Activate()
    {
        _active = true;
        if (_hook != IntPtr.Zero) return;
        IntPtr hMod = NativeMethods.GetModuleHandle(null);
        _hook = NativeMethods.SetWindowsHookEx(NativeMethods.WH_KEYBOARD_LL, _proc, hMod, 0);
    }

    public void Deactivate() => _active = false;

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && _active)
        {
            int msg = wParam.ToInt32();
            if (msg == NativeMethods.WM_KEYDOWN || msg == NativeMethods.WM_SYSKEYDOWN)
            {
                var data = System.Runtime.InteropServices.Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);
                if (ShouldBlock((int)data.vkCode))
                    return (IntPtr)1; // swallow
            }
        }
        return NativeMethods.CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    private static bool ShouldBlock(int vk)
    {
        bool alt = Down(NativeMethods.VK_MENU);
        bool ctrl = Down(NativeMethods.VK_CONTROL);

        if (vk == NativeMethods.VK_LWIN || vk == NativeMethods.VK_RWIN) return true;
        if (vk == NativeMethods.VK_TAB && alt) return true;    // Alt+Tab
        if (vk == NativeMethods.VK_ESCAPE && (alt || ctrl)) return true; // Alt+Esc / Ctrl+Esc
        if (vk == NativeMethods.VK_F4 && alt) return true;     // Alt+F4
        return false;
    }

    private static bool Down(int vk) => (NativeMethods.GetAsyncKeyState(vk) & 0x8000) != 0;

    public void Dispose()
    {
        _active = false;
        if (_hook != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
        }
    }
}
