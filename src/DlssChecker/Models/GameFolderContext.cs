using System.Diagnostics;

namespace DlssChecker.Models;

public sealed class GameFolderContext
{
    public string FolderPath { get; init; } = string.Empty;
    public string? DlssDllPath { get; init; }
    public FileVersionInfo? DetectedVersion { get; init; }
}
