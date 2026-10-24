using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Shop.Application.Orders;
using Shop.Domain.Catalog;
using Shop.Domain.Orders;
using Shop.Infrastructure.Data;

namespace Shop.Application.Admin;

public sealed class AdminDashboardService(ShopDbContext db, IOptions<ShopOptions> shop, TimeProvider clock)
{
    public const int LowStockThreshold = 3;

    public async Task<DashboardDto> GetAsync(CancellationToken ct)
    {
        // "Today" is the shop's business day (Bangladesh, UTC+6, no daylight saving), expressed in UTC for the query.
        var offset = TimeSpan.FromHours(shop.Value.UtcOffsetHours);
        var localNow = clock.GetUtcNow().ToOffset(offset);
        var startOfToday = new DateTimeOffset(localNow.Date, offset).UtcDateTime;
        var startOfWeek = startOfToday.AddDays(-6);

        var sold = db.Orders.AsNoTracking().Where(o => o.Status != OrderStatus.Cancelled);
        var salesToday = await sold.Where(o => o.CreatedAt >= startOfToday).SumAsync(o => o.Total, ct);
        var salesWeek = await sold.Where(o => o.CreatedAt >= startOfWeek).SumAsync(o => o.Total, ct);
        var ordersToday = await sold.CountAsync(o => o.CreatedAt >= startOfToday, ct);
        var pending = await db.Orders.CountAsync(o => o.Status == OrderStatus.Pending, ct);

        var lowStock = await db.Products.AsNoTracking()
            .Where(p => p.IsActive && p.InventoryType == InventoryType.Standard && p.StockQuantity <= LowStockThreshold)
            .OrderBy(p => p.StockQuantity).ThenBy(p => p.Name).ThenBy(p => p.Id)
            .Take(8)
            .Select(p => new LowStockDto(p.Id, p.Name, p.StockQuantity))
            .ToListAsync(ct);

        var recent = await AdminOrderService
            .ToListItems(db.Orders.AsNoTracking().OrderByDescending(o => o.CreatedAt).ThenBy(o => o.Number).Take(5))
            .ToListAsync(ct);

        return new DashboardDto(salesToday, salesWeek, ordersToday, pending, lowStock, recent);
    }
}
