# Kinbo: handmade Bangladeshi goods

A single-shop, multi-tenant-ready e-commerce site: .NET 10 Web API + Angular, PostgreSQL.
The full spec lives in [PROJECT_PROMPT.md](PROJECT_PROMPT.md).

## Local setup

Requirements: .NET 10 SDK, Node (version in `.nvmrc`), Docker.

```bash
cp .env.example .env                 # then set POSTGRES_PASSWORD (and POSTGRES_PORT if 5432 is taken)
dotnet user-secrets --project api/Shop.Api set ConnectionStrings:Default "Host=localhost;Port=<POSTGRES_PORT>;Database=shop;Username=shop;Password=<same password>"
dotnet user-secrets --project api/Shop.Api set Auth:SigningKey "$(openssl rand -base64 48)"
npm --prefix web ci
./scripts/dev.sh                     # Postgres + API (:5277) + web (:4200), stopped together with Ctrl+C
```

The web app proxies `/api` to the API, so if pages show "didn't load" and the network tab shows `502 Bad Gateway`,
the API isn't running: start everything with `./scripts/dev.sh` rather than `npm start` alone.

Quality gate (same as CI): `./scripts/check.sh`.

## Try it

1. Open http://localhost:4200, pick a one-of-a-kind piece and add it: it is held for you for 15 minutes
   (countdown in the cart drawer). Open the same piece in a private window: it shows as reserved.
2. Checkout asks you to sign in: use **Try the demo as a shopper** on the login page. Your cart comes with you.
3. Place a cash-on-delivery order (any Bangladeshi mobile number, e.g. 01712345678). The piece is now sold.

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
