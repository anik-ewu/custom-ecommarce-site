using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Shop.Application.Orders;
using Shop.Domain.Catalog;
using Shop.Domain.Orders;
using Shop.Domain.Stores;
using Shop.Tests.Infrastructure;
using static Shop.Tests.Auth.AuthTestHelpers;
using static Shop.Tests.Carts.CartTestHelpers;
using static Shop.Tests.Orders.CheckoutTestHelpers;

namespace Shop.Tests.Orders;

[Collection(ApiCollection.Name)]
public sealed class OrderHistoryTests(ApiFactory factory)
{
    // Mirrors Paged<OrderSummaryDto>; TotalPages is a computed property there, so it can't be deserialized into it.
    private sealed record PageOfOrders(List<OrderSummaryDto> Items, int Page, int PageSize, int TotalCount, int TotalPages);

    private static async Task<PageOfOrders> ListAsync(HttpClient client, string query = "")
    {
        var response = await client.GetAsync("/api/orders" + query);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<PageOfOrders>())!;
    }

    private async Task<Product> ProductNamedAsync(string name, decimal price, int stock = 10)
    {
        var product = await CreateProductAsync(factory, InventoryType.Standard, stock, price);
        var (scope, db) = await factory.CreateDbAsync();
        await using (scope)
            await db.Products.Where(p => p.Id == product.Id).ExecuteUpdateAsync(s => s.SetProperty(p => p.Name, name));
        product.Name = name;
        return product;
    }

    private async Task AddImagesAsync(params ProductImage[] images)
    {
        var (scope, db) = await factory.CreateDbAsync();
        await using (scope)
        {
            db.AddRange(images);
            await db.SaveChangesAsync();
        }
    }

    private static ProductImage Image(Guid productId, string url, int sortOrder) =>
        new() { Id = Guid.NewGuid(), ProductId = productId, Url = url, Alt = "-", SortOrder = sortOrder };

    [Fact]
    public async Task A_new_customer_gets_an_empty_page()
    {
        var (client, _) = await SignedInClientAsync(factory);

        var page = await ListAsync(client);

        page.Items.Should().BeEmpty();
        page.TotalCount.Should().Be(0);
        page.TotalPages.Should().Be(0);
        page.Page.Should().Be(1);
        page.PageSize.Should().Be(10);
    }

    [Fact]
    public async Task Orders_are_listed_newest_first_with_totals_item_counts_first_item_and_thumbnail()
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        var plain = await ProductNamedAsync($"Plain jug {tag}", 300m);
        var zinnia = await ProductNamedAsync($"Zinnia vase {tag}", 400m);
        var amber = await ProductNamedAsync($"Amber bowl {tag}", 500m);
        var single = await ProductNamedAsync($"Moss cup {tag}", 3500m);
        // Amber is the alphabetically first line of its order: its lowest-SortOrder image must win, and
        // Zinnia's image must not leak in even though Zinnia has more units in the order.
        await AddImagesAsync(
            Image(amber.Id, $"https://img.test/{tag}/amber-second.jpg", 1),
            Image(amber.Id, $"https://img.test/{tag}/amber-first.jpg", 0),
            Image(zinnia.Id, $"https://img.test/{tag}/zinnia.jpg", 0));
        var (client, _) = await SignedInClientAsync(factory);

        await AddOkAsync(client, plain.Id);
        var first = await CheckoutOkAsync(client);
        await AddOkAsync(client, zinnia.Id, quantity: 2);
        await AddOkAsync(client, amber.Id);
        var second = await CheckoutOkAsync(client);
        await AddOkAsync(client, single.Id);
        var third = await CheckoutOkAsync(client);

        var page = await ListAsync(client);

        page.TotalCount.Should().Be(3);
        page.TotalPages.Should().Be(1);
        page.Items.Select(o => o.Number).Should().Equal(third.Number, second.Number, first.Number);

        var (t, s, f) = (page.Items[0], page.Items[1], page.Items[2]);
        // 300 + 120 shipping; 2*400 + 500 + 120 shipping; 3500 is over the free-shipping threshold.
        f.Should().BeEquivalentTo(new { Total = 420m, ItemCount = 1, FirstItemName = plain.Name, FirstItemImageUrl = (string?)null, Status = "Pending" });
        s.Should().BeEquivalentTo(new { Total = 1420m, ItemCount = 3, FirstItemName = amber.Name, FirstItemImageUrl = $"https://img.test/{tag}/amber-first.jpg" });
        t.Should().BeEquivalentTo(new { Total = 3500m, ItemCount = 1, FirstItemName = single.Name, FirstItemImageUrl = (string?)null });

        foreach (var (row, placed) in new[] { (t, third), (s, second), (f, first) })
        {
            row.Total.Should().Be(placed.Total);
            row.CreatedAt.Should().Be(placed.CreatedAt);
        }
    }

    [Fact]
    public async Task Paging_splits_the_history_and_the_last_page_holds_the_oldest_order()
    {
        var (client, _) = await SignedInClientAsync(factory);
        var numbers = new List<string>();
        for (var i = 0; i < 3; i++)
        {
            var product = await CreateProductAsync(factory, InventoryType.Standard, stock: 5);
            await AddOkAsync(client, product.Id);
            numbers.Add((await CheckoutOkAsync(client)).Number);
        }

        var pageOne = await ListAsync(client, "?page=1&pageSize=2");
        var pageTwo = await ListAsync(client, "?page=2&pageSize=2");

        pageOne.Items.Select(o => o.Number).Should().Equal(numbers[2], numbers[1]);
        pageOne.Should().BeEquivalentTo(new { Page = 1, PageSize = 2, TotalCount = 3, TotalPages = 2 });
        pageTwo.Items.Select(o => o.Number).Should().Equal(numbers[0]);
        pageTwo.Should().BeEquivalentTo(new { Page = 2, PageSize = 2, TotalCount = 3, TotalPages = 2 });
    }

    [Fact]
    public async Task Another_customer_sees_none_of_my_orders()
    {
        var (mine, _) = await SignedInClientAsync(factory);
        var product = await CreateProductAsync(factory, InventoryType.Standard, stock: 5);
        await AddOkAsync(mine, product.Id);
        await CheckoutOkAsync(mine);
        var (theirs, _) = await SignedInClientAsync(factory);

        var page = await ListAsync(theirs);

        page.Items.Should().BeEmpty();
        page.TotalCount.Should().Be(0);
        (await ListAsync(mine)).TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task An_order_in_another_store_is_hidden_by_the_store_filter()
    {
        var (client, userId) = await SignedInClientAsync(factory);
        var product = await CreateProductAsync(factory, InventoryType.Standard, stock: 5);
        await AddOkAsync(client, product.Id);
        var own = await CheckoutOkAsync(client);

        var storeB = new Store { Id = Guid.NewGuid(), Name = "Other", Slug = $"other-{Guid.NewGuid():N}" };
        var foreignNumber = $"KB-B-{Guid.NewGuid():N}";
        var (scope, db) = await factory.CreateDbAsync(storeB.Id);
        await using (scope)
        {
            db.Stores.Add(storeB);
            db.Orders.Add(new Order
            {
                Id = Guid.NewGuid(),
                StoreId = storeB.Id,
                UserId = userId,
                Number = foreignNumber,
                Status = OrderStatus.Pending,
                ShippingAddress = new ShippingAddress { FullName = "B", Phone = "01712345678", Line1 = "-", City = "Dhaka" },
                Subtotal = 99m,
                Total = 99m,
                PaymentMethod = PaymentMethod.CashOnDelivery,
                // Newer than the real order, so a leak would show up first on the page.
                CreatedAt = DateTime.UtcNow.AddMinutes(5),
                Items = [new OrderItem { Id = Guid.NewGuid(), ProductId = Guid.NewGuid(), NameSnapshot = "Store B secret", PriceSnapshot = 99m, Quantity = 1 }],
            });
            await db.SaveChangesAsync();
        }

        var page = await ListAsync(client);

        page.Items.Select(o => o.Number).Should().Equal(own.Number);
        page.TotalCount.Should().Be(1);
        page.Items.Should().NotContain(o => o.Number == foreignNumber || o.FirstItemName == "Store B secret");
    }

    [Fact]
    public async Task Listing_orders_without_a_token_is_unauthorized()
    {
        var client = factory.CreateApiClient();

        var response = await client.GetAsync("/api/orders");

        await ShouldBeProblemAsync(response, 401, "UNAUTHORIZED");
    }

    [Theory]
    [InlineData("?pageSize=0", "PageSize")]
    [InlineData("?pageSize=51", "PageSize")]
    [InlineData("?page=0", "Page")]
    [InlineData("?page=1001", "Page")]
    public async Task Out_of_range_paging_is_a_validation_error(string query, string field)
    {
        var (client, _) = await SignedInClientAsync(factory);

        var response = await client.GetAsync("/api/orders" + query);

        var keys = await ValidationErrorKeysAsync(response);
        keys.Should().Contain(k => string.Equals(k, field, StringComparison.OrdinalIgnoreCase));
    }
}
