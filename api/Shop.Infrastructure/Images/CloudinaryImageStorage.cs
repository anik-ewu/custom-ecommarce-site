using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace Shop.Infrastructure.Images;

/// <summary>
/// Signed server-side upload to Cloudinary's REST API (no SDK needed for one call). The secret never leaves the API.
/// See https://cloudinary.com/documentation/authentication_signatures.
/// </summary>
public sealed class CloudinaryImageStorage(HttpClient http, IOptions<ImageStorageOptions> options, TimeProvider clock)
    : IImageStorage
{
    private readonly ImageStorageOptions _o = options.Value;

    public async Task<string> SaveAsync(Stream content, string extension, string contentType, CancellationToken ct)
    {
        var timestamp = clock.GetUtcNow().ToUnixTimeSeconds().ToString();
        var signed = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["folder"] = _o.CloudinaryFolder,
            ["timestamp"] = timestamp,
        };

        using var form = new MultipartFormDataContent();
        var file = new StreamContent(content);
        file.Headers.ContentType = new(contentType);
        form.Add(file, "file", $"upload{extension}");
        foreach (var (key, value) in signed)
            form.Add(new StringContent(value), key);
        form.Add(new StringContent(_o.CloudinaryApiKey!), "api_key");
        form.Add(new StringContent(Sign(signed, _o.CloudinaryApiSecret!)), "signature");

        using var response = await http.PostAsync(
            $"https://api.cloudinary.com/v1_1/{Uri.EscapeDataString(_o.CloudinaryCloudName!)}/image/upload", form, ct);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<UploadResult>(ct)
            ?? throw new InvalidOperationException("Cloudinary returned an empty response.");
        return result.SecureUrl;
    }

    /// <summary>SHA-1 of the alphabetically sorted "key=value" pairs joined by "&amp;", followed by the API secret.</summary>
    public static string Sign(IEnumerable<KeyValuePair<string, string>> parameters, string apiSecret)
    {
        var toSign = string.Join('&', parameters.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => $"{p.Key}={p.Value}"));
        return Convert.ToHexStringLower(SHA1.HashData(Encoding.UTF8.GetBytes(toSign + apiSecret)));
    }

    private sealed record UploadResult([property: JsonPropertyName("secure_url")] string SecureUrl);
}
