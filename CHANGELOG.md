# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- **Display Commander download** choice per game: **Auto (exe)**, **32-bit**, or **64-bit** (radio buttons in the Display Commander card on the library detail panel for Steam and Epic). Overrides wrong detection when the resolved `.exe` is a 32-bit launcher but the real game is 64-bit (or the reverse). Choices persist under `%LocalAppData%\Programs\Display_Commander\display-commander-addon-bitness-overrides.json` (Steam App ID and Epic stable key).
- **Epic Games** tab on the library page: scans `ProgramData\Epic\EpicGamesLauncher\Data\Manifests\*.item` for installed titles (same detail panel ideas as Steam: path, proxy status, architecture, **Install / Remove**, **Open folder**, **Search Epic Store**, **Start game** / **Start via exe**, **Stop** / **Kill**, process line). **Favorites** and **last played** persist under `%LocalAppData%\DisplayCommanderInstaller\` (`epic-favorites.json`, `epic-last-played.json`).
- **RenoDX (Mods wiki)** integration: loads [RenoDX Mods](https://github.com/clshortfuse/renodx/wiki/Mods) raw markdown on app start and before library refresh; matches installed titles to wiki rows. **RenoDX** chip on any wiki-listed game. **Install / update RenoDX addon** only for allowlisted `.addon32` / `.addon64` URLs: `https://clshortfuse.github.io/renodx/…`, `https://github.com/pmnoxx/renodx/…`, and `https://raw.githubusercontent.com/pmnoxx/renodx/…`. For other listings, shows an **untrusted source** warning with the parsed wiki reference URL (open in browser) plus a link to the Mods wiki.
- Library **Show** scope: **ComboBox** for **All** / **Favorites** / **RenoDX** / **Hidden**. **All**, **Favorites**, and **RenoDX** exclude user-hidden titles; **Hidden** lists only hidden games. **Hide from list** / **Unhide** on the detail panel; persistence under `%LocalAppData%\DisplayCommanderInstaller\` (`steam-hidden.json`, `epic-hidden.json`). **RenoDX** filter includes every wiki-matched title, not only trusted downloads.
- Selected game panel lists **`.addon32` / `.addon64` files** present in the install folder (non-recursive).
- **Crash diagnostics**: unhandled UI exceptions and unobserved task exceptions append to `%LocalAppData%\DisplayCommanderInstaller\logs\app.log`.
- Library page shows **game process** state (running / not running / unknown) for the resolved `.exe`, with **Stop** (`CloseMainWindow`) and **Kill** (`entireProcessTree`) when running. Polls every ~1.5s while a path is known; 32-bit games may not be detectable from a 64-bit app.
- When the chosen **proxy DLL** exists in the game folder, the status line includes **version text** from Windows **file metadata** (`ProductVersion`, else `FileVersion`) read from that file (e.g. `winmm.dll`), even if this installer did not deploy it.
- Steam library **favorites**: **Add favorite** / **Remove favorite** (persisted by App ID under `%LocalAppData%\DisplayCommanderInstaller\steam-favorites.json`). Sort order unchanged (last played, then name).

### Changed

- Library detail panel: **Display Commander** (proxy DLL status, **Display Commander download** radios, **Install** / **Remove**) is grouped in a **bordered card** (same chrome as RenoDX), after game process status on Steam and Epic; those controls are no longer above process status or in the main horizontal action row.
- Library detail panel: **RenoDX** (untrusted warning when applicable, **Install / update** / **Remove** addon) is grouped in a **bordered card** for wiki-listed games; those controls are no longer in the main horizontal action row (Steam and Epic).
- **Display Commander proxy DLL** install/remove/status, **RenoDX addon** downloads, and **`.addon32` / `.addon64` listing** use the folder that contains the **resolved game `.exe`** when one is found (e.g. `…\Hades II\Ship`), falling back to the Steam `common\{installdir}` or Epic install root if no exe is resolved. **Remove** tries the exe folder first, then the install root (covers older installs that only had the proxy at the manifest root). **Install** / RenoDX actions stay disabled while executable detection is in progress.
- **Start via exe** uses the **executable’s directory** as the process working directory (not only the Steam/Epic install root).
- Library page uses a **Steam / Epic** tab view; list rows show **name and install path** only (Steam App ID is not shown on each row; search by App ID still works).
- **Primary `.exe` resolver** accepts an install root and display name so the same heuristics serve **Steam** and **Epic** installs.

### Fixed

- Library list: **Start game** / **Start via exe** no longer clear the selected title — `ApplyFilter` rebuilds `FilteredGames` with `_suppressListSelectionSync` for the whole clear/rebind so `ListView` clearing the collection does not sync `SelectedGame` to null (Steam and Epic).
- Game **running** detection: use **QueryFullProcessImageName** when `Process.MainModule` fails (common with games / anti-cheat) and normalize paths (`\\?\` prefix, full path) before comparing.
- Library **RenoDX** UI: `BoolToVisibilityConverter` in **page** resources (reliable inside `ListView` templates) and **theme brushes** that exist on older Windows / theme dictionaries (`SystemControlBackgroundBaseLowBrush` instead of missing Fluent tokens).

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
