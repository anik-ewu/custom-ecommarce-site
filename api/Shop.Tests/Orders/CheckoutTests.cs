using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Shop.Domain.Catalog;
using Shop.Domain.Payments;
using Shop.Tests.Infrastructure;
using static Shop.Tests.Auth.AuthTestHelpers;
using static Shop.Tests.Carts.CartTestHelpers;
using static Shop.Tests.Orders.CheckoutTestHelpers;

namespace Shop.Tests.Orders;

[Collection(ApiCollection.Name)]
public sealed class CheckoutTests(ApiFactory factory)
{
    [Fact]
    public async Task Cod_checkout_with_unique_and_standard_items_creates_a_pending_order_and_clears_the_cart()
    {
        var unique = await CreateProductAsync(factory, InventoryType.Unique, stock: 1, price: 1000m);
        var standard = await CreateProductAsync(factory, InventoryType.Standard, stock: 5, price: 250m);
        var (client, userId) = await SignedInClientAsync(factory);
        await AddOkAsync(client, unique.Id);
        await AddOkAsync(client, standard.Id, quantity: 2);
        var cartId = await CartIdOfUserAsync(factory, userId);

        var summary = await SummaryAsync(client);
        summary.Should().BeEquivalentTo(new { Subtotal = 1500m, ShippingFee = 120m, Total = 1620m, FreeShippingThreshold = 3000m, ItemCount = 3 });

        var order = await CheckoutOkAsync(client);

        order.Status.Should().Be("Pending");
        order.PaymentMethod.Should().Be("CashOnDelivery");
        order.Number.Should().MatchRegex(OrderNumberPattern);
        order.Number.Should().StartWith($"KB-{order.CreatedAt.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}-");
        order.ShippingAddress.Phone.Should().Be("01712345678");
        order.ShippingAddress.City.Should().Be("Dhaka");
        order.Items.Should().BeEquivalentTo(new[]
        {
            new { ProductId = unique.Id, Name = unique.Name, Price = 1000m, Quantity = 1, LineTotal = 1000m },
            new { ProductId = standard.Id, Name = standard.Name, Price = 250m, Quantity = 2, LineTotal = 500m },
        });
        order.Subtotal.Should().Be(1500m);
        order.ShippingFee.Should().Be(120m);
        order.Total.Should().Be(1620m);

        var (scope, db) = await factory.CreateDbAsync();
        await using (scope)
        {
            (await db.Products.AsNoTracking().SingleAsync(p => p.Id == unique.Id)).StockQuantity.Should().Be(0);
            (await db.Products.AsNoTracking().SingleAsync(p => p.Id == standard.Id)).StockQuantity.Should().Be(3);
            (await db.CartItems.CountAsync(i => i.CartId == cartId)).Should().Be(0);
            (await db.Reservations.CountAsync(r => r.CartId == cartId)).Should().Be(0);
            (await db.Reservations.CountAsync(r => r.ProductId == unique.Id)).Should().Be(0);

            var stored = await db.Orders.AsNoTracking().Include(o => o.Items).SingleAsync(o => o.Number == order.Number);
            stored.UserId.Should().Be(userId);
            stored.Items.Should().BeEquivalentTo(new[]
            {
                new { ProductId = unique.Id, NameSnapshot = unique.Name, PriceSnapshot = 1000m, Quantity = 1 },
                new { ProductId = standard.Id, NameSnapshot = standard.Name, PriceSnapshot = 250m, Quantity = 2 },
            });
            var payment = await db.Payments.AsNoTracking().SingleAsync(p => p.OrderId == stored.Id);
            payment.Provider.Should().Be("cod");
            payment.Status.Should().Be(PaymentStatus.Pending);
            payment.Amount.Should().Be(1620m);
        }

        var fetched = await ReadOrderAsync(await client.GetAsync($"/api/orders/{order.Number}"));
        fetched.Should().BeEquivalentTo(order);

        var (stranger, _) = await SignedInClientAsync(factory);
        await ShouldBeProblemAsync(await stranger.GetAsync($"/api/orders/{order.Number}"), 404, "NOT_FOUND");
    }

    [Fact]
    public async Task Order_snapshots_survive_a_later_product_rename_and_price_change()
    {
        var product = await CreateProductAsync(factory, InventoryType.Standard, stock: 3, price: 400m);
        var (client, _) = await SignedInClientAsync(factory);
        await AddOkAsync(client, product.Id);
        var order = await CheckoutOkAsync(client);

        var (scope, db) = await factory.CreateDbAsync();
        await using (scope)
            await db.Products.Where(p => p.Id == product.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.Price, 999m).SetProperty(p => p.Name, "Renamed"));

