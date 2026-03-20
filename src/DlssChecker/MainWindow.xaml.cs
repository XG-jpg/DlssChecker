using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using MessageBox = System.Windows.MessageBox;
using DlssChecker.Models;
using DlssChecker.Services;

namespace DlssChecker;

public partial class MainWindow : Window
{
    private static string AppVersion => AppInfo.Version;
    private const string TweaksVersion = "0.310.5.0";
    private const string RepositoryUrl = "https://github.com/XG-jpg/DllsChecker";
    private const string RepositoryOwner = "XG-jpg";
    private const string RepositoryName = "DllsChecker";

    private readonly DlssScanner _scanner = new();
    private readonly IVersionRepository _versionRepo;
    private readonly DlssUpdater _updater = new();
    private readonly TweaksInstaller _tweaks;
    private readonly NvidiaOverrideService _nvidiaOverride = new();
    private readonly GitHubReleaseService _gitHubReleaseService = new();
    private readonly NvidiaDlssReleaseService _nvidiaDlssService = new();
    private readonly AppSelfUpdater _appSelfUpdater = new();
    private readonly GameLibraryScanner _gameLibraryScanner = new();
    private readonly LocalizationService _loc;
    private readonly CustomFoldersService _customFolders;
    private readonly string _bundledDlssZip;

    private GameFolderContext _context = new();
    private DlssVersionInfo? _latest;
    private bool _isBusy;

    public MainWindow()
    {
        InitializeComponent();

        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        _loc = LocalizationService.Load(Path.Combine(baseDir, "Resources", "Localization.json"));
        _versionRepo = new LocalVersionRepository(Path.Combine(baseDir, "Resources", "VersionInfo.json"));
        _tweaks = new TweaksInstaller(Path.Combine(baseDir, "Assets", "DLSSTweaks.zip"));
        _bundledDlssZip = Path.Combine(baseDir, "Assets", "DLSS.zip");
        _customFolders = new CustomFoldersService(baseDir);

        AppSelfUpdater.CleanupOldFiles();
        ApplyLocalization();
        ResetUiState();
        TweaksStatusText.Visibility = Visibility.Collapsed;

        var lastFolder = LoadLastFolder(baseDir);
        if (!string.IsNullOrWhiteSpace(lastFolder))
            GamePathBox.Text = lastFolder;

        Loaded += OnLoaded;
        SourceInitialized += (_, _) => EnableDarkTitleBar();
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private void EnableDarkTitleBar()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        int dark = 1;
        DwmSetWindowAttribute(hwnd, 20 /* DWMWA_USE_IMMERSIVE_DARK_MODE */, ref dark, sizeof(int));
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var flagPath = Path.Combine(baseDir, ".updated");
        if (File.Exists(flagPath))
        {
            try { File.Delete(flagPath); } catch { /* ignore */ }
            var changelogPath = Path.Combine(baseDir, "CHANGELOG.md");
            var win = new ChangelogWindow(AppVersion, changelogPath) { Owner = this };
            win.ShowDialog();
        }

        OfferDesktopShortcut(baseDir);

        await ScanGameLibrariesAsync();
    }

    private void OfferDesktopShortcut(string baseDir)
    {
        var flagPath = Path.Combine(baseDir, ".shortcut_asked");
        if (File.Exists(flagPath)) return;

        try { File.WriteAllText(flagPath, "1"); } catch { /* ignore */ }

        var result = MessageBox.Show(
            T("shortcut_prompt"),
            T("app_name"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        try
        {
            var exePath = Process.GetCurrentProcess().MainModule?.FileName
                          ?? Path.Combine(baseDir, "DlssChecker.exe");
            var shortcutPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                "DLSS Checker.lnk");

            var shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!)!;
            var shortcut = shell.GetType().InvokeMember(
                "CreateShortcut", System.Reflection.BindingFlags.InvokeMethod,
                null, shell, new object[] { shortcutPath })!;
            var st = shortcut.GetType();
            st.InvokeMember("TargetPath", System.Reflection.BindingFlags.SetProperty,
                null, shortcut, new object[] { exePath });
            st.InvokeMember("WorkingDirectory", System.Reflection.BindingFlags.SetProperty,
                null, shortcut, new object[] { baseDir });
            st.InvokeMember("IconLocation", System.Reflection.BindingFlags.SetProperty,
                null, shortcut, new object[] { exePath + ",0" });
            st.InvokeMember("Save", System.Reflection.BindingFlags.InvokeMethod,
                null, shortcut, null);

            UpdateStatusText.Text = T("shortcut_created");
        }
        catch (Exception ex)
        {
            UpdateStatusText.Text = string.Format(T("shortcut_error"), ex.Message);
        }
    }

