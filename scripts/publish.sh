#!/bin/bash
set -euo pipefail
reel_root="$(cd "$(dirname "$0")/.." && pwd)"
reel_output="${1:-$reel_root/dist}"
mkdir -p "$reel_output"
reel_output="$(cd "$reel_output" && pwd)"
cd "$reel_root/web"
pnpm install --frozen-lockfile
pnpm generate
cd "$reel_root"
# Publish into an empty folder so files removed from the project don't carry forward.
case "$reel_output" in
  / | "$HOME" | "$reel_root") echo "Refusing to clear $reel_output" >&2; exit 1 ;;
esac
rm -rf "$reel_output"
dotnet publish server/Reel.Server/Reel.Server.csproj -c Release -o "$reel_output"
rsync -a --delete web/.output/public/ "$reel_output/wwwroot/"
echo "Published Reel to $reel_output"
