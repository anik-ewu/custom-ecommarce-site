using Microsoft.EntityFrameworkCore;
using Shop.Application.Common;
using Shop.Domain.Carts;
using Shop.Domain.Catalog;
using Shop.Infrastructure.Data;

namespace Shop.Application.Carts;

public sealed class CartService(ShopDbContext db, ReservationService reservations, TimeProvider clock)
{
    private static readonly CartDto Empty = new([], 0, 0);

    public async Task<CartDto> GetAsync(CartOwner owner, CancellationToken ct)
    {
        await reservations.ReleaseExpiredAsync(ct);
        var cartId = await FindIdAsync(owner, ct);
        return cartId is null ? Empty : await ToDtoAsync(cartId.Value, ct);
    }

    public async Task<CartDto> AddAsync(CartOwner owner, AddCartItemRequest request, CancellationToken ct)
    {
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == request.ProductId && p.IsActive, ct)
            ?? throw AppException.NotFound("We couldn't find that piece.");

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var cart = await GetOrCreateAsync(owner, ct);
        var item = await db.CartItems.FirstOrDefaultAsync(i => i.CartId == cart.Id && i.ProductId == product.Id, ct);

        if (product.InventoryType == InventoryType.Unique)
        {
            if (request.Quantity != 1)
                throw Validation("quantity", "One-of-a-kind pieces can only be added once.");
            if (product.StockQuantity < 1)
                throw AppException.Conflict(ErrorCodes.OutOfStock, "This piece has already found its home.");
            if (!await reservations.TryClaimAsync(product.Id, cart.Id, ct))
                throw AppException.Conflict(ErrorCodes.ItemReserved,
                    "Someone else is holding this piece right now. It may come back in a few minutes.");

            item ??= Track(NewItem(cart.Id, product, 1));
            item.UnitPrice = product.Price;
        }
        else
        {
            var quantity = (item?.Quantity ?? 0) + request.Quantity;
            EnsureStock(product, quantity);
            item ??= Track(NewItem(cart.Id, product, 0));
            item.Quantity = quantity;
        }

