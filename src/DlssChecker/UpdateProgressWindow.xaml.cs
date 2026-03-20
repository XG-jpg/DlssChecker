using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using DlssChecker.Services;

namespace DlssChecker;

public partial class UpdateProgressWindow : Window
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    public UpdateProgressWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            int dark = 1;
            DwmSetWindowAttribute(hwnd, 20, ref dark, sizeof(int));
        };
    }

    public void SetVersionLine(string fromVersion, string toVersion)
    {
        VersionText.Text = $"v{fromVersion}  →  v{toVersion}";
    }

    public void Report(AppUpdateProgress p)
    {
        StatusText.Text = p.Status;

        if (p.Fraction.HasValue)
        {
            ProgressBar.IsIndeterminate = false;
            ProgressBar.Value = p.Fraction.Value * 100;

            var dlMB = p.DownloadedBytes / 1_048_576.0;
            var totalMB = p.TotalBytes / 1_048_576.0;
            var speedStr = p.SpeedMBps > 0 ? $"  ·  {p.SpeedMBps:F1} МБ/с" : string.Empty;
            DetailsText.Text = $"{dlMB:F1} МБ / {totalMB:F1} МБ{speedStr}";
        }
        else
        {
            ProgressBar.IsIndeterminate = true;
            DetailsText.Text = string.Empty;
        }
    }
}
