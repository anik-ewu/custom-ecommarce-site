# Kinbo: handmade Bangladeshi goods

A single-shop, multi-tenant-ready e-commerce site: .NET 10 Web API + Angular, PostgreSQL.
The full spec lives in [PROJECT_PROMPT.md](PROJECT_PROMPT.md).

## Local setup

Requirements: .NET 10 SDK, Node (version in `.nvmrc`), Docker.

```bash
cp .env.example .env                 # then set POSTGRES_PASSWORD
docker compose up -d db
cp api/Shop.Api/appsettings.Development.json.example api/Shop.Api/appsettings.Development.json
dotnet run --project api/Shop.Api    # http://localhost:5277
npm --prefix web ci && npm --prefix web start   # http://localhost:4200, /api is proxied to the API
```

Quality gate (same as CI): `./scripts/check.sh`.
