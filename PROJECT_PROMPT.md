# Project Prompt: Handmade E-commerce Shop (.NET + Angular)

> Paste this into your coding agent at the start of the project. The standing rules live in `CLAUDE.md` at the repo root.

---

## 1. What we're building

A real, single-shop e-commerce site selling **handmade Bangladeshi goods**:

- **Hero range:** nakshi kantha small items (cushion covers, table runners, wall hangings). Each piece is **one of a kind** (stock = 1).
- **Standard range:** jute goods (bags, baskets, mats) sold by **quantity**.

Working shop name: **Kinbo** (keep it in one config value, `Shop:Name`, so renaming is free).

The site must feel **warm, crafted and premium**: easy to navigate, simple, elegant, and pleasant to stay on. It is also a portfolio piece, so code quality, tests and the README matter.

The architecture is **single shop, multi-tenant-ready**: every tenant-owned table has a `StoreId`, filtered by an EF Core global query filter. No tenant signup, subdomains or billing.

---

## 2. Tech stack

| Layer | Choice |
|---|---|
| Backend | .NET 10 (LTS), ASP.NET Core Web API (controllers), EF Core + Npgsql |
| Frontend | Latest stable Angular, standalone components, signals, Angular Router, SCSS |
| Database | PostgreSQL (Neon in production, Docker locally) |
| Images | Cloudinary (free tier); store URLs only, never binaries in the DB |
| Payments | Cash on Delivery + Stripe **test mode**, behind `IPaymentProvider` |
| Tests | xUnit, FluentAssertions, Testcontainers (real Postgres, needs Docker locally and in CI), Angular unit tests for key services using the current Angular CLI's default test runner (don't assume Karma) |
| Hosting | Vercel (Angular) proxying `/api` to Render (API in Docker), Neon (Postgres) |
| CI | GitHub Actions: build, lint, test on every push |

Verify current package versions before installing; don't pin from memory.

---

## 3. Repo structure

Everything sits at the **repo root** (`custom-ecommarce-site/`), with no extra `shop/` folder.

```
<repo root>/
├─ api/
│  ├─ Shop.sln
│  ├─ Shop.Api/            # controllers, auth setup, middleware, Program.cs
│  ├─ Shop.Application/    # feature folders: Auth, Catalog, Cart, Orders, Payments, Admin
│  ├─ Shop.Domain/         # entities, enums, domain rules
│  ├─ Shop.Infrastructure/ # DbContext, migrations, seed, payment providers, Cloudinary
│  └─ Shop.Tests/          # integration tests on real Postgres
├─ web/
│  └─ src/
│     ├─ styles/_tokens.scss  # design tokens (CSS variables), the only place colors/sizes live
│     └─ app/
│        ├─ core/          # auth service, interceptors, guards, error handling
│        ├─ shared/        # UI components, pipes
│        └─ features/      # home, catalog, product, cart, checkout, account, admin
├─ scripts/check.sh        # the quality gate: used locally, by the commit hook, and by CI
├─ .claude/                # Claude Code hooks, agents, settings
├─ docker-compose.yml      # postgres (+ api) for local dev
├─ .github/workflows/ci.yml
└─ README.md
```

Email is **not in scope** (no order emails, no verification).

Keep it simple: **no MediatR, no repository-over-EF layer, no CQRS.** Application services call `DbContext` directly. Feature folders, not technical-layer folders.

---

## 4. Domain model

- `Store` (Id, Name, Slug)
- `Category` (StoreId, Name, Slug, SortOrder)
- `Artisan` (StoreId, Name, Region, Story, PhotoUrl)
- `Product` (StoreId, CategoryId, ArtisanId?, Name, Slug, Description, Materials, Dimensions, Price, `InventoryType` = `Unique | Standard`, StockQuantity, IsActive, `xmin` concurrency token, CreatedAt)
- `ProductImage` (ProductId, Url, Alt, SortOrder)
- `User` (Email, PasswordHash, Name, Role = `Customer | Admin`)
- `RefreshToken` (UserId, TokenHash, FamilyId, ExpiresAt, RevokedAt, ReplacedById, CreatedAt)
- `Cart` (StoreId, UserId or AnonymousId), `CartItem` (ProductId, Quantity, UnitPrice at add time, for `PRICE_CHANGED`)
- `Reservation` (ProductId, CartId, Quantity, ExpiresAt)
- `Order` (StoreId, UserId, Number, Status, ShippingAddress, Subtotal, ShippingFee, Total, PaymentMethod, CreatedAt)
- `OrderItem` (OrderId, ProductId, NameSnapshot, PriceSnapshot, Quantity)
- `Payment` (OrderId, Provider, ProviderRef, Status, Amount)
- `ProcessedWebhookEvent` (Provider, EventId) for idempotency

