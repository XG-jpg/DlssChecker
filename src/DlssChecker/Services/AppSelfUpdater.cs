using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using WpfApp = System.Windows.Application;

namespace DlssChecker.Services;

public record AppUpdateProgress(
    string Status,
    double? Fraction = null,
    long DownloadedBytes = 0,
    long TotalBytes = 0,
    double SpeedMBps = 0
);

public sealed class AppSelfUpdater
{
    private static readonly HttpClient HttpClient = CreateClient();

    public static void CleanupOldFiles()
    {
        var dir = AppDomain.CurrentDomain.BaseDirectory;
        foreach (var file in Directory.EnumerateFiles(dir, "*.old"))
        {
            try { File.Delete(file); } catch { /* locked or already gone */ }
        }
    }

    public async Task ApplyAsync(string downloadUrl, IProgress<AppUpdateProgress>? progress = null)
    {
        var appDir = AppDomain.CurrentDomain.BaseDirectory;
        var currentExe = Environment.ProcessPath
            ?? Process.GetCurrentProcess().MainModule?.FileName
            ?? throw new InvalidOperationException("Could not resolve executable path.");

        progress?.Report(new AppUpdateProgress("Загрузка обновления…", 0));

        using var response = await HttpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? -1L;
        await using var contentStream = await response.Content.ReadAsStreamAsync();
        using var ms = new MemoryStream(totalBytes > 0 ? (int)totalBytes : 8 * 1024 * 1024);

        var buffer = new byte[81920];
        long downloaded = 0;
        var sw = Stopwatch.StartNew();
        long lastSpeedBytes = 0;
        double lastSpeedTime = 0;
        double currentSpeedMBps = 0;

        int read;
        while ((read = await contentStream.ReadAsync(buffer)) > 0)
        {
            await ms.WriteAsync(buffer.AsMemory(0, read));
            downloaded += read;

            // Update speed every ~500 ms
            var elapsed = sw.Elapsed.TotalSeconds;
            if (elapsed - lastSpeedTime >= 0.5)
            {
                var bytesDelta = downloaded - lastSpeedBytes;
                var timeDelta = elapsed - lastSpeedTime;
                currentSpeedMBps = bytesDelta / timeDelta / 1_048_576.0;
                lastSpeedBytes = downloaded;
                lastSpeedTime = elapsed;
            }

            if (totalBytes > 0)
                progress?.Report(new AppUpdateProgress(
                    "Загрузка обновления…",
                    (double)downloaded / totalBytes,
                    downloaded,
                    totalBytes,
                    currentSpeedMBps));
        }

        progress?.Report(new AppUpdateProgress("Установка обновления…"));

        var tempDir = Path.Combine(Path.GetTempPath(), $"DlssCheckerUpd_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            using (var zipMs = new MemoryStream(ms.ToArray()))
            using (var archive = new ZipArchive(zipMs, ZipArchiveMode.Read))
            {
                archive.ExtractToDirectory(tempDir, overwriteFiles: true);
            }

            var extractRoot = GetExtractRoot(tempDir);
            var oldExe = currentExe + ".old";

            File.Move(currentExe, oldExe, overwrite: true);
            try
            {
                CopyDirectory(extractRoot, appDir);
            }
            catch
            {
                if (!File.Exists(currentExe))
                    try { File.Move(oldExe, currentExe); } catch { }
                throw;
            }
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }

        var flagPath = Path.Combine(appDir, ".updated");
        await File.WriteAllTextAsync(flagPath, string.Empty);

        Process.Start(new ProcessStartInfo { FileName = currentExe, UseShellExecute = true });
        WpfApp.Current.Dispatcher.Invoke(() => WpfApp.Current.Shutdown());
    }

    private static string GetExtractRoot(string dir)
    {
        var entries = Directory.GetFileSystemEntries(dir);
        return entries.Length == 1 && Directory.Exists(entries[0]) ? entries[0] : dir;
    }

    private static void CopyDirectory(string src, string dst)
    {
        Directory.CreateDirectory(dst);
        foreach (var file in Directory.EnumerateFiles(src))
            File.Copy(file, Path.Combine(dst, Path.GetFileName(file)), overwrite: true);
        foreach (var sub in Directory.EnumerateDirectories(src))
            CopyDirectory(sub, Path.Combine(dst, Path.GetFileName(sub)));
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("DlssChecker", AppInfo.Version));
        return client;
    }
}
