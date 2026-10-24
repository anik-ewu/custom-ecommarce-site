using System.Net.Http.Json;
using FluentAssertions;
using Shop.Application.Admin;
using Shop.Application.Common;
using Shop.Domain.Catalog;
using Shop.Domain.Orders;
using Shop.Domain.Stores;
using Shop.Tests.Infrastructure;
using static Shop.Tests.Admin.AdminTestHelpers;
using static Shop.Tests.Auth.AuthTestHelpers;
using static Shop.Tests.Orders.CheckoutTestHelpers;

namespace Shop.Tests.Admin;

[Collection(ApiCollection.Name)]
public sealed class AdminTenantIsolationTests(ApiFactory factory)
{
    [Fact]
    public async Task Admin_of_store_A_cannot_see_or_change_store_B_products_and_orders()
    {
        var admin = await AdminClientAsync(factory);
        var before = await DashboardAsync(admin);
        var (_, customerId) = await SignedInClientAsync(factory);
        var token = Token();

        var storeB = new Store { Id = Guid.NewGuid(), Name = "Other", Slug = $"other-{token}" };
        var categoryB = new Category { Id = Guid.NewGuid(), StoreId = storeB.Id, Name = "Secret", Slug = $"secret-{token}" };
        var productB = new Product
        {
            Id = Guid.NewGuid(),
            StoreId = storeB.Id,
            CategoryId = categoryB.Id,
            // Low stock, so it would show on store A's dashboard if the filter leaked.
            Name = $"0 Store B secret {token}",
            Slug = $"store-b-secret-{token}",
            Description = "-",
            Materials = "-",
            Dimensions = "-",
            Price = 10,
            InventoryType = InventoryType.Standard,
            StockQuantity = 0,
            CreatedAt = DateTime.UtcNow,
        };
        var orderB = new Order
        {
            Id = Guid.NewGuid(),
            StoreId = storeB.Id,
            UserId = customerId,
            Number = $"KB-B-{token}",
            Status = OrderStatus.Pending,
            ShippingAddress = new ShippingAddress { FullName = $"Store B {token}", Phone = "01700000000", Line1 = "-", City = "Dhaka" },
            Subtotal = 99_000m,
            Total = 99_000m,
            PaymentMethod = PaymentMethod.CashOnDelivery,
            // The newest order anywhere, so it would top store A's recent orders if the filter leaked.
            CreatedAt = DateTime.UtcNow.AddMinutes(1),
            Items = [new OrderItem { Id = Guid.NewGuid(), ProductId = productB.Id, NameSnapshot = productB.Name, PriceSnapshot = 10, Quantity = 1 }],
        };

        var (scopeB, dbB) = await factory.CreateDbAsync(storeB.Id);
        await using (scopeB)
        {
            dbB.AddRange(storeB, categoryB, productB, orderB);
            await dbB.SaveChangesAsync();
        }

        // Products: by id, in lists, and every write.
        await ShouldBeProblemAsync(await admin.GetAsync($"/api/admin/products/{productB.Id}"), 404, "NOT_FOUND");
        await ShouldBeProblemAsync(await admin.PutAsJsonAsync($"/api/admin/products/{productB.Id}",
            ProductRequest(await CreateCategoryAsync(factory), "Hijack", slug: $"hijack-{token}", version: 1)), 404, "NOT_FOUND");
        await ShouldBeProblemAsync(await admin.DeleteAsync($"/api/admin/products/{productB.Id}"), 404, "NOT_FOUND");
        await ShouldBeProblemAsync(await admin.PostAsJsonAsync($"/api/admin/products/{productB.Id}/images",
            new { url = "https://example.com/x.jpg", alt = "x" }), 404, "NOT_FOUND");
        (await ReadOkAsync<Paged<AdminProductListItemDto>>(await admin.GetAsync($"/api/admin/products?q={token}")))
            .TotalCount.Should().Be(0);

        // Orders: by number, in lists, and status changes.
        await ShouldBeProblemAsync(await admin.GetAsync($"/api/admin/orders/{orderB.Number}"), 404, "NOT_FOUND");
        await ShouldBeProblemAsync(await SetStatusAsync(admin, orderB.Number, "Cancelled"), 404, "NOT_FOUND");
        (await ReadOkAsync<Paged<AdminOrderListItemDto>>(await admin.GetAsync($"/api/admin/orders?q={token}")))
            .TotalCount.Should().Be(0);
        (await ReadOkAsync<Paged<AdminOrderListItemDto>>(await admin.GetAsync("/api/admin/orders?pageSize=100")))
            .Items.Should().NotContain(o => o.Number == orderB.Number);

        // Dashboard: no store B sales, pending count, recent order or low-stock product.
        var after = await DashboardAsync(admin);
        after.SalesToday.Should().Be(before.SalesToday);
        after.SalesThisWeek.Should().Be(before.SalesThisWeek);
        after.PendingOrders.Should().Be(before.PendingOrders);
        after.RecentOrders.Should().NotContain(o => o.Number == orderB.Number);
        after.LowStock.Should().NotContain(p => p.Id == productB.Id);

        // Nothing in store B changed.
        var (checkScope, check) = await factory.CreateDbAsync(storeB.Id);
        await using (checkScope)
        {
            var stored = check.Products.Single(p => p.Id == productB.Id);
            stored.Name.Should().Be(productB.Name);
            stored.IsActive.Should().BeTrue();
            check.Orders.Single(o => o.Id == orderB.Id).Status.Should().Be(OrderStatus.Pending);
        }
    }
}
