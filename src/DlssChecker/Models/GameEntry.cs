using System.Windows.Media;

namespace DlssChecker.Models;

public sealed class GameEntry
{
    public string Name { get; init; } = string.Empty;
    public string FolderPath { get; init; } = string.Empty;
    public string? DlssVersion { get; init; }
    public ImageSource? Icon { get; init; }
}
