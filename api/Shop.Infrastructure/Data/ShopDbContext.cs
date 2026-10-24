using Microsoft.EntityFrameworkCore;
using Shop.Domain;
using Shop.Domain.Carts;
using Shop.Domain.Catalog;
using Shop.Domain.Orders;
using Shop.Domain.Payments;
using Shop.Domain.Stores;
using Shop.Domain.Users;
using Shop.Infrastructure.Tenancy;

namespace Shop.Infrastructure.Data;

public class ShopDbContext(DbContextOptions<ShopDbContext> options, TenantContext tenant) : DbContext(options)
{
    // Read by the query filters; EF evaluates it per query, so each request sees its own store.
    private Guid CurrentStoreId => tenant.StoreId;

    public DbSet<Store> Stores => Set<Store>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Artisan> Artisans => Set<Artisan>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Cart> Carts => Set<Cart>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<Reservation> Reservations => Set<Reservation>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<ProcessedWebhookEvent> ProcessedWebhookEvents => Set<ProcessedWebhookEvent>();

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        builder.Properties<decimal>().HavePrecision(18, 2);
        builder.Properties<string>().HaveMaxLength(500);
    }

    // ProductImage, OrderItem, Payment and Reservation have no filter of their own: they are only reached through
    // their filtered parent (or by id from a webhook / the expiry job), and they have no navigation back to it.
    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Store>().HasIndex(s => s.Slug).IsUnique();

        b.Entity<Category>(e =>
        {
            e.HasIndex(c => new { c.StoreId, c.Slug }).IsUnique();
            e.HasQueryFilter(c => c.StoreId == CurrentStoreId);
        });

        b.Entity<Artisan>(e =>
        {
            e.Property(a => a.Story).HasMaxLength(4000);
            e.HasQueryFilter(a => a.StoreId == CurrentStoreId);
        });

        b.Entity<Product>(e =>
        {
            e.HasIndex(p => new { p.StoreId, p.Slug }).IsUnique();
            e.Property(p => p.Description).HasMaxLength(4000);
            e.Property(p => p.InventoryType).HasConversion<string>().HasMaxLength(20);
            e.Property(p => p.Version).IsRowVersion();
            e.HasMany(p => p.Images).WithOne().HasForeignKey(i => i.ProductId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(p => p.Category).WithMany().HasForeignKey(p => p.CategoryId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(p => p.Artisan).WithMany().HasForeignKey(p => p.ArtisanId).OnDelete(DeleteBehavior.SetNull);
            e.HasQueryFilter(p => p.StoreId == CurrentStoreId);
        });

        b.Entity<User>(e =>
        {
            e.HasIndex(u => u.Email).IsUnique();
            e.Property(u => u.Role).HasConversion<string>().HasMaxLength(20);
        });

        b.Entity<RefreshToken>(e =>
        {
            e.HasIndex(t => t.TokenHash).IsUnique();
            e.HasIndex(t => t.FamilyId);
            e.HasOne(t => t.User).WithMany().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Cart>(e =>
        {
            e.HasIndex(c => new { c.StoreId, c.UserId }).IsUnique().HasFilter("user_id IS NOT NULL");
            e.HasIndex(c => new { c.StoreId, c.AnonymousId }).IsUnique().HasFilter("anonymous_id IS NOT NULL");
            e.HasMany(c => c.Items).WithOne().HasForeignKey(i => i.CartId).OnDelete(DeleteBehavior.Cascade);
            e.HasQueryFilter(c => c.StoreId == CurrentStoreId);
        });

        b.Entity<CartItem>(e =>
        {
            e.HasIndex(i => new { i.CartId, i.ProductId }).IsUnique();
            e.HasOne(i => i.Product).WithMany().HasForeignKey(i => i.ProductId).OnDelete(DeleteBehavior.Cascade);
            // Matches Product's filter so a cart line never points at another store's product.
            e.HasQueryFilter(i => i.Product!.StoreId == CurrentStoreId);
        });

        b.Entity<Reservation>(e =>
        {
            // One hold per piece: the claim is a single upsert on this index, so concurrent adds can't both win.
            e.HasIndex(r => r.ProductId).IsUnique();
            e.HasIndex(r => r.ExpiresAt);
            e.HasOne<Product>().WithMany().HasForeignKey(r => r.ProductId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Cart>().WithMany().HasForeignKey(r => r.CartId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Order>(e =>
        {
            // Unique per store, matching the store-filtered clash check when a number is generated.
            e.HasIndex(o => new { o.StoreId, o.Number }).IsUnique();
            e.HasIndex(o => new { o.StoreId, o.UserId });
            e.Property(o => o.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(o => o.PaymentMethod).HasConversion<string>().HasMaxLength(20);
            e.OwnsOne(o => o.ShippingAddress);
            e.HasMany(o => o.Items).WithOne().HasForeignKey(i => i.OrderId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<User>().WithMany().HasForeignKey(o => o.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasQueryFilter(o => o.StoreId == CurrentStoreId);
        });

        b.Entity<Payment>(e =>
        {
            e.Property(p => p.Status).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(p => new { p.Provider, p.ProviderRef });
            e.HasOne<Order>().WithMany().HasForeignKey(p => p.OrderId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ProcessedWebhookEvent>().HasIndex(w => new { w.Provider, w.EventId }).IsUnique();
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        StampStoreId();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken ct = default)
    {
        StampStoreId();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, ct);
    }

    // New tenant-owned rows default to the request's store, so feature code can't forget it.
    private void StampStoreId()
    {
        foreach (var entry in ChangeTracker.Entries<IStoreOwned>())
        {
            if (entry.State == EntityState.Added && entry.Entity.StoreId == Guid.Empty)
                entry.Entity.StoreId = tenant.StoreId;
        }
    }
}
