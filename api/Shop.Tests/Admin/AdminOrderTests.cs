using System.Net.Http.Json;
using FluentAssertions;
using Shop.Application.Admin;
using Shop.Application.Common;
using Shop.Domain.Catalog;
using Shop.Domain.Payments;
using Shop.Tests.Infrastructure;
using static Shop.Tests.Admin.AdminTestHelpers;
using static Shop.Tests.Auth.AuthTestHelpers;
using static Shop.Tests.Carts.CartTestHelpers;
using static Shop.Tests.Orders.CheckoutTestHelpers;

namespace Shop.Tests.Admin;

[Collection(ApiCollection.Name)]
public sealed class AdminOrderTests(ApiFactory factory)
{
    [Fact]
    public async Task Order_moves_Pending_Confirmed_Shipped_Delivered_and_delivery_marks_the_payment_paid()
    {
        var admin = await AdminClientAsync(factory);
        var product = await CreateProductAsync(factory, InventoryType.Standard, stock: 5, price: 300m);
        var placed = await PlaceOrderAsync(factory, (product.Id, 1));

        var pending = await GetOrderOkAsync(admin, placed.Number);
        pending.Order.Status.Should().Be("Pending");
        pending.Order.Number.Should().Be(placed.Number);
        pending.CustomerEmail.Should().EndWith("@kinbo.test");
        pending.NextStatuses.Should().Equal("Confirmed", "Cancelled");

        var confirmed = await SetStatusOkAsync(admin, placed.Number, "Confirmed");
        confirmed.Order.Status.Should().Be("Confirmed");
        confirmed.NextStatuses.Should().Equal("Shipped", "Cancelled");
        (await PaymentStatusOfAsync(factory, placed.Number)).Should().Be(PaymentStatus.Pending);

        var shipped = await SetStatusOkAsync(admin, placed.Number, "Shipped");
        shipped.Order.Status.Should().Be("Shipped");
        shipped.NextStatuses.Should().Equal("Delivered");

        var delivered = await SetStatusOkAsync(admin, placed.Number, "Delivered");
        delivered.Order.Status.Should().Be("Delivered");
        delivered.NextStatuses.Should().BeEmpty();
        (await PaymentStatusOfAsync(factory, placed.Number)).Should().Be(PaymentStatus.Paid);

        await ShouldBeProblemAsync(await SetStatusAsync(admin, placed.Number, "Cancelled"), 409, "INVALID_STATUS_TRANSITION");
        (await GetOrderOkAsync(admin, placed.Number)).Order.Status.Should().Be("Delivered");
        (await PaymentStatusOfAsync(factory, placed.Number)).Should().Be(PaymentStatus.Paid);
        (await StockOfAsync(factory, product.Id)).Should().Be(4, "a refused cancel must not restore stock");
    }

    [Theory]
    [InlineData("Shipped")]
    [InlineData("Delivered")]
    public async Task Skipping_a_step_from_Pending_returns_409_INVALID_STATUS_TRANSITION(string target)
    {
        var admin = await AdminClientAsync(factory);
        var product = await CreateProductAsync(factory, InventoryType.Standard, stock: 5);
        var placed = await PlaceOrderAsync(factory, (product.Id, 1));

        await ShouldBeProblemAsync(await SetStatusAsync(admin, placed.Number, target), 409, "INVALID_STATUS_TRANSITION");
        (await GetOrderOkAsync(admin, placed.Number)).Order.Status.Should().Be("Pending");
    }

    [Fact]
    public async Task Setting_an_unknown_status_returns_400_VALIDATION_FAILED()
    {
        var admin = await AdminClientAsync(factory);
        var product = await CreateProductAsync(factory, InventoryType.Standard, stock: 5);
        var placed = await PlaceOrderAsync(factory, (product.Id, 1));

        await ShouldBeProblemAsync(await SetStatusAsync(admin, placed.Number, "Pending"), 400, "VALIDATION_FAILED");
    }

    [Fact]
    public async Task Unknown_order_number_returns_404_NOT_FOUND()
    {
        var admin = await AdminClientAsync(factory);

        await ShouldBeProblemAsync(await admin.GetAsync("/api/admin/orders/KB-19990101-ZZZZ"), 404, "NOT_FOUND");
        await ShouldBeProblemAsync(await SetStatusAsync(admin, "KB-19990101-ZZZZ", "Confirmed"), 404, "NOT_FOUND");
    }

    [Fact]
    // Regression: two cancels racing both passed the status check and each put the stock back.
    public async Task Concurrent_cancels_restore_stock_exactly_once()
    {
        var admin = await AdminClientAsync(factory);
        for (var round = 0; round < 3; round++)
        {
            var standard = await CreateProductAsync(factory, InventoryType.Standard, stock: 5, price: 200m);
            var placed = await PlaceOrderAsync(factory, (standard.Id, 2));

            var responses = await Task.WhenAll(
                SetStatusAsync(admin, placed.Number, "Cancelled"),
                SetStatusAsync(admin, placed.Number, "Cancelled"));

            responses.Select(r => (int)r.StatusCode).Should().BeEquivalentTo([200, 409]);
            (await StockOfAsync(factory, standard.Id)).Should().Be(5);
        }
    }

