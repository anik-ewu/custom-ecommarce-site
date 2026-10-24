namespace Shop.Infrastructure.Seed;

/// <summary>Passwords come from user-secrets or env vars; a user is skipped when its password is empty.</summary>
public sealed class SeedOptions
{
    public bool Enabled { get; set; } = true;
    public string AdminEmail { get; set; } = "admin@kinbo.test";
    public string AdminPassword { get; set; } = "";
    public string DemoEmail { get; set; } = "demo@kinbo.test";
    public string DemoPassword { get; set; } = "";
}
