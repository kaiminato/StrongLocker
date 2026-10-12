# StrongLocker — Architecture

Original WPF (.NET 9) application. Nothing here is derived from any other
product; it was designed from the general idea of a "screen cover" utility.

## Big picture

StrongLocker is a **tray app with no main window**. `App` (in `App.xaml.cs`) is
the composition root: it constructs the services, wires their events together,
and manages first-run and quit. There is no DI container — the object graph is
small and built by hand.

```
                 ┌─────────────┐
   hotkey ──────►│             │
   idle   ──────►│ LockController ├──► OverlayWindow (one per monitor)
   schedule ────►│             │        └─ primary monitor hosts the unlock card
   tray/dblclick │             ├──► InputBlocker (low-level keyboard hook)
                 └─────┬───────┘
                       │ reads
                 ┌─────▼───────┐
                 │ AppSettings │◄── SettingsStore (JSON in %AppData%)
                 └─────────────┘
```

## Lock lifecycle

1. A trigger (hotkey / idle / schedule / tray) calls `LockController.Lock()`.
2. **Capture first, show second.** `LockController` builds the background for
   *every* monitor before showing any overlay — otherwise a later screen capture
   would grab an overlay that's already on screen.
3. One `OverlayWindow` per monitor is shown, positioned in **device pixels** via
   `SetWindowPos` (so per-monitor DPI differences don't distort placement). The
   primary monitor's overlay shows the unlock card.
4. `InputBlocker` installs a `WH_KEYBOARD_LL` hook.
5. A correct password calls `LockController.Unlock()`, which removes the hook and
   closes every overlay (`OverlayWindow` refuses to close otherwise).

## Appearance

`AppearanceService` returns a `Brush` + a tint opacity per monitor:

- **Blur desktop** — capture the screen, downscale ~10×, let bilinear upscaling
  soften it. Cheap and fast; no real Gaussian kernel needed for a lock screen.
- **Dim desktop** — sharp capture plus the black tint layer.
- **Solid / Image** — a color or a stretched user image.

The overlay draws the brush as its `Background` and lays a black `Rectangle` over
it at the configured tint opacity.

## Security model — read this

StrongLocker is a **soft lock / privacy cover**, deliberately running as a normal
user process (`asInvoker`). The boundaries:

- **Can** swallow Win, Alt+Tab, Alt+Esc, Ctrl+Esc, Alt+F4 via the low-level hook.
- **Cannot** block `Ctrl+Alt+Del` (the Secure Attention Sequence) or `Win+L`.
  Those are handled by the OS below any user-mode hook — by design, so malware
  can't fake the logon screen. Intercepting them would require a **Credential
  Provider** or a **kernel driver**, which is out of scope and not something a
  tray utility should do.
- A determined person with physical access (safe mode, pulling power, a live USB)
  is **not** stopped by any user-mode locker.

Therefore the honest pitch is: *pairs with* Windows lock + BitLocker, and guards
against **casual** access, not a motivated attacker.

### Password storage

`PasswordStore` uses **PBKDF2 (SHA-256, 100k iterations, 16-byte random salt,
32-byte hash)**. Only salt + hash + iteration count are persisted. Verification
uses `CryptographicOperations.FixedTimeEquals`. The password itself is never
stored or logged.

## Persistence

`SettingsStore` serializes `AppSettings` to `%AppData%\StrongLocker\settings.json`
with a write-to-temp-then-replace to avoid truncation on a crash. Unreadable
settings fall back to defaults rather than crashing.

## Known limitations / future work

- Blur is an approximation; a true acrylic/DWM blur-behind could look nicer.
- Single password / single user (no multi-user).
- Schedule is a single daily time; no "unlock next morning" window yet.
- No command-line lock trigger yet.
- The generated tray icon leaks one `HICON` for the app's lifetime (harmless).
- `InputBlocker` cannot, and intentionally does not try to, trap the OS secure
  keys (see Security model).