    [Fact]
    public async Task Cancelling_restores_standard_stock_frees_the_unique_piece_and_drops_out_of_sales()
    {
        var admin = await AdminClientAsync(factory);
        var standard = await CreateProductAsync(factory, InventoryType.Standard, stock: 5, price: 200m);
        var unique = await CreateUniqueAsync(factory);
        var before = await DashboardAsync(admin);

        var placed = await PlaceOrderAsync(factory, (standard.Id, 2), (unique.Id, 1));
        (await StockOfAsync(factory, standard.Id)).Should().Be(3);
        (await StockOfAsync(factory, unique.Id)).Should().Be(0);
        (await DashboardAsync(admin)).SalesToday.Should().Be(before.SalesToday + placed.Total);

        var cancelled = await SetStatusOkAsync(admin, placed.Number, "Cancelled");

        cancelled.Order.Status.Should().Be("Cancelled");
        cancelled.NextStatuses.Should().BeEmpty();
        (await StockOfAsync(factory, standard.Id)).Should().Be(5);
        (await StockOfAsync(factory, unique.Id)).Should().Be(1);
        (await PaymentStatusOfAsync(factory, placed.Number)).Should().Be(PaymentStatus.Cancelled);

        var after = await DashboardAsync(admin);
        after.SalesToday.Should().Be(before.SalesToday);
        after.SalesThisWeek.Should().Be(before.SalesThisWeek);
        after.OrdersToday.Should().Be(before.OrdersToday);
        after.PendingOrders.Should().Be(before.PendingOrders);

        // The piece is for sale again: another shopper can hold and buy it.
        var (shopper, _) = await SignedInClientAsync(factory);
        var cart = await AddOkAsync(shopper, unique.Id);
        cart.Items.Should().ContainSingle(i => i.ProductId == unique.Id);
        await CheckoutOkAsync(shopper);
        (await StockOfAsync(factory, unique.Id)).Should().Be(0);

        // A cancelled order can't be cancelled again (which would add the stock back twice).
        await ShouldBeProblemAsync(await SetStatusAsync(admin, placed.Number, "Cancelled"), 409, "INVALID_STATUS_TRANSITION");
        (await StockOfAsync(factory, standard.Id)).Should().Be(5);
    }

    [Fact]
    public async Task Cancelling_a_confirmed_order_also_restores_stock()
    {
        var admin = await AdminClientAsync(factory);
        var standard = await CreateProductAsync(factory, InventoryType.Standard, stock: 4);
        var placed = await PlaceOrderAsync(factory, (standard.Id, 3));
        await SetStatusOkAsync(admin, placed.Number, "Confirmed");

        await SetStatusOkAsync(admin, placed.Number, "Cancelled");

        (await StockOfAsync(factory, standard.Id)).Should().Be(4);
    }

    [Fact]
    public async Task Order_list_filters_by_status_and_searches_by_number()
    {
        var admin = await AdminClientAsync(factory);
        var product = await CreateProductAsync(factory, InventoryType.Standard, stock: 5, price: 150m);
        var placed = await PlaceOrderAsync(factory, (product.Id, 2));

        var found = await ReadOkAsync<Paged<AdminOrderListItemDto>>(await admin.GetAsync($"/api/admin/orders?q={placed.Number}"));
        var row = found.Items.Should().ContainSingle().Subject;
        row.Number.Should().Be(placed.Number);
        row.Status.Should().Be("Pending");
        row.ItemCount.Should().Be(2);
        row.Total.Should().Be(placed.Total);
        row.CustomerName.Should().Be(placed.ShippingAddress.FullName);

        (await ReadOkAsync<Paged<AdminOrderListItemDto>>(await admin.GetAsync($"/api/admin/orders?status=Pending&q={placed.Number}")))
            .Items.Should().ContainSingle();
        (await ReadOkAsync<Paged<AdminOrderListItemDto>>(await admin.GetAsync($"/api/admin/orders?status=Shipped&q={placed.Number}")))
            .Items.Should().BeEmpty();
        await ShouldBeProblemAsync(await admin.GetAsync("/api/admin/orders?status=Lost"), 400, "VALIDATION_FAILED");
    }

    [Fact]
    public async Task Customer_sees_the_status_the_admin_set()
    {
        var admin = await AdminClientAsync(factory);
        var product = await CreateProductAsync(factory, InventoryType.Standard, stock: 5);
        var (customer, _) = await SignedInClientAsync(factory);
        await AddOkAsync(customer, product.Id);
        var placed = await CheckoutOkAsync(customer);

        await SetStatusOkAsync(admin, placed.Number, "Confirmed");

        var own = await ReadOrderAsync(await customer.GetAsync($"/api/orders/{placed.Number}"));
        own.Status.Should().Be("Confirmed");
    }
}
