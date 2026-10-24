#!/usr/bin/env bash
# PreToolUse(Bash): block dangerous git/shell commands and run the quality gate before commits.
# Exit 2 = block; stderr is shown to Claude.
input=$(cat)
cmd=$(printf '%s' "$input" | jq -r '.tool_input.command // empty')
[ -z "$cmd" ] && exit 0

block() { echo "Blocked by guard-bash: $1" >&2; exit 2; }

if printf '%s' "$cmd" | grep -Eq 'git[[:space:]]+push\b.*(--force|--force-with-lease|[[:space:]]-f\b|[[:space:]]\+)'; then
  block "force-push is not allowed (CLAUDE.md)."
fi
if printf '%s' "$cmd" | grep -Eq 'git[[:space:]].*--no-verify'; then
  block "--no-verify bypasses checks. Fix the failing check instead."
fi
if printf '%s' "$cmd" | grep -Eq 'git[[:space:]]+reset[[:space:]].*--hard'; then
  block "git reset --hard discards work. Ask the user first."
fi
# rm -rf is allowed only on build output folders.
if printf '%s' "$cmd" | grep -Eq '\brm[[:space:]]+-[a-zA-Z]*r[a-zA-Z]*f|\brm[[:space:]]+-[a-zA-Z]*f[a-zA-Z]*r'; then
  # Targets are the words after "rm -rf", up to the next ; & or |.
  targets=$(printf '%s' "$cmd" | sed -E 's/.*rm[[:space:]]+-[a-zA-Z]+[[:space:]]+//; s/[;&|].*//')
  for t in $targets; do
    case "$t" in
      *bin|*bin/|*obj|*obj/|*node_modules|*node_modules/|*dist|*dist/|*.angular/cache*|*TestResults*) ;;
      *) block "rm -rf on '$t'. Only bin/obj/node_modules/dist may be force-removed." ;;
    esac
  done
fi

# Never commit a red build.
if printf '%s' "$cmd" | grep -Eq '(^|[;&|[:space:]])git[[:space:]]+commit\b'; then
  root="${CLAUDE_PROJECT_DIR:-$(git rev-parse --show-toplevel 2>/dev/null)}"
  if [ -x "$root/scripts/check.sh" ]; then
    if ! out=$("$root/scripts/check.sh" 2>&1); then
      echo "Blocked by guard-bash: scripts/check.sh failed, fix it before committing." >&2
      printf '%s\n' "$out" | tail -n 60 >&2
      exit 2
    fi
  fi
fi
exit 0
