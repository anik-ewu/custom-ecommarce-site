using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Shop.Application.Auth;
using Shop.Tests.Infrastructure;
using static Shop.Tests.Auth.AuthTestHelpers;

namespace Shop.Tests.Auth;

[Collection(ApiCollection.Name)]
public sealed class AuthEndpointTests(ApiFactory factory)
{
    [Fact]
    public async Task Register_NewEmail_ReturnsTokenAndSetsSecureRefreshCookie()
    {
        using var client = factory.CreateApiClient();
        var email = UniqueEmail();

        var response = await RegisterAsync(client, email);

        var body = await ReadAuthAsync(response);
        body.AccessToken.Should().NotBeNullOrWhiteSpace();
        body.ExpiresAt.Should().BeAfter(DateTime.UtcNow);
        body.User.Email.Should().Be(email);
        body.User.Role.Should().Be("Customer");

        var cookie = RefreshSetCookie(response);
        cookie.Should().NotBeNull();
        var attributes = cookie!.Split(';').Select(a => a.Trim().ToLowerInvariant()).ToList();
        attributes.Should().Contain("httponly");
        attributes.Should().Contain("secure");
        attributes.Should().Contain("path=/api/auth");
        RefreshCookieValue(response).Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns409Conflict()
    {
        using var client = factory.CreateApiClient();
        var email = UniqueEmail();
        (await RegisterAsync(client, email)).EnsureSuccessStatusCode();

        // Different casing still counts as the same account.
        var response = await RegisterAsync(client, email.ToUpperInvariant());

        await ShouldBeProblemAsync(response, 409, "CONFLICT");
    }

    [Fact]
    public async Task Register_InvalidBody_Returns400ValidationFailed()
    {
        using var client = factory.CreateApiClient();

        var response = await client.PostAsJsonAsync("/api/auth/register",
            new { email = "not-an-email", password = "short", name = "" });

        await ShouldBeProblemAsync(response, 400, "VALIDATION_FAILED");
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401Unauthorized()
    {
        using var client = factory.CreateApiClient();

        var response = await LoginAsync(client, "demo@kinbo.test", "definitely-wrong-password");

        await ShouldBeProblemAsync(response, 401, "UNAUTHORIZED");
        RefreshSetCookie(response).Should().BeNull();
    }

    [Fact]
    public async Task Login_CorrectPassword_BearerTokenWorksOnMe()
    {
        using var client = factory.CreateApiClient();
        var auth = await ReadAuthAsync(await LoginAsync(client, "demo@kinbo.test", ApiFactory.DemoPassword));

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        var response = await client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        var me = await response.Content.ReadFromJsonAsync<UserDto>();
        me!.Email.Should().Be("demo@kinbo.test");
        me.Id.Should().Be(auth.User.Id);
        me.Role.Should().Be("Customer");
    }

    [Fact]
    public async Task Me_WithoutToken_Returns401Unauthorized()
    {
        using var client = factory.CreateApiClient();

        var response = await client.GetAsync("/api/auth/me");

        await ShouldBeProblemAsync(response, 401, "UNAUTHORIZED");
    }

    [Fact]
    public async Task Refresh_ValidCookie_RotatesTokenWithinSameFamily()
    {
        using var client = factory.CreateApiClient();
        var email = UniqueEmail();
        var registered = await RegisterAsync(client, email);
        var first = await ReadAuthAsync(registered);
        var oldRaw = RefreshCookieValue(registered);

        var refreshed = await client.PostAsync(RefreshPath, null);

        var second = await ReadAuthAsync(refreshed);
        second.AccessToken.Should().NotBeNullOrWhiteSpace();
        second.User.Id.Should().Be(first.User.Id);
        var newRaw = RefreshCookieValue(refreshed);
        newRaw.Should().NotBe(oldRaw);

        var (scope, db) = await factory.CreateDbAsync();
        await using (scope)
        {
            var oldToken = await db.RefreshTokens.AsNoTracking().SingleAsync(t => t.TokenHash == TokenService.Hash(oldRaw));
            var newToken = await db.RefreshTokens.AsNoTracking().SingleAsync(t => t.TokenHash == TokenService.Hash(newRaw));
            oldToken.RevokedAt.Should().NotBeNull();
            oldToken.ReplacedById.Should().Be(newToken.Id);
            newToken.FamilyId.Should().Be(oldToken.FamilyId);
            newToken.RevokedAt.Should().BeNull();
        }
    }

    [Fact]
    public async Task Refresh_ReusedRevokedToken_RevokesWholeFamily()
    {
        using var client = factory.CreateApiClient();
        var registered = await RegisterAsync(client, UniqueEmail());
        registered.EnsureSuccessStatusCode();
        var oldRaw = RefreshCookieValue(registered);
        var rotated = await client.PostAsync(RefreshPath, null);
        rotated.EnsureSuccessStatusCode();
        var newRaw = RefreshCookieValue(rotated);

        // Push the rotation outside the grace window, so the replay below counts as reuse rather than a tab race.
        var (backdateScope, backdate) = await factory.CreateDbAsync();
        await using (backdateScope)
        {
            var past = DateTime.UtcNow - AuthService.RotationGrace - TimeSpan.FromSeconds(5);
            await backdate.RefreshTokens.Where(t => t.TokenHash == TokenService.Hash(oldRaw))
                .ExecuteUpdateAsync(u => u.SetProperty(t => t.RevokedAt, past));
        }

        // An attacker replays the stolen, already-rotated token from somewhere else.
        using var attacker = factory.CreateApiClient();
        var replay = await RefreshWithCookieAsync(attacker, oldRaw);
        await ShouldBeProblemAsync(replay, 401, "UNAUTHORIZED");

        // The legitimate holder's newer token is now dead too.
        using var victim = factory.CreateApiClient();
        var afterReuse = await RefreshWithCookieAsync(victim, newRaw);
        await ShouldBeProblemAsync(afterReuse, 401, "UNAUTHORIZED");

        var (scope, db) = await factory.CreateDbAsync();
        await using (scope)
        {
            var familyId = await db.RefreshTokens.Where(t => t.TokenHash == TokenService.Hash(oldRaw))
                .Select(t => t.FamilyId).SingleAsync();
            var family = await db.RefreshTokens.AsNoTracking().Where(t => t.FamilyId == familyId).ToListAsync();
            family.Should().HaveCount(2);
            family.Should().OnlyContain(t => t.RevokedAt != null);
        }
    }

    [Fact]
    public async Task Refresh_SameTokenRightAfterRotation_IsRejectedWithoutRevokingFamily()
    {
        using var tabA = factory.CreateApiClient();
        var registered = await RegisterAsync(tabA, UniqueEmail());
        registered.EnsureSuccessStatusCode();
        var raw = RefreshCookieValue(registered);

        var first = await RefreshWithCookieAsync(tabA, raw);
        first.EnsureSuccessStatusCode();
        var winnerRaw = RefreshCookieValue(first);

        using var tabB = factory.CreateApiClient();
        var second = await RefreshWithCookieAsync(tabB, raw);
        await ShouldBeProblemAsync(second, 401, "UNAUTHORIZED");

        var stillValid = await RefreshWithCookieAsync(tabA, winnerRaw);
        stillValid.StatusCode.Should().Be(HttpStatusCode.OK, "a tab race must not log the user out everywhere");
    }

    [Fact]
    public async Task Refresh_ConcurrentSameToken_OneWinsAndFamilyStaysUsable()
    {
        using var setup = factory.CreateApiClient();
        var registered = await RegisterAsync(setup, UniqueEmail());
        registered.EnsureSuccessStatusCode();
        var raw = RefreshCookieValue(registered);

        using var tabA = factory.CreateApiClient();
        using var tabB = factory.CreateApiClient();
        var responses = await Task.WhenAll(RefreshWithCookieAsync(tabA, raw), RefreshWithCookieAsync(tabB, raw));

        responses.Count(r => r.StatusCode == HttpStatusCode.OK).Should().Be(1);
        responses.Count(r => r.StatusCode == HttpStatusCode.Unauthorized).Should().Be(1);

        var winnerRaw = RefreshCookieValue(responses.Single(r => r.StatusCode == HttpStatusCode.OK));
        using var later = factory.CreateApiClient();
        var next = await RefreshWithCookieAsync(later, winnerRaw);
        next.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Logout_RevokesRefreshToken_SoRefreshReturns401()
    {
        using var client = factory.CreateApiClient();
        var registered = await RegisterAsync(client, UniqueEmail());
        registered.EnsureSuccessStatusCode();
        var raw = RefreshCookieValue(registered);

        var logout = await client.PostAsync("/api/auth/logout", null);
        ((int)logout.StatusCode).Should().Be(204);

        // Present the token explicitly: logout also clears the cookie jar, which alone would prove nothing.
        using var other = factory.CreateApiClient();
        var refresh = await RefreshWithCookieAsync(other, raw);
        await ShouldBeProblemAsync(refresh, 401, "UNAUTHORIZED");

        var (scope, db) = await factory.CreateDbAsync();
        await using (scope)
        {
            var token = await db.RefreshTokens.AsNoTracking().SingleAsync(t => t.TokenHash == TokenService.Hash(raw));
            token.RevokedAt.Should().NotBeNull();
        }
    }

    [Fact]
    public async Task Login_SixthAttemptWithinAMinute_Returns429RateLimited()
    {
        using var client = factory.CreateApiClient();

        for (var i = 0; i < 5; i++)
        {
            var attempt = await LoginAsync(client, "demo@kinbo.test", "wrong-password");
            ((int)attempt.StatusCode).Should().Be(401, $"attempt {i + 1} is within the limit");
        }

        var sixth = await LoginAsync(client, "demo@kinbo.test", ApiFactory.DemoPassword);

        await ShouldBeProblemAsync(sixth, 429, "RATE_LIMITED");
    }

    [Theory]
    [InlineData("customer", "Customer", "demo@kinbo.test")]
    [InlineData("admin", "Admin", "demo-admin@kinbo.test")]
    public async Task Demo_Login_ReturnsAccountWithRequestedRole(string role, string expectedRole, string expectedEmail)
    {
        using var client = factory.CreateApiClient();

        var response = await client.PostAsJsonAsync("/api/auth/demo", new { role });

        var body = await ReadAuthAsync(response);
        body.User.Role.Should().Be(expectedRole);
        body.User.Email.Should().Be(expectedEmail);
        body.AccessToken.Should().NotBeNullOrWhiteSpace();
        RefreshCookieValue(response).Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Demo_UnknownRole_Returns400ValidationFailed()
    {
        using var client = factory.CreateApiClient();

        var response = await client.PostAsJsonAsync("/api/auth/demo", new { role = "superuser" });

        await ShouldBeProblemAsync(response, 400, "VALIDATION_FAILED");
    }
}
