using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using DlssChecker.Models;

namespace DlssChecker.Services;

public sealed class DlssScanner
{
    private static readonly string[] KnownDllNames =
    {
        "nvngx_dlss.dll",
        "nvngx_dlssg.dll",
        "nvngx_dlss.dll.backup"
    };

    public GameFolderContext Scan(string gameFolder)
    {
        if (string.IsNullOrWhiteSpace(gameFolder) || !Directory.Exists(gameFolder))
        {
            return new GameFolderContext { FolderPath = gameFolder };
        }

        var dllPath = FindFirstDll(gameFolder);
        var version = dllPath != null ? FileVersionInfo.GetVersionInfo(dllPath) : null;

        return new GameFolderContext
        {
            FolderPath = gameFolder,
            DlssDllPath = dllPath,
            DetectedVersion = version
        };
    }

    private static string? FindFirstDll(string folder)
    {
        var files = Directory.EnumerateFiles(folder, "*.dll", SearchOption.AllDirectories);
        foreach (var file in files)
        {
            var name = Path.GetFileName(file).ToLowerInvariant();
            if (KnownDllNames.Contains(name))
            {
                return file;
            }
        }

        return null;
    }
}