        var fetched = await ReadOrderAsync(await client.GetAsync($"/api/orders/{order.Number}"));
        fetched.Items.Single().Should().BeEquivalentTo(new { Name = product.Name, Price = 400m, LineTotal = 400m });
        fetched.Total.Should().Be(520m);
    }

    [Theory]
    [InlineData(1500, 2, 3000, 0)] // exactly at the threshold: free
    [InlineData(2000, 2, 4000, 0)] // above: free
    [InlineData(2999.99, 1, 2999.99, 120)] // just below: flat fee
    public async Task Shipping_fee_depends_on_the_free_shipping_threshold(
        double unitPrice, int quantity, double expectedSubtotal, double expectedFee)
    {
        var product = await CreateProductAsync(factory, InventoryType.Standard, stock: 5, price: (decimal)unitPrice);
        var (client, userId) = await SignedInClientAsync(factory);
        await AddOkAsync(client, product.Id, quantity);

        var order = await CheckoutOkAsync(client);

        order.Subtotal.Should().Be((decimal)expectedSubtotal);
        order.ShippingFee.Should().Be((decimal)expectedFee);
        order.Total.Should().Be((decimal)expectedSubtotal + (decimal)expectedFee);

        var (scope, db) = await factory.CreateDbAsync();
        await using (scope)
        {
            var stored = await db.Orders.AsNoTracking().SingleAsync(o => o.UserId == userId);
            (await db.Payments.AsNoTracking().SingleAsync(p => p.OrderId == stored.Id)).Amount.Should().Be(order.Total);
        }
    }

    [Fact]
    public async Task Changed_price_returns_PRICE_CHANGED_then_the_next_checkout_uses_the_new_price()
    {
        var product = await CreateProductAsync(factory, InventoryType.Standard, stock: 5, price: 500m);
        var (client, userId) = await SignedInClientAsync(factory);
        await AddOkAsync(client, product.Id);

        var (scope, db) = await factory.CreateDbAsync();
        await using (scope)
            await db.Products.Where(p => p.Id == product.Id).ExecuteUpdateAsync(s => s.SetProperty(p => p.Price, 650m));

        await ShouldBeProblemAsync(await CheckoutAsync(client), 409, "PRICE_CHANGED");
        (await OrderCountAsync(factory, userId)).Should().Be(0);
        (await StockOfAsync(factory, product.Id)).Should().Be(5, "a refused checkout must not touch stock");

        var summary = await SummaryAsync(client);
        summary.Subtotal.Should().Be(650m);
        summary.Total.Should().Be(770m);
        var cart = await GetCartAsync(client);
        cart.Items.Single().UnitPrice.Should().Be(650m, "the cart accepted the new price");

        var order = await CheckoutOkAsync(client);
        order.Items.Single().Price.Should().Be(650m);
        order.Subtotal.Should().Be(650m);
        order.Total.Should().Be(770m);
        (await OrderCountAsync(factory, userId)).Should().Be(1);
        (await StockOfAsync(factory, product.Id)).Should().Be(4);
    }

    [Fact]
    public async Task Checkout_of_a_piece_whose_expired_hold_was_claimed_by_another_cart_returns_ITEM_RESERVED()
    {
        var product = await CreateUniqueAsync(factory);
        var (shopperA, userA) = await SignedInClientAsync(factory);
        await AddOkAsync(shopperA, product.Id);
        await ExpireHoldAsync(factory, product.Id);

        var shopperB = factory.CreateApiClient();
        var claim = await AddAsync(shopperB, product.Id);
        var anonymousB = CartCookieValue(claim);
        (await ReadCartAsync(claim)).Items.Single().ReservedUntil.Should().NotBeNull();

        await ShouldBeProblemAsync(await CheckoutAsync(shopperA), 409, "ITEM_RESERVED");

        (await OrderCountAsync(factory, userA)).Should().Be(0);
        (await StockOfAsync(factory, product.Id)).Should().Be(1);
        var (scope, db) = await factory.CreateDbAsync();
        await using (scope)
        {
            var cartB = await db.Carts.AsNoTracking().SingleAsync(c => c.AnonymousId == anonymousB);
            var hold = (await ReservationsForAsync(factory, product.Id)).Should().ContainSingle().Subject;
            hold.CartId.Should().Be(cartB.Id);
            hold.ExpiresAt.Should().BeAfter(DateTime.UtcNow);
        }
    }

    [Fact]
    // The shopper pays only for what the checkout page shows: an expired hold drops the piece from the total,
    // so checkout must not quietly re-claim it and charge more. Holding it again from the cart fixes it.
    public async Task Checkout_with_an_expired_hold_is_refused_until_the_piece_is_held_again()
    {
        var product = await CreateUniqueAsync(factory);
        var (client, userId) = await SignedInClientAsync(factory);
        await AddOkAsync(client, product.Id);
        await ExpireHoldAsync(factory, product.Id);

        await ShouldBeProblemAsync(await CheckoutAsync(client), 409, "ITEM_RESERVED");
        (await OrderCountAsync(factory, userId)).Should().Be(0);
        (await StockOfAsync(factory, product.Id)).Should().Be(1);

        await AddOkAsync(client, product.Id);
        var order = await CheckoutOkAsync(client);

        order.Items.Should().ContainSingle(i => i.ProductId == product.Id);
        (await StockOfAsync(factory, product.Id)).Should().Be(0);
        (await ReservationsForAsync(factory, product.Id)).Should().BeEmpty();
    }

    [Fact]
    public async Task Checkout_with_an_empty_cart_returns_400_VALIDATION_FAILED()
    {
        var (client, userId) = await SignedInClientAsync(factory);

        await ShouldBeProblemAsync(await CheckoutAsync(client), 400, "VALIDATION_FAILED");
        (await OrderCountAsync(factory, userId)).Should().Be(0);
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("01212345678")] // operator digit must be 3-9
    [InlineData("0171234567")] // one digit short
    [InlineData("017123456789")] // one digit long
    public async Task Checkout_with_a_bad_phone_returns_400_with_an_error_on_the_phone_field(string phone)
    {
        var product = await CreateProductAsync(factory, InventoryType.Standard, stock: 2);
        var (client, userId) = await SignedInClientAsync(factory);
        await AddOkAsync(client, product.Id);

        var keys = await ValidationErrorKeysAsync(await CheckoutAsync(client, CheckoutBody(phone: phone)));

        keys.Should().ContainSingle().Which.Should().EndWithEquivalentOf("phone");
        (await OrderCountAsync(factory, userId)).Should().Be(0);
        (await StockOfAsync(factory, product.Id)).Should().Be(2);
    }

    [Theory]
    [InlineData("01712345678")]
    [InlineData("+8801712345678")]
    [InlineData("8801912345678")]
    public async Task Checkout_accepts_bangladeshi_mobile_formats(string phone)
    {
        var product = await CreateProductAsync(factory, InventoryType.Standard, stock: 2);
        var (client, _) = await SignedInClientAsync(factory);
        await AddOkAsync(client, product.Id);

        var order = await ReadOrderAsync(await CheckoutAsync(client, CheckoutBody(phone: phone)));

        order.ShippingAddress.Phone.Should().Be(phone);
    }

    [Fact]
    public async Task Checkout_with_card_payment_returns_400_VALIDATION_FAILED()
    {
        var product = await CreateProductAsync(factory, InventoryType.Standard, stock: 2);
        var (client, userId) = await SignedInClientAsync(factory);
        await AddOkAsync(client, product.Id);

        var keys = await ValidationErrorKeysAsync(await CheckoutAsync(client, CheckoutBody(paymentMethod: "Card")));

        keys.Should().ContainSingle().Which.Should().BeEquivalentTo("paymentMethod");
        (await OrderCountAsync(factory, userId)).Should().Be(0);
        (await StockOfAsync(factory, product.Id)).Should().Be(2);
    }

    [Fact]
    public async Task Checkout_endpoints_without_a_token_return_401_UNAUTHORIZED()
    {
        var client = factory.CreateApiClient();

        await ShouldBeProblemAsync(await CheckoutAsync(client), 401, "UNAUTHORIZED");
        await ShouldBeProblemAsync(await client.GetAsync("/api/checkout/summary"), 401, "UNAUTHORIZED");
        await ShouldBeProblemAsync(await client.GetAsync("/api/orders/KB-20260101-ABCD"), 401, "UNAUTHORIZED");
    }

    [Fact]
    public async Task Unknown_order_number_returns_404_NOT_FOUND()
    {
        var (client, _) = await SignedInClientAsync(factory);

        var response = await client.GetAsync("/api/orders/KB-19990101-ZZZZ");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await ShouldBeProblemAsync(response, 404, "NOT_FOUND");
    }

    [Fact]
    public async Task Anonymous_cart_merged_at_registration_can_be_checked_out()
    {
        var product = await CreateProductAsync(factory, InventoryType.Standard, stock: 4, price: 300m);
        var client = factory.CreateApiClient();
        await AddOkAsync(client, product.Id, quantity: 2);
        var auth = await ReadAuthAsync(await RegisterAsync(client, UniqueEmail()));
        client.DefaultRequestHeaders.Authorization = new("Bearer", auth.AccessToken);

        var order = await CheckoutOkAsync(client);

        order.Items.Single().Should().BeEquivalentTo(new { ProductId = product.Id, Quantity = 2, LineTotal = 600m });
        (await StockOfAsync(factory, product.Id)).Should().Be(2);
        var cart = await client.GetFromJsonAsync<Shop.Application.Carts.CartDto>("/api/cart");
        cart!.Items.Should().BeEmpty();
    }
}
