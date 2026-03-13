using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using DlssChecker.Models;

namespace DlssChecker.Services;

public sealed class LocalVersionRepository : IVersionRepository
{
    private readonly string _versionFilePath;

    public LocalVersionRepository(string versionFilePath)
    {
        _versionFilePath = versionFilePath;
    }

    public async Task<DlssVersionInfo?> GetLatestAsync()
    {
        if (!File.Exists(_versionFilePath))
        {
            return null;
        }

        await using var stream = File.OpenRead(_versionFilePath);
        return await JsonSerializer.DeserializeAsync<DlssVersionInfo>(stream, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
    }
}
