using FluentAssertions;
using Shop.Domain.Orders;
using Shop.Domain.Stores;
using Shop.Tests.Infrastructure;
using static Shop.Tests.Orders.CheckoutTestHelpers;

namespace Shop.Tests.Orders;

[Collection(ApiCollection.Name)]
public sealed class OrderNumberTests(ApiFactory factory)
{
    // Regression: the clash check when generating a number is store-filtered, but the unique index was global,
    // so a number already used by another store passed the check and then failed the insert with a 500.
    [Fact]
    public async Task Two_stores_can_use_the_same_order_number()
    {
        var (_, userId) = await SignedInClientAsync(factory);
        var number = $"KB-20261024-{Guid.NewGuid().ToString("N")[..4].ToUpperInvariant()}";
        var storeB = new Store { Id = Guid.NewGuid(), Name = "Other", Slug = $"other-{Guid.NewGuid():N}" };

        Order NewOrder(Guid storeId) => new()
        {
            Id = Guid.NewGuid(),
            StoreId = storeId,
            UserId = userId,
            Number = number,
            Status = OrderStatus.Pending,
            ShippingAddress = new ShippingAddress { FullName = "Test", Phone = "01700000000", Line1 = "-", City = "Dhaka" },
            Subtotal = 10,
            Total = 10,
            PaymentMethod = PaymentMethod.CashOnDelivery,
            CreatedAt = DateTime.UtcNow,
        };

        var (scopeA, dbA) = await factory.CreateDbAsync();
        await using (scopeA)
        {
            dbA.Orders.Add(NewOrder(Guid.Empty));
            await dbA.SaveChangesAsync();
        }

        var (scopeB, dbB) = await factory.CreateDbAsync(storeB.Id);
        await using (scopeB)
        {
            dbB.AddRange(storeB, NewOrder(storeB.Id));
            var save = () => dbB.SaveChangesAsync();
            await save.Should().NotThrowAsync();
        }
    }
}