    private async Task ScanGameLibrariesAsync()
    {
        DetectedGamesTitleText.Text = T("detected_games_scanning");
        GameScanBar.Visibility = Visibility.Visible;
        GamesPanelBorder.Visibility = Visibility.Visible;

        var (games, latestRelease) = await Task.Run(async () =>
        {
            var g = _gameLibraryScanner.Scan();
            NvidiaDlssRelease? rel = null;
            try { rel = await _nvidiaDlssService.GetLatestAsync(); } catch { }
            return (g, rel);
        });

        // Merge in custom (manually browsed) folders, deduplicating by path
        var customPaths = _customFolders.Load();
        var knownPaths = games.Select(g => g.FolderPath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var folder in customPaths)
        {
            if (knownPaths.Contains(folder)) continue;
            var entry = _gameLibraryScanner.CreateEntryForFolder(folder);
            if (entry != null) games.Add(entry);
        }
        games = games.OrderBy(g => g.Name).ToList();

        GameScanBar.Visibility = Visibility.Collapsed;

        if (games.Count == 0)
        {
            GamesPanelBorder.Visibility = Visibility.Collapsed;
            return;
        }

        // Enrich NeedsUpdate directly on the mutable property
        if (latestRelease?.Version != null && Version.TryParse(latestRelease.Version, out var latestVer))
        {
            foreach (var g in games)
            {
                if (g.DlssVersion != null && Version.TryParse(g.DlssVersion, out var gameVer))
                    g.NeedsUpdate = gameVer < latestVer;
            }
        }

        DetectedGamesTitleText.Text = T("detected_games");
        GameTilesList.ItemsSource = games;

        // Load missing Steam icons in the background (CDN with local cache)
        _ = LoadMissingSteamIconsAsync(games);
    }

    private void ApplyLocalization()
    {
        Title = $"{T("window_title")} v{AppVersion}";
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
        VersionButton.Content = T("select_version_dlss");
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
        VersionButton.Visibility = Visibility.Collapsed;
        ApplyTweaksButton.Visibility = Visibility.Collapsed;
        EnableOverrideButton.Visibility = Visibility.Collapsed;
        RemoveTweaksButton.Visibility = Visibility.Collapsed;
        UpdateStatusText.Text = string.Empty;
        UpdateStatusText.Visibility = Visibility.Collapsed;
        TweaksVersionText.Text = TF("built_in_tweaks_version", TweaksVersion);
    }

    private string T(string key) => _loc.Get(key);

    private string TF(string key, params object[] args) => _loc.Format(key, args);

    private void ClearTileSelection()
    {
        if (GameTilesList.ItemsSource is System.Collections.Generic.List<GameEntry> games)
            foreach (var g in games) g.IsSelected = false;
    }

    private async void OnGameTileClick(object sender, MouseButtonEventArgs e)
    {
        if (_isBusy) return;
        if (sender is not FrameworkElement { DataContext: GameEntry entry }) return;

        ClearTileSelection();
        entry.IsSelected = true;

        GamePathBox.Text = entry.FolderPath;
        await RunBusy(ScanAsync);
    }

