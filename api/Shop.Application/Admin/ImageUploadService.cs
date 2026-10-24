using Microsoft.Extensions.Hosting;
using Shop.Application.Common;
using Shop.Infrastructure.Images;

namespace Shop.Application.Admin;

/// <summary>Accepts only real JPEG, PNG or WebP files (checked by their first bytes, not the client's claim).</summary>
public sealed class ImageUploadService(IImageStorage storage, IHostEnvironment env)
{
    public const long MaxBytes = 5 * 1024 * 1024;

    public async Task<UploadResultDto> UploadAsync(Stream content, long length, CancellationToken ct)
    {
        // Production disks (Render) are wiped on every deploy: refuse rather than store photos that will vanish.
        if (storage is LocalImageStorage && env.IsProduction())
            throw new AppException(503, ErrorCodes.ImageStorageNotConfigured,
                "Photo uploads need Cloudinary. Set the Images:Cloudinary settings and restart.");

        if (length is <= 0 or > MaxBytes)
            throw AdminProductService.Validation("file", "Choose an image up to 5 MB.");

        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        var (extension, contentType) = Sniff(buffer.GetBuffer().AsSpan(0, (int)buffer.Length))
            ?? throw AdminProductService.Validation("file", "Use a JPEG, PNG or WebP image.");

        buffer.Position = 0;
        return new UploadResultDto(await storage.SaveAsync(buffer, extension, contentType, ct));
    }

    internal static (string Extension, string ContentType)? Sniff(ReadOnlySpan<byte> bytes) => bytes switch
    {
        [0xFF, 0xD8, 0xFF, ..] => (".jpg", "image/jpeg"),
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, ..] => (".png", "image/png"),
        [0x52, 0x49, 0x46, 0x46, _, _, _, _, 0x57, 0x45, 0x42, 0x50, ..] => (".webp", "image/webp"),
        _ => null,
    };
}
