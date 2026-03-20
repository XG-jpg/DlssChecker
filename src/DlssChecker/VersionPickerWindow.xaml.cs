using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;
using DlssChecker.Services;

namespace DlssChecker;

public sealed class DlssVersionItem
{
    public string? Version { get; init; }
    public string DownloadUrl { get; init; } = string.Empty;
    public string DateText { get; init; } = string.Empty;
    public bool IsLatest { get; init; }
    public bool IsCurrent { get; init; }

    public Visibility IsLatestBadgeVisible => IsLatest ? Visibility.Visible : Visibility.Collapsed;
    public Visibility IsInstalledBadgeVisible => IsCurrent ? Visibility.Visible : Visibility.Collapsed;
}

public partial class VersionPickerWindow : Window
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private readonly NvidiaDlssReleaseService _service = new();

    /// <summary>Set when user picks a GitHub release.</summary>
    public string? SelectedDownloadUrl { get; private set; }

    /// <summary>Set when user picks a local file.</summary>
    public string? SelectedLocalPath { get; private set; }

    public VersionPickerWindow(string? currentVersion)
    {
        InitializeComponent();

        SourceInitialized += (_, _) =>
        {
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            int dark = 1;
            DwmSetWindowAttribute(hwnd, 20, ref dark, sizeof(int));
        };

        if (!string.IsNullOrWhiteSpace(currentVersion))
            CurrentVersionText.Text = $"Установлена: v{currentVersion}";

        Loaded += async (_, _) =>
        {
            List<NvidiaDlssRelease> releases;
            try
            {
                releases = await _service.GetAllReleasesAsync();
            }
            catch (Exception ex)
            {
                LoadingPanel.Visibility = Visibility.Collapsed;
                ErrorPanel.Visibility = Visibility.Visible;
                ErrorText.Text = $"Не удалось загрузить список версий:\n{ex.Message}";
                return;
            }

            if (releases.Count == 0)
            {
                LoadingPanel.Visibility = Visibility.Collapsed;
                ErrorPanel.Visibility = Visibility.Visible;
                ErrorText.Text = "Список версий недоступен.\nВозможно, нет соединения с GitHub.";
                return;
            }

            var items = new List<DlssVersionItem>();
            for (int i = 0; i < releases.Count; i++)
            {
                var r = releases[i];
                items.Add(new DlssVersionItem
                {
                    Version = r.Version,
                    DownloadUrl = r.DownloadUrl,
                    DateText = r.PublishedAt != default
                        ? r.PublishedAt.ToString("dd.MM.yyyy")
                        : string.Empty,
                    IsLatest = i == 0,
                    IsCurrent = VersionsEqual(r.Version, currentVersion)
                });
            }

            VersionList.ItemsSource = items;
            VersionList.SelectedIndex = 0;

            LoadingPanel.Visibility = Visibility.Collapsed;
            VersionList.Visibility = Visibility.Visible;
            InstallButton.IsEnabled = true;
        };

        VersionList.SelectionChanged += (_, _) =>
            InstallButton.IsEnabled = VersionList.SelectedItem != null;
    }

    private static bool VersionsEqual(string? a, string? b)
    {
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)) return false;
        if (Version.TryParse(a, out var va) && Version.TryParse(b, out var vb))
        {
            // Compare only Major.Minor.Build (ignore Revision if 0)
            return va.Major == vb.Major && va.Minor == vb.Minor && va.Build == vb.Build;
        }
        return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }

    private void OnInstall(object sender, RoutedEventArgs e)
    {
        if (VersionList.SelectedItem is not DlssVersionItem item) return;
        SelectedDownloadUrl = item.DownloadUrl;
        DialogResult = true;
    }

    private void OnBrowseLocal(object sender, RoutedEventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "DLSS DLL / ZIP|nvngx_dlss*.dll;*.zip|Все файлы|*.*",
            Title = "Выберите файл nvngx_dlss.dll или ZIP с DLSS"
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            SelectedLocalPath = dialog.FileName;
            DialogResult = true;
        }
    }
}
