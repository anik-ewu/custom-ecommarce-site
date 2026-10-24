namespace Shop.Domain;

/// <summary>Marks tenant-owned entities; DbContext applies the StoreId query filter to all of them.</summary>
public interface IStoreOwned
{
    Guid StoreId { get; set; }
}
