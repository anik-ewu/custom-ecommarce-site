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

Seed users need local passwords (never committed): set `Seed:AdminPassword` and `Seed:DemoPassword` in
`appsettings.Development.json` or with `dotnet user-secrets --project api/Shop.Api set Seed:DemoPassword <value>`.
The API migrates and seeds on startup (`Database:MigrateOnStartup`, `Seed:Enabled`).

## Architecture notes

- Project references: `Api → Application → Infrastructure → Domain`. Application services use `ShopDbContext`
  directly (no repository layer), so Application depends on Infrastructure rather than the other way round.
- Tenancy: every `IStoreOwned` entity has a `StoreId` query filter. `TenantMiddleware` resolves the store from
  `Shop:StoreSlug` once per request; new rows get the request's `StoreId` in `SaveChangesAsync`.
- Placeholder images are brand-palette SVGs from `scripts/generate-placeholders.mjs` (in `web/public/placeholders`),
  so no third-party photos are used. Real photos will go to Cloudinary (URLs only in the DB).
- Local secrets: `dotnet user-secrets --project api/Shop.Api set Auth:SigningKey "$(openssl rand -base64 48)"`
  (also `ConnectionStrings:Default`, `Seed:AdminPassword`). If port 5432 is taken, set `POSTGRES_PORT` in `.env`.
