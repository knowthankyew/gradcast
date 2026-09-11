using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace GradCast.Api.Tests.Endpoints;

public class SpaServingTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public SpaServingTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task UnknownApiRouteReturns404NotFoundNotHtml()
    {
        var response = await _client.GetAsync("/api/unknown-nonexistent-endpoint");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var contentType = response.Content.Headers.ContentType?.MediaType;
        Assert.NotEqual("text/html", contentType);
    }

    [Fact]
    public async Task RootOrSpaRouteReturnsHtmlWhenStaticDistExists()
    {
        var response = await _client.GetAsync("/");
        // If web/dist is built, it serves text/html; if not built, it returns 404.
        // In either case, it must not return an error status (500).
        Assert.True(response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.NotFound);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var content = await response.Content.ReadAsStringAsync();
            Assert.Contains("<html", content, StringComparison.OrdinalIgnoreCase);
        }
    }
}
