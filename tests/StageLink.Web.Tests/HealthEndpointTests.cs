using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace StageLink.Web.Tests;

// Starts the whole web app in memory (no real port) and calls it over HTTP.
public class HealthEndpointTests : IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public HealthEndpointTests()
    {
        // Port 0: discovery listens on any free port, so tests never clash with 51001.
        _factory = TestApp.WithDiscoveryPort(0);
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task GetHealth_ReturnsOk()
    {
        HttpResponseMessage response = await _client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        HealthResponse? body = await response.Content.ReadFromJsonAsync<HealthResponse>();
        Assert.Equal("ok", body?.Status);
    }

    private sealed record HealthResponse(string Status);
}
