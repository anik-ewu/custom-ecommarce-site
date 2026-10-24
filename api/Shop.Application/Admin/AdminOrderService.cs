using Microsoft.EntityFrameworkCore;
using Shop.Application.Common;
using Shop.Application.Orders;
using Shop.Domain.Catalog;
using Shop.Domain.Orders;
using Shop.Domain.Payments;
using Shop.Infrastructure.Data;

namespace Shop.Application.Admin;

public sealed class AdminOrderService(ShopDbContext db)
{
    public async Task<Paged<AdminOrderListItemDto>> ListAsync(AdminOrderQuery query, CancellationToken ct)
    {
        var orders = db.Orders.AsNoTracking();
        if (query.Status is { } status)
        {
            var parsed = Enum.Parse<OrderStatus>(status);
            orders = orders.Where(o => o.Status == parsed);
        }
        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var q = query.Q.Trim();
            orders = orders.Where(o => o.Number.Contains(q) || o.ShippingAddress.Phone.Contains(q)
                || EF.Functions.ILike(o.ShippingAddress.FullName, "%" + q.Replace("%", "").Replace("_", "") + "%"));
        }

        var total = await orders.CountAsync(ct);
        var items = await ToListItems(orders.OrderByDescending(o => o.CreatedAt).ThenBy(o => o.Number)
                .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize))
            .ToListAsync(ct);
        return new Paged<AdminOrderListItemDto>(items, query.Page, query.PageSize, total);
    }

    public async Task<AdminOrderDto> GetAsync(string number, CancellationToken ct)
    {
        var order = await db.Orders.AsNoTracking().Include(o => o.Items).FirstOrDefaultAsync(o => o.Number == number, ct)
            ?? throw AppException.NotFound("We couldn't find that order.");
        var email = await db.Users.Where(u => u.Id == order.UserId).Select(u => u.Email).FirstAsync(ct);
        return new AdminOrderDto(CheckoutService.ToDto(order), email,
            OrderStatusRules.NextFrom(order.Status).Select(s => s.ToString()).ToList());
    }

    /// <summary>
    /// Moves an order along its lifecycle. Cancelling puts the stock back (standard items) and makes unique pieces
    /// available again, with the same xmin optimistic concurrency as checkout, all in one transaction.
    /// </summary>
    public async Task<AdminOrderDto> UpdateStatusAsync(string number, UpdateOrderStatusRequest request, CancellationToken ct)
    {
        var target = Enum.Parse<OrderStatus>(request.Status);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var order = await db.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Number == number, ct)
            ?? throw AppException.NotFound("We couldn't find that order.");

        var current = order.Status;
        if (!OrderStatusRules.CanMove(current, target))
            throw InvalidTransition(current, target);

        // Claim the transition atomically: a second request (double click, two admins, a payment webhook) racing
        // for the same change updates zero rows and stops here, so stock is never restored twice.
        var claimed = await db.Orders
            .Where(o => o.Id == order.Id && o.Status == current)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.Status, target), ct);
        if (claimed == 0)
            throw AppException.Conflict(ErrorCodes.InvalidStatusTransition,
                "This order was just updated by someone else. Reload to see its status.");
        var payment = await db.Payments.FirstOrDefaultAsync(p => p.OrderId == order.Id, ct);
        if (payment is not null && target is OrderStatus.Delivered or OrderStatus.Cancelled)
            // Cash on delivery is collected by the courier on delivery; a cancelled order is never charged.
            payment.Status = target == OrderStatus.Delivered ? PaymentStatus.Paid : PaymentStatus.Cancelled;

        if (target == OrderStatus.Cancelled)
            await RestoreStockAsync(order, ct);
        else
            await db.SaveChangesAsync(ct);

        await tx.CommitAsync(ct);
        return await GetAsync(number, ct);
    }

    private async Task RestoreStockAsync(Order order, CancellationToken ct)
    {
        var ids = order.Items.Select(i => i.ProductId).ToList();
        for (var attempt = 1; ; attempt++)
        {
            var products = await db.Products.Where(p => ids.Contains(p.Id)).ToListAsync(ct);
            foreach (var item in order.Items)
            {
                var product = products.FirstOrDefault(p => p.Id == item.ProductId);
                if (product is null) continue;
                product.StockQuantity = product.InventoryType == InventoryType.Unique
                    ? 1
                    : product.StockQuantity + item.Quantity;
            }

            try
            {
                await db.SaveChangesAsync(ct);
                return;
            }
            catch (DbUpdateConcurrencyException e) when (attempt == 1)
            {
                // A checkout changed one of these products meanwhile: reload and add the stock back on top of it.
                foreach (var entry in e.Entries)
                    await entry.ReloadAsync(ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                throw AppException.Conflict(ErrorCodes.Conflict, "Stock changed while cancelling. Please try again.");
            }
        }
    }

    private static AppException InvalidTransition(OrderStatus from, OrderStatus to) =>
        AppException.Conflict(ErrorCodes.InvalidStatusTransition, $"An order that is {from} can't be marked {to}.");

    internal static IQueryable<AdminOrderListItemDto> ToListItems(IQueryable<Order> orders) =>
        orders.Select(o => new AdminOrderListItemDto(
            o.Number, o.Status.ToString(), o.CreatedAt, o.ShippingAddress.FullName, o.ShippingAddress.Phone,
            o.Items.Sum(i => i.Quantity), o.Total));
}
