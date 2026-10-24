#!/usr/bin/env bash
# Quality gate: the same checks run locally and in CI.
set -euo pipefail
cd "$(dirname "$0")/.."

# Use the Node version pinned in .nvmrc when nvm is installed (CI uses setup-node instead).
if [ -z "${CI:-}" ] && [ -s "${NVM_DIR:-$HOME/.nvm}/nvm.sh" ]; then
  set +u
  # shellcheck disable=SC1091
  . "${NVM_DIR:-$HOME/.nvm}/nvm.sh" --no-use
  nvm use --silent >/dev/null
  set -u
fi

# Locally, print one line per passing step and only the tail of a failing one, so the
# output stays short. CI streams everything so a red run can be diagnosed from the log.
log=$(mktemp)
trap 'rm -f "$log"' EXIT
step() {
  local name=$1
  shift
  if [ -n "${CI:-}" ]; then
    echo "==> $name"
    "$@"
  elif "$@" >"$log" 2>&1; then
    echo "✓ $name"
  else
    echo "✗ $name"
    tail -n 60 "$log"
    exit 1
  fi
}

# In CI, print each failing test's message and stack.
test_logger=()
[ -n "${CI:-}" ] && test_logger=(--logger "console;verbosity=normal")

step "dotnet build" dotnet build api/Shop.sln -warnaserror --nologo -v q
step "dotnet test" dotnet test api/Shop.sln --no-build --nologo -v q ${test_logger[@]+"${test_logger[@]}"}
step "web lint" npm --prefix web run lint
step "web test" npm --prefix web test
step "web build" npm --prefix web run build

echo "all checks passed"
