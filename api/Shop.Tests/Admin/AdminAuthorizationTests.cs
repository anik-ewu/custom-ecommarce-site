using System.Net.Http.Headers;
using System.Net.Http.Json;
using Shop.Tests.Infrastructure;
using static Shop.Tests.Auth.AuthTestHelpers;
using static Shop.Tests.Orders.CheckoutTestHelpers;

namespace Shop.Tests.Admin;

[Collection(ApiCollection.Name)]
public sealed class AdminAuthorizationTests(ApiFactory factory)
{
    private const string SomeId = "6f1c2c4e-0000-4000-8000-000000000001";

    // One endpoint per admin controller action group: products, orders, dashboard/artisans and uploads.
    public static TheoryData<string, string> AdminEndpoints => new()
    {
        { "GET", "/api/admin/products" },
        { "POST", "/api/admin/products" },
        { "GET", $"/api/admin/products/{SomeId}" },
        { "PUT", $"/api/admin/products/{SomeId}" },
        { "DELETE", $"/api/admin/products/{SomeId}" },
        { "POST", $"/api/admin/products/{SomeId}/images" },
        { "GET", "/api/admin/orders" },
        { "GET", "/api/admin/orders/KB-20260101-AAAA" },
        { "PATCH", "/api/admin/orders/KB-20260101-AAAA/status" },
        { "GET", "/api/admin/dashboard" },
        { "GET", "/api/admin/artisans" },
        { "POST", "/api/admin/artisans" },
        { "POST", "/api/admin/uploads" },
    };

    [Theory]
    [MemberData(nameof(AdminEndpoints))]
    public async Task Admin_endpoint_without_a_token_returns_401_UNAUTHORIZED(string method, string path)
    {
        var client = factory.CreateApiClient();

        var response = await client.SendAsync(Request(method, path));

        await ShouldBeProblemAsync(response, 401, "UNAUTHORIZED");
    }

    [Theory]
    [MemberData(nameof(AdminEndpoints))]
    public async Task Admin_endpoint_with_a_customer_token_returns_403_FORBIDDEN(string method, string path)
    {
        var (client, _) = await SignedInClientAsync(factory);

        var response = await client.SendAsync(Request(method, path));

        await ShouldBeProblemAsync(response, 403, "FORBIDDEN");
    }

    [Fact]
    public async Task Demo_customer_token_is_also_forbidden_from_the_dashboard()
    {
        var client = factory.CreateApiClient();
        var auth = await ReadAuthAsync(await client.PostAsJsonAsync("/api/auth/demo", new { role = "customer" }));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        await ShouldBeProblemAsync(await client.GetAsync("/api/admin/dashboard"), 403, "FORBIDDEN");
    }

    [Fact]
    public async Task Admin_signed_in_with_password_can_read_the_dashboard()
    {
        var client = factory.CreateApiClient();
        var auth = await ReadAuthAsync(await LoginAsync(client, "admin@kinbo.test", ApiFactory.AdminPassword));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        await AdminTestHelpers.DashboardAsync(client);
    }

    private static HttpRequestMessage Request(string method, string path)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (method is "POST" or "PUT" or "PATCH")
            request.Content = path.EndsWith("/uploads", StringComparison.Ordinal)
                ? new MultipartFormDataContent { { new ByteArrayContent([1, 2, 3]), "file", "x.png" } }
                : JsonContent.Create(new { });
        return request;
    }
}
