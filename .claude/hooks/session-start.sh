#!/usr/bin/env bash
# SessionStart: orient the session. stdout is added to Claude's context.
root="${CLAUDE_PROJECT_DIR:-$(git rev-parse --show-toplevel 2>/dev/null)}"
cd "$root" || exit 0

echo "## Repo state"
echo "Uncommitted changes:"
git status -s | head -n 20
echo
echo "Recent commits:"
git log --oneline -3
echo
next=$(grep -m1 -E '^- \[ \]' PROJECT_PROMPT.md 2>/dev/null | sed 's/^- \[ \] //')
if [ -n "$next" ]; then
  echo "Next build step (PROJECT_PROMPT.md §10): $next"
else
  echo "All build steps are ticked."
fi
exit 0
