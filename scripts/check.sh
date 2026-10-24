#!/usr/bin/env bash
# Quality gate: the same checks run locally, in the commit hook and in CI.
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

echo "==> dotnet build api/Shop.sln"
dotnet build api/Shop.sln -warnaserror --nologo -v q
echo "==> dotnet test api/Shop.sln"
# In CI, print each failing test's message and stack so a red run can be diagnosed from the log.
test_logger=()
[ -n "${CI:-}" ] && test_logger=(--logger "console;verbosity=normal")
dotnet test api/Shop.sln --no-build --nologo -v q ${test_logger[@]+"${test_logger[@]}"}

echo "==> web lint"
npm --prefix web run lint
echo "==> web test"
npm --prefix web test
echo "==> web build"
npm --prefix web run build

echo "==> all checks passed"
