using System.Net;
using FluentAssertions;
using Shop.Domain.Catalog;
using Shop.Tests.Infrastructure;
using static Shop.Tests.Auth.AuthTestHelpers;
using static Shop.Tests.Carts.CartTestHelpers;
using static Shop.Tests.Orders.CheckoutTestHelpers;

namespace Shop.Tests.Orders;

[Collection(ApiCollection.Name)]
public sealed class CheckoutConcurrencyTests(ApiFactory factory)
{
    private const int Rounds = 5;

    [Fact]
    public async Task Two_shoppers_checking_out_the_last_unit_at_once_never_oversell()
    {
        for (var round = 0; round < Rounds; round++)
        {
            var product = await CreateProductAsync(factory, InventoryType.Standard, stock: 1, price: 100m);
            var shoppers = await ShoppersHoldingOneEachAsync(product.Id, count: 2);

            var responses = await CheckoutAllAtOnceAsync(shoppers.Select(s => s.Client));

            var winners = responses.Where(r => r.StatusCode == HttpStatusCode.OK).ToList();
            winners.Should().ContainSingle($"round {round}: only one unit exists");
            foreach (var loser in responses.Except(winners))
                await ShouldBeProblemAsync(loser, 409, "OUT_OF_STOCK");

            (await StockOfAsync(factory, product.Id)).Should().Be(0, $"round {round}");
            (await OrderLinesForProductAsync(factory, product.Id)).Should().Be(1, $"round {round}");
            (await OrderCountAsync(factory, shoppers[0].UserId) + await OrderCountAsync(factory, shoppers[1].UserId))
                .Should().Be(1, $"round {round}: the loser's order must be rolled back");
        }
    }

    [Fact]
    // Regression: a double submit used to reach the cart clean-up twice and fail with a 500.
    public async Task Double_submit_of_one_cart_places_exactly_one_order()
    {
        for (var round = 0; round < 3; round++)
        {
            var product = await CreateProductAsync(factory, InventoryType.Standard, stock: 5, price: 100m);
            var (client, userId) = await SignedInClientAsync(factory);
            await AddOkAsync(client, product.Id);

            var responses = await Task.WhenAll(CheckoutAsync(client), CheckoutAsync(client));

            responses.Select(r => (int)r.StatusCode).Should().BeEquivalentTo([200, 400]);
            (await OrderCountAsync(factory, userId)).Should().Be(1);
            (await StockOfAsync(factory, product.Id)).Should().Be(4);
        }
    }

    [Fact]
    public async Task Two_shoppers_checking_out_when_two_units_remain_both_succeed_via_the_retry()
    {
        for (var round = 0; round < Rounds; round++)
        {
            var product = await CreateProductAsync(factory, InventoryType.Standard, stock: 2, price: 100m);
            var shoppers = await ShoppersHoldingOneEachAsync(product.Id, count: 2);

            var responses = await CheckoutAllAtOnceAsync(shoppers.Select(s => s.Client));

            foreach (var response in responses)
            {
                var order = await ReadOrderAsync(response);
                order.Items.Should().ContainSingle(i => i.ProductId == product.Id && i.Quantity == 1);
            }
            (await StockOfAsync(factory, product.Id)).Should().Be(0, $"round {round}");
            (await OrderLinesForProductAsync(factory, product.Id)).Should().Be(2, $"round {round}");
        }
    }

    private async Task<List<(HttpClient Client, Guid UserId)>> ShoppersHoldingOneEachAsync(Guid productId, int count)
    {
        var shoppers = new List<(HttpClient, Guid)>();
        for (var i = 0; i < count; i++)
        {
            var (client, userId) = await SignedInClientAsync(factory);
            await AddOkAsync(client, productId);
            shoppers.Add((client, userId));
        }
        return shoppers;
    }

    /// <summary>All checkouts wait on one barrier, so they hit the server together.</summary>
    private static async Task<HttpResponseMessage[]> CheckoutAllAtOnceAsync(IEnumerable<HttpClient> clients)
    {
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var attempts = clients.Select(async client =>
        {
            await start.Task;
            return await CheckoutAsync(client);
        }).ToList();
        start.SetResult();
        return await Task.WhenAll(attempts);
    }
}
