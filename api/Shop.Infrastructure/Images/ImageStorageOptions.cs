namespace Shop.Infrastructure.Images;

public sealed class ImageStorageOptions
{
    /// <summary>Local development folder (relative to the API's content root). Not for production: hosts like
    /// Render have ephemeral disks, so set the Cloudinary values there.</summary>
    public string LocalPath { get; set; } = "App_Data/uploads";

    public string? CloudinaryCloudName { get; set; }
    public string? CloudinaryApiKey { get; set; }
    public string? CloudinaryApiSecret { get; set; }
    public string CloudinaryFolder { get; set; } = "kinbo";

    public bool UseCloudinary =>
        !string.IsNullOrWhiteSpace(CloudinaryCloudName)
        && !string.IsNullOrWhiteSpace(CloudinaryApiKey)
        && !string.IsNullOrWhiteSpace(CloudinaryApiSecret);
}
