namespace DlssChecker.Models;

public sealed class GitHubReleaseInfo
{
    public string TagName { get; init; } = string.Empty;
    public string HtmlUrl { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public string DownloadUrl { get; init; } = string.Empty;
    public string DownloadName { get; init; } = string.Empty;
}
