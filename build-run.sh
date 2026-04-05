#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "$0")" && pwd)"
cd "$ROOT"

CONFIG="${CONFIG:-Debug}"
PLATFORM="${PLATFORM:-x64}"
PROJ="src/DisplayCommanderInstaller/DisplayCommanderInstaller.csproj"

dotnet build "$PROJ" -c "$CONFIG" -p:Platform="$PLATFORM" -v:q

target_path="$(dotnet msbuild "$PROJ" -getProperty:TargetPath -p:Configuration="$CONFIG" -p:Platform="$PLATFORM" -nologo)"
exe="${target_path%.dll}.exe"

if [[ ! -f "$exe" ]]; then
  echo "Expected exe not found: $exe" >&2
  exit 1
fi

# Start the app without blocking the shell (do not use exec).
"$exe" "$@" &
disown 2>/dev/null || true
