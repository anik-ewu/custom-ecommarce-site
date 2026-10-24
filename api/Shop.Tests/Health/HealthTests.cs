using System.Net;
using FluentAssertions;
using Shop.Tests.Infrastructure;

namespace Shop.Tests.Health;

[Collection(ApiCollection.Name)]
public sealed class HealthTests(ApiFactory factory)
{
    [Fact]
    public async Task Health_returns_ok_when_database_is_reachable()
    {
        var response = await factory.CreateClient().GetAsync("/api/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Ping_returns_ok()
    {
        var response = await factory.CreateClient().GetAsync("/api/ping");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
