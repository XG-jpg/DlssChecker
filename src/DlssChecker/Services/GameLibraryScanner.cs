using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DlssChecker.Models;
using Microsoft.Win32;

namespace DlssChecker.Services;

public sealed class GameLibraryScanner
{
    private static readonly string[] DlssNames = { "nvngx_dlss.dll", "nvngx_dlssg.dll" };

    public List<GameEntry> Scan()
    {
        var results = new List<GameEntry>();

        foreach (var (name, path) in EnumerateGameFolders())
        {
            var dlssPath = FindDlss(path);
            if (dlssPath == null) continue;

            results.Add(new GameEntry
            {
                Name = name,
                FolderPath = path,
                DlssVersion = TryGetVersion(dlssPath),
                Icon = TryGetIcon(path)
            });
        }

        return results
            .GroupBy(g => g.FolderPath, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(g => g.Name)
            .ToList();
    }

    private IEnumerable<(string name, string path)> EnumerateGameFolders()
    {
        return GetSteamGameFolders()
            .Concat(GetEpicGameFolders())
            .Concat(GetGogGameFolders());
    }

    private static string? FindDlss(string folder)
    {
        try
        {
            return Directory
                .EnumerateFiles(folder, "*.dll", SearchOption.AllDirectories)
                .FirstOrDefault(f => DlssNames.Contains(Path.GetFileName(f).ToLowerInvariant()));
        }
        catch
        {
            return null;
        }
    }

    private static string? TryGetVersion(string dllPath)
    {
        try
        {
            var v = FileVersionInfo.GetVersionInfo(dllPath);
            if (v.FileVersion == null) return null;
            var normalized = v.FileVersion.Replace(',', '.');
            return Version.TryParse(normalized, out var ver)
                ? $"{ver.Major}.{ver.Minor}.{ver.Build}"
                : normalized;
        }
        catch
        {
            return null;
        }
    }

    private static ImageSource? TryGetIcon(string folder)
    {
        try
        {
            var exe = Directory.EnumerateFiles(folder, "*.exe", SearchOption.TopDirectoryOnly)
                .Where(f => !Path.GetFileName(f).StartsWith("unins", StringComparison.OrdinalIgnoreCase)
                         && !Path.GetFileName(f).StartsWith("crash", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(f => new FileInfo(f).Length)
                .FirstOrDefault();

            if (exe == null) return null;

            using var icon = Icon.ExtractAssociatedIcon(exe);
            if (icon == null) return null;

            using var bmp = icon.ToBitmap();
            using var ms = new MemoryStream();
            bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            ms.Position = 0;

            var img = new BitmapImage();
            img.BeginInit();
            img.CacheOption = BitmapCacheOption.OnLoad;
            img.StreamSource = ms;
            img.EndInit();
            img.Freeze();
            return img;
        }
        catch
        {
            return null;
        }
    }

    // ── Steam ─────────────────────────────────────────────────────────────

    private static IEnumerable<(string name, string path)> GetSteamGameFolders()
    {
        var steamPath =
            Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath", null) as string
            ?? Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Valve\Steam", "InstallPath", null) as string;

        if (steamPath == null) yield break;

        var libraries = new List<string> { Path.Combine(steamPath, "steamapps") };

        var vdfPath = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
        if (File.Exists(vdfPath))
        {
            foreach (var line in File.ReadAllLines(vdfPath))
            {
                var trimmed = line.Trim();
                if (!trimmed.StartsWith("\"path\"", StringComparison.OrdinalIgnoreCase)) continue;

                var parts = trimmed.Split('"');
                if (parts.Length >= 4)
                    libraries.Add(Path.Combine(parts[3].Replace(@"\\", @"\"), "steamapps"));
            }
        }

        foreach (var lib in libraries)
        {
            var common = Path.Combine(lib, "common");
            if (!Directory.Exists(common)) continue;

            foreach (var gameDir in TryEnumerateDirs(common))
                yield return (Path.GetFileName(gameDir), gameDir);
        }
    }

    // ── Epic Games ────────────────────────────────────────────────────────

    private static IEnumerable<(string name, string path)> GetEpicGameFolders()
    {
        var manifestDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Epic", "EpicGamesLauncher", "Data", "Manifests");

        if (!Directory.Exists(manifestDir)) yield break;

        foreach (var item in TryEnumerateFiles(manifestDir, "*.item"))
        {
            (string name, string path)? entry = null;
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(item));
                var root = doc.RootElement;
                var location = root.TryGetProperty("InstallLocation", out var loc) ? loc.GetString() : null;
                var display = root.TryGetProperty("DisplayName", out var dn) ? dn.GetString() : null;
                if (location != null && display != null && Directory.Exists(location))
                    entry = (display, location);
            }
            catch { }

            if (entry.HasValue) yield return entry.Value;
        }
    }

    // ── GOG Galaxy ────────────────────────────────────────────────────────

    private static IEnumerable<(string name, string path)> GetGogGameFolders()
    {
        var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\GOG.com\Games")
               ?? Registry.LocalMachine.OpenSubKey(@"SOFTWARE\GOG.com\Games");

        if (key == null) yield break;

        foreach (var subName in key.GetSubKeyNames())
        {
            using var sub = key.OpenSubKey(subName);
            if (sub == null) continue;
            var path = sub.GetValue("path") as string;
            var name = sub.GetValue("gameName") as string;
            if (path != null && name != null && Directory.Exists(path))
                yield return (name, path);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static IEnumerable<string> TryEnumerateDirs(string path)
    {
        try { return Directory.EnumerateDirectories(path); }
        catch { return []; }
    }

    private static IEnumerable<string> TryEnumerateFiles(string path, string pattern)
    {
        try { return Directory.EnumerateFiles(path, pattern); }
        catch { return []; }
    }
}
