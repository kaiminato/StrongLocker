# StrongLocker

A lightweight **privacy screen cover / soft lock** for Windows. It paints a
full-screen overlay across every monitor, blocks casual input, and unlocks with
a password. Built from scratch in **WPF on .NET 9** — original code, no third-party
dependencies.

> **Read this first — what StrongLocker is and isn't.** It is a *privacy cover*
> that stops someone casually poking at your desktop while you step away. It is
> **not** a security boundary: a user-mode app cannot block `Ctrl+Alt+Del` or
> `Win+L`, and a determined person with physical access can get around any
> user-mode locker. For real protection, use it **together with** the built-in
> Windows lock (`Win+L`) and BitLocker. See [ARCHITECTURE.md](ARCHITECTURE.md).

## Features

- **Lock overlay** on all monitors — blur the desktop, dim it, a solid color, or a custom image, with adjustable darkening and an optional clock.
- **Password unlock** — stored only as a salted PBKDF2-SHA256 hash, never in plain text.
- **Global hotkey** to lock instantly (default `Ctrl+Alt+L`).
- **System tray** icon with Lock / Settings / Quit.
- **Auto-lock on idle** after N minutes.
- **Scheduled lock** at a daily time on chosen weekdays.
- **Input blocking** while locked — swallows Win, Alt+Tab, Alt+Esc, Ctrl+Esc, Alt+F4 (not the OS secure-attention keys; by design).
- **Optional lock/unlock sounds** and **require-password-to-quit**.

## Build & run

Requires the **.NET 9 SDK**.

```bash
dotnet build src/StrongLocker/StrongLocker.csproj -c Release
```

Run the built executable:

```
src/StrongLocker/bin/Release/net9.0-windows/StrongLocker.exe
```

On first launch it opens **Settings** and asks you to set a password (locking is
disabled until one exists), then it lives in the system tray. Double-click the
tray icon or press the hotkey to lock.

## Configuration

Settings are stored as JSON at:

```
%AppData%\StrongLocker\settings.json
```

Only the password *hash* and salt are stored there — not the password.

## Project layout

```
src/StrongLocker/
  App.xaml(.cs)            Composition root: wires services, tray, first-run
  app.manifest            DPI awareness, asInvoker (no elevation)
  Models/                 AppSettings, enums
  Interop/NativeMethods   P/Invoke (keyboard hook, hotkey, idle, window placement)
  Services/               LockController, InputBlocker, Hotkey/Idle/Schedule,
                          Appearance, Sound, Password/Settings stores, Tray
  Views/                  OverlayWindow, SettingsWindow, PasswordPrompt
```

## Status

MVP. See [ARCHITECTURE.md](ARCHITECTURE.md) for design notes, the security
boundary, and ideas for future work.