Money is `decimal(18,2)`, currency BDT, shown as "Tk 2,400" (lakh grouping). Snapshots on `OrderItem` so later price changes never rewrite history.

---

## 5. Auth: JWT + refresh tokens (keep it simple, but correct)

- **Register / login** with email + password (ASP.NET Core `PasswordHasher<User>`; no full Identity UI).
- **Access token:** JWT, **15 minutes**, signed with HS256 from config. Returned in the response body. The Angular app keeps it **in memory only** (a signal), never in localStorage.
- **Refresh token:** random 64-byte value, **7 days**, sent as an **httpOnly, Secure, SameSite=Lax cookie** scoped to `/api/auth`. Only a **SHA-256 hash** is stored.
- **Rotation:** every `/api/auth/refresh` revokes the used token and issues a new one in the same `FamilyId`.
- **Reuse detection:** if a revoked token is presented, revoke the whole family (forces re-login). A token presented again within 30 s of its own rotation (e.g. two tabs refreshing together) is treated as a race, not reuse: the loser gets a plain 401 and the family stays valid.
- **Logout:** revoke the current token and clear the cookie.
- **Angular:** an HTTP interceptor attaches the access token; on 401 it calls refresh **once** (single-flight: concurrent 401s share one refresh call), retries the request, and redirects to login if refresh fails. On app start, call refresh silently to restore the session.
- Roles: `Customer`, `Admin`. Admin routes protected on both API (`[Authorize(Roles="Admin")]`) and client (route guard).
- Rate-limit `/api/auth/login` (5/min per IP) and `/register` (3/min per IP) with ASP.NET Core's built-in rate limiter; return `429` as ProblemDetails.
- Seeded admin and demo passwords come from config or user-secrets, never from source code.

Because Vercel proxies `/api` to Render, the cookie is first-party and **no CORS setup is needed** in production.

---

## 6. Key business rules

1. **Unique items:** adding to cart creates a `Reservation` for 15 minutes. Claim with a single conditional update so when two shoppers grab the same piece, exactly one wins; the other gets a clear `409 ITEM_RESERVED` message.
2. **Standard items:** decrement stock at order placement using optimistic concurrency (`xmin`); retry once on conflict, then return `409 OUT_OF_STOCK`.
3. **Reservation expiry:** release expired holds lazily on read (cart, product page, checkout) **and** with a background `IHostedService`. Never rely only on the background job (free-tier API sleeps). Product reads treat expired holds as released; the cart read, checkout and the sweeper delete them. Re-adding a piece you already hold does not extend the hold.
4. **Checkout:** validate cart → re-check prices and stock server-side → create order in one transaction → convert reservations → clear cart. A unique piece needs this cart's *live* hold: an expired hold is not silently re-claimed (the checkout total already excluded it), so the shopper holds it again from the cart. A price change returns `PRICE_CHANGED` and updates the cart, so the next attempt shows and uses the new total.
5. **Payments:** COD creates the order as `Pending`. Stripe test mode uses Checkout Sessions; the webhook marks the payment `Paid`. Webhooks verify signature and are **idempotent** via `ProcessedWebhookEvent`.
6. **Tenancy:** `StoreId` resolved once per request (single store for now, from config) and applied via global query filter. A test must prove store A never sees store B's data.

**Decided defaults** (assumptions; change them here if the shop needs something else):

