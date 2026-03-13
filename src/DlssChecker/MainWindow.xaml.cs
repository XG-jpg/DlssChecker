using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media;
using MessageBox = System.Windows.MessageBox;
using DlssChecker.Models;
using DlssChecker.Services;

namespace DlssChecker;

public partial class MainWindow : Window
{
    private const string AppVersion = "0.0.2";
    private const string TweaksVersion = "0.310.5.0";
    private const string RepositoryUrl = "https://github.com/XG-jpg/DllsChecker";
    private const string RepositoryOwner = "XG-jpg";
    private const string RepositoryName = "DllsChecker";

    private readonly DlssScanner _scanner = new();
    private readonly IVersionRepository _versionRepo;
    private readonly DlssUpdater _updater = new();
    private readonly BackupService _backup;
    private readonly TweaksInstaller _tweaks;
    private readonly NvidiaOverrideService _nvidiaOverride = new();
    private readonly GitHubReleaseService _gitHubReleaseService = new();
    private readonly NvidiaDlssReleaseService _nvidiaDlssService = new();
    private readonly AppSelfUpdater _appSelfUpdater = new();
    private readonly LocalizationService _loc;
    private readonly string _bundledDlssZip;

    private GameFolderContext _context = new();
    private DlssVersionInfo? _latest;
    private bool _isBusy;
    private bool _hasBackup;

    public MainWindow()
    {
        InitializeComponent();

        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        _loc = LocalizationService.Load(Path.Combine(baseDir, "Resources", "Localization.json"));
        _versionRepo = new LocalVersionRepository(Path.Combine(baseDir, "Resources", "VersionInfo.json"));
        _backup = new BackupService(Path.Combine(baseDir, "Backups"));
        _tweaks = new TweaksInstaller(Path.Combine(baseDir, "Assets", "DLSSTweaks.zip"));
        _bundledDlssZip = Path.Combine(baseDir, "Assets", "DLSS.zip");

        AppSelfUpdater.CleanupOldFiles();
        ApplyLocalization();
        ResetUiState();
        TweaksStatusText.Visibility = Visibility.Collapsed;

        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var flagPath = Path.Combine(baseDir, ".updated");
        if (!File.Exists(flagPath))
        {
            return;
        }

        try { File.Delete(flagPath); } catch { /* ignore */ }

        var changelogPath = Path.Combine(baseDir, "CHANGELOG.md");
        var win = new ChangelogWindow(AppVersion, changelogPath) { Owner = this };
        win.ShowDialog();
    }

    private void ApplyLocalization()
    {
        Title = T("window_title");
        HeaderTitleText.Text = T("app_name");
        HeaderVersionText.Text = $"v{AppVersion}";
        SubtitleText.Text = T("subtitle");
        GameFolderTitleText.Text = T("select_game_folder");
        BrowseButton.Content = T("browse");
        SupportLabelText.Text = T("support_dlss");
        DllLabelText.Text = T("found_dll");
        GameVersionLabelText.Text = T("version_in_game");
        LatestVersionLabelText.Text = T("latest_version");
        UpdateButton.Content = T("update_dlss");
        RollbackButton.Content = T("rollback_dlss");
        TweaksTitleText.Text = "DLSSTweaks";
        PresetLabelText.Text = T("preset");
        ApplyTweaksButton.Content = T("apply");
        EnableOverrideButton.Content = T("enable_override");
        RemoveTweaksButton.Content = T("remove");
        CheckAppUpdatesButtonText.Text = T("check_app_updates");
        CheckAppUpdatesButton.ToolTip = T("check_app_updates_tooltip");
        GithubButtonText.Text = T("github");
        GithubButton.ToolTip = T("open_github_repo");
    }

