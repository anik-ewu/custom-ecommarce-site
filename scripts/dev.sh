#!/usr/bin/env bash
# Local development: Postgres (Docker), the API and the Angular dev server, started together.
# Without the API the web app's /api proxy only returns 502s, so this script waits for /api/health first.
set -euo pipefail
cd "$(dirname "$0")/.."

if [ ! -f .env ]; then
  echo "Missing .env. Copy .env.example to .env and set POSTGRES_PASSWORD; it must match the password in" >&2
  echo "ConnectionStrings:Default (dotnet user-secrets --project api/Shop.Api list)." >&2
  exit 1
fi

echo "==> database"
docker compose up -d --wait db

if [ -s "${NVM_DIR:-$HOME/.nvm}/nvm.sh" ]; then
  set +u
  # shellcheck disable=SC1091
  . "${NVM_DIR:-$HOME/.nvm}/nvm.sh" --no-use
  nvm use --silent >/dev/null
  set -u
fi

echo "==> api on http://localhost:5277"
dotnet run --project api/Shop.Api --launch-profile http &
api_pid=$!
trap 'kill "$api_pid" 2>/dev/null || true' EXIT
trap 'exit 130' INT TERM

healthy=false
for _ in $(seq 1 90); do
  if curl -fs http://localhost:5277/api/health >/dev/null 2>&1; then healthy=true; break; fi
  if ! kill -0 "$api_pid" 2>/dev/null; then echo "The API exited during startup; see the output above." >&2; exit 1; fi
  sleep 1
done
if [ "$healthy" != true ]; then
  echo "The API wasn't healthy after 90 s (is the database reachable?); not starting the web app." >&2
  exit 1
fi

echo "==> web on http://localhost:4200"
npm --prefix web start
