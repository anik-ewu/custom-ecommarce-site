using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Shop.Domain.Catalog;
using Shop.Domain.Stores;
using Shop.Tests.Infrastructure;
using static Shop.Tests.Auth.AuthTestHelpers;
using static Shop.Tests.Carts.CartTestHelpers;

namespace Shop.Tests.Carts;

[Collection(ApiCollection.Name)]
public sealed class CartTests(ApiFactory factory)
{
    [Fact]
    public async Task Get_without_a_cart_returns_an_empty_cart()
    {
        var cart = await GetCartAsync(factory.CreateApiClient());

        cart.Items.Should().BeEmpty();
        cart.ItemCount.Should().Be(0);
        cart.Subtotal.Should().Be(0);
    }

    [Fact]
    public async Task Standard_item_quantity_is_limited_by_stock()
    {
        var product = await CreateProductAsync(factory, InventoryType.Standard, stock: 3, price: 10m);
        var client = factory.CreateApiClient();

        await ShouldBeProblemAsync(await AddAsync(client, product.Id, quantity: 4), 409, "OUT_OF_STOCK");

        var cart = await AddOkAsync(client, product.Id, quantity: 2);
        var item = cart.Items.Should().ContainSingle().Subject;
        item.Quantity.Should().Be(2);
        item.ReservedUntil.Should().BeNull();
        item.Available.Should().BeTrue();
        cart.Subtotal.Should().Be(20m);

        // Adding again sums quantities, so 2 + 2 exceeds the stock of 3.
        await ShouldBeProblemAsync(await AddAsync(client, product.Id, quantity: 2), 409, "OUT_OF_STOCK");

        var patched = await ReadCartAsync(await client.PatchAsJsonAsync($"/api/cart/items/{item.Id}", new { quantity = 3 }));
        patched.Items.Single().Quantity.Should().Be(3);
        patched.ItemCount.Should().Be(3);
        patched.Subtotal.Should().Be(30m);

        await ShouldBeProblemAsync(
            await client.PatchAsJsonAsync($"/api/cart/items/{item.Id}", new { quantity = 4 }), 409, "OUT_OF_STOCK");
        (await GetCartAsync(client)).Items.Single().Quantity.Should().Be(3);
    }

    [Fact]
    public async Task Removing_an_item_from_another_cart_returns_not_found()
    {
        var product = await CreateProductAsync(factory, InventoryType.Standard, stock: 5);
        var owner = factory.CreateApiClient();
        var cart = await AddOkAsync(owner, product.Id);

        var response = await factory.CreateApiClient().DeleteAsync($"/api/cart/items/{cart.Items.Single().Id}");

        await ShouldBeProblemAsync(response, 404, "NOT_FOUND");
        (await GetCartAsync(owner)).Items.Should().ContainSingle();
    }

    [Fact]
    public async Task Signing_up_merges_the_anonymous_cart_and_keeps_the_hold()
    {
        var unique = await CreateUniqueAsync(factory);
        var standard = await CreateProductAsync(factory, InventoryType.Standard, stock: 5);
        var client = factory.CreateApiClient();

        var firstAdd = await AddAsync(client, unique.Id);
        var anonymousId = CartCookieValue(firstAdd);
        var anonymousCart = await ReadCartAsync(firstAdd);
        var heldUntil = anonymousCart.Items.Single().ReservedUntil;
        await AddOkAsync(client, standard.Id, quantity: 2);
        var holdBefore = (await ReservationsForAsync(factory, unique.Id)).Single();

        var auth = await ReadAuthAsync(await RegisterAsync(client, UniqueEmail()));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        var cart = await GetCartAsync(client);
        cart.Items.Should().HaveCount(2);
        var piece = cart.Items.Single(i => i.ProductId == unique.Id);
        piece.Available.Should().BeTrue();
        piece.ReservedUntil.Should().Be(heldUntil);
        cart.Items.Single(i => i.ProductId == standard.Id).Quantity.Should().Be(2);

        var (scope, db) = await factory.CreateDbAsync();
        await using (scope)
        {
            var userCartId = await db.Carts.Where(c => c.UserId == auth.User.Id).Select(c => c.Id).SingleAsync();
            (await db.Carts.AnyAsync(c => c.AnonymousId == anonymousId)).Should().BeFalse();
            var holdAfter = await db.Reservations.AsNoTracking().SingleAsync(r => r.ProductId == unique.Id);
            holdAfter.Id.Should().Be(holdBefore.Id);
            holdAfter.CartId.Should().Be(userCartId);
            holdAfter.ExpiresAt.Should().Be(holdBefore.ExpiresAt);
        }
    }