- **Reservations** apply to unique items only. Standard items are checked and decremented at checkout.
- **Anonymous cart:** an `AnonymousId` httpOnly cookie. On login, merge it into the user's cart; unique items keep their existing reservation.
- **Checkout requires login** (`Order.UserId` is required). Browsing and the cart work signed out; the cart merges on login.
- **Shipping:** flat `Shop:ShippingFee` (e.g. 120 BDT), free at or above `Shop:FreeShippingThreshold`. Both are config values.
- **Order number:** `KB-YYYYMMDD-XXXX` (random suffix, unique index).
- **Order status:** `Pending → Confirmed → Shipped → Delivered`, or `Cancelled` from `Pending`/`Confirmed`. Cancelling restores standard stock and frees unique items.
- **Stripe:** Checkout Session expires after 30 minutes; the `checkout.session.expired` webhook cancels the order and restores stock.
- **Images:** the API does a signed upload to Cloudinary and stores only the returned URL and alt text.

---

## 7. API (REST, `/api`)

- `POST /auth/register`, `POST /auth/login`, `POST /auth/demo` (role: customer|admin, no password), `POST /auth/refresh`, `POST /auth/logout`, `GET /auth/me`
- `GET /categories` (with a tile `imageUrl`), `GET /products?category=&q=&sort=&type=unique|standard&inStock=&page=&pageSize=`, `GET /products/{slug}`, `GET /artisans`, `GET /artisans/{id}` (with their pieces)
- `GET /cart`, `POST /cart/items`, `PATCH /cart/items/{id}`, `DELETE /cart/items/{id}`
- `POST /checkout` (returns order, or Stripe checkout URL), `POST /payments/stripe/webhook`
- `GET /orders?page=&pageSize=` (own orders, newest first), `GET /orders/{number}` (own order only)
- Admin (role `Admin`): `/admin/products` (CRUD; `DELETE` archives; `PUT` needs the `version` it loaded, stale → `CONFLICT`; `POST/DELETE /{id}/images`), `/admin/uploads` (multipart JPEG/PNG/WebP ≤ 5 MB → URL; Cloudinary when `Images:Cloudinary*` is set, else local disk in development), `/admin/orders` (list, detail, `PATCH /{number}/status`), `/admin/artisans`, `/admin/dashboard` (sales today/7 days in shop time, low stock ≤ 3, recent orders)
- `GET /health`

**One error shape:** `ProblemDetails` with an `errors` dictionary for field errors and a stable `code`. Validate input with FluentValidation or data annotations. Paginate every list.

| Code | Status | When |
|---|---|---|
| `VALIDATION_FAILED` | 400 | Invalid input (field errors in `errors`) |
| `UNAUTHORIZED` | 401 | Missing/expired access token, bad credentials |
| `FORBIDDEN` | 403 | Wrong role |
| `NOT_FOUND` | 404 | Resource missing (or belongs to another store) |
| `ITEM_RESERVED` | 409 | Unique item held by another cart |
| `OUT_OF_STOCK` | 409 | Not enough standard stock |
| `PRICE_CHANGED` | 409 | Price changed since it was added to the cart; client shows the new total |
| `CONFLICT` | 409 | Other concurrency conflict (e.g. email already registered) |
| `RATE_LIMITED` | 429 | Too many auth attempts |
| `INVALID_STATUS_TRANSITION` | 409 | Order status change not allowed (e.g. Delivered → Cancelled) |
| `IMAGE_STORAGE_NOT_CONFIGURED` | 503 | Photo upload in production without Cloudinary settings |
| `INTERNAL_ERROR` | 500 | Unexpected server error |
| `STORE_NOT_CONFIGURED` | 503 | The configured store doesn't exist yet (seed not run) |

Add new codes to this table when you introduce them.

---

## 8. Design direction

**Feel:** a calm, high-end craft boutique. Think gallery, not marketplace. Big photography, generous whitespace, slow and confident.

- **Palette (CSS variables in `web/src/styles/_tokens.scss`, light + dark):**
  - Background: warm off-white `#FAF6F0` · Surface: `#FFFFFF`
  - Ink: deep indigo `#1F2A44` · Muted text: `#5B6475`
  - Accent: terracotta `#C2603A` (stitches, decoration, large text) · `--color-accent-strong` `#A64E2C` for buttons and small text, since `#C2603A` is only ~4:1 on white · Secondary: kantha red `#9E2B25` · Soft: jute beige `#E8DCC4`
  - Dark mode: background `#14171F`, text `#EDE7DD`, same accents slightly lifted
