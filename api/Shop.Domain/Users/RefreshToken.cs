namespace Shop.Domain.Users;

public class RefreshToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public User? User { get; set; }

    /// <summary>SHA-256 of the raw token; the raw value only ever lives in the cookie.</summary>
    public required string TokenHash { get; set; }

    /// <summary>All tokens from one login share a family, so reuse of any old one revokes them all.</summary>
    public Guid FamilyId { get; set; }

    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public Guid? ReplacedById { get; set; }
    public DateTime CreatedAt { get; set; }
}
