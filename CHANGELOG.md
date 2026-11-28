# Changelog

All notable changes to PipSentry are documented in this file.

The format is based on Keep a Changelog, and the project uses Semantic Versioning. Releases are listed newest first.

## 1.7.0 - 2026-11-28

Added
- Settings window (gear in the header, or 設定 in the tray): current version, the notify-only startup check toggle, refresh, and a list of recent releases with their notes.
- Any release in the list can be installed, including older ones; the button says "downgrade" when the selection is older than the running version.
- The MSI now allows downgrades so the version picker can move backwards.

Changed
- The startup check no longer opens a window. It shows one toast per new version and leaves the decision to the user.
- The tray update entries collapse into a single 設定 entry.
- `--render --window settings` replaces `--window update`.

Removed
- The old update dialog (`UpdateWindow`).

## 1.6.2 - 2026-11-28

Fixed
- The opacity slider used the default track repeat buttons: a click started a repeat that raced to the ends, so the thumb jumped between the minimum and 100% and only the thumb itself was draggable. Clicking the track now moves the thumb to that point (`IsMoveToPointEnabled`, 5% snapping).
- The percentage label had a variable width, so the track shifted while dragging. It now has a fixed width.

Changed
- Opacity is applied to the overlay live while dragging, and saved 450 ms after the last change instead of only on explicit actions.

## 1.6.1 - 2026-11-28

Changed
- The installer hand-off lives in `UpdateService.LaunchInstaller`, shared by the dialog and the dev switch.

Internal
- `--applylatest`: check, download and apply an update without the dialog (used to verify the portable replace-and-restart path end to end).

## 1.6.0 - 2026-11-28

Added
- Update check against the GitHub releases API (`releases/latest`). Runs at most once every 24 hours on startup and on demand from the tray menu.
- Update dialog: current and latest version, release notes, download progress, "open download page", "later" and "skip this version".
- Downloads are limited to HTTPS URLs on github.com / githubusercontent.com and the file size is checked against the release asset.
- Installed builds (Program Files) download the MSI and hand off to msiexec; portable builds hand off to a small script that waits for exit, replaces the exe and restarts it.
- `--checkupdate` dev switch and `--render --window update`.

## 1.5.0 - 2026-11-28

Added
- Authenticode signing pipeline: `sign.ps1` (store thumbprint, subject or PFX, SHA-256, timestamp, verification), `build.ps1 -Sign` and an optional CI step driven by `SIGNING_PFX_BASE64` / `SIGNING_PFX_PASSWORD` secrets.
- `tools/new-signing-cert.ps1` and `tools/trust-signing-cert.ps1` for local self-signed builds.
- `docs/SIGNING.md` and a README section explaining the "unknown publisher" prompt and the certificate options.
- The build script now reads the version from the csproj, so the MSI name cannot drift.

## 1.4.2 - 2026-11-28

Fixed
- Dragging the overlay could throw `InvalidOperationException` ("Can only call DragMove when primary mouse button is down") on synthetic clicks and showed an error toast. The drag now checks the button state and ignores that case.

Internal
- `--thumbtest` probe for the DWM thumbnail experiment (`ScreenGrab`, `ThumbTestWindow`).

## 1.4.1 - 2026-11-28

Fixed
- The hotkey recorder ignored Alt combinations: Alt arrives as `Key.System`, so the recorded key was dropped. It now reads `SystemKey`.

## 1.4.0 - 2026-11-28

Added
- Custom global hotkeys: an in-app recorder for show/hide, pause, snapshot, back, click-through and next preset. Each binding is validated and re-registered immediately.
- Preset cycling hotkey (default `Ctrl+Alt+N`) walks through saved setups.
- `--render --window hotkeys` renders the hotkey dialog for screenshots and review.

Fixed
- A duplicate localization key (`HotkeyHint`) that made the app fail at startup.

## 1.3.0 - 2026-11-28

Added
- Monitor presets: save the current window, region, frame rate and scaling, then re-apply them from the pin button in the header.
- "Start with Windows" toggle in the tray menu.
- Launching a second copy now brings the existing window forward instead of exiting silently.
- Blank capture detection: after about 30 mostly black frames the app shows one hint about exclusive fullscreen or `WDA_EXCLUDEFROMCAPTURE`.
- "Stop capture when hidden" toggle in the overlay context menu.
- Test project with 55 xUnit tests, plus a GitHub Actions workflow that builds, tests and packages.

Changed
- The monitor window title is localized.
- Toasts are throttled: the same message within 3 s is dropped, and no tray balloon while the window is visible.
- GPU scaling uses HighQualityCubic instead of linear.
- Captured frames are reused from a pool instead of allocated per frame.
- `--bench` gained `--warmup`.

Fixed
- Switching to another source no longer reuses the previous region.

Notes
- ReadyToRun was measured and rejected: 86.4 MB binary and 1.1-5.9 s cold start, against 0.65 s without it.

## 1.2.0 - 2026-11-28

Added
- GPU scaling: the overlay and the shell preview report their real pixel size, and captures are scaled with Direct2D on the GPU before read-back.
- `--bench` capture benchmark mode and `--grab` frame dumps (`--fps`, `--scale`, `--region`, `--paused`, `--seconds`).

Changed
- Preview capture runs at 15 fps (was 20) and uses the same scaling as the overlay.
- Preview capture stops while the shell is hidden or minimized, and restarts through `ShowShell` when it returns to the foreground.
- While the overlay is hidden, frame delivery and the stats timer stop; the WGC session stays alive so DWM keeps composing the source.

Fixed
- i18n key clash on `Live` (theme color and string) that threw on the LIVE label and spammed error notifications.

## 1.1.1 - 2026-11-28

Added
- Cross-fade theme switching: snapshot the window, swap the palette, then fade the snapshot out over 280 ms (CubicEase).

Changed
- None.

Fixed
- Theme switching falls back to an instant swap when the window is hidden or too small.
- `--render` dev switch creates the target directory before writing.

## 1.1.0 - 2026-11-28

Added
- zh-Hant, zh-Hans and en-US dictionaries (134 keys) under `src/PipSentry/Localization`.
- Globe button in the header switches language at runtime; the choice is saved in settings.
- `--render` dev switch for privacy-safe screenshots.

Changed
- First launch follows the system UI language.
- Shell, monitor, region selector and tray strings moved to `DynamicResource` / `L.Get` / `L.Format`; the layout was widened for English labels, and tray texts refresh after a switch.

Fixed
- None.

## 1.0.0 - 2026-11-28

Added
- Windows Graphics Capture for window and display sources, with region crop.
- Frameless always-on-top overlay: aspect lock, click-through, opacity, snap and size presets.
- Background keep-alive: DWM composition session, sleep prevention, and a per-exe opt-out of windowed game optimizations.
- Dark and light themes, tray icon, global hotkeys and settings persistence.
- Single-file portable exe and WiX 5 MSI through `build.ps1`.

Changed
- None.

Fixed
- None.