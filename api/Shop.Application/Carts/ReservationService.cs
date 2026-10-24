using Microsoft.EntityFrameworkCore;
using Shop.Infrastructure.Data;

namespace Shop.Application.Carts;

/// <summary>15-minute holds on unique pieces. One row per product (unique index), claimed with a single upsert.</summary>
public sealed class ReservationService(ShopDbContext db, TimeProvider clock)
{
    public static readonly TimeSpan HoldDuration = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Claims the hold for this cart. The ON CONFLICT ... WHERE makes it atomic: when two carts race for the same
    /// piece, exactly one insert/update succeeds and the other affects zero rows. Re-adding a piece this cart
    /// already holds succeeds without extending the hold, so nobody can keep a piece forever.
    /// </summary>
    public async Task<bool> TryClaimAsync(Guid productId, Guid cartId, CancellationToken ct)
    {
        var now = Now;
        var expires = now + HoldDuration;
        var affected = await db.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO reservations (id, product_id, cart_id, quantity, expires_at)
            VALUES ({Guid.NewGuid()}, {productId}, {cartId}, 1, {expires})
            ON CONFLICT (product_id) DO UPDATE
                SET cart_id = EXCLUDED.cart_id,
                    expires_at = CASE WHEN reservations.expires_at <= {now}
                                      THEN EXCLUDED.expires_at ELSE reservations.expires_at END
                WHERE reservations.expires_at <= {now} OR reservations.cart_id = EXCLUDED.cart_id
            """,
            ct);
        return affected == 1;
    }

    public Task ReleaseAsync(Guid productId, Guid cartId, CancellationToken ct) =>
        db.Reservations.Where(r => r.ProductId == productId && r.CartId == cartId).ExecuteDeleteAsync(ct);

    /// <summary>
    /// Deletes expired holds. Called lazily on reads (the free-tier API may sleep, so the background sweeper
    /// alone isn't enough) and by <see cref="ReservationSweeper"/>.
    /// </summary>
    public Task<int> ReleaseExpiredAsync(CancellationToken ct)
    {
        var now = Now;
        return db.Reservations.Where(r => r.ExpiresAt <= now).ExecuteDeleteAsync(ct);
    }

    private DateTime Now => clock.GetUtcNow().UtcDateTime;
}
