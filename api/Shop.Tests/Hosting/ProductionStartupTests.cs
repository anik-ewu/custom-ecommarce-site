using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Shop.Application.Auth;
using Shop.Tests.Infrastructure;

namespace Shop.Tests.Hosting;

/// <summary>The API as Render runs it: Production, no Cloudinary settings.</summary>
public sealed class ProductionApiFactory : ApiFactory
{
    public string UploadsFolder { get; } = Path.Combine(Path.GetTempPath(), $"kinbo-prod-uploads-{Guid.NewGuid():N}");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseEnvironment("Production");
        builder.UseSetting("Images:LocalPath", UploadsFolder);
    }
}

// Regression: in Production the API created the local uploads folder at startup, which crashed in the container
// (non-root user, root-owned /app). Production must start without it and refuse uploads instead.
public sealed class ProductionStartupTests(ProductionApiFactory factory) : IClassFixture<ProductionApiFactory>
{
    [Fact]
    public async Task Starts_without_creating_or_serving_a_local_uploads_folder()
    {
        var client = factory.CreateApiClient();

        (await client.GetAsync("/api/health")).StatusCode.Should().Be(HttpStatusCode.OK);
        Directory.Exists(factory.UploadsFolder).Should().BeFalse();
        (await client.GetAsync("/api/uploads/anything.png")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Refuses_uploads_without_cloudinary()
    {
        var client = factory.CreateApiClient();
        var demo = await client.PostAsJsonAsync("/api/auth/demo", new { role = "admin" });
        var auth = await demo.Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        using var form = new MultipartFormDataContent();
        var png = new ByteArrayContent([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0]);
        png.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(png, "file", "photo.png");
        var response = await client.PostAsync("/api/admin/uploads", form);

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("code").GetString().Should().Be("IMAGE_STORAGE_NOT_CONFIGURED");
        Directory.Exists(factory.UploadsFolder).Should().BeFalse();
    }
}
