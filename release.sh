#!/usr/bin/env bash
#
# NetSwitch release script — builds the distributable exe and publishes a
# GitHub Release with it attached.
#
#   ./release.sh                 bump patch version, then full release
#   ./release.sh --no-bump       release the current version (no increment)
#   ./release.sh --notes "text"  custom release notes (default: auto)
#   ./release.sh --draft         create the release as a draft
#
# Creating the GitHub Release + uploading the exe:
#   * uses `gh` (GitHub CLI) if installed and authenticated, else
#   * uses the GitHub API if $GITHUB_TOKEN is set, else
#   * prints the manual web steps (tag + exe are still ready).

set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CSPROJ="$ROOT/src/NetSwitch/NetSwitch.csproj"
REPO="mahdi-kh1/NetSwitch"

BUMP_ARG=""        # passed through to build.sh
NOTES=""
DRAFT=0

for arg in "$@"; do
  case "$arg" in
    --no-bump) BUMP_ARG="--no-bump" ;;
    --draft)   DRAFT=1 ;;
    --notes)   ;;                       # value handled below
    *) if [[ "${PREV:-}" == "--notes" ]]; then NOTES="$arg"; fi ;;
  esac
  PREV="$arg"
done

# ----------------------------------------------------------------------------
# 1) Build the single-file, self-contained exe (build.sh bumps the version).
# ----------------------------------------------------------------------------
echo "==> Building distributable exe..."
bash "$ROOT/build.sh" --publish $BUMP_ARG

VERSION=$(grep -oE '<Version>[0-9]+\.[0-9]+\.[0-9]+</Version>' "$CSPROJ" | grep -oE '[0-9]+\.[0-9]+\.[0-9]+')
TAG="v$VERSION"
EXE="$ROOT/dist/NetSwitch-v$VERSION.exe"

if [[ ! -f "$EXE" ]]; then
  echo "ERROR: expected exe not found: $EXE" >&2
  exit 1
fi
[[ -z "$NOTES" ]] && NOTES="NetSwitch $TAG — standalone Windows tray app to switch between Wi-Fi and Ethernet."

echo "==> Release $TAG"
echo "    asset: $EXE"

# ----------------------------------------------------------------------------
# 2) Commit version bump (if any) and push branch + tag.
# ----------------------------------------------------------------------------
git -C "$ROOT" add -A
if ! git -C "$ROOT" diff --cached --quiet; then
  git -C "$ROOT" commit -q -m "Release $TAG"
  echo "==> Committed version bump"
fi

if ! git -C "$ROOT" rev-parse "$TAG" >/dev/null 2>&1; then
  git -C "$ROOT" tag -a "$TAG" -m "NetSwitch $TAG"
  echo "==> Tagged $TAG"
fi

echo "==> Pushing branch + tags..."
git -C "$ROOT" push origin HEAD --follow-tags

# ----------------------------------------------------------------------------
# 3) Create the GitHub Release and upload the exe.
# ----------------------------------------------------------------------------
EXE_WIN="$(cygpath -w "$EXE" 2>/dev/null || echo "$EXE")"

draft_flag=""
[[ "$DRAFT" -eq 1 ]] && draft_flag="--draft"

if command -v gh >/dev/null 2>&1 && gh auth status >/dev/null 2>&1; then
  echo "==> Creating release via gh..."
  if gh release view "$TAG" --repo "$REPO" >/dev/null 2>&1; then
    gh release upload "$TAG" "$EXE" --repo "$REPO" --clobber
  else
    gh release create "$TAG" "$EXE" --repo "$REPO" --title "NetSwitch $TAG" --notes "$NOTES" $draft_flag
  fi
  echo "Done -> https://github.com/$REPO/releases/tag/$TAG"

elif [[ -n "${GITHUB_TOKEN:-}" ]]; then
  echo "==> Creating release via GitHub API..."
  api="https://api.github.com/repos/$REPO/releases"
  is_draft=$([[ "$DRAFT" -eq 1 ]] && echo true || echo false)
  payload=$(printf '{"tag_name":"%s","name":"NetSwitch %s","body":%s,"draft":%s}' \
    "$TAG" "$TAG" "$(printf '%s' "$NOTES" | python -c 'import json,sys; print(json.dumps(sys.stdin.read()))')" "$is_draft")

  resp=$(curl -s -H "Authorization: token $GITHUB_TOKEN" -H "Accept: application/vnd.github+json" -d "$payload" "$api")
  rel_id=$(printf '%s' "$resp" | python -c 'import json,sys; print(json.load(sys.stdin).get("id",""))')

  if [[ -z "$rel_id" ]]; then
    echo "ERROR: failed to create release. Response:" >&2
    printf '%s\n' "$resp" >&2
    exit 1
  fi

  echo "==> Uploading asset..."
  curl -s -H "Authorization: token $GITHUB_TOKEN" -H "Content-Type: application/octet-stream" \
    --data-binary "@$EXE" \
    "https://uploads.github.com/repos/$REPO/releases/$rel_id/assets?name=NetSwitch-v$VERSION.exe" >/dev/null
  echo "Done -> https://github.com/$REPO/releases/tag/$TAG"

else
  echo ""
  echo "=============================================================="
  echo " No gh CLI and no \$GITHUB_TOKEN — finish the release manually:"
  echo ""
  echo "   1. Open: https://github.com/$REPO/releases/new?tag=$TAG"
  echo "   2. Title: NetSwitch $TAG"
  echo "   3. Attach this file (keep the name):"
  echo "      $EXE_WIN"
  echo "   4. Publish release"
  echo ""
  echo " Tip: install gh (https://cli.github.com), run 'gh auth login',"
  echo "      then re-run ./release.sh to fully automate this step."
  echo "=============================================================="
fi
