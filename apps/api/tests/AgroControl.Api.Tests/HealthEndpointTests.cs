using System.Net;
using System.Net.Http.Json;
using AgroControl.Contracts.Health;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AgroControl.Api.Tests;

public sealed class HealthEndpointTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task GetHealth_ReturnsExpectedContract()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health");
        var payload = await response.Content.ReadFromJsonAsync<ApiHealthResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal("healthy", payload.Status);
        Assert.Equal("AgroControl.Api", payload.Service);
        Assert.False(string.IsNullOrWhiteSpace(payload.Version));
        Assert.False(string.IsNullOrWhiteSpace(payload.Environment));
        Assert.NotEqual(default, payload.CheckedAt);
        Assert.True(payload.Checks.ContainsKey("api"));
        Assert.Equal("healthy", payload.Checks["api"]);
    }

    [Fact]
    public async Task GetHealth_ReturnsJsonContent()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health");

        Assert.Equal("application/json; charset=utf-8", response.Content.Headers.ContentType?.ToString());
    }

    [Fact]
    public async Task GetHealthReady_WithoutConnectionString_ReturnsServiceUnavailable()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health/ready");
        var payload = await response.Content.ReadFromJsonAsync<ApiReadinessResponse>();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal("unhealthy", payload.Status);
        Assert.Single(payload.Dependencies);
        Assert.Equal("postgres", payload.Dependencies[0].Name);
    }

    [Fact]
    public async Task UnknownRoute_ReturnsProblemDetailsPayload()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/missing-route");
        var payload = await response.Content.ReadFromJsonAsync<ProblemDetailsDto>();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.NotNull(payload);
        Assert.Equal(404, payload.Status);
        Assert.Equal("/missing-route", payload.Instance);
        Assert.False(string.IsNullOrWhiteSpace(payload.TraceId));
    }

    [Fact]
    public async Task HealthEndpoint_AllowsCorsForLocalFrontend()
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, "/health");
        request.Headers.Add("Origin", "http://127.0.0.1:3001");
        request.Headers.Add("Access-Control-Request-Method", "GET");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("http://127.0.0.1:3001", response.Headers.GetValues("Access-Control-Allow-Origin").Single());
    }

    private sealed record ProblemDetailsDto(
        string? Title,
        int? Status,
        string? Instance,
        string? TraceId);
}
