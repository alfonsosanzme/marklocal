using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace MarkLocal.Infrastructure;

public static class WindowsIntegration
{
    private const string ProgId = "MarkLocal.md";
    private const string AppRegistration = "Applications\\MarkLocal.exe";
    private static readonly string[] HandledExtensions = { ".md", ".markdown" };

    public static string ExecutablePath
    {
        get
        {
            string path = Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
            if (string.IsNullOrEmpty(path) || path.EndsWith("dotnet.exe", StringComparison.OrdinalIgnoreCase))
            {
                path = Path.ChangeExtension(typeof(WindowsIntegration).Assembly.Location, ".exe");
            }
            return path;
        }
    }

    public static bool IsAssociated()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey($"Software\\Classes\\{ProgId}\\shell\\open\\command");
            if (key == null) return false;
            string value = key.GetValue(string.Empty) as string ?? string.Empty;
            return value.IndexOf("marklocal.exe", StringComparison.OrdinalIgnoreCase) >= 0;
        }
        catch
        {
            return false;
        }
    }

    public static void Associate()
    {
        string exe = ExecutablePath;
        string command = $"\"{exe}\" \"%1\"";
        string icon = $"\"{exe}\",0";

        using (var progKey = Registry.CurrentUser.CreateSubKey($"Software\\Classes\\{ProgId}"))
        {
            progKey.SetValue(string.Empty, "Documento Markdown");
            progKey.SetValue("FriendlyTypeName", "Documento Markdown");
        }
        using (var iconKey = Registry.CurrentUser.CreateSubKey($"Software\\Classes\\{ProgId}\\DefaultIcon"))
        {
            iconKey.SetValue(string.Empty, icon);
        }
        using (var cmdKey = Registry.CurrentUser.CreateSubKey($"Software\\Classes\\{ProgId}\\shell\\open\\command"))
        {
            cmdKey.SetValue(string.Empty, command);
        }

        foreach (var ext in HandledExtensions)
        {
            using var extKey = Registry.CurrentUser.CreateSubKey($"Software\\Classes\\{ext}");
            extKey.SetValue(string.Empty, ProgId);
            using var openWith = Registry.CurrentUser.CreateSubKey($"Software\\Classes\\{ext}\\OpenWithProgids");
            openWith.SetValue(ProgId, Array.Empty<byte>(), RegistryValueKind.None);
        }

        using (var appKey = Registry.CurrentUser.CreateSubKey($"Software\\Classes\\{AppRegistration}"))
        {
            appKey.SetValue("FriendlyAppName", "MarkLocal");
        }
        using (var appCmd = Registry.CurrentUser.CreateSubKey($"Software\\Classes\\{AppRegistration}\\shell\\open\\command"))
        {
            appCmd.SetValue(string.Empty, command);
        }
        using (var supported = Registry.CurrentUser.CreateSubKey($"Software\\Classes\\{AppRegistration}\\SupportedTypes"))
        {
            foreach (var ext in HandledExtensions) supported.SetValue(ext, string.Empty);
        }

        NotifyShell();
    }

    public static void Disassociate()
    {
        try { Registry.CurrentUser.DeleteSubKeyTree($"Software\\Classes\\{ProgId}", throwOnMissingSubKey: false); } catch { }
        try { Registry.CurrentUser.DeleteSubKeyTree($"Software\\Classes\\{AppRegistration}", throwOnMissingSubKey: false); } catch { }
        foreach (var ext in HandledExtensions)
        {
            try
            {
                using var extKey = Registry.CurrentUser.OpenSubKey($"Software\\Classes\\{ext}", writable: true);
                if (extKey != null)
                {
                    string? current = extKey.GetValue(string.Empty) as string;
                    if (string.Equals(current, ProgId, StringComparison.OrdinalIgnoreCase))
                    {
                        extKey.DeleteValue(string.Empty, throwOnMissingValue: false);
                    }
                    using var openWith = extKey.OpenSubKey("OpenWithProgids", writable: true);
                    openWith?.DeleteValue(ProgId, throwOnMissingValue: false);
                }
            }
            catch
            {
            }
        }
        NotifyShell();
    }

    public static string GetUninstallRegistryPath() =>
        "Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\MarkLocal";

    public static void RegisterUninstaller(string installDirectory, string uninstallCommand, string version)
    {
        using var key = Registry.CurrentUser.CreateSubKey(GetUninstallRegistryPath());
        key.SetValue("DisplayName", "MarkLocal");
        key.SetValue("DisplayVersion", version);
        key.SetValue("Publisher", "MarkLocal");
        key.SetValue("InstallLocation", installDirectory);
        key.SetValue("DisplayIcon", $"{ExecutablePath},0");
        key.SetValue("UninstallString", uninstallCommand);
        key.SetValue("NoModify", 1, RegistryValueKind.DWord);
        key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
    }

    public static void UnregisterUninstaller()
    {
        try { Registry.CurrentUser.DeleteSubKeyTree(GetUninstallRegistryPath(), throwOnMissingSubKey: false); } catch { }
    }

    public static bool IsShellNewEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey("Software\\Classes\\.md\\ShellNew");
            if (key == null) return false;
            object? fileName = key.GetValue("FileName");
            object? nullFile = key.GetValue("NullFile");
            return fileName != null || nullFile != null;
        }
        catch
        {
            return false;
        }
    }

    public static void EnableShellNew(string templateFilePath)
    {
        using var key = Registry.CurrentUser.CreateSubKey("Software\\Classes\\.md\\ShellNew");
        // Limpiar valores previos para evitar combinaciones FileName + NullFile
        try { key.DeleteValue("FileName", throwOnMissingValue: false); } catch { }
        try { key.DeleteValue("NullFile", throwOnMissingValue: false); } catch { }
        if (string.IsNullOrEmpty(templateFilePath))
        {
            key.SetValue("NullFile", string.Empty);
        }
        else
        {
            key.SetValue("FileName", templateFilePath);
        }
        NotifyShell();
    }

    public static void DisableShellNew()
    {
        try
        {
            using var ext = Registry.CurrentUser.OpenSubKey("Software\\Classes\\.md", writable: true);
            ext?.DeleteSubKeyTree("ShellNew", throwOnMissingSubKey: false);
        }
        catch
        {
        }
        NotifyShell();
    }

    [DllImport("shell32.dll", SetLastError = true)]
    private static extern void SHChangeNotify(int wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

    private static void NotifyShell()
    {
        const int SHCNE_ASSOCCHANGED = 0x08000000;
        const uint SHCNF_IDLIST = 0x0000;
        try
        {
            SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);
        }
        catch
        {
        }
    }
}
