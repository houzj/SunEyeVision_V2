using System.Text.Json;

namespace SunEyeVision.Infrastructure.Persistence.ApplicationData;

/// <summary>Owned application preferences and recovery paths, independent of solution execution.</summary>
public sealed class ApplicationDataPaths
{
    public const int SchemaVersion = 2;
    public ApplicationDataPaths(string root)
    { ArgumentException.ThrowIfNullOrWhiteSpace(root); Root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)); }
    public string Root { get; }
    public string ConfigFile => Path.Combine(Root, "Config", "start-page.json");
    public string SolutionsDirectory => Path.Combine(Root, "Solutions");
    public string LogDirectory => Path.Combine(Root, "Logs");
    public string RecoveryDirectory => Path.Combine(Root, ".recovery");
    public string MarkerFile => Path.Combine(Root, "suneyevision-data.json");
    public void ValidateAvailable()
    {
        if (!Directory.Exists(Root)) throw new DirectoryNotFoundException("应用数据目录不可用：" + Root);
        using var marker = JsonDocument.Parse(File.ReadAllText(MarkerFile));
        var value = marker.RootElement;
        if (value.GetProperty("format").GetString() != "SunEyeVisionData" ||
            value.GetProperty("schemaVersion").GetInt32() != SchemaVersion ||
            !Guid.TryParse(value.GetProperty("dataId").GetString(), out var id) || id == Guid.Empty)
            throw new InvalidDataException("应用数据目录身份或格式不受支持。");
        if (File.Exists(ConfigFile))
        {
            using var preferences = JsonDocument.Parse(File.ReadAllText(ConfigFile));
            if (preferences.RootElement.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException("应用偏好必须是JSON对象。");
        }
        else if (value.TryGetProperty("configRequired", out var required) && required.GetBoolean())
            throw new FileNotFoundException("原应用偏好文件已丢失。", ConfigFile);
        CheckWritable(Root);
    }
    public static void CheckWritable(string directory)
    {
        var probe = Path.Combine(directory, ".suneyevision-write-" + Guid.NewGuid().ToString("N"));
        using var stream = new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose);
    }
}
