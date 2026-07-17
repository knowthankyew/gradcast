namespace GradCast.Api.Configuration;

public class AdzunaOptions
{
    public const string SectionName = "Adzuna";

    public string AppId { get; set; } = string.Empty;
    public string AppKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.adzuna.com/v1/api";
    public string Country { get; set; } = "us";
}
