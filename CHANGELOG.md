# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Library page shows **game process** state (running / not running / unknown) for the resolved `.exe`, with **Stop** (`CloseMainWindow`) and **Kill** (`entireProcessTree`) when running. Polls every ~1.5s while a path is known; 32-bit games may not be detectable from a 64-bit app.
- When the chosen **proxy DLL** exists in the game folder, the status line includes **version text** from Windows **file metadata** (`ProductVersion`, else `FileVersion`) read from that file (e.g. `winmm.dll`), even if this installer did not deploy it.

### Changed

- Steam library list rows show **name and install path** only (App ID is no longer shown on each row; search by App ID still works).

### Fixed

- Game **running** detection: use **QueryFullProcessImageName** when `Process.MainModule` fails (common with games / anti-cheat) and normalize paths (`\\?\` prefix, full path) before comparing.

## [0.0.2] - 2026-04-06

### Added

- GitHub Actions publishes a **Latest** release (tag `latest`) with a self-contained x64 zip on every push to `main` / `master`.
- Library page detects selected game **executable architecture** (32 / 64 / ARM64) via PE headers; **Install** uses **addon32** in the download URL when the detected exe is 32-bit (derived from the Settings URL by replacing addon64). Unknown architecture prompts before using the 64-bit URL.
- **Settings → Proxy DLL**: install Display Commander as `winmm.dll`, `dxgi.dll`, `version.dll`, `dbghelp.dll`, or `vulkan-1.dll`. Marker stores the active name; switching proxy **removes** the previously installed managed DLL before deploying the new one. **Remove** always clears the file recorded in the marker (any of the above).
- **Start via exe** next to **Start game** — runs the resolved game `.exe` directly with the game install folder as working directory (enabled after architecture detection finds a path).
- Steam library list is ordered by **most recently started here** (**Start game** or **Start via exe**), then alphabetically for titles never launched from this app. Timestamps persist under `%LocalAppData%\DisplayCommanderInstaller\steam-last-played.json`.

## [0.0.1] - 2026-04-05

### Added

- `build-run.sh` — build the WinUI app and launch `DisplayCommanderInstaller.exe` (defaults: `Debug`, `x64`; override with `CONFIG` / `PLATFORM`).
- **Start game** on the library page — launches the selected title via `steam://rungameid/{AppId}`.
- **winmm.dll status** for the selected game — shows whether Display Commander is installed (managed by this app), absent, or a non-managed `winmm.dll`.

### Fixed

- **Settings** no longer crashes in **unpackaged** runs: download URL is stored under `%LocalAppData%\DisplayCommanderInstaller\` instead of `Windows.Storage.ApplicationData` (which requires package identity).
