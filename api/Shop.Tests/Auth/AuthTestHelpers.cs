using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Shop.Application.Auth;

namespace Shop.Tests.Auth;

internal static class AuthTestHelpers
{
    public const string RefreshCookie = "kinbo_refresh";
    public const string RefreshPath = "/api/auth/refresh";

    public static string UniqueEmail() => $"user-{Guid.NewGuid():N}@kinbo.test";

    public static Task<HttpResponseMessage> RegisterAsync(HttpClient client, string email, string password = "Strong-pass-123") =>
        client.PostAsJsonAsync("/api/auth/register", new { email, password, name = "Test User" });

    public static Task<HttpResponseMessage> LoginAsync(HttpClient client, string email, string password) =>
        client.PostAsJsonAsync("/api/auth/login", new { email, password });

    public static async Task<AuthResponse> ReadAuthAsync(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        body.Should().NotBeNull();
        return body!;
    }

    /// <summary>The raw Set-Cookie header for the refresh cookie (attributes included), or null.</summary>
    public static string? RefreshSetCookie(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var values)
            ? values.FirstOrDefault(v => v.StartsWith(RefreshCookie + "=", StringComparison.Ordinal))
            : null;

    /// <summary>Just the cookie value from the refresh Set-Cookie header.</summary>
    public static string RefreshCookieValue(HttpResponseMessage response)
    {
        var header = RefreshSetCookie(response);
        header.Should().NotBeNull("the response should set the refresh cookie");
        var pair = header!.Split(';')[0];
        var value = pair[(RefreshCookie.Length + 1)..];
        value.Should().NotBeEmpty();
        return Uri.UnescapeDataString(value);
    }

    /// <summary>Posts to /refresh with an explicit cookie, bypassing any cookie jar state.</summary>
    public static Task<HttpResponseMessage> RefreshWithCookieAsync(HttpClient client, string rawToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, RefreshPath);
        request.Headers.Add("Cookie", $"{RefreshCookie}={Uri.EscapeDataString(rawToken)}");
        return client.SendAsync(request);
    }

    public static async Task ShouldBeProblemAsync(HttpResponseMessage response, int status, string code)
    {
        var raw = await response.Content.ReadAsStringAsync();
        ((int)response.StatusCode).Should().Be(status, raw);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        using var doc = JsonDocument.Parse(raw);
        doc.RootElement.GetProperty("code").GetString().Should().Be(code);
    }
}
