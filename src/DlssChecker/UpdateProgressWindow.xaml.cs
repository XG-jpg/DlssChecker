using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

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

    public void SetStatus(string text) => StatusText.Text = text;

    public void SetProgress(double value)
    {
        ProgressBar.IsIndeterminate = false;
        ProgressBar.Value = value * 100;
        PercentText.Text = $"{(int)(value * 100)}%";
    }

    public void SetIndeterminate(string text)
    {
        StatusText.Text = text;
        ProgressBar.IsIndeterminate = true;
        PercentText.Text = string.Empty;
    }
}
