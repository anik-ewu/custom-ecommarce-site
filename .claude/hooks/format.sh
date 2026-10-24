#!/usr/bin/env bash
# PostToolUse(Edit|Write): format the touched file. Never blocks; no-ops until the projects exist.
input=$(cat)
path=$(printf '%s' "$input" | jq -r '.tool_input.file_path // empty')
[ -f "$path" ] || exit 0
root="${CLAUDE_PROJECT_DIR:-$(git rev-parse --show-toplevel 2>/dev/null)}"

case "$path" in
  "$root"/web/*.ts|"$root"/web/*.html|"$root"/web/*.scss|"$root"/web/*.json)
    if [ -x "$root/web/node_modules/.bin/prettier" ]; then
      "$root/web/node_modules/.bin/prettier" --write --log-level warn "$path" >&2 || true
    fi ;;
  *.cs)
    sln=$(ls "$root"/api/Shop.sln* 2>/dev/null | head -n1)
    if [ -n "$sln" ] && command -v dotnet >/dev/null; then
      dotnet format "$sln" whitespace --include "$path" >/dev/null 2>&1 || true
    fi ;;
esac
exit 0
