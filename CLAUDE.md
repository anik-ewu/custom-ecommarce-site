# CLAUDE.md: Working Rules for This Repo

The full spec is in `PROJECT_PROMPT.md`. This file is the only workflow control: there are no hooks or subagents.

## Session start and tokens

- Next step: `grep -m1 '^- \[ \]' PROJECT_PROMPT.md`. Read only the spec sections that step needs (`grep -n '^## ' PROJECT_PROMPT.md`, then Read with offset/limit), not the whole file.
- The spec's "Decided defaults" (§6) and error-code table (§7) are binding. If you change a decision, update the spec in the same commit.
- One build step per session. After the step is pushed and CI is green, report and stop.
- Don't spawn subagents. Prefer `grep` and partial reads over whole-file reads.

## Git workflow

- Work directly on **`main`**. **One feature = one commit, then push.** Tick the step's box in `PROJECT_PROMPT.md` §10 in that commit. Finish one step before writing code for the next.
- Before committing: run Prettier on changed `web/` files and `dotnet format api/Shop.sln whitespace --include <files>` on changed `.cs` files, then `./scripts/check.sh` once (build, tests, lint, web build; CI runs the same script). Commit only when it's green.
- Self-review `git diff --staged` once against the rules below before committing.
- Conventional Commits (`feat(cart): ...`, `fix(auth): ...`, `chore(ci): ...`, `test(...)`, `docs(...)`); body 1–3 lines on *why* if not obvious.
- After pushing: `gh run watch <id> --exit-status` once (id from `gh run list --limit 1`). Only if red, read `gh run view <id> --log-failed | tail -n 80`. A red CI is fixed before any new feature.
- Never force-push, never use `--no-verify` or `git reset --hard`, and `rm -rf` only build output (bin, obj, node_modules, dist).
- Never commit secrets, `.env`, real `appsettings.*.json`, connection strings or API keys. Secrets go in `dotnet user-secrets` or a local `.env`; commit only `.example` files.

## Code style

- **Keep it simple.** No MediatR, generic repositories, CQRS or abstractions "for later". Interfaces only with two implementations or a test need (e.g. `IPaymentProvider`, `IClock`).
- Small files, clear names, feature folders. Delete dead code. Comments explain *why*.
- C#: nullable, warnings as errors, `async` all the way, `CancellationToken` on endpoints, records for DTOs, no business logic in controllers.
- Angular: standalone, signals, `inject()`, typed reactive forms, lazy routes, OnPush.

## Correctness rules

- Every tenant-owned query goes through the `StoreId` global filter. Ask the user before adding `IgnoreQueryFilters()` outside admin/seed/migrations, and comment why.
- Inventory changes use optimistic concurrency or a single conditional update, inside a transaction.
- Prices and stock are re-checked server-side at checkout. Never trust the client.
- Webhooks: verify the signature and check `ProcessedWebhookEvent` before doing anything.
- Errors return `ProblemDetails` with a stable `code` from `PROJECT_PROMPT.md` §7.
- Times in UTC via `IClock`/`TimeProvider`. Money is `decimal`.

## Auth rules

- Access token: 15-minute JWT, in memory on the client only.
- Refresh token: httpOnly Secure cookie, hashed in the DB, rotated on every use, family revoked on reuse.
- No OAuth providers, 2FA or email verification unless asked.

## Design rules

- Use the tokens in `web/src/styles/_tokens.scss`; no hard-coded colors or font sizes.
- Mobile-first: for UI steps check 375px with `read_page`/`javascript_tool` (no horizontal scroll, contrast); at most one screenshot at scale 0.5.
- Accessibility: input labels, image alt text, visible focus, AA contrast, keyboard nav, `prefers-reduced-motion`.
- Calm and premium: whitespace over clutter, one primary action per screen.

## Testing

- Integration tests use real Postgres via Testcontainers (Docker running). Don't mock EF Core.
- Every bug fix gets a test that would have caught it.
- Required: auth rotation and reuse detection, unique-item reservation race, stock conflict, webhook idempotency, tenant isolation.

## When unsure

Make a reasonable assumption, note it in the commit body or README, and continue. Ask only before something destructive or hard to reverse.

## After each feature, report

What was built, commit hash, how to try it in the browser, anything skipped or assumed. Keep it short.
