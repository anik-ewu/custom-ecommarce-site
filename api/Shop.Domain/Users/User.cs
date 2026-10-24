namespace Shop.Domain.Users;

public class User
{
    public Guid Id { get; set; }
    public required string Email { get; set; }
    public string PasswordHash { get; set; } = "";
    public required string Name { get; set; }
    public UserRole Role { get; set; }
    public DateTime CreatedAt { get; set; }
}
