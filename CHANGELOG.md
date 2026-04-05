# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- GitHub Actions publishes a **Latest** release (tag `latest`) with a self-contained x64 zip on every push to `main` / `master`.

## [0.0.1] - 2026-04-05

### Added

- `build-run.sh` — build the WinUI app and launch `DisplayCommanderInstaller.exe` (defaults: `Debug`, `x64`; override with `CONFIG` / `PLATFORM`).
- **Start game** on the library page — launches the selected title via `steam://rungameid/{AppId}`.
- **winmm.dll status** for the selected game — shows whether Display Commander is installed (managed by this app), absent, or a non-managed `winmm.dll`.

### Fixed

- **Settings** no longer crashes in **unpackaged** runs: download URL is stored under `%LocalAppData%\DisplayCommanderInstaller\` instead of `Windows.Storage.ApplicationData` (which requires package identity).
