using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Shop.Application.Admin;
using Shop.Application.Catalog;
using Shop.Application.Common;
using Shop.Tests.Infrastructure;
using static Shop.Tests.Admin.AdminTestHelpers;
using static Shop.Tests.Auth.AuthTestHelpers;
using static Shop.Tests.Orders.CheckoutTestHelpers;

namespace Shop.Tests.Admin;

[Collection(ApiCollection.Name)]
public sealed class AdminProductTests(ApiFactory factory)
{
    [Fact]
    public async Task Create_get_update_and_archive_a_product()
    {
        var admin = await AdminClientAsync(factory);
        var categoryId = await CreateCategoryAsync(factory);
        var token = Token();

        // Create without a slug: it is generated from the name.
        var created = await CreateProductOkAsync(admin, ProductRequest(categoryId, $"Admin Vase {token}", slug: null, stock: 4));
        created.Slug.Should().Be($"admin-vase-{token}");
        created.IsActive.Should().BeTrue();
        created.InventoryType.Should().Be("Standard");
        created.StockQuantity.Should().Be(4);
        created.Images.Should().BeEmpty();

        var loaded = await GetProductOkAsync(admin, created.Id);
        loaded.Should().BeEquivalentTo(created);

        // Update with the version the form was loaded with.
        var update = ProductRequest(categoryId, $"Admin Vase {token} renamed", slug: loaded.Slug, stock: 6, price: 900m,
            version: loaded.Version);
        var updated = await ReadProductAsync(await admin.PutAsJsonAsync($"/api/admin/products/{created.Id}", update));
        updated.Name.Should().Be($"Admin Vase {token} renamed");
        updated.Price.Should().Be(900m);
        updated.StockQuantity.Should().Be(6);
        (await GetProductOkAsync(admin, created.Id)).Version.Should().NotBe(loaded.Version);

        (await PublicSearchAsync(token)).Should().Contain(p => p.Id == created.Id);

        // Archive.
        var archive = await admin.DeleteAsync($"/api/admin/products/{created.Id}");
        archive.StatusCode.Should().Be(HttpStatusCode.NoContent, await archive.Content.ReadAsStringAsync());

        (await PublicSearchAsync(token)).Should().NotContain(p => p.Id == created.Id);
        await ShouldBeProblemAsync(await factory.CreateApiClient().GetAsync($"/api/products/{created.Slug}"), 404, "NOT_FOUND");

        var archived = await AdminListAsync(admin, $"?status=archived&q={token}");
        archived.Items.Should().ContainSingle(p => p.Id == created.Id).Which.IsActive.Should().BeFalse();
        (await AdminListAsync(admin, $"?status=active&q={token}")).Items.Should().NotContain(p => p.Id == created.Id);
        (await GetProductOkAsync(admin, created.Id)).IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Create_with_an_empty_slug_generates_one_from_the_name()
    {
        var admin = await AdminClientAsync(factory);
        var categoryId = await CreateCategoryAsync(factory);
        var token = Token();

        var created = await CreateProductOkAsync(admin, ProductRequest(categoryId, $"  Jute Basket & Lid {token}!  ", slug: ""));

        created.Slug.Should().Be($"jute-basket-lid-{token}");
        created.Name.Should().Be($"Jute Basket & Lid {token}!");
    }

    [Fact]
    public async Task Create_with_a_slug_another_product_uses_returns_409_CONFLICT_with_a_slug_error()
    {
        var admin = await AdminClientAsync(factory);
        var categoryId = await CreateCategoryAsync(factory);
        var slug = $"taken-{Token()}";
        await CreateProductOkAsync(admin, ProductRequest(categoryId, "First", slug: slug));

        var response = await admin.PostAsJsonAsync("/api/admin/products", ProductRequest(categoryId, "Second", slug: slug));

        await ShouldBeProblemAsync(response, 409, "CONFLICT");
        (await ErrorKeysAsync(response)).Should().Contain("slug");
    }

    [Fact]
    public async Task Update_to_a_slug_another_product_uses_returns_409_CONFLICT()
    {
        var admin = await AdminClientAsync(factory);
        var categoryId = await CreateCategoryAsync(factory);
        var first = await CreateProductOkAsync(admin, ProductRequest(categoryId, $"First {Token()}"));
        var second = await CreateProductOkAsync(admin, ProductRequest(categoryId, $"Second {Token()}"));

        var response = await admin.PutAsJsonAsync($"/api/admin/products/{second.Id}",
            ProductRequest(categoryId, second.Name, slug: first.Slug, version: second.Version));

        await ShouldBeProblemAsync(response, 409, "CONFLICT");
        (await GetProductOkAsync(admin, second.Id)).Slug.Should().Be(second.Slug);
    }

    [Fact]
    public async Task Create_a_unique_piece_with_stock_2_returns_400_VALIDATION_FAILED()
    {
        var admin = await AdminClientAsync(factory);
        var categoryId = await CreateCategoryAsync(factory);
        var token = Token();

        var response = await admin.PostAsJsonAsync("/api/admin/products",
            ProductRequest(categoryId, $"Nakshi Kantha {token}", inventoryType: "Unique", stock: 2));

        (await ValidationErrorKeysAsync(response)).Should().Contain(k => k.Equals("stockQuantity", StringComparison.OrdinalIgnoreCase));
        (await AdminListAsync(admin, $"?q={token}")).TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Update_without_a_version_returns_400_VALIDATION_FAILED()
    {
        var admin = await AdminClientAsync(factory);
        var categoryId = await CreateCategoryAsync(factory);
        var created = await CreateProductOkAsync(admin, ProductRequest(categoryId, $"No version {Token()}"));

        var response = await admin.PutAsJsonAsync($"/api/admin/products/{created.Id}",
            ProductRequest(categoryId, created.Name, slug: created.Slug, price: 1m, version: null));

        (await ValidationErrorKeysAsync(response)).Should().Contain("version");
        (await GetProductOkAsync(admin, created.Id)).Price.Should().Be(created.Price);
    }

    [Fact]
    public async Task Get_a_missing_product_returns_404_NOT_FOUND()
    {
        var admin = await AdminClientAsync(factory);

        await ShouldBeProblemAsync(await admin.GetAsync($"/api/admin/products/{Guid.NewGuid()}"), 404, "NOT_FOUND");
    }

    [Fact]
    public async Task Saving_a_stale_form_after_a_checkout_changed_the_stock_returns_409_CONFLICT_and_keeps_the_stock()
    {
        var admin = await AdminClientAsync(factory);
        var categoryId = await CreateCategoryAsync(factory);
        var created = await CreateProductOkAsync(admin, ProductRequest(categoryId, $"Clay Pitcher {Token()}", stock: 5, price: 600m));

        // The admin opens the edit form...
        var form = await GetProductOkAsync(admin, created.Id);
        // ...meanwhile a customer buys two...
        await PlaceOrderAsync(factory, (created.Id, 2));
        (await ProductFromDbAsync(factory, created.Id)).StockQuantity.Should().Be(3);

        // ...then the admin saves the form they loaded earlier, which still says stock 5.
        var stale = ProductRequest(categoryId, form.Name, slug: form.Slug, stock: form.StockQuantity, price: 650m,
            version: form.Version);
        var response = await admin.PutAsJsonAsync($"/api/admin/products/{created.Id}", stale);

        await ShouldBeProblemAsync(response, 409, "CONFLICT");
        var stored = await ProductFromDbAsync(factory, created.Id);
        stored.StockQuantity.Should().Be(3, "the stale form must not overwrite the stock the checkout left");
        stored.Price.Should().Be(600m, "nothing from the stale form is saved");

        // Reloading the form gives the fresh version, and that save goes through.
        var fresh = await GetProductOkAsync(admin, created.Id);
        var retry = await admin.PutAsJsonAsync($"/api/admin/products/{created.Id}",
            ProductRequest(categoryId, fresh.Name, slug: fresh.Slug, stock: fresh.StockQuantity, price: 650m, version: fresh.Version));
        (await ReadProductAsync(retry)).Should().Match<AdminProductDto>(p => p.Price == 650m && p.StockQuantity == 3);
    }

    private async Task<IReadOnlyList<ProductCardDto>> PublicSearchAsync(string q)
    {
        var page = await ReadOkAsync<Paged<ProductCardDto>>(await factory.CreateApiClient().GetAsync($"/api/products?q={q}&pageSize=48"));
        return page.Items;
    }

    private static async Task<Paged<AdminProductListItemDto>> AdminListAsync(HttpClient admin, string query) =>
        await ReadOkAsync<Paged<AdminProductListItemDto>>(await admin.GetAsync($"/api/admin/products{query}"));

    private static async Task<IReadOnlyList<string>> ErrorKeysAsync(HttpResponseMessage response)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.TryGetProperty("errors", out var errors).Should().BeTrue();
        return errors.EnumerateObject().Select(p => p.Name).ToList();
    }
}
