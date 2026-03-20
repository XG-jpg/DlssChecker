using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace DlssChecker.Services;

public sealed class CustomFoldersService
{
    private readonly string _filePath;

    public CustomFoldersService(string baseDir)
    {
        _filePath = Path.Combine(baseDir, "custom_folders.json");
    }

    /// <summary>Loads saved folders, removes any that no longer exist on disk.</summary>
    public List<string> Load()
    {
        try
        {
            if (!File.Exists(_filePath)) return [];

            var json = File.ReadAllText(_filePath);
            var list = JsonSerializer.Deserialize<List<string>>(json) ?? [];

            // Filter out deleted folders and save the cleaned list back
            var existing = list.Where(Directory.Exists).ToList();
            if (existing.Count != list.Count)
                Save(existing);

            return existing;
        }
        catch
        {
            return [];
        }
    }

    public void Add(string folder)
    {
        var list = Load();
        if (list.Any(f => string.Equals(f, folder, StringComparison.OrdinalIgnoreCase)))
            return;
        list.Add(folder);
        Save(list);
    }

    private void Save(List<string> list)
    {
        try
        {
            File.WriteAllText(_filePath, JsonSerializer.Serialize(list));
        }
        catch { }
    }
}
