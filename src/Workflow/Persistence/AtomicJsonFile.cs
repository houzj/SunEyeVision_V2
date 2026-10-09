using System;
using System.IO;
using System.Text;
using System.Text.Json;

namespace SunEyeVision.Workflow.Persistence;

public static class AtomicJsonFile
{
    public static void Write(string path, string json, bool overwrite = true)
    {
        using (JsonDocument.Parse(json)) { }
        path = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = Path.Combine(Path.GetDirectoryName(path)!, "." + Path.GetFileName(path) + "." + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            File.WriteAllText(temporary, json, new UTF8Encoding(false));
            if (File.Exists(path))
            {
                if (!overwrite) throw new IOException("目标文件已存在：" + path);
                File.Replace(temporary, path, null);
            }
            else File.Move(temporary, path);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    public static bool SamePath(string first, string second) =>
        string.Equals(Path.GetFullPath(first).TrimEnd(Path.DirectorySeparatorChar),
            Path.GetFullPath(second).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase);

    public static void ValidateSolutionName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name != name.Trim() || name.EndsWith('.') ||
            name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name is "." or "..")
            throw new ArgumentException("名称不能为空，也不能包含路径、非法字符或末尾空格/点。");
        var stem = name.Split('.')[0].ToUpperInvariant();
        if (stem is "CON" or "PRN" or "AUX" or "NUL" ||
            (stem.Length == 4 && (stem.StartsWith("COM") || stem.StartsWith("LPT")) && stem[3] >= '1' && stem[3] <= '9'))
            throw new ArgumentException("名称不能使用 Windows 保留设备名。");
    }
}
