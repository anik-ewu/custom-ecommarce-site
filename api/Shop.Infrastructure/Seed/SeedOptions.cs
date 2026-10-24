namespace Shop.Infrastructure.Seed;

/// <summary>Passwords come from user-secrets or env vars. The admin is skipped without one; demo users get a random one.</summary>
public sealed class SeedOptions
{
    public bool Enabled { get; set; } = true;
    public string AdminEmail { get; set; } = "admin@kinbo.test";
    public string AdminPassword { get; set; } = "";
    public string DemoEmail { get; set; } = "demo@kinbo.test";
    public string DemoAdminEmail { get; set; } = "demo-admin@kinbo.test";
    public string DemoAdminPassword { get; set; } = "";
    public string DemoPassword { get; set; } = "";
}
