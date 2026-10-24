using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Shop.Application.Orders;
using Shop.Tests.Infrastructure;
using static Shop.Tests.Auth.AuthTestHelpers;

namespace Shop.Tests.Orders;

internal static class CheckoutTestHelpers
{
    public const string OrderNumberPattern = "^KB-[0-9]{8}-[ABCDEFGHJKLMNPQRSTUVWXYZ23456789]{4}$";

    /// <summary>A fresh client signed in as a brand-new customer, with the access token attached as Bearer.</summary>
    public static async Task<(HttpClient Client, Guid UserId)> SignedInClientAsync(ApiFactory factory)
    {
        var client = factory.CreateApiClient();
        var auth = await ReadAuthAsync(await RegisterAsync(client, UniqueEmail()));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return (client, auth.User.Id);
    }

    public static object CheckoutBody(string phone = "01712345678", string paymentMethod = "CashOnDelivery") => new
    {
        shippingAddress = new
        {
            fullName = "Rahima Begum",
            phone,
            line1 = "House 12, Road 4",
            line2 = "Dhanmondi",
            city = "Dhaka",
            postalCode = "1205",
        },
        paymentMethod,
    };

    public static Task<HttpResponseMessage> CheckoutAsync(HttpClient client, object? body = null) =>
        client.PostAsJsonAsync("/api/checkout", body ?? CheckoutBody());

    public static async Task<OrderDto> ReadOrderAsync(HttpResponseMessage response)
    {
        var raw = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, raw);
        var order = await response.Content.ReadFromJsonAsync<OrderDto>();
        order.Should().NotBeNull();
        return order!;
    }

    public static async Task<OrderDto> CheckoutOkAsync(HttpClient client) => await ReadOrderAsync(await CheckoutAsync(client));

    public static async Task<CheckoutSummaryDto> SummaryAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/checkout/summary");
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<CheckoutSummaryDto>())!;
    }

    public static async Task<int> StockOfAsync(ApiFactory factory, Guid productId)
    {
        var (scope, db) = await factory.CreateDbAsync();
        await using (scope)
            return await db.Products.AsNoTracking().Where(p => p.Id == productId).Select(p => p.StockQuantity).SingleAsync();
    }

    public static async Task<int> OrderCountAsync(ApiFactory factory, Guid userId)
    {
        var (scope, db) = await factory.CreateDbAsync();
        await using (scope)
            return await db.Orders.CountAsync(o => o.UserId == userId);
    }

    public static async Task<int> OrderLinesForProductAsync(ApiFactory factory, Guid productId)
    {
        var (scope, db) = await factory.CreateDbAsync();
        await using (scope)
            return await db.OrderItems.CountAsync(i => i.ProductId == productId);
    }

    public static async Task<Guid> CartIdOfUserAsync(ApiFactory factory, Guid userId)
    {
        var (scope, db) = await factory.CreateDbAsync();
        await using (scope)
            return await db.Carts.Where(c => c.UserId == userId).Select(c => c.Id).SingleAsync();
    }

    /// <summary>The keys of the ProblemDetails `errors` dictionary (asserting the code along the way).</summary>
    public static async Task<IReadOnlyList<string>> ValidationErrorKeysAsync(HttpResponseMessage response)
    {
        await ShouldBeProblemAsync(response, 400, "VALIDATION_FAILED");
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.TryGetProperty("errors", out var errors).Should().BeTrue("field errors belong in `errors`");
        return errors.EnumerateObject().Select(p => p.Name).ToList();
    }
}
