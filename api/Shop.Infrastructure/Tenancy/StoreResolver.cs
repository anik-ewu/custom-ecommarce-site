using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Shop.Infrastructure.Data;

namespace Shop.Infrastructure.Tenancy;

/// <summary>Resolves a store slug to its id and caches it; the store never changes while the app runs.</summary>
public sealed class StoreResolver
{
    private readonly ConcurrentDictionary<string, Guid> _ids = new();

    public async Task<Guid?> ResolveAsync(ShopDbContext db, string slug, CancellationToken ct)
    {
        if (_ids.TryGetValue(slug, out var cached))
            return cached;

        // A missing store isn't cached, so it is picked up as soon as the seed creates it.
        var id = await db.Stores.Where(s => s.Slug == slug).Select(s => (Guid?)s.Id).FirstOrDefaultAsync(ct);
        if (id is { } found)
            _ids[slug] = found;
        return id;
    }
}
