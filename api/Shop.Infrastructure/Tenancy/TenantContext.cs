namespace Shop.Infrastructure.Tenancy;

/// <summary>The store the current request works on. Set once per request by the tenant middleware.</summary>
public sealed class TenantContext
{
    public Guid StoreId { get; set; }
}
