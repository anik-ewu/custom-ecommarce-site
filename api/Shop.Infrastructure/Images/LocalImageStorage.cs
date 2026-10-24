using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Shop.Infrastructure.Images;

/// <summary>Writes uploads to a local folder that the API serves at /api/uploads (development without Cloudinary).</summary>
public sealed class LocalImageStorage(IOptions<ImageStorageOptions> options, IHostEnvironment env) : IImageStorage
{
    public const string RequestPath = "/api/uploads";

    public string Folder { get; } = Path.GetFullPath(Path.Combine(env.ContentRootPath, options.Value.LocalPath));

    public async Task<string> SaveAsync(Stream content, string extension, string contentType, CancellationToken ct)
    {
        Directory.CreateDirectory(Folder);
        var name = $"{Guid.NewGuid():N}{extension}";
        await using (var file = File.Create(Path.Combine(Folder, name)))
            await content.CopyToAsync(file, ct);
        return $"{RequestPath}/{name}";
    }
}
