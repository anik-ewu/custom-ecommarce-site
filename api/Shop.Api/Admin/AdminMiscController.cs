using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shop.Application.Admin;

namespace Shop.Api.Admin;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin")]
public sealed class AdminMiscController(
    AdminDashboardService dashboard,
    AdminArtisanService artisans,
    ImageUploadService uploads) : ControllerBase
{
    [HttpGet("dashboard")]
    public Task<DashboardDto> Dashboard(CancellationToken ct) => dashboard.GetAsync(ct);

    [HttpGet("artisans")]
    public Task<IReadOnlyList<AdminArtisanDto>> Artisans(CancellationToken ct) => artisans.ListAsync(ct);

    [HttpPost("artisans")]
    public Task<AdminArtisanDto> CreateArtisan(ArtisanWriteRequest request, CancellationToken ct) =>
        artisans.CreateAsync(request, ct);

    [HttpPut("artisans/{id:guid}")]
    public Task<AdminArtisanDto> UpdateArtisan(Guid id, ArtisanWriteRequest request, CancellationToken ct) =>
        artisans.UpdateAsync(id, request, ct);

    /// <summary>Multipart upload (field "file"); returns the URL to attach to a product or artisan.</summary>
    [HttpPost("uploads")]
    [RequestSizeLimit(ImageUploadService.MaxBytes + 64 * 1024)]
    public async Task<UploadResultDto> Upload(IFormFile? file, CancellationToken ct)
    {
        if (file is null)
            throw AdminValidation.Missing("file", "Choose an image to upload.");
        await using var stream = file.OpenReadStream();
        return await uploads.UploadAsync(stream, file.Length, ct);
    }
}
