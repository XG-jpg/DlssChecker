using System.Reflection;

namespace DlssChecker;

internal static class AppInfo
{
    public static readonly string Version =
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";
}
