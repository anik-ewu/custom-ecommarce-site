namespace Shop.Infrastructure.Images;

/// <summary>Stores an uploaded image and returns its public URL. Only the URL goes into the database.</summary>
public interface IImageStorage
{
    Task<string> SaveAsync(Stream content, string extension, string contentType, CancellationToken ct);
}
