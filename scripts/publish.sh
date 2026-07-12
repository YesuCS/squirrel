#!/usr/bin/env bash
# Publish a self-contained Squirrel binary for the current (or given) platform.
#   ./scripts/publish.sh              -> auto-detect
#   ./scripts/publish.sh osx-arm64    -> Apple Silicon
#   ./scripts/publish.sh win-x64      -> Windows
#   ./scripts/publish.sh linux-x64    -> Linux
set -euo pipefail
cd "$(dirname "$0")/.."

RID="${1:-}"
if [ -z "$RID" ]; then
  case "$(uname -s)-$(uname -m)" in
    Darwin-arm64)  RID=osx-arm64 ;;
    Darwin-x86_64) RID=osx-x64 ;;
    Linux-aarch64) RID=linux-arm64 ;;
    Linux-*)       RID=linux-x64 ;;
    *)             RID=win-x64 ;;
  esac
fi

echo "Publishing Squirrel for $RID..."
dotnet publish src/Squirrel.App -c Release -r "$RID" \
  --self-contained -p:PublishSingleFile=true \
  -o "publish/$RID"

echo
echo "Done -> publish/$RID/Squirrel"