    private void ResetUiState()
    {
        UpdateButton.Visibility = Visibility.Collapsed;
        RollbackButton.Visibility = Visibility.Collapsed;
        ApplyTweaksButton.Visibility = Visibility.Collapsed;
        EnableOverrideButton.Visibility = Visibility.Collapsed;
        RemoveTweaksButton.Visibility = Visibility.Collapsed;
        UpdateStatusText.Text = string.Empty;
        UpdateStatusText.Visibility = Visibility.Collapsed;
        TweaksVersionText.Text = TF("built_in_tweaks_version", TweaksVersion);
    }

    private string T(string key) => _loc.Get(key);

    private string TF(string key, params object[] args) => _loc.Format(key, args);

    private async void OnBrowse(object sender, RoutedEventArgs e)
    {
        if (_isBusy)
        {
            return;
        }

        using var dialog = new FolderBrowserDialog();
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            GamePathBox.Text = dialog.SelectedPath;
            await RunBusy(ScanAsync);
        }
    }

    private async Task ScanAsync()
    {
        var path = GamePathBox.Text;
        try
        {
            _context = await Task.Run(() => _scanner.Scan(path));
        }
        catch (Exception ex)
        {
            _context = new GameFolderContext { FolderPath = path };
            ShowWarning(TF("scan_error", ex.Message));
        }

        _latest = await _versionRepo.GetLatestAsync();
        _hasBackup = DetermineHasBackup();

        UpdateContextUi();
        UpdateButtonsState();
    }

    private async void OnUpdateDlss(object sender, RoutedEventArgs e)
    {
        if (_isBusy)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_context.DlssDllPath))
        {
            ShowWarning(T("select_folder_first"));
            return;
        }

        await RunBusy(async () =>
        {
            try
            {
                _latest ??= await _versionRepo.GetLatestAsync();
                if (_latest == null)
                {
                    ShowWarning(T("dlss_info_unavailable"));
                    return;
                }

                string tempPath;
                try
                {
                    // 1. Try official NVIDIA/DLSS GitHub release (always newest)
                    var nvRelease = await _nvidiaDlssService.GetLatestAsync();
                    if (nvRelease == null)
                        throw new InvalidOperationException("No release found in NVIDIA/DLSS repository.");

                    tempPath = await _updater.DownloadAsync(nvRelease.DownloadUrl, _context.DlssDllPath!, expectedSha256: null);
                }
                catch
                {
                    // 2. Fall back to bundled DLSS.zip shipped with the app
                    if (!File.Exists(_bundledDlssZip))
                        throw;

                    tempPath = await _updater.UseLocalAsync(_bundledDlssZip, _context.DlssDllPath!, _latest.Sha256);
                }

                _updater.ReplaceWithBackup(tempPath, _context.DlssDllPath!, _backup);
                _context = await Task.Run(() => _scanner.Scan(_context.FolderPath));
                _hasBackup = DetermineHasBackup();

                UpdateContextUi();
                UpdateButtonsState();
                ShowInfo(T("dlss_updated_backup_created"));
            }
            catch (Exception ex)
            {
                await TryLocalUpdate(ex.Message);
            }
        });
    }

    private async Task TryLocalUpdate(string rootError)
    {
        if (string.IsNullOrWhiteSpace(_context.DlssDllPath))
        {
            ShowError(TF("update_error", rootError));
            return;
        }

        var localPath = FindLocalCandidate();
        if (string.IsNullOrWhiteSpace(localPath))
        {
            using var dialog = new OpenFileDialog
            {
                Filter = T("open_dlss_file_filter"),
                Title = T("open_dlss_file_title")
            };

            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            {
                ShowError(TF("update_error", rootError));
                return;
            }

            localPath = dialog.FileName;
        }

        try
        {
            var tempPath = await _updater.UseLocalAsync(localPath, _context.DlssDllPath!, _latest?.Sha256);
            _updater.ReplaceWithBackup(tempPath, _context.DlssDllPath!, _backup);
            _context = _scanner.Scan(_context.FolderPath);
            _hasBackup = DetermineHasBackup();

            UpdateContextUi();
            UpdateButtonsState();
            ShowInfo(T("dlss_updated_from_local"));
        }
        catch (Exception ex)
        {
            ShowError(TF("local_update_error", ex.Message));
        }
    }

    private string? FindLocalCandidate()
    {
        try
        {
            var downloads = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Downloads");

            if (!Directory.Exists(downloads))
            {
                return null;
            }

            return Directory
                .EnumerateFiles(downloads, "nvngx*.*", SearchOption.TopDirectoryOnly)
                .Where(path =>
                    path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(File.GetLastWriteTime)
                .FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    private void OnRollback(object sender, RoutedEventArgs e)
    {
        if (_isBusy)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_context.DlssDllPath))
        {
            ShowWarning(T("dll_not_found"));
            return;
        }

        var gameName = new DirectoryInfo(_context.FolderPath).Name;
        var backupPath = _backup.GetLatestBackup(gameName);
        if (backupPath == null)
        {
            ShowWarning(T("backup_not_found"));
            return;
        }

        try
        {
            File.Copy(backupPath, _context.DlssDllPath, overwrite: true);
        }
        catch (Exception ex)
        {
            ShowError(TF("rollback_error", ex.Message));
            return;
        }

        try
        {
            File.Delete(backupPath);
            var dir = Path.GetDirectoryName(backupPath);
            if (!string.IsNullOrWhiteSpace(dir) &&
                Directory.Exists(dir) &&
                !Directory.EnumerateFileSystemEntries(dir).Any())
            {
                Directory.Delete(dir, recursive: false);
            }
        }
        catch
        {
            // Cleanup failure is non-critical
        }

        _context = _scanner.Scan(_context.FolderPath);
        _hasBackup = DetermineHasBackup();

        UpdateContextUi();
        UpdateButtonsState();
        ShowInfo(T("rollback_completed"));
    }

    private async void OnApplyTweaks(object sender, RoutedEventArgs e)
    {
        if (_isBusy)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_context.FolderPath) || !Directory.Exists(_context.FolderPath))
        {
            ShowWarning(T("select_folder_first"));
            return;
        }

        var presetPath = GetPresetPath();
        var wasInstalled = HasTweaksInstalled();
        await RunBusy(async () =>
        {
            try
            {
                await _tweaks.InstallAsync(_context.FolderPath, presetPath);
                UpdateButtonsState();
                ShowInfo(wasInstalled ? T("tweaks_updated_preset_applied") : T("tweaks_installed_preset_applied"));
            }
            catch (Exception ex)
            {
                ShowError(TF("tweaks_install_error", ex.Message));
            }
        });
    }

    private void OnEnableOverride(object sender, RoutedEventArgs e)
    {
        if (_isBusy)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_context.FolderPath) || !Directory.Exists(_context.FolderPath))
        {
            ShowWarning(T("select_folder_first"));
            return;
        }

        var result = MessageBox.Show(
            this,
            T("nvidia_override_prompt"),
            T("app_name"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        TryEnableNvidiaOverride();
        UpdateButtonsState();
    }

    private void OnRemoveTweaks(object sender, RoutedEventArgs e)
    {
        if (_isBusy)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_context.FolderPath) || !Directory.Exists(_context.FolderPath))
        {
            ShowWarning(T("select_folder_first"));
            return;
        }

        _tweaks.Remove(_context.FolderPath);
        UpdateButtonsState();
        ShowInfo(T("tweaks_removed"));
    }

    private string GetPresetPath()
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var presetName = PresetBox.SelectedIndex == 0 ? "4000_5000.ini" : "3000.ini";
        return Path.Combine(baseDir, "Assets", "Presets", presetName);
    }

    private void TryEnableNvidiaOverride()
    {
        if (string.IsNullOrWhiteSpace(_context.FolderPath) || !Directory.Exists(_context.FolderPath))
        {
            return;
        }

        var regPath = Path.Combine(_context.FolderPath, "EnableNvidiaSigOverride.reg");
        if (!File.Exists(regPath))
        {
            ShowWarning(T("nvidia_override_file_missing"));
            return;
        }

        try
        {
            _nvidiaOverride.Enable(regPath);
            ShowInfo(T("nvidia_override_enabled"));
        }
        catch (OperationCanceledException)
        {
            ShowWarning(T("nvidia_override_cancelled"));
        }
        catch (Exception ex)
        {
            ShowError(TF("nvidia_override_enable_error", ex.Message));
        }
    }

    private void OnOpenGithub(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = RepositoryUrl,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
    }

    private async void OnCheckAppUpdates(object sender, RoutedEventArgs e)
    {
        if (_isBusy)
        {
            return;
        }

        await RunBusy(async () =>
        {
            try
            {
                var release = await _gitHubReleaseService.GetLatestReleaseAsync(RepositoryOwner, RepositoryName);
                if (release == null || string.IsNullOrWhiteSpace(release.Version))
                {
                    ShowWarning(T("no_release_info"));
                    return;
                }

                if (!Version.TryParse(AppVersion, out var currentVersion) ||
                    !Version.TryParse(release.Version, out var latestVersion))
                {
                    ShowWarning(TF("app_update_parse_error", release.TagName));
                    return;
                }

                if (latestVersion <= currentVersion)
                {
                    ShowInfo(TF("app_up_to_date", AppVersion));
                    return;
                }

                var result = MessageBox.Show(
                    this,
                    TF("app_update_available", AppVersion, release.Version),
                    T("app_name"),
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (result == MessageBoxResult.Yes)
                {
                    if (!string.IsNullOrWhiteSpace(release.DownloadUrl))
                    {
                        await _appSelfUpdater.ApplyAsync(release.DownloadUrl);
                    }
                    else
                    {
                        var fallbackUrl = !string.IsNullOrWhiteSpace(release.HtmlUrl)
                            ? release.HtmlUrl
                            : RepositoryUrl + "/releases/latest";
                        OpenUrl(fallbackUrl);
                    }
                }
            }
            catch (Exception ex)
            {
                ShowError(TF("app_update_check_error", ex.Message));
            }
        });
    }

    private void UpdateContextUi()
    {
        DllPathText.Text = _context.DlssDllPath ?? "-";

        var currentVersion = _context.DetectedVersion?.FileVersion;
        if (!string.IsNullOrWhiteSpace(currentVersion))
        {
            currentVersion = currentVersion.Replace(',', '.');
            currentVersion = FormatVersionForDisplay(currentVersion);
        }

        CurrentVersionText.Text = currentVersion ?? "-";
        LatestVersionText.Text = _latest?.LatestVersion ?? "-";

        var supported = _context.DlssDllPath != null;
        DlssSupportText.Text = supported ? T("yes") : T("no");
        var color = supported ? Colors.LightGreen : Colors.OrangeRed;
        DlssSupportText.Foreground = new SolidColorBrush(color);
        SupportDot.Fill = new SolidColorBrush(color);
    }

    private static string FormatVersionForDisplay(string versionText)
    {
        if (!Version.TryParse(versionText, out var version))
        {
            return versionText;
        }

        if (version.Revision == 0)
        {
            return $"{version.Major}.{version.Minor}.{version.Build}";
        }

        return version.ToString();
    }

    private void UpdateButtonsState()
    {
        var hasDll = !string.IsNullOrWhiteSpace(_context.DlssDllPath);
        var hasLatest = _latest != null && !string.IsNullOrWhiteSpace(_latest.LatestVersion);
        var hasGameFolder = !string.IsNullOrWhiteSpace(_context.FolderPath) && Directory.Exists(_context.FolderPath);
        var tweaksInstalled = hasGameFolder && HasTweaksInstalled();

        bool? updateNeeded = null;
        if (hasDll && hasLatest &&
            Version.TryParse(_latest!.LatestVersion.Replace(',', '.'), out var latestVersion) &&
            Version.TryParse((_context.DetectedVersion?.FileVersion ?? string.Empty).Replace(',', '.'), out var currentVersion))
        {
            updateNeeded = currentVersion < latestVersion;
        }

        UpdateButton.Visibility = updateNeeded == true ? Visibility.Visible : Visibility.Collapsed;
        UpdateStatusText.Text = updateNeeded == false ? T("update_not_required") : string.Empty;
        UpdateStatusText.Visibility = updateNeeded == false ? Visibility.Visible : Visibility.Collapsed;

        RollbackButton.Visibility = _hasBackup ? Visibility.Visible : Visibility.Collapsed;
        ApplyTweaksButton.Visibility = hasGameFolder ? Visibility.Visible : Visibility.Collapsed;
        RemoveTweaksButton.Visibility = tweaksInstalled ? Visibility.Visible : Visibility.Collapsed;

        if (tweaksInstalled)
        {
            var logPath = Path.Combine(_context.FolderPath!, "dlsstweaks.log");
            if (File.Exists(logPath))
            {
                TweaksStatusText.Text = T("tweaks_status_loaded");
                TweaksStatusText.Foreground = new SolidColorBrush(Colors.LightGreen);
                EnableOverrideButton.Visibility = Visibility.Collapsed;
            }
            else if (_nvidiaOverride.IsEnabled())
            {
                TweaksStatusText.Text = T("tweaks_status_not_loaded_override_on");
                TweaksStatusText.Foreground = new SolidColorBrush(Colors.Gold);
                EnableOverrideButton.Visibility = Visibility.Collapsed;
            }
            else
            {
                TweaksStatusText.Text = T("tweaks_status_override_needed");
                TweaksStatusText.Foreground = new SolidColorBrush(Colors.OrangeRed);
                EnableOverrideButton.Visibility = Visibility.Visible;
            }
            TweaksStatusText.Visibility = Visibility.Visible;
        }
        else
        {
            TweaksStatusText.Visibility = Visibility.Collapsed;
            EnableOverrideButton.Visibility = Visibility.Collapsed;
        }
        TweaksVersionText.Text = tweaksInstalled
            ? TF("installed_tweaks_version", TweaksVersion)
            : TF("built_in_tweaks_version", TweaksVersion);
    }

    private bool HasTweaksInstalled()
    {
        if (string.IsNullOrWhiteSpace(_context.FolderPath) || !Directory.Exists(_context.FolderPath))
        {
            return false;
        }

        string[] names =
        {
            "dlsstweaks.ini",
            "DLSSTweaksConfig.exe",
            "dxgi.dll",
            "dlsstweaks.log",
            "EnableNvidiaSigOverride.reg",
            "DisableNvidiaSigOverride.reg"
        };

        return names.Any(name => File.Exists(Path.Combine(_context.FolderPath, name)));
    }

    private bool DetermineHasBackup()
    {
        if (string.IsNullOrWhiteSpace(_context.FolderPath) || !Directory.Exists(_context.FolderPath))
        {
            return false;
        }

        var gameName = new DirectoryInfo(_context.FolderPath).Name;
        return _backup.GetLatestBackup(gameName) != null;
    }

    private async Task RunBusy(Func<Task> action)
    {
        SetBusy(true);
        try
        {
            await action();
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SetBusy(bool busy)
    {
        _isBusy = busy;
        BusyBar.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ShowInfo(string message)
    {
        MessageBox.Show(this, message, T("app_name"), MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ShowWarning(string message)
    {
        MessageBox.Show(this, message, T("app_name"), MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private void ShowError(string message)
    {
        MessageBox.Show(this, message, T("app_name"), MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private static void OpenUrl(string url)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });
    }
}
