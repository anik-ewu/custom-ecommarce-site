using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Shop.Application.Catalog;
using Shop.Application.Common;
using Shop.Domain.Catalog;
using Shop.Domain.Stores;
using Shop.Tests.Infrastructure;

namespace Shop.Tests.Catalog;

[Collection(ApiCollection.Name)]
public sealed class CatalogTests(ApiFactory factory)
{
    [Fact]
    public async Task Categories_are_listed_in_sort_order()
    {
        var categories = await factory.CreateApiClient().GetFromJsonAsync<List<CategoryDto>>("/api/categories");

        categories!.Select(c => c.Slug).Should().StartWith(["kantha-cushions", "runners-wall-hangings"]);
    }

    [Fact]
    public async Task Products_are_paged_and_filtered_by_category()
    {
        var client = factory.CreateApiClient();

        var page = await client.GetFromJsonAsync<Paged<ProductCardDto>>("/api/products?category=jute-bags&pageSize=3");

        page!.TotalCount.Should().Be(4);
        page.Items.Should().HaveCount(3);
        page.Items.Should().OnlyContain(p => p.CategorySlug == "jute-bags" && !p.IsUnique);
        page.TotalPages.Should().Be(2);
    }

    [Fact]
    public async Task Search_matches_name_description_and_materials_case_insensitively()
    {
        var page = await factory.CreateApiClient().GetFromJsonAsync<Paged<ProductCardDto>>("/api/products?q=LOTUS");

        page!.Items.Select(p => p.Slug).Should().Contain("lotus-pond-cushion");
    }

    [Fact]
    public async Task Search_treats_like_wildcards_literally()
    {
        var page = await factory.CreateApiClient().GetFromJsonAsync<Paged<ProductCardDto>>("/api/products?q=%25");

        page!.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Sort_by_price_ascending()
    {
        var page = await factory.CreateApiClient()
            .GetFromJsonAsync<Paged<ProductCardDto>>("/api/products?sort=price-asc&pageSize=48");

        page!.Items.Select(p => p.Price).Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task Invalid_query_returns_validation_failed()
    {
        var response = await factory.CreateApiClient().GetAsync("/api/products?sort=cheapest&page=0");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await CodeOf(response)).Should().Be("VALIDATION_FAILED");
    }

    [Fact]
    public async Task Product_detail_includes_images_category_and_artisan()
    {
        var product = await factory.CreateApiClient().GetFromJsonAsync<ProductDetailDto>("/api/products/lotus-pond-cushion");

        product!.IsUnique.Should().BeTrue();
        product.Images.Should().NotBeEmpty().And.OnlyContain(i => i.Alt.Length > 0);
        product.Category.Slug.Should().Be("kantha-cushions");
        product.Artisan!.Name.Should().Be("Rahima Begum");
    }

    [Fact]
    public async Task Unknown_product_returns_not_found()
    {
        var response = await factory.CreateApiClient().GetAsync("/api/products/no-such-piece");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await CodeOf(response)).Should().Be("NOT_FOUND");
    }

    [Fact]
    public async Task Product_from_another_store_returns_not_found()
    {
        var storeB = new Store { Id = Guid.NewGuid(), Name = "Other", Slug = $"other-{Guid.NewGuid():N}" };
        var category = new Category { Id = Guid.NewGuid(), StoreId = storeB.Id, Name = "B", Slug = "b" };
        var slug = $"store-b-{Guid.NewGuid():N}";
        var (scope, db) = await factory.CreateDbAsync(storeB.Id);
        await using (scope)
        {
            db.AddRange(storeB, category, new Product
            {
                Id = Guid.NewGuid(),
                StoreId = storeB.Id,
                CategoryId = category.Id,
                Name = "Hidden",
                Slug = slug,
                Description = "-",
                Materials = "-",
                Dimensions = "-",
                Price = 1,
                InventoryType = InventoryType.Standard,
                StockQuantity = 1,
                CreatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        var client = factory.CreateApiClient();
        (await client.GetAsync($"/api/products/{slug}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        var all = await client.GetFromJsonAsync<Paged<ProductCardDto>>("/api/products?q=Hidden");
        all!.TotalCount.Should().Be(0);
    }

    private static async Task<string?> CodeOf(HttpResponseMessage response)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("code").GetString();
    }
}

[Collection(ApiCollection.Name)]
public sealed class ArtisanAndFeaturedTests(ApiFactory factory)
{
    [Fact]
    public async Task Artisans_list_and_detail_include_their_pieces()
    {
        var client = factory.CreateApiClient();
        var artisans = await client.GetFromJsonAsync<List<ArtisanSummaryDto>>("/api/artisans");
        var karim = artisans!.Single(a => a.Name == "Abdul Karim");

        var detail = await client.GetFromJsonAsync<ArtisanDetailDto>($"/api/artisans/{karim.Id}");

        detail!.Artisan.Region.Should().Be("Rangpur");
        detail.Products.Should().HaveCount(8).And.OnlyContain(p => p.ArtisanName == "Abdul Karim");
    }

    [Fact]
    public async Task Unknown_artisan_returns_not_found()
    {
        var response = await factory.CreateApiClient().GetAsync($"/api/artisans/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Type_and_in_stock_filters_select_featured_unique_pieces()
    {
        var page = await factory.CreateApiClient()
            .GetFromJsonAsync<Paged<ProductCardDto>>("/api/products?type=unique&inStock=true&pageSize=48");

        page!.Items.Should().NotBeEmpty().And.OnlyContain(p => p.IsUnique && p.InStock);
    }

    [Fact]
    public async Task Categories_carry_a_tile_image()
    {
        var categories = await factory.CreateApiClient().GetFromJsonAsync<List<CategoryDto>>("/api/categories");

        categories!.Where(c => c.Slug.StartsWith("kantha") || c.Slug.StartsWith("jute"))
            .Should().OnlyContain(c => c.ImageUrl != null);
    }
}
