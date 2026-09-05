namespace backend.Settings;

// Named CorsSettings, not CorsOptions, to avoid colliding with
// Microsoft.AspNetCore.Cors.Infrastructure.CorsOptions.
public sealed class CorsSettings
{
    public const string SectionName = "Cors";
    public const string PolicyName = "frontend";

    public string[] AllowedOrigins { get; init; } = [];
}
