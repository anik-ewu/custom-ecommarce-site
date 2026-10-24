using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Shop.Application.Auth;
using Shop.Application.Common;

namespace Shop.Api.Auth;

public static class AuthSetup
{
    public const string LoginPolicy = "login";
    public const string RegisterPolicy = "register";

    public static IServiceCollection AddShopAuth(this IServiceCollection services, IConfiguration config)
    {
        var auth = config.GetSection("Auth").Get<AuthOptions>() ?? new AuthOptions();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
        {
            o.MapInboundClaims = false;
            o.TokenValidationParameters = new TokenValidationParameters
            {
                ValidIssuer = auth.Issuer,
                ValidAudience = auth.Audience,
                IssuerSigningKey = TokenService.SigningKey(auth),
                NameClaimType = "name",
                RoleClaimType = "role",
                ClockSkew = TimeSpan.FromSeconds(30),
            };
            o.Events = new JwtBearerEvents
            {
                OnChallenge = ctx =>
                {
                    ctx.HandleResponse();
                    return WriteProblem(ctx.HttpContext, 401, "Please sign in.");
                },
                OnForbidden = ctx => WriteProblem(ctx.HttpContext, 403, "You don't have access to this."),
            };
        });
        services.AddAuthorization();

        services.Configure<ForwardedHeadersOptions>(o =>
        {
            // Behind Vercel → Render the client IP arrives in X-Forwarded-For; rate limits partition on it.
            o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            o.KnownIPNetworks.Clear();
            o.KnownProxies.Clear();
        });

        var limits = config.GetSection("RateLimits");
        services.AddRateLimiter(o =>
        {
            o.AddPolicy(LoginPolicy, ctx => PerIp(ctx, limits.GetValue("LoginPerMinute", 5)));
            o.AddPolicy(RegisterPolicy, ctx => PerIp(ctx, limits.GetValue("RegisterPerMinute", 3)));
            o.OnRejected = (ctx, _) =>
                new ValueTask(WriteProblem(ctx.HttpContext, 429, "Too many attempts. Please wait a minute and try again."));
        });
        return services;
    }

    private static RateLimitPartition<string> PerIp(HttpContext ctx, int permitsPerMinute) =>
        RateLimitPartition.GetFixedWindowLimiter(
            ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = permitsPerMinute, Window = TimeSpan.FromMinutes(1) });

    private static Task WriteProblem(HttpContext http, int status, string title)
    {
        http.Response.StatusCode = status;
        return http.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(new ProblemDetailsContext
        {
            HttpContext = http,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                Extensions = { ["code"] = Errors.ProblemDetailsSetup.CodeFor(status) },
            },
        }).AsTask();
    }
}
