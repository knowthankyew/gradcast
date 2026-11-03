using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace GradCast.Api.Tests.Endpoints;

public class SearchEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public SearchEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Theory]
    [InlineData("/api/schools/search?q=")]
    [InlineData("/api/schools/search?q=a")]
    [InlineData("/api/schools/search?q=%22%22")]
    [InlineData("/api/schools/search?q=%22a%22")]
    [InlineData("/api/schools/search?q=%22a")]
    [InlineData("/api/locations/search?q=a")]
    [InlineData("/api/locations/search?q=%22%22")]
    [InlineData("/api/locations/search?q=%22a%22")]
    public async Task SearchEndpoints_RejectQueriesWithFewerThanTwoEffectiveCharacters(string url)
    {
        var response = await _client.GetAsync(url);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task LocationSearchEndpoint_AllowsEmptyQueryForInitialMetroPreload()
    {
        var response = await _client.GetAsync("/api/locations/search");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/schools/search?q=Texas&state=INVALID")]
    [InlineData("/api/schools/search?q=Texas&state=ZZ")]
    [InlineData("/api/schools/search?q=Texas&state=12")]
    public async Task SchoolSearchEndpoint_RejectsNonAllowlistedStateCodes(string url)
    {
        var response = await _client.GetAsync(url);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SearchEndpoints_RejectOverlyLongQueries()
    {
        var oversized = new string('a', 201);
        var schoolResp = await _client.GetAsync($"/api/schools/search?q={oversized}");
        var locationResp = await _client.GetAsync($"/api/locations/search?q={oversized}");

        Assert.Equal(HttpStatusCode.BadRequest, schoolResp.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, locationResp.StatusCode);
    }
}
