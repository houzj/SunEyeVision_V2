using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using SunEyeVision.Workflow.Persistence;

namespace SunEyeVision.UI.Services.Startup;

/// <summary>启动页偏好与路径历史不参与方案格式/身份校验，也不作为打开前置。</summary>
public sealed class StartPageSettings
{
    public bool HideStartPage { get; set; }
    public List<RecentSolutionFile> RecentFiles { get; set; } = new();
    // Hide migrated catalog suggestions without deleting files or closing the current workspace.
    public List<string> DismissedPaths { get; set; } = new();
    public static StartPageSettings Load(string file)
    {
        if (!File.Exists(file)) return new();
        var value = JsonSerializer.Deserialize<StartPageSettings>(File.ReadAllText(file))
            ?? throw new InvalidDataException("起始页设置为空。");
        value.RecentFiles ??= new();
        value.DismissedPaths ??= new();
        value.RecentFiles = value.RecentFiles.Where(x => !string.IsNullOrWhiteSpace(x.Path))
            .GroupBy(x => System.IO.Path.GetFullPath(x.Path), StringComparer.OrdinalIgnoreCase)
            .Select(x => x.OrderByDescending(r => r.LastOpened).First()).OrderByDescending(x => x.LastOpened).Take(12).ToList();
        return value;
    }
    public void RecordOpened(string path, string name)
    {
        path = System.IO.Path.GetFullPath(path);
        DismissedPaths.RemoveAll(x => string.Equals(x, path, StringComparison.OrdinalIgnoreCase));
        RecentFiles.RemoveAll(x => string.Equals(x.Path, path, StringComparison.OrdinalIgnoreCase));
        RecentFiles.Insert(0, new() { Path = path, Name = name, LastOpened = DateTimeOffset.Now });
        RecentFiles = RecentFiles.Take(12).ToList();
    }
    public void Save(string file) => AtomicJsonFile.Write(file, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
}
public sealed class RecentSolutionFile
{
    public string Path { get; set; } = "";
    public string Name { get; set; } = "";
    public DateTimeOffset LastOpened { get; set; }
    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? System.IO.Path.GetFileNameWithoutExtension(Path) : Name;
    public bool IsMissing => !File.Exists(Path);
    public string Availability => IsMissing ? "文件已移动" : "可打开";
    public string DisplayTime => IsMissing ? "文件已移动" : LastOpened.LocalDateTime.Date == DateTime.Today
        ? "今天 " + LastOpened.LocalDateTime.ToString("HH:mm")
        : LastOpened.LocalDateTime.Date == DateTime.Today.AddDays(-1) ? "昨天"
        : LastOpened.LocalDateTime.ToString("MM-dd HH:mm");
}
