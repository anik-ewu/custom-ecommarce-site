using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Shop.Application.Carts;
using Shop.Domain.Carts;
using Shop.Domain.Catalog;
using Shop.Tests.Infrastructure;

namespace Shop.Tests.Carts;

internal static class CartTestHelpers
{
    public const string CartCookie = "kinbo_cart";

    /// <summary>Creates a fresh product in the seeded store, so no other test can touch its stock or hold.</summary>
    public static async Task<Product> CreateProductAsync(ApiFactory factory, InventoryType type, int stock = 1, decimal price = 25m)
    {
        var (scope, db) = await factory.CreateDbAsync();
        await using (scope)
        {
            // Own category, sorted last, so catalog tests that count seeded categories/products aren't affected.
            var category = new Category { Id = Guid.NewGuid(), Name = "Cart test", Slug = $"cart-test-{Guid.NewGuid():N}", SortOrder = 10_000 };
            var slug = $"test-{type.ToString().ToLowerInvariant()}-{Guid.NewGuid():N}";
            var product = new Product
            {
                Id = Guid.NewGuid(),
                CategoryId = category.Id,
                Name = $"Test {type} piece",
                Slug = slug,
                Description = "-",
                Materials = "-",
                Dimensions = "-",
                Price = price,
                InventoryType = type,
                StockQuantity = stock,
                CreatedAt = DateTime.UtcNow,
            };
            db.AddRange(category, product);
            await db.SaveChangesAsync();
            return product;
        }
    }

    public static Task<Product> CreateUniqueAsync(ApiFactory factory) => CreateProductAsync(factory, InventoryType.Unique);

    public static Task<HttpResponseMessage> AddAsync(HttpClient client, Guid productId, int quantity = 1) =>
        client.PostAsJsonAsync("/api/cart/items", new { productId, quantity });

    public static async Task<CartDto> ReadCartAsync(HttpResponseMessage response)
    {
        var raw = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, raw);
        var cart = await response.Content.ReadFromJsonAsync<CartDto>();
        cart.Should().NotBeNull();
        return cart!;
    }

    public static async Task<CartDto> AddOkAsync(HttpClient client, Guid productId, int quantity = 1) =>
        await ReadCartAsync(await AddAsync(client, productId, quantity));

    public static async Task<CartDto> GetCartAsync(HttpClient client) => await ReadCartAsync(await client.GetAsync("/api/cart"));

    public static async Task<List<Reservation>> ReservationsForAsync(ApiFactory factory, Guid productId)
    {
        var (scope, db) = await factory.CreateDbAsync();
        await using (scope)
            return await db.Reservations.AsNoTracking().Where(r => r.ProductId == productId).ToListAsync();
    }

    /// <summary>Moves the product's hold into the past, as if 15 minutes had gone by.</summary>
    public static async Task ExpireHoldAsync(ApiFactory factory, Guid productId)
    {
        var (scope, db) = await factory.CreateDbAsync();
        await using (scope)
        {
            var past = DateTime.UtcNow.AddMinutes(-1);
            var updated = await db.Reservations.Where(r => r.ProductId == productId)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.ExpiresAt, past));
            updated.Should().Be(1);
        }
    }

    /// <summary>The anonymous cart id from the response's Set-Cookie header.</summary>
    public static Guid CartCookieValue(HttpResponseMessage response)
    {
        response.Headers.TryGetValues("Set-Cookie", out var values).Should().BeTrue("the first add should set the cart cookie");
        var header = values!.FirstOrDefault(v => v.StartsWith(CartCookie + "=", StringComparison.Ordinal));
        header.Should().NotBeNull();
        header!.ToLowerInvariant().Should().Contain("httponly").And.Contain("path=/api");
        return Guid.Parse(header!.Split(';')[0][(CartCookie.Length + 1)..]);
    }
}
