using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Shop.Application.Admin;
using Shop.Application.Orders;
using Shop.Domain.Catalog;
using Shop.Domain.Payments;
using Shop.Tests.Infrastructure;
using static Shop.Tests.Auth.AuthTestHelpers;
using static Shop.Tests.Carts.CartTestHelpers;
using static Shop.Tests.Orders.CheckoutTestHelpers;

namespace Shop.Tests.Admin;

internal static class AdminTestHelpers
{
    /// <summary>A fresh client signed in as the demo admin, with the access token attached as Bearer.</summary>
    public static async Task<HttpClient> AdminClientAsync(ApiFactory factory)
    {
        var client = factory.CreateApiClient();
        var auth = await ReadAuthAsync(await client.PostAsJsonAsync("/api/auth/demo", new { role = "admin" }));
        auth.User.Role.Should().Be("Admin");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }

    /// <summary>A category of this test's own, sorted last so catalog tests counting seeded categories aren't affected.</summary>
    public static async Task<Guid> CreateCategoryAsync(ApiFactory factory)
    {
        var (scope, db) = await factory.CreateDbAsync();
        await using (scope)
        {
            var category = new Category
            {
                Id = Guid.NewGuid(),
                Name = "Admin test",
                Slug = $"admin-test-{Guid.NewGuid():N}",
                SortOrder = 10_000,
            };
            db.Categories.Add(category);
            await db.SaveChangesAsync();
            return category.Id;
        }
    }

    /// <summary>A short random token to put in names, so searches only ever find this test's rows.</summary>
    public static string Token() => Guid.NewGuid().ToString("N")[..10];

    public static ProductWriteRequest ProductRequest(Guid categoryId, string name, string? slug = null,
        string inventoryType = "Standard", int stock = 5, decimal price = 750m, uint? version = null) =>
        new(name, slug, "Hand-thrown and glazed.", "Terracotta", "12 x 12 cm", price, inventoryType, stock,
            categoryId, null, true, version);

    public static async Task<AdminProductDto> ReadProductAsync(HttpResponseMessage response)
    {
        var raw = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, raw);
        return (await response.Content.ReadFromJsonAsync<AdminProductDto>())!;
    }

    public static async Task<AdminProductDto> CreateProductOkAsync(HttpClient admin, ProductWriteRequest request) =>
        await ReadProductAsync(await admin.PostAsJsonAsync("/api/admin/products", request));

    public static async Task<AdminProductDto> GetProductOkAsync(HttpClient admin, Guid id) =>
        await ReadProductAsync(await admin.GetAsync($"/api/admin/products/{id}"));

    public static async Task<T> ReadOkAsync<T>(HttpResponseMessage response)
    {
        var raw = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, raw);
        var body = await response.Content.ReadFromJsonAsync<T>();
        body.Should().NotBeNull();
        return body!;
    }

    public static async Task<DashboardDto> DashboardAsync(HttpClient admin) =>
        await ReadOkAsync<DashboardDto>(await admin.GetAsync("/api/admin/dashboard"));

    public static async Task<AdminOrderDto> GetOrderOkAsync(HttpClient admin, string number) =>
        await ReadOkAsync<AdminOrderDto>(await admin.GetAsync($"/api/admin/orders/{number}"));

    public static Task<HttpResponseMessage> SetStatusAsync(HttpClient admin, string number, string status) =>
        admin.PatchAsJsonAsync($"/api/admin/orders/{number}/status", new { status });

    public static async Task<AdminOrderDto> SetStatusOkAsync(HttpClient admin, string number, string status) =>
        await ReadOkAsync<AdminOrderDto>(await SetStatusAsync(admin, number, status));

    /// <summary>A brand-new customer buys the given lines through the real cart and COD checkout.</summary>
    public static async Task<OrderDto> PlaceOrderAsync(ApiFactory factory, params (Guid ProductId, int Quantity)[] lines)
    {
        var (client, _) = await SignedInClientAsync(factory);
        foreach (var (productId, quantity) in lines)
            await AddOkAsync(client, productId, quantity);
        return await CheckoutOkAsync(client);
    }

    public static async Task<PaymentStatus> PaymentStatusOfAsync(ApiFactory factory, string orderNumber)
    {
        var (scope, db) = await factory.CreateDbAsync();
        await using (scope)
        {
            var orderId = await db.Orders.Where(o => o.Number == orderNumber).Select(o => o.Id).SingleAsync();
            return await db.Payments.Where(p => p.OrderId == orderId).Select(p => p.Status).SingleAsync();
        }
    }

    public static async Task<Product> ProductFromDbAsync(ApiFactory factory, Guid id)
    {
        var (scope, db) = await factory.CreateDbAsync();
        await using (scope)
            return await db.Products.AsNoTracking().SingleAsync(p => p.Id == id);
    }
}
