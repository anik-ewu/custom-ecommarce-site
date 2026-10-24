using System.Security.Claims;
using Shop.Application.Carts;
using Shop.Application.Common;

namespace Shop.Api.Carts;

/// <summary>Works out whose cart a request is for, issuing the anonymous-cart cookie on first write.</summary>
public static class CartOwnerResolver
{
    public const string AnonymousCookie = "kinbo_cart";

    public static CartOwner Resolve(HttpContext http, bool createAnonymous)
    {
        if (http.User.Identity?.IsAuthenticated == true)
            return new CartOwner(Guid.Parse(http.User.FindFirstValue("sub")!), null);

        // A token was sent but didn't validate (usually expired): answer 401 so the client refreshes, rather
        // than silently showing the signed-in shopper an empty anonymous cart.
        if (http.Request.Headers.Authorization.Count > 0)
            throw AppException.Unauthorized();

        if (Guid.TryParse(http.Request.Cookies[AnonymousCookie], out var anonymousId))
            return new CartOwner(null, anonymousId);

        if (!createAnonymous)
            return new CartOwner(null, null);

        anonymousId = Guid.NewGuid();
        http.Response.Cookies.Append(AnonymousCookie, anonymousId.ToString(), new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Path = "/api",
            Expires = http.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow().AddDays(30),
            IsEssential = true,
        });
        return new CartOwner(null, anonymousId);
    }

    public static Guid? AnonymousId(HttpContext http) =>
        Guid.TryParse(http.Request.Cookies[AnonymousCookie], out var id) ? id : null;

    public static void Clear(HttpContext http) =>
        http.Response.Cookies.Delete(AnonymousCookie, new CookieOptions { Path = "/api", Secure = true, HttpOnly = true });
}