    private async void OnBrowse(object sender, RoutedEventArgs e)
    {
        if (_isBusy) return;

        using var dialog = new FolderBrowserDialog();
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            ClearTileSelection();
            GamePathBox.Text = dialog.SelectedPath;
            await RunBusy(ScanAsync);

            // Persist folder, add to tiles and select the card
            _customFolders.Add(dialog.SelectedPath);
            AddCustomFolderToTiles(dialog.SelectedPath);
            SelectTileByPath(dialog.SelectedPath);
        }
    }

    private void SelectTileByPath(string folderPath)
    {
        if (GameTilesList.ItemsSource is not List<GameEntry> games) return;
        var entry = games.FirstOrDefault(g =>
            string.Equals(g.FolderPath, folderPath, StringComparison.OrdinalIgnoreCase));
        if (entry == null) return;
        ClearTileSelection();
        entry.IsSelected = true;
    }

    private void AddCustomFolderToTiles(string folderPath)
    {
        if (GameTilesList.ItemsSource is not System.Collections.Generic.List<GameEntry> games) return;
        if (games.Any(g => string.Equals(g.FolderPath, folderPath, StringComparison.OrdinalIgnoreCase))) return;

        var entry = _gameLibraryScanner.CreateEntryForFolder(folderPath);
        if (entry == null) return;

        if (_latest?.LatestVersion != null &&
            entry.DlssVersion != null &&
            Version.TryParse(entry.DlssVersion, out var gameVer) &&
            Version.TryParse(_latest.LatestVersion, out var latestVer))
        {
            entry.NeedsUpdate = gameVer < latestVer;
        }

        var newList = games.Concat(new[] { entry }).OrderBy(g => g.Name).ToList();
        GameTilesList.ItemsSource = newList;
        GamesPanelBorder.Visibility = Visibility.Visible;
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

        _latest = await FetchLatestDlssVersionAsync();

        UpdateContextUi();
        UpdateButtonsState();
    }

    private async Task<DlssVersionInfo?> FetchLatestDlssVersionAsync()
    {
        try
        {
            var release = await _nvidiaDlssService.GetLatestAsync();
            if (release?.Version != null)
                return new DlssVersionInfo { LatestVersion = release.Version, DownloadUrl = release.DownloadUrl };
        }
        catch { /* no network — fall back */ }

        return await _versionRepo.GetLatestAsync();
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
            var progressWin = new UpdateProgressWindow { Owner = this };
            try
            {
                _latest ??= await FetchLatestDlssVersionAsync();
                if (_latest == null)
                {
                    ShowWarning(T("dlss_info_unavailable"));
                    return;
                }

                var currentVer = _context.DetectedVersion?.FileVersion?.Replace(',', '.');
                if (currentVer != null) currentVer = FormatVersionForDisplay(currentVer);
                progressWin.SetVersionLine(currentVer ?? "?", _latest.LatestVersion ?? "?");
                progressWin.Show();

                var prog = new Progress<AppUpdateProgress>(p => progressWin.Report(p));

                string tempPath;
                try
                {
                    // 1. Try official NVIDIA/DLSS GitHub release (always newest)
                    var nvRelease = await _nvidiaDlssService.GetLatestAsync();
                    if (nvRelease == null)
                        throw new InvalidOperationException("No release found in NVIDIA/DLSS repository.");

                    tempPath = await _updater.DownloadAsync(nvRelease.DownloadUrl, _context.DlssDllPath!,
                        expectedSha256: null, progress: prog);
                }
                catch
                {
                    // 2. Fall back to bundled DLSS.zip shipped with the app
                    if (!File.Exists(_bundledDlssZip))
                        throw;

                    progressWin.Report(new AppUpdateProgress("Установка из встроенного архива…"));
                    tempPath = await _updater.UseLocalAsync(_bundledDlssZip, _context.DlssDllPath!, _latest.Sha256);
                }

                _updater.Replace(tempPath, _context.DlssDllPath!);
                _context = await Task.Run(() => _scanner.Scan(_context.FolderPath));

                progressWin.Close();
                UpdateContextUi();
                RefreshCurrentGameTile();
                UpdateButtonsState();
                ShowInfo(T("dlss_updated"));
            }
            catch (Exception ex)
            {
                progressWin.Close();
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
            _updater.Replace(tempPath, _context.DlssDllPath!);
            _context = _scanner.Scan(_context.FolderPath);

            UpdateContextUi();
            RefreshCurrentGameTile();
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

    private async void OnSelectVersion(object sender, RoutedEventArgs e)
    {
        if (_isBusy) return;
        if (string.IsNullOrWhiteSpace(_context.DlssDllPath))
        {
            ShowWarning(T("dll_not_found"));
            return;
        }

        var currentVer = _context.DetectedVersion?.FileVersion?.Replace(',', '.');
        if (currentVer != null) currentVer = FormatVersionForDisplay(currentVer);

        var picker = new VersionPickerWindow(currentVer) { Owner = this };
        if (picker.ShowDialog() != true) return;

        await RunBusy(async () =>
        {
            var progressWin = new UpdateProgressWindow { Owner = this };
            try
            {
                string tempPath;

                if (picker.SelectedDownloadUrl != null)
                {
                    progressWin.Show();
                    var prog = new Progress<AppUpdateProgress>(p => progressWin.Report(p));
                    tempPath = await _updater.DownloadAsync(
                        picker.SelectedDownloadUrl, _context.DlssDllPath!, progress: prog);
                }
                else if (picker.SelectedLocalPath != null)
                {
                    progressWin.Report(new AppUpdateProgress("Установка из локального файла…"));
                    progressWin.Show();
                    tempPath = await _updater.UseLocalAsync(picker.SelectedLocalPath, _context.DlssDllPath!);
                }
                else return;

                _updater.Replace(tempPath, _context.DlssDllPath!);
                _context = await Task.Run(() => _scanner.Scan(_context.FolderPath));

                progressWin.Close();
                UpdateContextUi();
                RefreshCurrentGameTile();
                UpdateButtonsState();
                ShowInfo(T("dlss_updated"));
            }
            catch (Exception ex)
            {
                progressWin.Close();
                ShowError(TF("local_update_error", ex.Message));
            }
        });
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
        if (_isBusy || !CheckAppUpdatesButton.IsEnabled) return;

        CheckAppUpdatesButton.IsEnabled = false;
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
                    var progressWin = new UpdateProgressWindow { Owner = this };
                    progressWin.SetVersionLine(AppVersion, release.Version);
                    progressWin.Show();
                    var prog = new Progress<AppUpdateProgress>(p => progressWin.Report(p));
                    await _appSelfUpdater.ApplyAsync(release.DownloadUrl, prog);
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
        finally
        {
            CheckAppUpdatesButton.IsEnabled = true;
        }
    }

    private static string? LoadLastFolder(string baseDir)
    {
        try
        {
            var path = Path.Combine(baseDir, ".last_folder");
            if (File.Exists(path))
            {
                var folder = File.ReadAllText(path).Trim();
                if (Directory.Exists(folder)) return folder;
            }
        }
        catch { }
        return null;
    }

    private static void SaveLastFolder(string baseDir, string folder)
    {
        try { File.WriteAllText(Path.Combine(baseDir, ".last_folder"), folder); }
        catch { }
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

        // Color current version based on comparison with latest
        var versionColor = Colors.Gray;
        if (currentVersion != null && _latest?.LatestVersion != null &&
            Version.TryParse(currentVersion.Replace(',', '.'), out var cv) &&
            Version.TryParse(_latest.LatestVersion.Replace(',', '.'), out var lv))
        {
            versionColor = cv < lv ? Colors.Orange : Colors.LightGreen;
        }
        CurrentVersionText.Foreground = new SolidColorBrush(versionColor);

        var supported = _context.DlssDllPath != null;
        DlssSupportText.Text = supported ? T("yes") : T("no");
        var color = supported ? Colors.LightGreen : Colors.OrangeRed;
        DlssSupportText.Foreground = new SolidColorBrush(color);
        SupportDot.Fill = new SolidColorBrush(color);

        if (!string.IsNullOrWhiteSpace(_context.FolderPath))
            SaveLastFolder(AppDomain.CurrentDomain.BaseDirectory, _context.FolderPath);
    }

    private void RefreshCurrentGameTile()
    {
        if (string.IsNullOrWhiteSpace(_context.FolderPath)) return;
        if (GameTilesList.ItemsSource is not System.Collections.Generic.List<GameEntry> games) return;

        var tile = games.FirstOrDefault(g =>
            string.Equals(g.FolderPath, _context.FolderPath, StringComparison.OrdinalIgnoreCase));
        if (tile == null) return;

        bool? newNeedsUpdate = null;
        var currentVersion = _context.DetectedVersion?.FileVersion?.Replace(',', '.');
        if (currentVersion != null && _latest?.LatestVersion != null &&
            Version.TryParse(currentVersion, out var cv) &&
            Version.TryParse(_latest.LatestVersion.Replace(',', '.'), out var lv))
        {
            newNeedsUpdate = cv < lv;
        }

        tile.NeedsUpdate = newNeedsUpdate;
    }

    private async Task LoadMissingSteamIconsAsync(List<GameEntry> games)
    {
        var iconCacheDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icon_cache");
        try { Directory.CreateDirectory(iconCacheDir); } catch { return; }

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

        foreach (var game in games.Where(g => g.Icon == null && g.SteamAppId != null))
        {
            var cachedPath = Path.Combine(iconCacheDir, $"{game.SteamAppId}.jpg");
            ImageSource? icon = null;

            if (File.Exists(cachedPath))
            {
                icon = GameLibraryScanner.LoadImageFromFile(cachedPath);
            }
            else
            {
                try
                {
                    var url = $"https://cdn.akamai.steamstatic.com/steam/apps/{game.SteamAppId}/header.jpg";
                    var bytes = await http.GetByteArrayAsync(url);
                    await File.WriteAllBytesAsync(cachedPath, bytes);
                    icon = GameLibraryScanner.LoadImageFromBytes(bytes);
                }
                catch { }
            }

            if (icon != null)
                game.Icon = icon;
        }
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
        if (updateNeeded == true)
        {
            var fromVer = FormatVersionForDisplay((_context.DetectedVersion?.FileVersion ?? "?").Replace(',', '.'));
            var toVer = _latest?.LatestVersion ?? "?";
            UpdateButton.ToolTip = $"{fromVer} → {toVer}";
        }
        UpdateStatusText.Text = updateNeeded == false ? T("update_not_required") : string.Empty;
        UpdateStatusText.Visibility = updateNeeded == false ? Visibility.Visible : Visibility.Collapsed;

        VersionButton.Visibility = hasDll ? Visibility.Visible : Visibility.Collapsed;
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
        if (!busy)
        {
            BusyBar.IsIndeterminate = true;
            BusyBar.Value = 0;
            BusyProgressText.Visibility = Visibility.Collapsed;
            BusyProgressText.Text = string.Empty;
        }
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
