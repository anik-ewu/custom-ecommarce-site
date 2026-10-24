using System.ComponentModel.DataAnnotations;

namespace Shop.Application.Auth;

public sealed record RegisterRequest(
    [Required, EmailAddress, MaxLength(200)] string Email,
    [Required, MinLength(8), MaxLength(100)] string Password,
    [Required, MaxLength(100)] string Name);

public sealed record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password);

public sealed record DemoLoginRequest([Required, RegularExpression("^(customer|admin)$")] string Role);

public sealed record UserDto(Guid Id, string Email, string Name, string Role);

public sealed record AuthResponse(string AccessToken, DateTime ExpiresAt, UserDto User);

/// <summary>Service result: the response body plus the raw refresh token, which only ever goes into the cookie.</summary>
public sealed record AuthResult(AuthResponse Response, string RefreshToken, DateTime RefreshExpiresAt);
