using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.IO.Compression;

namespace DlssChecker.Services;

public sealed class DlssUpdater
{
    private readonly HttpClient _httpClient;

    public DlssUpdater(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        try
        {
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
            _httpClient.DefaultRequestHeaders.Accept.ParseAdd("*/*");
            _httpClient.DefaultRequestHeaders.Referrer = new Uri("https://www.techspot.com/");
        }
        catch
        {
            // ignore header issues
        }
    }

    public async Task<string> DownloadAsync(string url, string destinationPath, string? expectedSha256 = null,
        IProgress<double>? progress = null)
    {
        using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? -1L;
        await using var contentStream = await response.Content.ReadAsStreamAsync();

        using var ms = new MemoryStream(totalBytes > 0 ? (int)totalBytes : 4 * 1024 * 1024);
        var buffer = new byte[81920];
        long downloaded = 0;
        int read;
        while ((read = await contentStream.ReadAsync(buffer)) > 0)
        {
            await ms.WriteAsync(buffer.AsMemory(0, read));
            downloaded += read;
            if (totalBytes > 0)
                progress?.Report((double)downloaded / totalBytes);
        }
        progress?.Report(1.0);

        var bytes = ms.ToArray();
        var contentType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
        var isZip = url.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) || contentType.Contains("zip");

        if (isZip)
        {
            var extractedPath = await ExtractDllFromZip(bytes, destinationPath);
            await ValidateHash(extractedPath, expectedSha256, isZip);
            return extractedPath;
        }
        else
        {
            await ValidateHash(bytes, expectedSha256);
            var temp = destinationPath + ".tmp";
            await File.WriteAllBytesAsync(temp, bytes);
            return temp;
        }
    }

    public async Task<string> UseLocalAsync(string localPath, string destinationPath, string? expectedSha256 = null)
    {
        if (!File.Exists(localPath))
            throw new FileNotFoundException("Файл не найден", localPath);

        var ext = Path.GetExtension(localPath).ToLowerInvariant();
        if (ext == ".zip")
        {
            var bytes = await File.ReadAllBytesAsync(localPath);
            var extracted = await ExtractDllFromZip(bytes, destinationPath);
            await ValidateHash(extracted, expectedSha256);
            return extracted;
        }
        else
        {
            var bytes = await File.ReadAllBytesAsync(localPath);
            await ValidateHash(bytes, expectedSha256);
            var temp = destinationPath + ".local.tmp";
            await File.WriteAllBytesAsync(temp, bytes);
            return temp;
        }
    }

    public void ReplaceWithBackup(string sourceTempPath, string targetPath, BackupService backup)
    {
        if (File.Exists(targetPath))
        {
            backup.CreateBackup(targetPath);
        }

        var targetDir = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrWhiteSpace(targetDir))
        {
            Directory.CreateDirectory(targetDir);
        }

        File.Copy(sourceTempPath, targetPath, overwrite: true);
        File.Delete(sourceTempPath);
    }

    private static async Task<string> ExtractDllFromZip(byte[] zipBytes, string destinationPath)
    {
        await using var ms = new MemoryStream(zipBytes);
        using var archive = new ZipArchive(ms, ZipArchiveMode.Read);

        var entry = archive.Entries
            .FirstOrDefault(e => e.Name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) &&
                                 e.Name.StartsWith("nvngx", StringComparison.OrdinalIgnoreCase));

        if (entry == null)
            throw new InvalidOperationException("В архиве не найден nvngx*.dll");

        var tempPath = destinationPath + ".unzipped.tmp";
        await using var entryStream = entry.Open();
        await using var outStream = File.Create(tempPath);
        await entryStream.CopyToAsync(outStream);
        return tempPath;
    }

    private static async Task ValidateHash(string filePath, string? expectedSha256, bool isZip = false)
    {
        if (string.IsNullOrWhiteSpace(expectedSha256)) return;
        var bytes = await File.ReadAllBytesAsync(filePath);
        await ValidateHash(bytes, expectedSha256);
    }

    private static Task ValidateHash(byte[] bytes, string? expectedSha256)
    {
        if (string.IsNullOrWhiteSpace(expectedSha256)) return Task.CompletedTask;

        var sha = ComputeSha256(bytes);
        if (!sha.Equals(expectedSha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Хэш не совпадает. Ожидался {expectedSha256}, получен {sha}");
        }
        return Task.CompletedTask;
    }

    private static string ComputeSha256(byte[] bytes)
    {
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(bytes);
        return Convert.ToHexString(hash);
    }
}
