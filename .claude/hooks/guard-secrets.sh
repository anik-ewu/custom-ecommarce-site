#!/usr/bin/env bash
# PreToolUse(Edit|Write): keep secrets out of the repo and flag tenant-filter bypasses.
# Exit 2 = block; stderr is shown to Claude.
input=$(cat)
path=$(printf '%s' "$input" | jq -r '.tool_input.file_path // empty')
[ -z "$path" ] && exit 0
content=$(printf '%s' "$input" | jq -r '[.tool_input.content, .tool_input.new_string] | map(select(. != null)) | join("\n")')
name=$(basename "$path")

block() { echo "Blocked by guard-secrets: $1" >&2; exit 2; }

case "$name" in
  *.example|*.sample) ;;
  .env|.env.*) block "$name holds real secrets. Edit .env.example instead and keep real values local." ;;
  appsettings.*.json) block "$name is local-only. Put defaults in appsettings.json, secrets in dotnet user-secrets, and a template in $name.example." ;;
esac

# This hook's own patterns would match themselves.
case "$path" in */.claude/hooks/*) exit 0 ;; esac

patterns=(
  'sk_live_[A-Za-z0-9]+'
  'rk_live_[A-Za-z0-9]+'
  'sk_test_[A-Za-z0-9]{20,}'
  'whsec_[A-Za-z0-9]{10,}'
  'cloudinary://[0-9]+:[A-Za-z0-9_-]+@'
  '-----BEGIN [A-Z ]*PRIVATE KEY-----'
  '(Password|Pwd)=[^;"$<{ ]{3,}'
  'postgres(ql)?://[^:/ ]+:[^@ ]{3,}@'
)
for p in "${patterns[@]}"; do
  if printf '%s' "$content" | grep -Eq -- "$p"; then
    block "content in $name matches secret pattern '$p'. Use config/user-secrets and placeholders."
  fi
done

# The StoreId filter must not be bypassed in normal feature code: ask the user instead of silently allowing it.
if [[ "$path" == *.cs ]] && printf '%s' "$content" | grep -q 'IgnoreQueryFilters'; then
  case "$path" in
    */Admin/*|*Seed*|*/Migrations/*|*Tests*) ;;
    *) jq -n --arg r "IgnoreQueryFilters() in $name bypasses the StoreId tenant filter. Allowed only in admin/seed code with a comment explaining why." \
         '{hookSpecificOutput:{hookEventName:"PreToolUse",permissionDecision:"ask",permissionDecisionReason:$r}}' ;;
  esac
fi
exit 0
