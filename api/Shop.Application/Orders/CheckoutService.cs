using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Shop.Application.Carts;
using Shop.Application.Common;
using Shop.Domain.Carts;
using Shop.Domain.Catalog;
using Shop.Domain.Orders;
using Shop.Domain.Payments;
using Shop.Infrastructure.Data;

namespace Shop.Application.Orders;

public sealed class CheckoutService(
    ShopDbContext db,
    ReservationService reservations,
    CartService carts,
    IOptions<ShopOptions> shop,
    TimeProvider clock)
{
    // No 0/O or 1/I, so numbers read clearly over the phone.
    private const string NumberAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    /// <summary>Totals for the checkout page, from the same available-lines subtotal the cart shows.</summary>
    public async Task<CheckoutSummaryDto> GetSummaryAsync(Guid userId, CancellationToken ct)
    {
        var cart = await carts.GetAsync(new CartOwner(userId, null), ct);
        var shipping = shop.Value.ShippingFor(cart.Subtotal);
        return new CheckoutSummaryDto(cart.Subtotal, shipping, cart.Subtotal + shipping, shop.Value.FreeShippingThreshold,
            cart.ItemCount);
    }

    /// <summary>
    /// Validates the cart, re-checks prices and stock on the server, then creates the order, decrements stock,
    /// converts holds and clears the cart, all in one transaction.
    /// </summary>
    public async Task<OrderDto> PlaceOrderAsync(Guid userId, CheckoutRequest request, CancellationToken ct)
    {
        await reservations.ReleaseExpiredAsync(ct);

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        // Serialise checkouts of the same cart (double submit, two tabs): the second waits, then finds it empty.
        // Only a row lock: the cart is then read through the tenant-filtered DbSet.
        await db.Database.ExecuteSqlAsync($"SELECT 1 FROM carts WHERE user_id = {userId} FOR UPDATE", ct);
        var cart = await LoadCartAsync(userId, ct);
        if (cart is null || cart.Items.Count == 0)
            throw new AppException(400, ErrorCodes.ValidationFailed, "Your cart is empty.");

        await EnsureHoldsAsync(cart, ct);

        if (cart.Items.Any(i => i.UnitPrice != i.Product!.Price))
        {
            // Accept the new prices in the cart so the shopper can review them and place the order again.
            foreach (var item in cart.Items)
                item.UnitPrice = item.Product!.Price;
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            throw AppException.Conflict(ErrorCodes.PriceChanged,
                "Some prices changed since you added them. Please review the new total.");
        }

        var order = BuildOrder(userId, cart, request);
        db.Orders.Add(order);
        db.Payments.Add(new Payment
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            Provider = "cod",
            Status = PaymentStatus.Pending,
            Amount = order.Total,
            CreatedAt = Now,
        });
        order.Number = await NewOrderNumberAsync(ct);

        await DecrementStockAsync(cart, ct);

        db.CartItems.RemoveRange(cart.Items);
        await db.SaveChangesAsync(ct);
        // The pieces are sold (stock 0), so their holds have done their job.
        await db.Reservations.Where(r => r.CartId == cart.Id).ExecuteDeleteAsync(ct);
        await tx.CommitAsync(ct);

        return ToDto(order);
    }

    public async Task<Paged<OrderSummaryDto>> GetOrdersAsync(Guid userId, OrderHistoryQuery query, CancellationToken ct)
    {
        var orders = db.Orders.AsNoTracking().Where(o => o.UserId == userId);
        var total = await orders.CountAsync(ct);
        var items = await orders
            .OrderByDescending(o => o.CreatedAt).ThenBy(o => o.Number)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(o => new OrderSummaryDto(
                o.Number,
                o.Status.ToString(),
                o.CreatedAt,
                o.Total,
                o.Items.Sum(i => i.Quantity),
                o.Items.OrderBy(i => i.NameSnapshot).Select(i => i.NameSnapshot).First(),
                // The product may since be edited or hidden; its current first photo is only a thumbnail.
                db.Products
                    .Where(p => p.Id == o.Items.OrderBy(i => i.NameSnapshot).Select(i => i.ProductId).First())
                    .SelectMany(p => p.Images.OrderBy(img => img.SortOrder).Select(img => img.Url).Take(1))
                    .FirstOrDefault()))
            .ToListAsync(ct);
        return new Paged<OrderSummaryDto>(items, query.Page, query.PageSize, total);
    }

    public async Task<OrderDto> GetOrderAsync(Guid userId, string number, CancellationToken ct)
    {
        var order = await db.Orders.AsNoTracking().Include(o => o.Items)
                        .FirstOrDefaultAsync(o => o.Number == number && o.UserId == userId, ct)
                    ?? throw AppException.NotFound("We couldn't find that order.");
        return ToDto(order);
    }

    private Task<Cart?> LoadCartAsync(Guid userId, CancellationToken ct) =>
        db.Carts.Include(c => c.Items).ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(c => c.UserId == userId, ct);

    /// <summary>
    /// Each unique piece must still be held by this cart. An expired hold isn't re-claimed here: the checkout page
    /// already left the piece out of the total, so the shopper holds it again from the cart and sees the new total.
    /// </summary>
    private async Task EnsureHoldsAsync(Cart cart, CancellationToken ct)
    {
        var now = Now;
        foreach (var item in cart.Items)
        {
            var product = item.Product!;
            if (!product.IsActive)
                throw AppException.Conflict(ErrorCodes.OutOfStock, $"{product.Name} is no longer available.");
            if (product.InventoryType != InventoryType.Unique)
                continue;

            var holder = await db.Reservations
                .Where(r => r.ProductId == product.Id && r.ExpiresAt > now)
                .Select(r => (Guid?)r.CartId)
                .FirstOrDefaultAsync(ct);
            if (holder == cart.Id)
                continue;

            throw AppException.Conflict(ErrorCodes.ItemReserved, holder is null
                ? $"Your hold on {product.Name} ended. Hold it again from your cart to continue."
                : $"{product.Name} is being held by another shopper. Remove it to continue.");
        }
    }

    /// <summary>
    /// Optimistic concurrency on Product.xmin: if another checkout changed a product since we read it, reload and
    /// retry once; if the stock is still short (or it conflicts again), OUT_OF_STOCK.
    /// </summary>
    private async Task DecrementStockAsync(Cart cart, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            foreach (var item in cart.Items)
            {
                var product = item.Product!;
                if (product.StockQuantity < item.Quantity)
                {
                    var message = product.StockQuantity == 0
                        ? $"{product.Name} just sold out."
                        : $"Only {product.StockQuantity} of {product.Name} left.";
                    throw AppException.Conflict(ErrorCodes.OutOfStock, message);
                }
                product.StockQuantity -= item.Quantity;
            }

            try
            {
                await db.SaveChangesAsync(ct);
                return;
            }
            catch (DbUpdateConcurrencyException e) when (attempt == 1)
            {
                foreach (var entry in e.Entries)
                    await entry.ReloadAsync(ct);
                // The conflicting change may have been a price edit; the order snapshot must not use a stale price.
                if (cart.Items.Any(i => i.UnitPrice != i.Product!.Price))
                    throw AppException.Conflict(ErrorCodes.PriceChanged,
                        "Some prices changed since you added them. Please review the new total.");
                // Undo our decrement on products that didn't conflict, so the retry starts from what's stored.
                foreach (var entry in db.ChangeTracker.Entries<Product>().Where(x => x.State == EntityState.Modified))
                    entry.Property(p => p.StockQuantity).CurrentValue = entry.Property(p => p.StockQuantity).OriginalValue;
            }
            catch (DbUpdateConcurrencyException)
            {
                throw AppException.Conflict(ErrorCodes.OutOfStock,
                    "Someone else just bought one of these items. Please check your cart.");
            }
        }
    }

    private Order BuildOrder(Guid userId, Cart cart, CheckoutRequest request)
    {
        var items = cart.Items.Select(i => new OrderItem
        {
            Id = Guid.NewGuid(),
            ProductId = i.ProductId,
            NameSnapshot = i.Product!.Name,
            PriceSnapshot = i.Product.Price,
            Quantity = i.Quantity,
        }).ToList();
        var subtotal = items.Sum(i => i.PriceSnapshot * i.Quantity);
        var shipping = shop.Value.ShippingFor(subtotal);
        var a = request.ShippingAddress;

        return new Order
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Number = "",
            Status = OrderStatus.Pending,
            PaymentMethod = PaymentMethod.CashOnDelivery,
            ShippingAddress = new ShippingAddress
            {
                FullName = a.FullName.Trim(),
                Phone = a.Phone.Trim(),
                Line1 = a.Line1.Trim(),
                Line2 = string.IsNullOrWhiteSpace(a.Line2) ? null : a.Line2.Trim(),
                City = a.City.Trim(),
                PostalCode = string.IsNullOrWhiteSpace(a.PostalCode) ? null : a.PostalCode.Trim(),
            },
            Subtotal = subtotal,
            ShippingFee = shipping,
            Total = subtotal + shipping,
            CreatedAt = Now,
            Items = items,
        };
    }

    /// <summary>KB-YYYYMMDD-XXXX. The unique index is the final guard; the pre-check makes a clash vanishingly rare.</summary>
    private async Task<string> NewOrderNumberAsync(CancellationToken ct)
    {
        while (true)
        {
            var suffix = string.Create(4, 0, (span, _) =>
            {
                for (var i = 0; i < span.Length; i++)
                    span[i] = NumberAlphabet[RandomNumberGenerator.GetInt32(NumberAlphabet.Length)];
            });
            var number = $"KB-{Now:yyyyMMdd}-{suffix}";
            if (!await db.Orders.AnyAsync(o => o.Number == number, ct))
                return number;
        }
    }

    private static OrderDto ToDto(Order o) => new(
        o.Number,
        o.Status.ToString(),
        o.CreatedAt,
        o.PaymentMethod.ToString(),
        new ShippingAddressDto(o.ShippingAddress.FullName, o.ShippingAddress.Phone, o.ShippingAddress.Line1,
            o.ShippingAddress.Line2, o.ShippingAddress.City, o.ShippingAddress.PostalCode),
        o.Items.Select(i => new OrderItemDto(i.ProductId, i.NameSnapshot, i.PriceSnapshot, i.Quantity, i.PriceSnapshot * i.Quantity)).ToList(),
        o.Subtotal,
        o.ShippingFee,
        o.Total);

    // Postgres stores microseconds, Linux clocks tick in 100 ns: truncate so the order returned by POST /checkout
    // matches the same order read back later.
    private DateTime Now
    {
        get
        {
            var now = clock.GetUtcNow().UtcDateTime;
            return now.AddTicks(-(now.Ticks % 10));
        }
    }
}
