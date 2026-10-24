using FluentAssertions;
using Shop.Application.Admin;
using Shop.Domain.Catalog;
using Shop.Tests.Infrastructure;
using static Shop.Tests.Admin.AdminTestHelpers;
using static Shop.Tests.Carts.CartTestHelpers;

namespace Shop.Tests.Admin;

[Collection(ApiCollection.Name)]
public sealed class AdminDashboardTests(ApiFactory factory)
{
    // Tests in this collection run one at a time, so before/after deltas only see this test's own orders.
    [Fact]
    public async Task A_new_order_adds_to_sales_and_pending_and_shows_in_recent_orders()
    {
        var admin = await AdminClientAsync(factory);
        var product = await CreateProductAsync(factory, InventoryType.Standard, stock: 10, price: 1234m);
        var before = await DashboardAsync(admin);

        var placed = await PlaceOrderAsync(factory, (product.Id, 1));
        var after = await DashboardAsync(admin);

        after.SalesToday.Should().Be(before.SalesToday + placed.Total);
        after.SalesThisWeek.Should().Be(before.SalesThisWeek + placed.Total);
        after.OrdersToday.Should().Be(before.OrdersToday + 1);
        after.PendingOrders.Should().Be(before.PendingOrders + 1);
        var recent = after.RecentOrders.Should().ContainSingle(o => o.Number == placed.Number).Subject;
        recent.Total.Should().Be(placed.Total);
        recent.Status.Should().Be("Pending");
        after.RecentOrders[0].Number.Should().Be(placed.Number, "it is the newest order");
    }

    [Fact]
    public async Task Confirming_an_order_keeps_it_in_sales_but_not_in_pending()
    {
        var admin = await AdminClientAsync(factory);
        var product = await CreateProductAsync(factory, InventoryType.Standard, stock: 10, price: 500m);
        var placed = await PlaceOrderAsync(factory, (product.Id, 1));
        var before = await DashboardAsync(admin);

        await SetStatusOkAsync(admin, placed.Number, "Confirmed");
        var after = await DashboardAsync(admin);

        after.PendingOrders.Should().Be(before.PendingOrders - 1);
        after.SalesToday.Should().Be(before.SalesToday);
    }

    [Fact]
    public async Task Low_stock_lists_active_standard_products_with_three_or_fewer_left()
    {
        var admin = await AdminClientAsync(factory);
        var low = await CreateProductAsync(factory, InventoryType.Standard, stock: 2);
        var plenty = await CreateProductAsync(factory, InventoryType.Standard, stock: 4);
        var unique = await CreateUniqueAsync(factory);
        var archived = await CreateProductAsync(factory, InventoryType.Standard, stock: 0);
        (await admin.DeleteAsync($"/api/admin/products/{archived.Id}")).EnsureSuccessStatusCode();

        var lowStock = (await DashboardAsync(admin)).LowStock;

        lowStock.Should().NotContain(p => p.Id == plenty.Id || p.Id == unique.Id || p.Id == archived.Id);
        lowStock.Should().OnlyContain(p => p.StockQuantity <= AdminDashboardService.LowStockThreshold);
        lowStock.Select(p => p.StockQuantity).Should().BeInAscendingOrder();

        // The list is capped (lowest stock first) and other tests leave sold-out products behind in the shared
        // database, so the product is only guaranteed a place when the list isn't already full of lower stock.
        var crowdedOut = lowStock.Count == 8 && lowStock.All(p => p.StockQuantity < 2
            || (p.StockQuantity == 2 && string.CompareOrdinal(p.Name, low.Name) <= 0));
        if (!crowdedOut)
            lowStock.Should().ContainSingle(p => p.Id == low.Id).Which.StockQuantity.Should().Be(2);
    }
}
