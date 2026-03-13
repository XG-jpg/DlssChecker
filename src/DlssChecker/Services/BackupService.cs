using System;
using System.IO;

namespace DlssChecker.Services;

public sealed class BackupService
{
    private readonly string _backupRoot;

    public BackupService(string backupRoot)
    {
        _backupRoot = backupRoot;
        Directory.CreateDirectory(_backupRoot);
    }

    public string CreateBackup(string sourceFile)
    {
        var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var gameName = new DirectoryInfo(Path.GetDirectoryName(sourceFile) ?? "game").Name;
        var destDir = Path.Combine(_backupRoot, gameName, stamp);
        Directory.CreateDirectory(destDir);

        var destPath = Path.Combine(destDir, Path.GetFileName(sourceFile));
        File.Copy(sourceFile, destPath, overwrite: true);
        return destPath;
    }

    public string? GetLatestBackup(string gameName)
    {
        var gamePath = Path.Combine(_backupRoot, gameName);
        if (!Directory.Exists(gamePath))
        {
            return null;
        }

        var latest = new DirectoryInfo(gamePath)
            .EnumerateDirectories()
            .OrderByDescending(d => d.Name)
            .FirstOrDefault();

        if (latest == null)
        {
            return null;
        }

        var file = latest.EnumerateFiles("nvngx_dlss*.dll").FirstOrDefault();
        return file?.FullName;
    }
}
