using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace SunEyeVision.UI.Services;

public class FileAssociationService
{
    private const string Extension = ".solution";
    private static string ProgId => SunEyeVision.Core.Versioning.AppVersionInfo.Channel == "local" ? "SunEyeVision.Local.SolutionFile" : "SunEyeVision.SolutionFile";
    [DllImport("shell32.dll")] private static extern void SHChangeNotify(uint eventId, uint flags, IntPtr item1, IntPtr item2);
    public static bool IsInstalledLocation()
    {
        using var machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using var installation = machine.OpenSubKey(SunEyeVision.Core.Versioning.AppVersionInfo.Channel == "local" ? @"Software\SunEyeVision\Local" : @"Software\SunEyeVision");
        var root = installation?.GetValue("InstallLocation") as string;
        return !string.IsNullOrEmpty(root) && SunEyeVision.Workflow.Persistence.AtomicJsonFile.SamePath(root, AppContext.BaseDirectory);
    }
    public void RegisterFileAssociation()
    {
        if (!IsInstalledLocation()) throw new InvalidOperationException("开发或未安装版本不修改文件关联，请在安装后的软件中修复。");
        var executable = Environment.ProcessPath ?? throw new InvalidOperationException("无法识别当前程序路径。");
        var icon = System.IO.Path.Combine(AppContext.BaseDirectory, "Icons", "solution.ico");
        if (!File.Exists(icon)) throw new FileNotFoundException("方案图标缺失。", icon);
        using (var extension = Registry.CurrentUser.CreateSubKey(@"Software\Classes\.solution"))
        {
            var current = extension?.GetValue("")?.ToString() ?? "";
            if (string.IsNullOrEmpty(current))
            {
                using var effective = Registry.ClassesRoot.OpenSubKey(Extension);
                current = effective?.GetValue("")?.ToString() ?? "";
            }
            if (SunEyeVision.Core.Versioning.AppVersionInfo.Channel != "local" &&
                (string.IsNullOrEmpty(current) || current.StartsWith("SunEyeVision.", StringComparison.OrdinalIgnoreCase) || current == @"Applications\SunEyeVision.UI.exe"))
                extension?.SetValue("", ProgId);
        }
        using (var openWith = Registry.CurrentUser.CreateSubKey(@"Software\Classes\.solution\OpenWithProgids")) openWith?.SetValue(ProgId, "");
        WriteType(ProgId, executable, icon);
        // Repair our own Windows Open With identity without changing UserChoice or another application.
        if (SunEyeVision.Core.Versioning.AppVersionInfo.Channel != "local") WriteType(@"Applications\SunEyeVision.UI.exe", executable, icon);
        SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero);
    }
    private static void WriteType(string id, string executable, string icon)
    {
        using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Classes\" + id)) key?.SetValue("", "SunEyeVision 解决方案文件");
        using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Classes\" + id + @"\DefaultIcon")) key?.SetValue("", $"\"{icon}\",0");
        using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Classes\" + id + @"\shell\open\command")) key?.SetValue("", $"\"{executable}\" \"%1\"");
    }
    public bool IsRegistered()
    {
        try
        {
            using var command = Registry.CurrentUser.OpenSubKey(@"Software\Classes\" + ProgId + @"\shell\open\command");
            using var icon = Registry.CurrentUser.OpenSubKey(@"Software\Classes\" + ProgId + @"\DefaultIcon");
            return command?.GetValue("")?.ToString() == $"\"{Environment.ProcessPath}\" \"%1\"" &&
                icon?.GetValue("")?.ToString() == $"\"{System.IO.Path.Combine(AppContext.BaseDirectory, "Icons", "solution.ico")}\",0" &&
                File.Exists(System.IO.Path.Combine(AppContext.BaseDirectory, "Icons", "solution.ico"));
        }
        catch { return false; }
    }
    public void UnregisterFileAssociation()
    {
        using var command = Registry.CurrentUser.OpenSubKey(@"Software\Classes\" + ProgId + @"\shell\open\command");
        if (command?.GetValue("")?.ToString() != $"\"{Environment.ProcessPath}\" \"%1\"") return;
        Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\" + ProgId, false);
        using var extension = Registry.CurrentUser.OpenSubKey(@"Software\Classes\.solution", true);
        if (extension?.GetValue("")?.ToString() == ProgId) extension.DeleteValue("", false);
        using var openWith = Registry.CurrentUser.OpenSubKey(@"Software\Classes\.solution\OpenWithProgids", true);
        openWith?.DeleteValue(ProgId, false);
    }
}
