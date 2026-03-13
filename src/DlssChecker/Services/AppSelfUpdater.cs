using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using WpfApp = System.Windows.Application;

namespace DlssChecker.Services;

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

    public async Task ApplyAsync(string downloadUrl)
    {
        var appDir = AppDomain.CurrentDomain.BaseDirectory;
        var currentExe = Environment.ProcessPath
            ?? Process.GetCurrentProcess().MainModule?.FileName
            ?? throw new InvalidOperationException("Could not resolve executable path.");

        var zipBytes = await HttpClient.GetByteArrayAsync(downloadUrl);

        var tempDir = Path.Combine(Path.GetTempPath(), $"DlssCheckerUpd_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            using (var ms = new MemoryStream(zipBytes))
            using (var archive = new ZipArchive(ms, ZipArchiveMode.Read))
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
                // Restore original exe so the app still works
                if (!File.Exists(currentExe))
                {
                    try { File.Move(oldExe, currentExe); } catch { }
                }
                throw;
            }
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }

        // Signal new process to show changelog on first launch
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
        {
            File.Copy(file, Path.Combine(dst, Path.GetFileName(file)), overwrite: true);
        }
        foreach (var sub in Directory.EnumerateDirectories(src))
        {
            CopyDirectory(sub, Path.Combine(dst, Path.GetFileName(sub)));
        }
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("DlssChecker", "0.0.2"));
        return client;
    }
}
