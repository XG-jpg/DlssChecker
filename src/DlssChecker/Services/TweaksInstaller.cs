using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;

namespace DlssChecker.Services;

public sealed class TweaksInstaller
{
    private readonly string _zipPath;

    public TweaksInstaller(string zipPath)
    {
        _zipPath = zipPath;
    }

    public async Task InstallAsync(string gameFolder, string presetIniPath, bool overwriteExisting = true)
    {
        if (!File.Exists(_zipPath))
            throw new FileNotFoundException("Файл DLSSTweaks.zip не найден", _zipPath);

        var tempDir = Path.Combine(Path.GetTempPath(), "dlsstweaks_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            ZipFile.ExtractToDirectory(_zipPath, tempDir);

            foreach (var file in Directory.EnumerateFiles(tempDir, "*", SearchOption.TopDirectoryOnly))
            {
                var dest = Path.Combine(gameFolder, Path.GetFileName(file));
                if (!overwriteExisting && File.Exists(dest))
                    continue;

                File.Copy(file, dest, overwriteExisting);
            }

            if (File.Exists(presetIniPath))
            {
                var targetIni = Path.Combine(gameFolder, "dlsstweaks.ini");
                File.Copy(presetIniPath, targetIni, overwrite: true);
            }
        }
        finally
        {
            await SafeDeleteDirectory(tempDir);
        }
    }

    public void Remove(string gameFolder)
    {
        foreach (var file in EnumerateTweakFiles(gameFolder))
        {
            try
            {
                File.Delete(file);
            }
            catch
            {
                // ignore per-file deletion issues
            }
        }
    }

    private static IEnumerable<string> EnumerateTweakFiles(string folder)
    {
        if (!Directory.Exists(folder))
            yield break;

        var names = new[]
        {
            "dlsstweaks.ini",
            "dlsstweaks.log",
            "DLSSTweaksConfig.exe",
            "dxgi.dll",
            "EnableNvidiaSigOverride.reg",
            "DisableNvidiaSigOverride.reg"
        };

        foreach (var name in names)
        {
            var path = Path.Combine(folder, name);
            if (File.Exists(path))
            {
                yield return path;
            }
        }
    }

    private static Task SafeDeleteDirectory(string path)
    {
        return Task.Run(() =>
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, recursive: true);
                }
            }
            catch
            {
                // ignore cleanup
            }
        });
    }
}