        cart.UpdatedAt = Now;
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException e) when (e.InnerException is Npgsql.PostgresException { SqlState: "23505" })
        {
            // Two first adds for the same shopper raced (e.g. two tabs); the unique index kept the cart consistent.
            throw AppException.Conflict(ErrorCodes.Conflict, "Your cart changed in another tab. Please try again.");
        }
        await tx.CommitAsync(ct);
        return await ToDtoAsync(cart.Id, ct);
    }

    public async Task<CartDto> UpdateAsync(CartOwner owner, Guid itemId, UpdateCartItemRequest request, CancellationToken ct)
    {
        var item = await FindItemAsync(owner, itemId, ct);
        if (item.Product!.InventoryType == InventoryType.Unique)
            throw Validation("quantity", "One-of-a-kind pieces can only be added once.");

        EnsureStock(item.Product, request.Quantity);
        item.Quantity = request.Quantity;
        await db.SaveChangesAsync(ct);
        return await ToDtoAsync(item.CartId, ct);
    }

    public async Task<CartDto> RemoveAsync(CartOwner owner, Guid itemId, CancellationToken ct)
    {
        var item = await FindItemAsync(owner, itemId, ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        db.CartItems.Remove(item);
        await db.SaveChangesAsync(ct);
        await reservations.ReleaseAsync(item.ProductId, item.CartId, ct);
        await tx.CommitAsync(ct);
        return await ToDtoAsync(item.CartId, ct);
    }

    /// <summary>
    /// On sign-in, moves the anonymous cart into the user's cart. Unique pieces keep their existing hold;
    /// standard quantities are added together (capped at stock).
    /// </summary>
    public async Task MergeAsync(Guid anonymousId, Guid userId, CancellationToken ct)
    {
        var anon = await db.Carts.Include(c => c.Items).ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(c => c.AnonymousId == anonymousId, ct);
        if (anon is null)
            return;

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var mine = await db.Carts.Include(c => c.Items).FirstOrDefaultAsync(c => c.UserId == userId, ct);
        if (mine is null)
        {
            anon.UserId = userId;
            anon.AnonymousId = null;
        }
        else
        {
            foreach (var item in anon.Items)
            {
                var existing = mine.Items.FirstOrDefault(i => i.ProductId == item.ProductId);
                if (existing is null)
                {
                    // Added through the DbSet: attached only via mine.Items, a pre-set key would be treated as an UPDATE.
                    Track(NewItem(mine.Id, item.Product!, item.Quantity, item.UnitPrice));
                }
                else if (item.Product!.InventoryType == InventoryType.Standard)
                {
                    existing.Quantity = Math.Min(existing.Quantity + item.Quantity, Math.Max(item.Product.StockQuantity, 1));
                }
            }

            await db.Reservations.Where(r => r.CartId == anon.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.CartId, mine.Id), ct);
            db.Carts.Remove(anon);
            mine.UpdatedAt = Now;
        }

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    private async Task<CartDto> ToDtoAsync(Guid cartId, CancellationToken ct)
    {
        var now = Now;
        var rows = await db.CartItems.AsNoTracking()
            .Where(i => i.CartId == cartId)
            .OrderBy(i => i.AddedAt)
            .Select(i => new
            {
                i.Id,
                i.ProductId,
                i.Product!.Slug,
                i.Product.Name,
                ImageUrl = i.Product.Images.OrderBy(x => x.SortOrder).Select(x => x.Url).FirstOrDefault(),
                i.UnitPrice,
                CurrentPrice = i.Product.Price,
                i.Quantity,
                IsUnique = i.Product.InventoryType == InventoryType.Unique,
                i.Product.StockQuantity,
                i.Product.IsActive,
                ReservedUntil = db.Reservations
                    .Where(r => r.ProductId == i.ProductId && r.CartId == cartId && r.ExpiresAt > now)
                    .Select(r => (DateTime?)r.ExpiresAt)
                    .FirstOrDefault(),
            })
            .ToListAsync(ct);

        var items = rows.Select(r => new CartItemDto(
                r.Id, r.ProductId, r.Slug, r.Name, r.ImageUrl, r.UnitPrice, r.CurrentPrice, r.Quantity, r.IsUnique,
                r.ReservedUntil,
                Available: r.IsActive && (r.IsUnique ? r.ReservedUntil is not null : r.StockQuantity >= r.Quantity)))
            .ToList();

        var subtotal = items.Where(i => i.Available).Sum(i => i.CurrentPrice * i.Quantity);
        return new CartDto(items, subtotal, items.Sum(i => i.Quantity));
    }

    private Task<Guid?> FindIdAsync(CartOwner owner, CancellationToken ct) =>
        Owned(owner).Select(c => (Guid?)c.Id).FirstOrDefaultAsync(ct);

    private async Task<Cart> GetOrCreateAsync(CartOwner owner, CancellationToken ct)
    {
        var cart = await Owned(owner).FirstOrDefaultAsync(ct);
        if (cart is not null)
            return cart;

        cart = new Cart { Id = Guid.NewGuid(), UserId = owner.UserId, AnonymousId = owner.UserId is null ? owner.AnonymousId : null, UpdatedAt = Now };
        db.Carts.Add(cart);
        await db.SaveChangesAsync(ct);
        return cart;
    }

    private async Task<CartItem> FindItemAsync(CartOwner owner, Guid itemId, CancellationToken ct)
    {
        var cartId = await FindIdAsync(owner, ct);
        return await db.CartItems.Include(i => i.Product)
                   .FirstOrDefaultAsync(i => i.Id == itemId && i.CartId == cartId, ct)
               ?? throw AppException.NotFound("That item is no longer in your cart.");
    }

    private IQueryable<Cart> Owned(CartOwner owner) =>
        owner.UserId is { } userId
            ? db.Carts.Where(c => c.UserId == userId)
            : owner.AnonymousId is { } anonId
                ? db.Carts.Where(c => c.AnonymousId == anonId)
                : db.Carts.Where(_ => false);

    private CartItem NewItem(Guid cartId, Product product, int quantity, decimal? unitPrice = null) => new()
    {
        Id = Guid.NewGuid(),
        CartId = cartId,
        ProductId = product.Id,
        Quantity = quantity,
        UnitPrice = unitPrice ?? product.Price,
        AddedAt = Now,
    };

    private CartItem Track(CartItem item)
    {
        db.CartItems.Add(item);
        return item;
    }

    private static void EnsureStock(Product product, int quantity)
    {
        if (quantity > product.StockQuantity)
        {
            var message = product.StockQuantity == 0 ? "This item is out of stock." : $"Only {product.StockQuantity} left.";
            throw AppException.Conflict(ErrorCodes.OutOfStock, message);
        }
    }

    private static AppException Validation(string field, string message) =>
        new(400, ErrorCodes.ValidationFailed, message, new Dictionary<string, string[]> { [field] = [message] });

    private DateTime Now => clock.GetUtcNow().UtcDateTime;
}
