using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shop.Domain.Catalog;
using Shop.Domain.Stores;
using Shop.Domain.Users;
using Shop.Infrastructure.Data;

namespace Shop.Infrastructure.Seed;

public sealed class ShopSeeder(
    ShopDbContext db,
    IConfiguration config,
    IOptions<SeedOptions> options,
    TimeProvider clock,
    ILogger<ShopSeeder> logger)
{
    public async Task SeedAsync(CancellationToken ct = default)
    {
        var slug = config["Shop:StoreSlug"] ?? "kinbo";
        if (!await db.Stores.AnyAsync(s => s.Slug == slug, ct))
            await SeedCatalogAsync(slug, ct);

        var o = options.Value;
        await SeedUserAsync(o.AdminEmail, o.AdminPassword, "Nokshi Kathar Math Admin", UserRole.Admin, ct);
        // Demo accounts are signed into via /api/auth/demo, so a random password is fine when none is configured.
        await SeedUserAsync(o.DemoEmail, OrRandom(o.DemoPassword), "Demo Shopper", UserRole.Customer, ct);
        await SeedUserAsync(o.DemoAdminEmail, OrRandom(o.DemoAdminPassword), "Demo Admin", UserRole.Admin, ct);
    }

    private async Task SeedCatalogAsync(string slug, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var store = new Store { Id = Guid.NewGuid(), Name = config["Shop:Name"] ?? "Nokshi Kathar Math", Slug = slug };
        db.Stores.Add(store);

        Category Cat(string name, string catSlug, int order) =>
            new() { Id = Guid.NewGuid(), StoreId = store.Id, Name = name, Slug = catSlug, SortOrder = order };
        var cushions = Cat("Kantha Cushions", "kantha-cushions", 1);
        var runners = Cat("Runners & Wall Hangings", "runners-wall-hangings", 2);
        var bags = Cat("Jute Bags", "jute-bags", 3);
        var home = Cat("Jute Baskets & Mats", "jute-baskets-mats", 4);
        db.Categories.AddRange(cushions, runners, bags, home);

        Artisan Art(string name, string region, string story, string photoSeed) => new()
        {
            Id = Guid.NewGuid(),
            StoreId = store.Id,
            Name = name,
            Region = region,
            Story = story,
            PhotoUrl = Placeholder(photoSeed),
        };
        var rahima = Art("Rahima Begum", "Jashore",
            "Rahima learned the running stitch from her grandmother and now leads a circle of twelve women who stitch "
            + "old saris into new stories. Each piece takes her between two and six weeks.", "artisan-rahima");
        var shefali = Art("Shefali Khatun", "Kushtia",
            "Shefali draws her motifs from the river and the village fair: fish, lotus, and the wheel of life. "
            + "She signs every piece with a small stitched leaf.", "artisan-shefali");
        var karim = Art("Abdul Karim", "Rangpur",
            "Karim's family has grown and woven jute for three generations. His workshop braids, dyes and stitches "
            + "every bag and basket by hand, using natural fibre and cotton thread.", "artisan-karim");
        db.Artisans.AddRange(rahima, shefali, karim);

        Product P(Category c, Artisan a, string name, string pSlug, decimal price, InventoryType type, int stock,
            string materials, string dimensions, string description) => new()
        {
            Id = Guid.NewGuid(),
            StoreId = store.Id,
            CategoryId = c.Id,
            ArtisanId = a.Id,
            Name = name,
            Slug = pSlug,
            Price = price,
            InventoryType = type,
            StockQuantity = stock,
            Materials = materials,
            Dimensions = dimensions,
            Description = description,
            CreatedAt = now,
            Images =
            [
                new() { Id = Guid.NewGuid(), Url = Placeholder(pSlug), Alt = name, SortOrder = 0 },
                new() { Id = Guid.NewGuid(), Url = Placeholder(pSlug + "-detail"), Alt = $"{name}, stitch detail", SortOrder = 1 },
            ],
        };

        const string kantha = "Layered cotton saris, cotton thread";
        var u = InventoryType.Unique;
        var s = InventoryType.Standard;
        db.Products.AddRange(
            P(cushions, rahima, "Lotus Pond Cushion Cover", "lotus-pond-cushion", 2400, u, 1, kantha, "40 × 40 cm",
                "A pond of pink lotuses worked in dense running stitch over three layers of soft old cotton."),
            P(cushions, rahima, "Indigo Fish Cushion Cover", "indigo-fish-cushion", 2600, u, 1, kantha, "45 × 45 cm",
                "Two fish circle each other on an indigo ground, a traditional sign of plenty and good fortune."),
            P(cushions, shefali, "Wheel of Life Cushion Cover", "wheel-of-life-cushion", 2800, u, 1, kantha, "45 × 45 cm",
                "A central wheel radiates rows of tiny stitches in red, ochre and white."),
            P(cushions, shefali, "Village Fair Cushion Cover", "village-fair-cushion", 3200, u, 1, kantha, "50 × 50 cm",
                "Elephants, drummers and kites from a village mela, stitched in bright cotton thread."),
            P(runners, rahima, "Kantha Vine Table Runner", "kantha-vine-runner", 4500, u, 1, kantha, "35 × 150 cm",
                "A slow climbing vine of leaves and buds runs the length of this cream runner."),
            P(runners, shefali, "River Bank Table Runner", "river-bank-runner", 4800, u, 1, kantha, "35 × 180 cm",
                "Boats, reeds and herons along a blue river, edged with a narrow red border."),
            P(runners, rahima, "Tree of Life Wall Hanging", "tree-of-life-hanging", 8500, u, 1, kantha, "70 × 100 cm",
                "A large tree with birds in its branches, stitched over two months on layered saris."),
            P(runners, shefali, "Monsoon Wall Hanging", "monsoon-hanging", 9200, u, 1, kantha, "80 × 110 cm",
                "Falling rain and blooming kadam flowers in deep greens and greys."),
            P(bags, karim, "Everyday Jute Tote", "everyday-jute-tote", 650, s, 40, "Jute, cotton lining", "38 × 40 × 12 cm",
                "A sturdy tote with cotton handles and an inner pocket, made for the market and every day after."),
            P(bags, karim, "Braided Jute Shoulder Bag", "braided-jute-shoulder-bag", 1200, s, 25, "Braided jute, leather strap", "30 × 25 × 8 cm",
                "Hand-braided jute with a soft leather strap and a wooden toggle."),
            P(bags, karim, "Jute Laptop Sleeve", "jute-laptop-sleeve", 950, s, 20, "Jute, padded cotton", "36 × 26 cm",
                "A padded sleeve for 13–14 inch laptops, with a stitched kantha flap."),
            P(bags, karim, "Mini Jute Pouch Set", "mini-jute-pouch-set", 450, s, 50, "Jute, cotton zip", "Set of 3: 12–20 cm",
                "Three zip pouches for coins, cables and keepsakes."),
            P(home, karim, "Round Storage Basket", "round-storage-basket", 1400, s, 30, "Coiled jute", "Ø 35 × 30 cm",
                "A coiled basket with two handles for blankets, toys or plants."),
            P(home, karim, "Nesting Baskets, Set of 3", "nesting-baskets", 1800, s, 15, "Coiled jute", "Ø 20, 25, 30 cm",
                "Three baskets that nest neatly inside each other."),
            P(home, karim, "Braided Jute Floor Mat", "braided-jute-floor-mat", 1500, s, 20, "Braided jute", "60 × 90 cm",
                "A tightly braided mat for the doorway or kitchen."),
            P(home, karim, "Jute Table Mats, Set of 4", "jute-table-mats", 900, s, 35, "Jute, cotton edge", "Set of 4: 30 × 45 cm",
                "Four woven mats with a stitched cotton edge."));

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Seeded store {Slug} with catalog", slug);
    }

    private async Task SeedUserAsync(string email, string password, string name, UserRole role, CancellationToken ct)
    {
        email = email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(x => x.Email == email, ct))
            return;
        if (string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("Skipping seed user {Email}: no password configured (set it with dotnet user-secrets)", email);
            return;
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            Name = name,
            Role = role,
            CreatedAt = clock.GetUtcNow().UtcDateTime,
        };
        user.PasswordHash = new PasswordHasher<User>().HashPassword(user, password);
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
    }

    private static string OrRandom(string password) =>
        string.IsNullOrWhiteSpace(password) ? Convert.ToBase64String(RandomNumberGenerator.GetBytes(24)) : password;

    // Brand-palette SVGs generated by scripts/generate-placeholders.mjs and served by the web app,
    // so the demo has no third-party photos until real product photography exists.
    private static string Placeholder(string name) => $"/placeholders/{name}.svg";
}
