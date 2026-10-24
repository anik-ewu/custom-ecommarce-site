using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Shop.Api.Carts;
using Shop.Application.Auth;
using Shop.Application.Carts;
using Shop.Application.Common;

namespace Shop.Api.Auth;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(AuthService auth, CartService carts) : ControllerBase
{
    public const string RefreshCookie = "kinbo_refresh";

    [HttpPost("register")]
    [EnableRateLimiting(AuthSetup.RegisterPolicy)]
    public async Task<AuthResponse> Register(RegisterRequest request, CancellationToken ct) =>
        await SignedInAsync(await auth.RegisterAsync(request, ct), ct);

    [HttpPost("login")]
    [EnableRateLimiting(AuthSetup.LoginPolicy)]
    public async Task<AuthResponse> Login(LoginRequest request, CancellationToken ct) =>
        await SignedInAsync(await auth.LoginAsync(request, ct), ct);

    [HttpPost("demo")]
    [EnableRateLimiting(AuthSetup.LoginPolicy)]
    public async Task<AuthResponse> Demo(DemoLoginRequest request, CancellationToken ct) =>
        await SignedInAsync(await auth.DemoLoginAsync(request, ct), ct);

    [HttpPost("refresh")]
    public async Task<AuthResponse> Refresh(CancellationToken ct)
    {
        try
        {
            return WithCookie(await auth.RefreshAsync(Request.Cookies[RefreshCookie], ct));
        }
        catch (AppException e) when (e.Status == StatusCodes.Status401Unauthorized)
        {
            // Only a rejected token ends the session; a transient error keeps the cookie for the next try.
            ClearCookie();
            throw;
        }
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        await auth.LogoutAsync(Request.Cookies[RefreshCookie], ct);
        ClearCookie();
        return NoContent();
    }

    [Authorize]
    [HttpGet("me")]
    public Task<UserDto> Me(CancellationToken ct) =>
        auth.GetUserAsync(Guid.Parse(User.FindFirstValue("sub")!), ct);

    /// <summary>Moves the anonymous cart into the account, so what the shopper picked before signing in stays.</summary>
    private async Task<AuthResponse> SignedInAsync(AuthResult result, CancellationToken ct)
    {
        if (CartOwnerResolver.AnonymousId(HttpContext) is { } anonymousId)
        {
            await carts.MergeAsync(anonymousId, result.Response.User.Id, ct);
            CartOwnerResolver.Clear(HttpContext);
        }
        return WithCookie(result);
    }

    private AuthResponse WithCookie(AuthResult result)
    {
        Response.Cookies.Append(RefreshCookie, result.RefreshToken, CookieOptions(result.RefreshExpiresAt));
        return result.Response;
    }

    private void ClearCookie() => Response.Cookies.Delete(RefreshCookie, CookieOptions(null));

    // Scoped to /api/auth so the refresh token is never sent with ordinary API calls.
    private static CookieOptions CookieOptions(DateTime? expires) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Lax,
        Path = "/api/auth",
        Expires = expires,
        IsEssential = true,
    };
}
