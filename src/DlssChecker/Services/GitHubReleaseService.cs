using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using DlssChecker.Models;

namespace DlssChecker.Services;

public sealed class GitHubReleaseService
{
    private static readonly HttpClient HttpClient = CreateClient();

    public async Task<GitHubReleaseInfo?> GetLatestReleaseAsync(string owner, string repository)
    {
        var url = $"https://api.github.com/repos/{owner}/{repository}/releases/latest";
        using var response = await HttpClient.GetAsync(url);

        if (response.StatusCode == System.Net.HttpStatusCode.Forbidden ||
            response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            throw new InvalidOperationException("GitHub API rate limit exceeded. Please try again later.");

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync();
        var dto = await JsonSerializer.DeserializeAsync<GitHubReleaseDto>(
            stream,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

        if (dto == null || string.IsNullOrWhiteSpace(dto.TagName))
        {
            return null;
        }

        var preferredAsset = GetPreferredAsset(dto);

        return new GitHubReleaseInfo
        {
            TagName = dto.TagName,
            HtmlUrl = dto.HtmlUrl ?? string.Empty,
            Name = dto.Name ?? string.Empty,
            Version = NormalizeVersion(dto.TagName),
            DownloadUrl = preferredAsset?.BrowserDownloadUrl ?? string.Empty,
            DownloadName = preferredAsset?.Name ?? string.Empty
        };
    }

    private static GitHubReleaseAssetDto? GetPreferredAsset(GitHubReleaseDto dto)
    {
        if (dto.Assets == null || dto.Assets.Length == 0)
        {
            return null;
        }

        return dto.Assets
            .OrderByDescending(GetAssetScore)
            .ThenBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    private static int GetAssetScore(GitHubReleaseAssetDto asset)
    {
        var name = asset.Name ?? string.Empty;
        var score = 0;

        if (name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            score += 40;
        }

        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            score += 30;
        }

        if (name.Contains("portable", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("selfcontained", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("self-contained", StringComparison.OrdinalIgnoreCase))
        {
            score += 50;
        }

        if (name.Contains("win-x64", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("windows", StringComparison.OrdinalIgnoreCase))
        {
            score += 20;
        }

        if (name.Contains("dlsschecker", StringComparison.OrdinalIgnoreCase))
        {
            score += 10;
        }

        return score;
    }

    private static string NormalizeVersion(string tagName)
    {
        var value = tagName.Trim();
        if (value.StartsWith("v", StringComparison.OrdinalIgnoreCase))
        {
            value = value[1..];
        }

        return value;
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("DlssChecker", "0.0.5"));
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return client;
    }

    private sealed class GitHubReleaseDto
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; set; }

        [JsonPropertyName("html_url")]
        public string? HtmlUrl { get; set; }

        public string? Name { get; set; }

        public GitHubReleaseAssetDto[]? Assets { get; set; }
    }

    private sealed class GitHubReleaseAssetDto
    {
        public string? Name { get; set; }

        [JsonPropertyName("browser_download_url")]
        public string? BrowserDownloadUrl { get; set; }
    }
}