    [Fact]
    public async Task Logging_in_merges_the_anonymous_cart_into_an_existing_user_cart()
    {
        var email = UniqueEmail();
        var existing = await CreateProductAsync(factory, InventoryType.Standard, stock: 5);
        var unique = await CreateUniqueAsync(factory);

        var device1 = factory.CreateApiClient();
        var registered = await ReadAuthAsync(await RegisterAsync(device1, email));
        device1.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", registered.AccessToken);
        await AddOkAsync(device1, existing.Id, quantity: 2);

        var device2 = factory.CreateApiClient();
        var anonymousId = CartCookieValue(await AddAsync(device2, unique.Id));
        await AddOkAsync(device2, existing.Id, quantity: 4);
        var holdBefore = (await ReservationsForAsync(factory, unique.Id)).Single();

        var auth = await ReadAuthAsync(await LoginAsync(device2, email, "Strong-pass-123"));
        device2.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        var cart = await GetCartAsync(device2);
        cart.Items.Should().HaveCount(2);
        cart.Items.Single(i => i.ProductId == unique.Id).Available.Should().BeTrue();
        // 2 + 4 is capped at the stock of 5.
        cart.Items.Single(i => i.ProductId == existing.Id).Quantity.Should().Be(5);

        var (scope, db) = await factory.CreateDbAsync();
        await using (scope)
        {
            var userCartId = await db.Carts.Where(c => c.UserId == auth.User.Id).Select(c => c.Id).SingleAsync();
            (await db.Carts.AnyAsync(c => c.AnonymousId == anonymousId)).Should().BeFalse();
            var holdAfter = await db.Reservations.AsNoTracking().SingleAsync(r => r.ProductId == unique.Id);
            holdAfter.Id.Should().Be(holdBefore.Id);
            holdAfter.CartId.Should().Be(userCartId);
        }
    }

    [Fact]
    public async Task Adding_a_product_from_another_store_returns_not_found()
    {
        var storeB = new Store { Id = Guid.NewGuid(), Name = "Other", Slug = $"other-{Guid.NewGuid():N}" };
        var categoryB = new Category { Id = Guid.NewGuid(), StoreId = storeB.Id, Name = "Elsewhere", Slug = "elsewhere" };
        var productB = new Product
        {
            Id = Guid.NewGuid(),
            StoreId = storeB.Id,
            CategoryId = categoryB.Id,
            Name = "Store B piece",
            Slug = $"store-b-{Guid.NewGuid():N}",
            Description = "-",
            Materials = "-",
            Dimensions = "-",
            Price = 10,
            InventoryType = InventoryType.Unique,
            StockQuantity = 1,
            CreatedAt = DateTime.UtcNow,
        };
        var (scope, db) = await factory.CreateDbAsync(storeB.Id);
        await using (scope)
        {
            db.AddRange(storeB, categoryB, productB);
            await db.SaveChangesAsync();
        }

        await ShouldBeProblemAsync(await AddAsync(factory.CreateApiClient(), productB.Id), 404, "NOT_FOUND");

        (await ReservationsForAsync(factory, productB.Id)).Should().BeEmpty();
    }
}
