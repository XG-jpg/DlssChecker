using System.ComponentModel;
using System.Windows.Media;

namespace DlssChecker.Models;

public sealed class GameEntry : INotifyPropertyChanged
{
    public string Name { get; init; } = string.Empty;
    public string FolderPath { get; init; } = string.Empty;
    public string? DlssVersion { get; init; }
    public ImageSource? Icon { get; init; }

    private bool? _needsUpdate;
    public bool? NeedsUpdate  // null = unknown, true = update available, false = up to date
    {
        get => _needsUpdate;
        set
        {
            if (_needsUpdate == value) return;
            _needsUpdate = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NeedsUpdate)));
        }
    }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value) return;
            _isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
