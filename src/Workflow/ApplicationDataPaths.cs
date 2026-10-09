using System;
using System.IO;

namespace SunEyeVision.Workflow;

public sealed class ApplicationDataPaths
{
    public string Root { get; }
    public string ConfigFile => Path.Combine(Root, "Config", "user-settings.json");
    public string SolutionsDirectory => Path.Combine(Root, "Solutions");
    public string LogDirectory => Path.Combine(Root, "Logs");
    public string RecoveryDirectory => Path.Combine(Root, ".recovery");
    public string MarkerFile => Path.Combine(Root, "suneyevision-data.json");

    public ApplicationDataPaths(string root) => Root = Path.GetFullPath(root);

    public void ValidateAvailable()
    {
        if (!Directory.Exists(Root)) throw new DirectoryNotFoundException("数据目录不可用，请重新定位：" + Root);
        if (!File.Exists(MarkerFile)) throw new InvalidDataException("该位置不是已初始化的 SunEyeVision 数据目录。");
        using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(MarkerFile));
        if (document.RootElement.GetProperty("format").GetString() != "SunEyeVisionData" ||
            document.RootElement.GetProperty("schemaVersion").GetInt32() != 1)
            throw new InvalidDataException("不支持的数据目录格式。");
        if (File.Exists(ConfigFile))
        {
            using var configuration = System.Text.Json.JsonDocument.Parse(File.ReadAllText(ConfigFile));
            if (configuration.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object)
                throw new InvalidDataException("用户配置损坏，不能用空配置覆盖。");
            if (configuration.RootElement.TryGetProperty("SchemaVersion", out var schema) && schema.GetInt32() != 1)
                throw new InvalidDataException("不支持该用户配置格式，请选择兼容的数据目录。");
            var settings = new SolutionSettings(); settings.SetBasePath(Root);
            if (!settings.Load(ConfigFile)) throw new InvalidDataException("用户配置记录损坏，请恢复原配置或选择其他目录。");
        }
        else if (document.RootElement.TryGetProperty("configRequired", out var required) && required.GetBoolean())
            throw new FileNotFoundException("原用户配置已丢失，请恢复配置或选择其他数据目录，不能用空配置替代。", ConfigFile);
        CheckWritable(Root);
    }

    public static void CheckWritable(string directory)
    {
        var probe = Path.Combine(directory, ".suneyevision-write-" + Guid.NewGuid().ToString("N"));
        try { using var stream = new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose); }
        catch (Exception ex) { throw new IOException("数据目录不可写，请选择其他位置：" + directory, ex); }
    }
}
