using Microsoft.Extensions.Options;

namespace StrykerRepro.Options;

/// <summary>
/// Bug 2: Variable shadowing with 'is { Length: > 0 } varName' pattern variables.
///
/// The original failure was inside a PostConfigure lambda in
/// IServiceCollectionExtensions. The lambda context is important — Stryker's
/// ConditionalInstrumentationEngine generates equality mutations for the
/// relational pattern "> 0" inside each 'is { … } varName' condition. When it
/// combines multiple mutation variants in a single ternary chain, the pattern
/// variable is declared once per variant, giving CS0136 ("already defined").
///
/// Three separate 'is' pattern declarations in the same lambda body are
/// required to reproduce all three error locations seen in the original log.
/// </summary>
public static class AppOptionsExtensions
{
    public static IServiceCollection ConfigureAppOptions(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<AppOptions>(configuration.GetSection("App"));

        // The PostConfigure lambda is the context that triggers the bug.
        // Each 'is { Length: > 0 } <name>' condition generates equality
        // mutations (e.g. "> 0" → ">= 0", "< 0") via EqualityMutator /
        // RelationalPatternMutator. ConditionalInstrumentationEngine then
        // places all variants in a nested ternary — each copy of the 'is'
        // pattern re-declares the variable, causing the scope conflict.
        services.PostConfigure<AppOptions>((options) =>
        {
            if (configuration["AWS_REGION"] is { Length: > 0 } region)
            {
                options.Region = region;
            }

            if (configuration["JET_ENV"] is { Length: > 0 } environmentName)
            {
                options.EnvironmentName = environmentName;
            }

            if (configuration["SERVICE_NAME"] is { Length: > 0 } serviceName)
            {
                options.ServiceName = serviceName;
            }
        });

        return services;
    }
}
