using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Shop.Application.Common;
using Shop.Domain.Users;
using Shop.Infrastructure.Data;
using Shop.Infrastructure.Seed;

namespace Shop.Application.Auth;

public sealed class AuthService(
    ShopDbContext db,
    TokenService tokens,
    IOptions<AuthOptions> authOptions,
    IOptions<SeedOptions> seedOptions,
    TimeProvider clock)
{
    public static readonly TimeSpan RotationGrace = TimeSpan.FromSeconds(30);

    private static readonly PasswordHasher<User> Hasher = new();
    private static readonly User DummyUser = new() { Email = "", Name = "" };
    private static readonly string DummyHash = Hasher.HashPassword(DummyUser, Guid.NewGuid().ToString());

    public async Task<AuthResult> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        var email = Normalize(request.Email);
        if (await db.Users.AnyAsync(u => u.Email == email, ct))
            throw DuplicateEmail();

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            Name = request.Name.Trim(),
            Role = UserRole.Customer,
            CreatedAt = Now,
        };
        user.PasswordHash = Hasher.HashPassword(user, request.Password);
        db.Users.Add(user);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException e) when (e.InnerException is Npgsql.PostgresException { SqlState: "23505" })
        {
            // Two sign-ups with the same email raced past the check above; the unique index decided.
            throw DuplicateEmail();
        }
        return await IssueAsync(user, familyId: Guid.NewGuid(), replaces: null, ct);
    }

    public async Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var email = Normalize(request.Email);
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);
        // Hash even for unknown emails so response time doesn't reveal which accounts exist.
        var hash = user?.PasswordHash ?? DummyHash;
        var result = Hasher.VerifyHashedPassword(user ?? DummyUser, hash, request.Password);
        if (user is null || result == PasswordVerificationResult.Failed)
            throw AppException.Unauthorized("Email or password is incorrect.");

        return await IssueAsync(user, Guid.NewGuid(), null, ct);
    }

    /// <summary>Signs in as a seeded demo account without a password, so the demo works without sharing one.</summary>
    public async Task<AuthResult> DemoLoginAsync(DemoLoginRequest request, CancellationToken ct)
    {
        var o = seedOptions.Value;
        var email = Normalize(request.Role == "admin" ? o.DemoAdminEmail : o.DemoEmail);
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, ct)
            ?? throw AppException.NotFound("The demo account is not available.");
        return await IssueAsync(user, Guid.NewGuid(), null, ct);
    }

    public async Task<AuthResult> RefreshAsync(string? rawToken, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(rawToken))
            throw AppException.Unauthorized();

        var hash = TokenService.Hash(rawToken);
        var token = await db.RefreshTokens.Include(t => t.User).FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (token?.User is null)
            throw AppException.Unauthorized();

        if (token.RevokedAt is not null)
        {
            // Rotated moments ago by a parallel request (another tab): a race, not a replay, so keep the family.
            if (token.ReplacedById is not null && token.RevokedAt > Now - RotationGrace)
                throw AppException.Unauthorized();

            await RevokeFamilyAsync(token.FamilyId, ct);
            throw AppException.Unauthorized();
        }

        if (token.ExpiresAt <= Now)
            throw AppException.Unauthorized();

        // Revoke, insert and link in one transaction so a failure mid-way never strands the session.
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        // Conditional revoke: if two requests race with the same token (e.g. two tabs), only one rotates it.
        // The loser gets a plain 401 instead of a family revoke, since this is a race rather than replay.
        var claimed = await db.RefreshTokens
            .Where(t => t.Id == token.Id && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, Now), ct);
        if (claimed == 0)
            throw AppException.Unauthorized();

        var result = await IssueAsync(token.User, token.FamilyId, token.Id, ct);
        await tx.CommitAsync(ct);
        return result;
    }

    public async Task LogoutAsync(string? rawToken, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(rawToken))
            return;

        var hash = TokenService.Hash(rawToken);
        await db.RefreshTokens
            .Where(t => t.TokenHash == hash && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, Now), ct);
    }

    public async Task<UserDto> GetUserAsync(Guid userId, CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct) ?? throw AppException.Unauthorized();
        return ToDto(user);
    }

    private async Task<AuthResult> IssueAsync(User user, Guid familyId, Guid? replaces, CancellationToken ct)
    {
        var raw = TokenService.NewRefreshToken();
        var refresh = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = TokenService.Hash(raw),
            FamilyId = familyId,
            CreatedAt = Now,
            ExpiresAt = Now.AddDays(authOptions.Value.RefreshTokenDays),
        };
        db.RefreshTokens.Add(refresh);
        await db.SaveChangesAsync(ct);

        if (replaces is { } oldId)
        {
            await db.RefreshTokens.Where(t => t.Id == oldId)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.ReplacedById, refresh.Id), ct);
        }

        var (access, expiresAt) = tokens.CreateAccessToken(user);
        return new AuthResult(new AuthResponse(access, expiresAt, ToDto(user)), raw, refresh.ExpiresAt);
    }

    private Task RevokeFamilyAsync(Guid familyId, CancellationToken ct) =>
        db.RefreshTokens
            .Where(t => t.FamilyId == familyId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, Now), ct);

    private static AppException DuplicateEmail() =>
        AppException.Conflict(ErrorCodes.Conflict, "An account with this email already exists.",
            new Dictionary<string, string[]> { ["email"] = ["This email is already registered."] });

    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    private static string Normalize(string email) => email.Trim().ToLowerInvariant();

    private static UserDto ToDto(User u) => new(u.Id, u.Email, u.Name, u.Role.ToString());
}
