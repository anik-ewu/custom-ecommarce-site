# CLAUDE.md: Working Rules for This Repo

Read this before every task. The full spec is in `PROJECT_PROMPT.md`.

## Session start

- Check the build-plan checklist in `PROJECT_PROMPT.md` §10 and continue with the **first unticked step**. The SessionStart hook prints it.
- The spec's "Decided defaults" (§6) and error-code table (§7) are binding. If you change a decision, update the spec in the same commit.

## Git workflow

- Work directly on **`main`** for now. No feature branches.
- **One feature = one commit, then push.** Don't batch several features into one commit, and don't leave work uncommitted between features. Tick the step's box in `PROJECT_PROMPT.md` §10 in that same commit.
- The quality gate is `./scripts/check.sh` from the repo root. It runs `dotnet build api/Shop.sln -warnaserror`, `dotnet test api/Shop.sln`, `npm --prefix web run lint`, `npm --prefix web test` and `npm --prefix web run build`. CI runs the same script. Never commit a red build.
- Commit messages follow Conventional Commits: `feat(cart): reserve unique items for 15 minutes`, `fix(auth): ...`, `chore(ci): ...`, `test(...)`, `docs(...)`.
- Commit body: 1–3 lines on *why*, if it isn't obvious.
- After pushing, check that GitHub Actions passes (`gh run list --limit 1`). If CI fails, the next commit fixes it before any new feature.
- Never commit secrets, `.env`, connection strings, or API keys. Never force-push.

## Hooks (enforced by `.claude/settings.json`)

| Hook | What it does |
|---|---|
| `guard-bash.sh` (before Bash) | Blocks force-push, `--no-verify`, `git reset --hard`, and risky `rm -rf`. On `git commit` it runs `scripts/check.sh` and blocks the commit if it fails. |
| `guard-secrets.sh` (before Edit/Write) | Blocks writing `.env` / real `appsettings.*.json` and content that looks like a live key, webhook secret, password in a connection string, or private key. Asks the user before adding `IgnoreQueryFilters()` outside admin/seed/migrations. |
| `format.sh` (after Edit/Write) | Prettier for `web/` files, `dotnet format whitespace` for `.cs`. |
| `session-start.sh` | Prints git status, recent commits and the next build step. |

When a hook blocks you, fix the cause. Never work around it (no `--no-verify`, no editing the hook to get past it). Config secrets go in `dotnet user-secrets` or a local `.env`, and the committed files are the `.example` versions.

## Agents (`.claude/agents/`)

- **`reviewer`**: run on the staged diff **before every commit**. Fix every ❌ it reports.
- **`test-writer`**: use it for integration tests, especially the required race, rotation, idempotency and tenant-isolation tests, and for the regression test that every bug fix needs.
- **`ui-reviewer`**: run after any step that changes UI, with the dev servers running. It checks 375px, a11y and token usage.

## Code style

- **Keep it simple.** Pick the plainest solution that is correct. No MediatR, no generic repositories, no CQRS, no extra abstraction "for later".
- Add an interface only when there are two implementations or a test needs it (e.g. `IPaymentProvider`, `IClock`).
- Small files, clear names, feature folders. Delete dead code instead of commenting it out.
- C#: nullable enabled, warnings as errors, `async` all the way, `CancellationToken` on API endpoints, records for DTOs, no business logic in controllers.
- Angular: standalone components, signals for state, `inject()`, typed reactive forms, lazy-loaded feature routes, OnPush change detection.
- Comments explain *why*, not *what*.

## Correctness rules (don't break these)

- Every tenant-owned query goes through the `StoreId` global filter. Never use `IgnoreQueryFilters()` outside admin/seed code, and when you do, add a comment explaining why.
- Inventory changes use optimistic concurrency or a single conditional update, inside a transaction.
- Prices and stock are always re-checked server-side at checkout. Never trust the client.
- Webhooks: verify the signature and check `ProcessedWebhookEvent` before doing anything.
- Errors always return `ProblemDetails` with a stable `code` from the table in `PROJECT_PROMPT.md` §7.
- Times are stored in UTC. Use `IClock` (or `TimeProvider`) so tests can control time.

## Auth rules

- Access token: 15-minute JWT, kept in memory on the client only.
- Refresh token: httpOnly Secure cookie, hashed in the DB, rotated on every use, family revoked on reuse.
- Don't add OAuth providers, 2FA, or email verification unless asked.

## Design rules

- Use the design tokens (CSS variables) from `web/src/styles/_tokens.scss`. No hard-coded colors or font sizes in components.
- Mobile-first. Check every page at 375px width before committing UI work.
- Accessibility is required: labels on inputs, alt text on images, visible focus states, AA contrast, keyboard navigation.
- Respect `prefers-reduced-motion`.
- Keep the look calm and premium: whitespace over clutter, one primary action per screen.

## Testing

- Integration tests run against real Postgres via Testcontainers (Docker must be running). Don't mock EF Core.
- Every bug fix gets a test that would have caught it.
- Required coverage: auth rotation and reuse detection, unique-item reservation race, stock conflict, webhook idempotency, tenant isolation.

## When unsure

- Make a reasonable assumption, write it in the commit body or a short note in the README, and continue.
- Stop and ask only before something destructive or hard to reverse (dropping data, rewriting history, changing the auth model).

## After each feature, report

1. What was built (one or two lines).
2. Commit hash and message.
3. How to try it in the browser.
4. Anything skipped or assumed.
