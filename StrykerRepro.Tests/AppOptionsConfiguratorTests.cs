using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StrykerRepro.Options;

namespace StrykerRepro.Tests;

public class AppOptionsExtensionsTests
{
    private static AppOptions BuildOptions(Dictionary<string, string?> values)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var services = new ServiceCollection();
        services.ConfigureAppOptions(config);
        return services.BuildServiceProvider().GetRequiredService<IOptions<AppOptions>>().Value;
    }

    [Fact]
    public void ConfigureAppOptions_WithRegion_SetsRegion()
    {
        var options = BuildOptions(new() { ["AWS_REGION"] = "us-east-1" });

        Assert.Equal("us-east-1", options.Region);
    }

    [Fact]
    public void ConfigureAppOptions_WithEmptyRegion_LeavesDefaultRegion()
    {
        var options = BuildOptions(new() { ["AWS_REGION"] = "" });

        Assert.Equal("eu-west-1", options.Region);
    }

    [Fact]
    public void ConfigureAppOptions_WithEnvironmentName_SetsEnvironmentName()
    {
        var options = BuildOptions(new() { ["JET_ENV"] = "staging" });

        Assert.Equal("staging", options.EnvironmentName);
    }

    [Fact]
    public void ConfigureAppOptions_WithNoValues_LeavesDefaults()
    {
        var options = BuildOptions(new());

        Assert.Equal("eu-west-1", options.Region);
        Assert.Equal("production", options.EnvironmentName);
        Assert.Null(options.ServiceName);
    }

    [Fact]
    public void ConfigureAppOptions_WithAllValues_SetsAll()
    {
        var options = BuildOptions(new()
        {
            ["AWS_REGION"] = "ap-southeast-1",
            ["JET_ENV"] = "dev",
            ["SERVICE_NAME"] = "my-service",
        });

        Assert.Equal("ap-southeast-1", options.Region);
        Assert.Equal("dev", options.EnvironmentName);
        Assert.Equal("my-service", options.ServiceName);
    }
}
