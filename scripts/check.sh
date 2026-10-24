#!/usr/bin/env bash
# Quality gate: the same checks run locally, in the commit hook and in CI.
# Parts whose project doesn't exist yet are skipped, so this stays green before step 1.
set -euo pipefail
cd "$(dirname "$0")/.."

if [ -f api/Shop.sln ] || [ -f api/Shop.slnx ]; then
  sln=$(ls api/Shop.sln* | head -n1)
  echo "==> dotnet build $sln"
  dotnet build "$sln" -warnaserror --nologo -v q
  echo "==> dotnet test $sln"
  dotnet test "$sln" --no-build --nologo -v q
else
  echo "==> skip api (no api/Shop.sln yet)"
fi

if [ -f web/package.json ]; then
  echo "==> web lint"
  npm --prefix web run lint
  echo "==> web build"
  npm --prefix web run build
else
  echo "==> skip web (no web/package.json yet)"
fi

echo "==> all checks passed"
