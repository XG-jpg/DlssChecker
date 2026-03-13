using System.Globalization;
using System.IO;
using System.Text.Json;

namespace DlssChecker.Services;

public sealed class LocalizationService
{
    private readonly Dictionary<string, string> _strings;

    private LocalizationService(Dictionary<string, string> strings)
    {
        _strings = strings;
    }

    public static LocalizationService Load(string path)
    {
        if (!File.Exists(path))
        {
            return new LocalizationService(new Dictionary<string, string>());
        }

        var json = File.ReadAllText(path);
        var file = JsonSerializer.Deserialize<LocalizationFile>(
            json,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new LocalizationFile();
        var language = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

        if (!file.Languages.TryGetValue(language, out var strings))
        {
            if (!file.Languages.TryGetValue(file.DefaultLanguage, out strings))
            {
                strings = file.Languages.Values.FirstOrDefault() ?? new Dictionary<string, string>();
            }
        }

        return new LocalizationService(strings);
    }

    public string Get(string key)
    {
        return _strings.TryGetValue(key, out var value) ? value : key;
    }

    public string Format(string key, params object[] args)
    {
        return string.Format(CultureInfo.CurrentCulture, Get(key), args);
    }

    private sealed class LocalizationFile
    {
        public string DefaultLanguage { get; set; } = "en";
        public Dictionary<string, Dictionary<string, string>> Languages { get; set; } = new();
    }
}
