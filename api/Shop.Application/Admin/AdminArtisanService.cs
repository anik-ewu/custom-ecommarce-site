using Microsoft.EntityFrameworkCore;
using Shop.Application.Common;
using Shop.Domain.Catalog;
using Shop.Infrastructure.Data;

namespace Shop.Application.Admin;

public sealed class AdminArtisanService(ShopDbContext db)
{
    public async Task<IReadOnlyList<AdminArtisanDto>> ListAsync(CancellationToken ct) =>
        await db.Artisans.AsNoTracking()
            .OrderBy(a => a.Name)
            .Select(a => new AdminArtisanDto(a.Id, a.Name, a.Region, a.Story, a.PhotoUrl,
                db.Products.Count(p => p.ArtisanId == a.Id)))
            .ToListAsync(ct);

    public async Task<AdminArtisanDto> CreateAsync(ArtisanWriteRequest request, CancellationToken ct)
    {
        var artisan = new Artisan { Id = Guid.NewGuid(), Name = "", Region = "", Story = "" };
        Apply(artisan, request);
        db.Artisans.Add(artisan);
        await db.SaveChangesAsync(ct);
        return new AdminArtisanDto(artisan.Id, artisan.Name, artisan.Region, artisan.Story, artisan.PhotoUrl, 0);
    }

    public async Task<AdminArtisanDto> UpdateAsync(Guid id, ArtisanWriteRequest request, CancellationToken ct)
    {
        var artisan = await db.Artisans.FirstOrDefaultAsync(a => a.Id == id, ct)
            ?? throw AppException.NotFound("We couldn't find that artisan.");
        Apply(artisan, request);
        await db.SaveChangesAsync(ct);
        var count = await db.Products.CountAsync(p => p.ArtisanId == id, ct);
        return new AdminArtisanDto(artisan.Id, artisan.Name, artisan.Region, artisan.Story, artisan.PhotoUrl, count);
    }

    private static void Apply(Artisan artisan, ArtisanWriteRequest request)
    {
        var photo = string.IsNullOrWhiteSpace(request.PhotoUrl) ? null : request.PhotoUrl.Trim();
        if (photo is not null && !photo.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            && !photo.StartsWith("/api/uploads/", StringComparison.Ordinal) && !photo.StartsWith("/placeholders/", StringComparison.Ordinal))
            throw AdminProductService.Validation("photoUrl", "Upload the photo first, then add it.");

        artisan.Name = request.Name.Trim();
        artisan.Region = request.Region.Trim();
        artisan.Story = request.Story.Trim();
        artisan.PhotoUrl = photo;
    }
}
