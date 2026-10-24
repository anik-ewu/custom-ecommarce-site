using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Shop.Application.Catalog;
using Shop.Tests.Infrastructure;
using static Shop.Tests.Auth.AuthTestHelpers;
using static Shop.Tests.Carts.CartTestHelpers;

namespace Shop.Tests.Carts;

[Collection(ApiCollection.Name)]
public sealed class ReservationTests(ApiFactory factory)
{
    private const int Contenders = 10;
    private const int Rounds = 5;

    [Fact]
    public async Task Concurrent_adds_of_one_unique_piece_have_exactly_one_winner()
    {
        for (var round = 0; round < Rounds; round++)
        {
            var product = await CreateUniqueAsync(factory);
            var clients = Enumerable.Range(0, Contenders).Select(_ => factory.CreateApiClient()).ToList();
            var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            var attempts = clients.Select(async client =>
            {
                await start.Task;
                return await AddAsync(client, product.Id);
            }).ToList();
            start.SetResult();
            var responses = await Task.WhenAll(attempts);

            var winners = responses.Where(r => r.StatusCode == HttpStatusCode.OK).ToList();
            winners.Should().ContainSingle($"round {round}: exactly one cart may hold the piece");
            foreach (var loser in responses.Except(winners))
                await ShouldBeProblemAsync(loser, 409, "ITEM_RESERVED");

            var winnerCart = await ReadCartAsync(winners[0]);
            winnerCart.Items.Should().ContainSingle(i => i.ProductId == product.Id && i.ReservedUntil != null);
            (await ReservationsForAsync(factory, product.Id)).Should().ContainSingle($"round {round}");
        }
    }

    [Fact]
    public async Task Re_adding_a_held_piece_succeeds_without_extending_the_hold()
    {
        var product = await CreateUniqueAsync(factory);
        var client = factory.CreateApiClient();

        var first = await AddOkAsync(client, product.Id);
        var heldUntil = first.Items.Single().ReservedUntil;
        heldUntil.Should().NotBeNull();

        var second = await AddOkAsync(client, product.Id);

        var item = second.Items.Should().ContainSingle().Subject;
        item.Quantity.Should().Be(1);
        item.ReservedUntil.Should().Be(heldUntil);
        (await ReservationsForAsync(factory, product.Id)).Single().ExpiresAt.Should().Be(heldUntil!.Value);
    }

    [Fact]
    public async Task Expired_hold_can_be_claimed_by_another_cart_and_shows_unavailable_to_the_first()
    {
        var product = await CreateUniqueAsync(factory);
        var cartA = factory.CreateApiClient();
        var cartB = factory.CreateApiClient();
        await AddOkAsync(cartA, product.Id);
        await ShouldBeProblemAsync(await AddAsync(cartB, product.Id), 409, "ITEM_RESERVED");

        await ExpireHoldAsync(factory, product.Id);

        var claimed = await AddOkAsync(cartB, product.Id);
        claimed.Items.Single().ReservedUntil.Should().NotBeNull();
        var a = await GetCartAsync(cartA);
        var stale = a.Items.Should().ContainSingle().Subject;
        stale.ProductId.Should().Be(product.Id);
        stale.Available.Should().BeFalse();
        stale.ReservedUntil.Should().BeNull();
        a.Subtotal.Should().Be(0);
    }

    [Fact]
    public async Task Product_detail_shows_hold_while_active_and_ignores_expired_hold()
    {
        var product = await CreateUniqueAsync(factory);
        var reader = factory.CreateApiClient();
        (await reader.GetFromJsonAsync<ProductDetailDto>($"/api/products/{product.Slug}"))!.ReservedUntil.Should().BeNull();

        var cart = await AddOkAsync(factory.CreateApiClient(), product.Id);

        (await reader.GetFromJsonAsync<ProductDetailDto>($"/api/products/{product.Slug}"))!.ReservedUntil
            .Should().Be(cart.Items.Single().ReservedUntil);

        await ExpireHoldAsync(factory, product.Id);

        (await reader.GetFromJsonAsync<ProductDetailDto>($"/api/products/{product.Slug}"))!.ReservedUntil.Should().BeNull();
    }

    [Fact]
    public async Task Removing_a_unique_piece_releases_it_immediately()
    {
        var product = await CreateUniqueAsync(factory);
        var holder = factory.CreateApiClient();
        var other = factory.CreateApiClient();
        var cart = await AddOkAsync(holder, product.Id);

        var afterRemove = await ReadCartAsync(await holder.DeleteAsync($"/api/cart/items/{cart.Items.Single().Id}"));

        afterRemove.Items.Should().BeEmpty();
        (await ReservationsForAsync(factory, product.Id)).Should().BeEmpty();
        (await AddOkAsync(other, product.Id)).Items.Should().ContainSingle(i => i.ProductId == product.Id && i.Available);
    }

    [Fact]
    public async Task Adding_a_unique_piece_with_quantity_two_is_rejected()
    {
        var product = await CreateUniqueAsync(factory);

        await ShouldBeProblemAsync(await AddAsync(factory.CreateApiClient(), product.Id, quantity: 2), 400, "VALIDATION_FAILED");

        (await ReservationsForAsync(factory, product.Id)).Should().BeEmpty();
    }

    [Fact]
    public async Task Changing_quantity_of_a_unique_piece_is_rejected()
    {
        var product = await CreateUniqueAsync(factory);
        var client = factory.CreateApiClient();
        var cart = await AddOkAsync(client, product.Id);

        var response = await client.PatchAsJsonAsync($"/api/cart/items/{cart.Items.Single().Id}", new { quantity = 2 });

        await ShouldBeProblemAsync(response, 400, "VALIDATION_FAILED");
        (await GetCartAsync(client)).Items.Single().Quantity.Should().Be(1);
    }
}
