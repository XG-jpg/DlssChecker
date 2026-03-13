using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;

namespace DlssChecker;

public partial class ChangelogWindow : Window
{
    public ChangelogWindow(string version, string changelogPath)
    {
        InitializeComponent();

        TitleText.Text = $"Что нового в v{version}  /  What's new in v{version}";
        SubText.Text = "Изменения последнего обновления  ·  Changes in this update";

        ChangelogText.Text = ParseVersionSection(changelogPath, version);
    }

    private void OnOk(object sender, RoutedEventArgs e) => Close();

    private static string ParseVersionSection(string path, string version)
    {
        if (!File.Exists(path))
        {
            return "CHANGELOG.md not found.";
        }

        var lines = File.ReadAllLines(path);
        var result = new List<string>();
        var inSection = false;

        foreach (var line in lines)
        {
            if (line.StartsWith("## "))
            {
                if (inSection) break;
                if (line.Contains(version))
                {
                    inSection = true;
                }
                continue;
            }

            if (inSection)
            {
                result.Add(line);
            }
        }

        return result.Count > 0
            ? string.Join(Environment.NewLine, result).Trim()
            : $"No changelog found for v{version}.";
    }
}
