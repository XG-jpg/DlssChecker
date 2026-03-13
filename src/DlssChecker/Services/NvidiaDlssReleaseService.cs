using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace DlssChecker.Services;

public sealed class NvidiaDlssReleaseService
{
    private static readonly HttpClient HttpClient = CreateClient();
    private const string ApiUrl = "https://api.github.com/repos/NVIDIA/DLSS/releases/latest";

    public async Task<NvidiaDlssRelease?> GetLatestAsync()
    {
        using var response = await HttpClient.GetAsync(ApiUrl);

        if (response.StatusCode == System.Net.HttpStatusCode.Forbidden ||
            response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            return null; // rate limited — caller falls back to local

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
            Version = NormalizeVersion(dto.TagName)
        };
    }

    private static ReleaseAssetDto? PickWindowsAsset(ReleaseDto dto)
    {
        if (dto.Assets == null || dto.Assets.Length == 0) return null;

        // Prefer the _windows.zip (demo app that bundles nvngx_dlss.dll)
        foreach (var asset in dto.Assets)
        {
            var name = asset.Name ?? string.Empty;
            if (name.EndsWith("_windows.zip", StringComparison.OrdinalIgnoreCase))
                return asset;
        }

        // Fallback: any zip
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
            new ProductInfoHeaderValue("DlssChecker", "0.0.4"));
        client.DefaultRequestHeaders.Accept.Add(
            new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return client;
    }

    private sealed class ReleaseDto
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; set; }

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
}
