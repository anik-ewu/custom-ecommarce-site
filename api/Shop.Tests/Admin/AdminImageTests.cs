using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Shop.Application.Admin;
using Shop.Application.Catalog;
using Shop.Tests.Infrastructure;
using static Shop.Tests.Admin.AdminTestHelpers;
using static Shop.Tests.Auth.AuthTestHelpers;
using static Shop.Tests.Orders.CheckoutTestHelpers;

namespace Shop.Tests.Admin;

[Collection(ApiCollection.Name)]
public sealed class AdminImageTests(ApiFactory factory)
{
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>A complete, valid 1x1 transparent PNG.</summary>
    private static byte[] TinyPng() =>
        Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");

    [Fact]
    public async Task Uploaded_png_is_served_and_can_be_attached_to_and_removed_from_a_product()
    {
        var admin = await AdminClientAsync(factory);
        var png = TinyPng();
        png.AsSpan(0, 8).ToArray().Should().Equal(PngSignature);

        var upload = await ReadOkAsync<UploadResultDto>(await UploadAsync(admin, png, "pixel.png", "image/png"));
        upload.Url.Should().MatchRegex("^/api/uploads/[0-9a-f]{32}\\.png$");

        // Served publicly (no token), byte for byte.
        var served = await factory.CreateApiClient().GetAsync(upload.Url);
        served.StatusCode.Should().Be(HttpStatusCode.OK);
        served.Content.Headers.ContentType?.MediaType.Should().Be("image/png");
        (await served.Content.ReadAsByteArrayAsync()).Should().Equal(png);

        var categoryId = await CreateCategoryAsync(factory);
        var product = await CreateProductOkAsync(admin, ProductRequest(categoryId, $"Brass Bowl {Token()}"));

        var withImage = await ReadProductAsync(await admin.PostAsJsonAsync($"/api/admin/products/{product.Id}/images",
            new { url = upload.Url, alt = "  A brass bowl on linen  " }));
        withImage.Images.Should().ContainSingle();
        var image = withImage.Images[0];
        image.Url.Should().Be(upload.Url);
        image.Alt.Should().Be("A brass bowl on linen");
        (await GetProductOkAsync(admin, product.Id)).Images.Select(i => i.Url).First().Should().Be(upload.Url);

        var publicPage = await ReadOkAsync<ProductDetailDto>(await factory.CreateApiClient().GetAsync($"/api/products/{product.Slug}"));
        publicPage.Images.Should().Equal(new ImageDto(upload.Url, "A brass bowl on linen"));

        var removed = await ReadProductAsync(await admin.DeleteAsync($"/api/admin/products/{product.Id}/images/{image.Id}"));
        removed.Images.Should().BeEmpty();
        (await GetProductOkAsync(admin, product.Id)).Images.Should().BeEmpty();
        (await ReadOkAsync<ProductDetailDto>(await factory.CreateApiClient().GetAsync($"/api/products/{product.Slug}")))
            .Images.Should().BeEmpty();
    }

    [Fact]
    public async Task A_second_image_is_added_after_the_first()
    {
        var admin = await AdminClientAsync(factory);
        var categoryId = await CreateCategoryAsync(factory);
        var product = await CreateProductOkAsync(admin, ProductRequest(categoryId, $"Two Photos {Token()}"));

        await ReadProductAsync(await admin.PostAsJsonAsync($"/api/admin/products/{product.Id}/images",
            new { url = "https://res.cloudinary.com/demo/one.jpg", alt = "One" }));
        var both = await ReadProductAsync(await admin.PostAsJsonAsync($"/api/admin/products/{product.Id}/images",
            new { url = "https://res.cloudinary.com/demo/two.jpg", alt = "Two" }));

        both.Images.Select(i => i.Alt).Should().Equal("One", "Two");
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("http://example.com/a.png")]
    [InlineData("data:image/png;base64,AAAA")]
    public async Task Attaching_an_image_url_that_is_not_https_or_an_upload_returns_400(string url)
    {
        var admin = await AdminClientAsync(factory);
        var categoryId = await CreateCategoryAsync(factory);
        var product = await CreateProductOkAsync(admin, ProductRequest(categoryId, $"Bad Url {Token()}"));

        var response = await admin.PostAsJsonAsync($"/api/admin/products/{product.Id}/images", new { url, alt = "x" });

        (await ValidationErrorKeysAsync(response)).Should().Contain("url");
        (await GetProductOkAsync(admin, product.Id)).Images.Should().BeEmpty();
    }

    [Fact]
    public async Task Removing_an_image_that_is_not_on_the_product_returns_404_NOT_FOUND()
    {
        var admin = await AdminClientAsync(factory);
        var categoryId = await CreateCategoryAsync(factory);
        var product = await CreateProductOkAsync(admin, ProductRequest(categoryId, $"No Image {Token()}"));

        await ShouldBeProblemAsync(await admin.DeleteAsync($"/api/admin/products/{product.Id}/images/{Guid.NewGuid()}"),
            404, "NOT_FOUND");
    }

    [Fact]
    public async Task Uploading_a_text_file_renamed_to_png_returns_400_and_stores_nothing()
    {
        var admin = await AdminClientAsync(factory);
        var before = StoredFileCount();

        var response = await UploadAsync(admin, "just some text, not an image"u8.ToArray(), "photo.png", "image/png");

        (await ValidationErrorKeysAsync(response)).Should().Contain("file");
        StoredFileCount().Should().Be(before);
    }

    [Fact]
    public async Task Uploading_more_than_5_MB_is_rejected_and_stores_nothing()
    {
        var admin = await AdminClientAsync(factory);
        var before = StoredFileCount();
        // A real PNG signature, so only the size can be the reason for the rejection.
        var big = new byte[ImageUploadService.MaxBytes + 1];
        PngSignature.CopyTo(big, 0);

        var response = await UploadAsync(admin, big, "big.png", "image/png");

        if (response.StatusCode == HttpStatusCode.RequestEntityTooLarge)
            ((int)response.StatusCode).Should().Be(413);
        else
            (await ValidationErrorKeysAsync(response)).Should().Contain("file");
        StoredFileCount().Should().Be(before);
    }

    [Fact]
    public async Task Upload_without_a_file_returns_400_VALIDATION_FAILED()
    {
        var admin = await AdminClientAsync(factory);
        using var form = new MultipartFormDataContent { { new StringContent("x"), "notfile" } };

        var response = await admin.PostAsync("/api/admin/uploads", form);

        (await ValidationErrorKeysAsync(response)).Should().Contain("file");
    }

    private static Task<HttpResponseMessage> UploadAsync(HttpClient client, byte[] bytes, string fileName, string contentType)
    {
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        var form = new MultipartFormDataContent { { file, "file", fileName } };
        return client.PostAsync("/api/admin/uploads", form);
    }

    // Tests in this collection run one at a time, so the folder only changes through this test's own request.
    private static int StoredFileCount() =>
        Directory.Exists(ApiFactory.UploadsPath) ? Directory.GetFiles(ApiFactory.UploadsPath).Length : 0;
}
