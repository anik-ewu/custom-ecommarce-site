namespace Shop.Application.Auth;

public sealed class AuthOptions
{
    public string Issuer { get; set; } = "kinbo";
    public string Audience { get; set; } = "kinbo";

    /// <summary>HS256 key, at least 32 bytes. Set via user-secrets or the Auth__SigningKey env var.</summary>
    public string SigningKey { get; set; } = "";

    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 7;
}
