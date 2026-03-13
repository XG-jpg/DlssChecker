using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
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

        return new GitHubReleaseInfo
        {
            TagName = dto.TagName,
            HtmlUrl = dto.HtmlUrl ?? string.Empty,
            Name = dto.Name ?? string.Empty,
            Version = NormalizeVersion(dto.TagName)
        };
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
            new ProductInfoHeaderValue("DlssChecker", "0.0.1"));
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return client;
    }

    private sealed class GitHubReleaseDto
    {
        public string? TagName { get; set; }
        public string? HtmlUrl { get; set; }
        public string? Name { get; set; }
    }
}
