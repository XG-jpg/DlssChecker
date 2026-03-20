using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace DlssChecker.Services;

public sealed class NvidiaDlssReleaseService
{
    private static readonly HttpClient HttpClient = CreateClient();
    private const string LatestApiUrl = "https://api.github.com/repos/NVIDIA/DLSS/releases/latest";
    private const string AllReleasesApiUrl = "https://api.github.com/repos/NVIDIA/DLSS/releases?per_page=30";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(1);
    private static readonly string CacheFile =
        System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dlss_versions_cache.json");

    public async Task<NvidiaDlssRelease?> GetLatestAsync()
    {
        using var response = await HttpClient.GetAsync(LatestApiUrl);

        if (response.StatusCode == System.Net.HttpStatusCode.Forbidden ||
            response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            return null;

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync();
        var dto = await JsonSerializer.DeserializeAsync<ReleaseDto>(stream,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (dto == null) return null;

        var asset = PickWindowsAsset(dto);
        if (asset?.BrowserDownloadUrl == null) return null;

        return new NvidiaDlssRelease
        {
            DownloadUrl = asset.BrowserDownloadUrl,
            Version = NormalizeVersion(dto.TagName),
            PublishedAt = dto.PublishedAt
        };
    }

    public async Task<List<NvidiaDlssRelease>> GetAllReleasesAsync()
    {
        // Return cached data if fresh enough
        var cached = TryLoadCache();
        if (cached != null) return cached;

        // Fetch from GitHub
        using var response = await HttpClient.GetAsync(AllReleasesApiUrl);

        if (response.StatusCode == System.Net.HttpStatusCode.Forbidden ||
            response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
        {
            // Return stale cache rather than failing with no data
            var stale = TryLoadCache(ignoreAge: true);
            if (stale != null) return stale;
            throw new InvalidOperationException("Превышен лимит запросов GitHub API (60/час). Подождите немного и попробуйте снова.");
        }

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync();
        var dtos = await JsonSerializer.DeserializeAsync<ReleaseDto[]>(stream,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (dtos == null) return [];

        var result = new List<NvidiaDlssRelease>();
        foreach (var dto in dtos)
        {
            var asset = PickWindowsAsset(dto);
            if (asset?.BrowserDownloadUrl == null) continue;

            result.Add(new NvidiaDlssRelease
            {
                DownloadUrl = asset.BrowserDownloadUrl,
                Version = NormalizeVersion(dto.TagName),
                PublishedAt = dto.PublishedAt
            });
        }

        SaveCache(result);
        return result;
    }

    private static List<NvidiaDlssRelease>? TryLoadCache(bool ignoreAge = false)
    {
        try
        {
            if (!System.IO.File.Exists(CacheFile)) return null;

            var json = System.IO.File.ReadAllText(CacheFile);
            var cache = JsonSerializer.Deserialize<CacheDto>(json);
            if (cache == null) return null;

            if (!ignoreAge && DateTime.UtcNow - cache.SavedAt > CacheTtl) return null;

            return cache.Releases;
        }
        catch { return null; }
    }

    private static void SaveCache(List<NvidiaDlssRelease> releases)
    {
        try
        {
            var cache = new CacheDto { SavedAt = DateTime.UtcNow, Releases = releases };
            System.IO.File.WriteAllText(CacheFile,
                JsonSerializer.Serialize(cache, new JsonSerializerOptions { WriteIndented = false }));
        }
        catch { }
    }

    private sealed class CacheDto
    {
        public DateTime SavedAt { get; set; }
        public List<NvidiaDlssRelease> Releases { get; set; } = [];
    }

    private static ReleaseAssetDto? PickWindowsAsset(ReleaseDto dto)
    {
        if (dto.Assets == null || dto.Assets.Length == 0) return null;

        foreach (var asset in dto.Assets)
        {
            var name = asset.Name ?? string.Empty;
            if (name.EndsWith("_windows.zip", StringComparison.OrdinalIgnoreCase))
                return asset;
        }

        foreach (var asset in dto.Assets)
        {
            if (asset.Name?.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) == true)
                return asset;
        }

        return null;
    }

    private static string? NormalizeVersion(string? tagName)
    {
        if (string.IsNullOrWhiteSpace(tagName)) return null;
        var v = tagName.Trim();
        if (v.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            v = v[1..];
        return v;
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("DlssChecker", AppInfo.Version));
        client.DefaultRequestHeaders.Accept.Add(
            new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return client;
    }

    private sealed class ReleaseDto
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; set; }

        [JsonPropertyName("published_at")]
        public DateTime PublishedAt { get; set; }

        public ReleaseAssetDto[]? Assets { get; set; }
    }

    private sealed class ReleaseAssetDto
    {
        public string? Name { get; set; }

        [JsonPropertyName("browser_download_url")]
        public string? BrowserDownloadUrl { get; set; }
    }
}

public sealed class NvidiaDlssRelease
{
    public string DownloadUrl { get; init; } = string.Empty;
    public string? Version { get; init; }
    public DateTime PublishedAt { get; init; }
}
