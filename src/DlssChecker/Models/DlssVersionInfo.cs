namespace DlssChecker.Models;

public sealed class DlssVersionInfo
{
    public string LatestVersion { get; init; } = "0.0.0.0";
    public string DownloadUrl { get; init; } = string.Empty;
    public string? Sha256 { get; init; }
}
