#!/usr/bin/env bash
#
# NetSwitch build script.
#   ./build.sh                bump patch version, build (Debug) to dist/v<version>
#   ./build.sh --run          ...and launch it (UAC prompt, since the app is elevated)
#   ./build.sh --release      build in Release configuration
#   ./build.sh --publish      single-file self-contained exe (for distribution)
#   ./build.sh --no-bump      don't increment the version
#
# Each build goes to its own dist/v<version> folder, so a running (elevated)
# instance never locks the output — no need to close the app between builds.

set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CSPROJ="$ROOT/src/NetSwitch/NetSwitch.csproj"

CONFIG="Debug"
DO_RUN=0
DO_PUBLISH=0
DO_BUMP=1

for arg in "$@"; do
  case "$arg" in
    --run)     DO_RUN=1 ;;
    --release) CONFIG="Release" ;;
    --publish) DO_PUBLISH=1; CONFIG="Release" ;;
    --no-bump) DO_BUMP=0 ;;
    *) echo "Unknown option: $arg" >&2; exit 1 ;;
  esac
done

# --- read current version ---
CURRENT=$(grep -oE '<Version>[0-9]+\.[0-9]+\.[0-9]+</Version>' "$CSPROJ" | grep -oE '[0-9]+\.[0-9]+\.[0-9]+')
if [[ -z "$CURRENT" ]]; then
  echo "Could not find <Version> in $CSPROJ" >&2
  exit 1
fi

AUTHOR="mikhodaee@gmail.com"

VERSION="$CURRENT"
if [[ "$DO_BUMP" -eq 1 ]]; then
  IFS='.' read -r MAJ MIN PAT <<< "$CURRENT"
  PAT=$((PAT + 1))
  VERSION="$MAJ.$MIN.$PAT"
  sed -i "s|<Version>$CURRENT</Version>|<Version>$VERSION</Version>|" "$CSPROJ"
  echo "Version: $CURRENT -> $VERSION"
else
  echo "Version: $VERSION (not bumped)"
fi
echo "Developed by $AUTHOR"

OUT="$ROOT/dist/v$VERSION"
mkdir -p "$OUT"

echo "Building NetSwitch v$VERSION ($CONFIG) -> $OUT"

if [[ "$DO_PUBLISH" -eq 1 ]]; then
  dotnet publish "$CSPROJ" -c "$CONFIG" -r win-x64 --self-contained true \
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
    -o "$OUT" --nologo
else
  dotnet build "$CSPROJ" -c "$CONFIG" -o "$OUT" --nologo
fi

EXE="$OUT/NetSwitch.exe"

if [[ "$DO_PUBLISH" -eq 1 ]]; then
  # Hand back a single, clearly-named, distributable exe.
  RELEASE_EXE="$ROOT/dist/NetSwitch-v$VERSION.exe"
  cp "$EXE" "$RELEASE_EXE"
  echo ""
  echo "=============================================================="
  echo " Distributable exe ready:"
  echo "   $RELEASE_EXE"
  echo "   (standalone, self-contained — no .NET install needed)"
  echo "=============================================================="
  EXE="$RELEASE_EXE"
else
  echo ""
  echo "Done -> $EXE"
fi

if [[ "$DO_RUN" -eq 1 ]]; then
  echo "Launching (approve the UAC prompt)..."
  EXE_WIN="$(cygpath -w "$EXE")"
  powershell.exe -NoProfile -Command "Start-Process '$EXE_WIN'"
fi
