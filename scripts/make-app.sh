#!/usr/bin/env bash
# Build a proper Squirrel.app bundle locally on a Mac (no CI needed).
# Requires the .NET 8 SDK and the Velopack CLI: dotnet tool install -g vpk
#   ./scripts/make-app.sh            -> version 0.0.1-local
#   ./scripts/make-app.sh 1.2.0      -> version 1.2.0
set -euo pipefail
cd "$(dirname "$0")/.."

if [ "$(uname -s)" != "Darwin" ]; then
  echo "This script builds a macOS .app; on Windows/Linux use scripts/publish.sh or CI." >&2
  exit 1
fi

VERSION="${1:-0.0.1-local}"
case "$(uname -m)" in
  arm64) RID=osx-arm64 ;;
  *)     RID=osx-x64 ;;
esac

echo "Publishing Squirrel $VERSION for $RID..."
dotnet publish src/Squirrel.App -c Release -r "$RID" --self-contained -o "publish/$RID"

echo "Packing Squirrel.app..."
vpk pack \
  --packId Squirrel \
  --packTitle Squirrel \
  --packVersion "$VERSION" \
  --packDir "publish/$RID" \
  --mainExe Squirrel \
  --icon src/Squirrel.App/Assets/squirrel.icns

echo
echo "Done. Look in ./Releases for Squirrel.app (drag it to /Applications)."
echo "Unsigned build: first launch is right-click > Open."
