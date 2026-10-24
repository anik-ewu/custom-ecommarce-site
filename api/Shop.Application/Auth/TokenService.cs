using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Shop.Domain.Users;

namespace Shop.Application.Auth;

public sealed class TokenService(IOptions<AuthOptions> options, TimeProvider clock)
{
    private readonly AuthOptions _o = options.Value;

    public (string Token, DateTime ExpiresAt) CreateAccessToken(User user)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var expires = now.AddMinutes(_o.AccessTokenMinutes);
        var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = _o.Issuer,
            Audience = _o.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = expires,
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim("name", user.Name),
                new Claim("role", user.Role.ToString()),
            ]),
            SigningCredentials = new SigningCredentials(SigningKey(_o), SecurityAlgorithms.HmacSha256),
        });
        return (token, expires);
    }

    public static SymmetricSecurityKey SigningKey(AuthOptions o)
    {
        var bytes = Encoding.UTF8.GetBytes(o.SigningKey);
        if (bytes.Length < 32)
            throw new InvalidOperationException("Auth:SigningKey must be at least 32 bytes.");
        return new SymmetricSecurityKey(bytes);
    }

    public static string NewRefreshToken() => Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(64));

    public static string Hash(string refreshToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));
}
