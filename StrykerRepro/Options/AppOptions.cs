namespace StrykerRepro.Options;

public class AppOptions
{
    public string Region { get; set; } = "eu-west-1";
    public string EnvironmentName { get; set; } = "production";
    public string? ServiceName { get; set; }
}
