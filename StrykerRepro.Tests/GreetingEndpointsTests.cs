using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace StrykerRepro.Tests;

/// <summary>
/// Integration test exercising GreetingEndpoints via WebApplicationFactory.
/// This ensures the request delegate pipeline is exercised and the endpoints
/// behave correctly, which means any mutation in GreetingEndpoints.cs that
/// changes observable behaviour will be caught by these tests.
///
/// When running under Stryker, the compilation of the mutated GreetingEndpoints.cs
/// triggers the CS9234 bug (see GreetingEndpoints.cs for details).
/// </summary>
public class GreetingEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public GreetingEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Get_WithoutName_ReturnsHelloWorld()
    {
        var response = await _client.GetAsync("/greet");

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal("Hello, World!", body);
    }

    [Fact]
    public async Task Get_WithName_ReturnsPersonalisedGreeting()
    {
        var response = await _client.GetAsync("/greet?name=Alice");

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal("Hello, Alice!", body);
    }

    [Fact]
    public async Task Post_WithEmptyName_ReturnsBadRequest()
    {
        var response = await _client.PostAsJsonAsync("/greet", new { Name = "" });

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_WithName_ReturnsOk()
    {
        var response = await _client.PostAsJsonAsync("/greet", new { Name = "Bob" });

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Bob", body);
    }
}