- **Type:** serif display for headings (Fraunces or Cormorant Garamond), clean sans for body (Inter). Large headline sizes, comfortable 1.6 line height.
- **Signature motif:** a subtle **running-stitch dashed line** (the kantha stitch) used for section dividers, hover underlines and focus rings. Use it sparingly; it's the brand's fingerprint.
- **Motion:** gentle fades and image zooms on hover (200–300 ms), respecting `prefers-reduced-motion`. No carousels that auto-advance.

**Pages:**
1. **Home:** full-width hero photo with one line of story and one CTA ("Explore the collection"); featured one-of-a-kind pieces; shop by category tiles; "Meet the artisans" strip; trust row (handmade, COD, delivery time).
2. **Catalog:** clean grid (2 cols mobile, 3–4 desktop), category chips, sort, search, "One of a kind" badge, skeleton loaders.
3. **Product:** large gallery with zoom, price, materials, dimensions, artisan card with story, stitch-style note, sticky "Add to cart" on mobile, "Only 1 available" or reserved-timer messaging.
4. **Cart:** slide-in drawer + full page, reservation countdown for unique items.
5. **Checkout:** single page, few fields, COD or card, clear order summary.
6. **Account:** login/register, order history, order detail.
7. **Admin:** sidebar layout, dashboard cards, products table with image upload, orders with status updates.
8. **Legal:** `/privacy` (incl. how Google sign-in data is used, per Google's API User Data Policy) and `/terms` (holds, COD, delivery, returns), linked from the footer, sign-up and checkout. Business contact details live in `web/src/app/core/shop-info.ts`; they are required before enabling Google OAuth.

**UX rules:** maximum 3 clicks from home to checkout; sticky header with search and cart count; breadcrumbs on product pages; empty states with a helpful next step; every action gives visible feedback (toast or inline). Mobile-first, accessible (WCAG AA contrast, keyboard navigation, alt text, labelled inputs). Lighthouse target: 90+ performance and accessibility.

---

## 9. Seed data

Seed one store, 4 categories, 3 artisans with short stories, ~16 products (8 unique kantha pieces, 8 standard jute items), an admin user and a demo customer. Use **placeholder images you have rights to** (your own photos, or a royalty-free source). Until real photos exist, the seed uses brand-palette SVGs from `scripts/generate-placeholders.mjs`. Never use other sellers' product photos.

Add a "Try the demo" option on the login page that signs in as the demo customer or demo admin (`POST /api/auth/demo`, separate `demo-admin@kinbo.test` account, no shared password).

---

## 10. Build plan (one feature = one commit + push)

Tick the box **in the same commit** that finishes the step. A new session starts at the first unticked box.

- [x] 0. `chore(claude)`: project rules, hooks and agents
- [x] 1. `chore`: solution, Angular app (with Prettier + ESLint), docker-compose, `scripts/check.sh` filled in, CI calling it, health endpoint
- [x] 2. `feat(db)`: entities, DbContext, tenant filter, migrations, seed
- [x] 3. `feat(auth)`: register/login, JWT + refresh rotation, interceptor, guards
- [x] 4. `feat(ui)`: design tokens, typography, stitch motif, app shell (header, footer, toast)
- [x] 5. `feat(catalog)`: categories, product list/detail API + pages
- [x] 6. `feat(home)`: home page, artisans
- [x] 7. `feat(cart)`: cart, reservations, expiry
- [x] 8. `feat(checkout)`: orders with COD
- [ ] 9. `feat(payments)`: Stripe test mode + idempotent webhook
- [x] 10. `feat(account)`: order history
- [x] 11. `feat(admin)`: dashboard, products, orders, image upload
- [ ] 12. `chore(deploy)`: Dockerfile, Render, Vercel proxy, Neon, README, demo video script

Each step must end with: `./scripts/check.sh` green, the `reviewer` agent passing, and the feature usable in the browser. UI steps also need the `ui-reviewer` agent passing.

---

## 11. Definition of done

- Live demo URL works, including the demo login.
- Integration tests cover auth (rotation + reuse detection), concurrent reservation of a unique item, stock conflict, webhook idempotency, and tenant isolation.
- README has: screenshots, architecture diagram, setup steps, environment variables, and a **Design decisions** section (tenancy, concurrency, auth, payments, free-tier trade-offs).
- No secrets committed; `.env.example` and `appsettings.Development.json.example` provided.
