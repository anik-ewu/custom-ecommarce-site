using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Shop.Application.Auth;
using Shop.Application.Common;

namespace Shop.Api.Auth;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(AuthService auth) : ControllerBase
{
    public const string RefreshCookie = "kinbo_refresh";

    [HttpPost("register")]
    [EnableRateLimiting(AuthSetup.RegisterPolicy)]
    public async Task<AuthResponse> Register(RegisterRequest request, CancellationToken ct) =>
        WithCookie(await auth.RegisterAsync(request, ct));

    [HttpPost("login")]
    [EnableRateLimiting(AuthSetup.LoginPolicy)]
    public async Task<AuthResponse> Login(LoginRequest request, CancellationToken ct) =>
        WithCookie(await auth.LoginAsync(request, ct));

    [HttpPost("demo")]
    [EnableRateLimiting(AuthSetup.LoginPolicy)]
    public async Task<AuthResponse> Demo(DemoLoginRequest request, CancellationToken ct) =>
        WithCookie(await auth.DemoLoginAsync(request, ct));

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
