using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace DlssChecker.Services;

public sealed class NvidiaOverrideService
{
    private const string OverrideValueName = "{41FCC608-8496-4DEF-B43E-7D9BD675A6FF}";

    public bool IsEnabled()
    {
        return HasEnabledValue(@"SOFTWARE\NVIDIA Corporation\Global", OverrideValueName) &&
               (HasEnabledValue(@"SYSTEM\ControlSet001\Services\nvlddmkm", OverrideValueName) ||
                HasEnabledValue(@"SYSTEM\CurrentControlSet\Services\nvlddmkm", OverrideValueName));
    }

    public void Enable(string regFilePath)
    {
        ImportRegFile(regFilePath);
    }

    private static bool HasEnabledValue(string subKeyPath, string valueName)
    {
        using var key = Registry.LocalMachine.OpenSubKey(subKeyPath, writable: false);
        var value = key?.GetValue(valueName);

        if (value is byte[] bytes)
        {
            return bytes.Length > 0 && bytes[0] == 0x01;
        }

        if (value is int intValue)
        {
            return intValue == 1;
        }

        return false;
    }

    private static void ImportRegFile(string regFilePath)
    {
        if (!File.Exists(regFilePath))
        {
            throw new FileNotFoundException("NVIDIA override file was not found.", regFilePath);
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "reg.exe",
                Arguments = $"import \"{regFilePath}\"",
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                throw new InvalidOperationException("Failed to start reg.exe.");
            }

            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"reg.exe exited with code {process.ExitCode}.");
            }
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            throw new OperationCanceledException("UAC prompt was cancelled.", ex);
        }
    }
}
