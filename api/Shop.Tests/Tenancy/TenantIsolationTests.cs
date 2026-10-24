using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Shop.Domain.Catalog;
using Shop.Domain.Stores;
using Shop.Tests.Infrastructure;

namespace Shop.Tests.Tenancy;

[Collection(ApiCollection.Name)]
public sealed class TenantIsolationTests(ApiFactory factory)
{
    [Fact]
    public async Task Store_A_never_sees_store_B_data()
    {
        var storeB = new Store { Id = Guid.NewGuid(), Name = "Other", Slug = $"other-{Guid.NewGuid():N}" };
        var categoryB = new Category { Id = Guid.NewGuid(), StoreId = storeB.Id, Name = "Secret", Slug = "secret" };
        var productB = new Product
        {
            Id = Guid.NewGuid(),
            StoreId = storeB.Id,
            CategoryId = categoryB.Id,
            Name = "Store B only",
            Slug = "store-b-only",
            Description = "-",
            Materials = "-",
            Dimensions = "-",
            Price = 10,
            InventoryType = InventoryType.Standard,
            StockQuantity = 5,
            CreatedAt = DateTime.UtcNow,
        };

        var (arrangeScope, arrange) = await factory.CreateDbAsync(storeB.Id);
        await using (arrangeScope)
        {
            arrange.AddRange(storeB, categoryB, productB);
            await arrange.SaveChangesAsync();
        }

        var (scopeA, dbA) = await factory.CreateDbAsync();
        await using (scopeA)
        {
            (await dbA.Products.AnyAsync(p => p.Id == productB.Id)).Should().BeFalse();
            (await dbA.Categories.AnyAsync(c => c.Id == categoryB.Id)).Should().BeFalse();
            (await dbA.Products.AnyAsync(p => p.StoreId == storeB.Id)).Should().BeFalse();
        }

        var (scopeB, dbB) = await factory.CreateDbAsync(storeB.Id);
        await using (scopeB)
        {
            (await dbB.Products.Select(p => p.Id).ToListAsync()).Should().Equal(productB.Id);
        }
    }
}
