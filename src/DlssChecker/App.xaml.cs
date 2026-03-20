using System;
using System.Windows;
using DlssChecker.Services;

namespace DlssChecker;

public partial class App : System.Windows.Application
{
    private const string RepositoryOwner = "XG-jpg";
    private const string RepositoryName = "DllsChecker";

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Try to find and apply an update before showing the main window
        try
        {
            var gitHub = new GitHubReleaseService();
            var release = await gitHub.GetLatestReleaseAsync(RepositoryOwner, RepositoryName);

            if (release != null &&
                !string.IsNullOrWhiteSpace(release.DownloadUrl) &&
                Version.TryParse(release.Version, out var latestVer) &&
                Version.TryParse(AppInfo.Version, out var currentVer) &&
                latestVer > currentVer)
            {
                var progressWin = new UpdateProgressWindow();
                progressWin.SetVersionLine(AppInfo.Version, release.Version);
                progressWin.Show();

                var updater = new AppSelfUpdater();
                var prog = new Progress<AppUpdateProgress>(p => progressWin.Report(p));

                try
                {
                    await updater.ApplyAsync(release.DownloadUrl, prog);
                    // ApplyAsync restarts the process and shuts down — we never reach here
                    return;
                }
                catch
                {
                    // Update failed — close the progress window and open main app normally
                    progressWin.Close();
                }
            }
        }
        catch
        {
            // Network unavailable or GitHub down — just open normally
        }

        new MainWindow().Show();
    }
}
