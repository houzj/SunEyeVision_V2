using System.Text.Json;
using System.Text.Json.Nodes;
using SunEyeVision.Persistence;

namespace SunEyeVision.Infrastructure.Persistence.ApplicationData;

/// <summary>Locates or initializes owned data. Never imports, rewrites or migrates solution files.</summary>
public sealed class ApplicationDataLocationService
{
    public ApplicationDataLocationService(string? locatorFile = null)
        => LocatorFile = Path.GetFullPath(locatorFile ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SunEyeVision", "data-location.json"));
    public string LocatorFile { get; }
    public ApplicationDataPaths? Locate()
    {
        if (!File.Exists(LocatorFile)) return null;
        using var document = JsonDocument.Parse(File.ReadAllText(LocatorFile));
        var locator = document.RootElement;
        if (locator.GetProperty("schemaVersion").GetInt32() != ApplicationDataPaths.SchemaVersion)
            throw new InvalidDataException("应用目录定位格式不受支持。");
        var paths = new ApplicationDataPaths(locator.GetProperty("dataRoot").GetString() ?? throw new InvalidDataException("应用目录为空。"));
        paths.ValidateAvailable();
        using var marker = JsonDocument.Parse(File.ReadAllText(paths.MarkerFile));
        if (marker.RootElement.GetProperty("dataId").GetString() != locator.GetProperty("dataId").GetString())
            throw new InvalidDataException("应用数据目录身份已变化，请重新选择。");
        return paths;
    }
    public ApplicationDataPaths Prepare(string root)
    {
        var paths = new ApplicationDataPaths(root);
        var installation = Path.TrimEndingDirectorySeparator(Path.GetFullPath(AppContext.BaseDirectory));
        if (AtomicJsonFile.SamePath(paths.Root, installation) || paths.Root.StartsWith(installation + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new IOException("应用数据不能放在程序输出或安装目录内。");
        if (File.Exists(paths.MarkerFile)) { paths.ValidateAvailable(); return paths; }
        if (File.Exists(paths.ConfigFile) || Directory.Exists(paths.SolutionsDirectory) && Directory.EnumerateFileSystemEntries(paths.SolutionsDirectory).Any())
            throw new InvalidDataException("已有应用数据但缺少目录身份，不会自动接管或覆盖。");
        Directory.CreateDirectory(paths.Root); ApplicationDataPaths.CheckWritable(paths.Root);
        foreach (var folder in new[] { Path.GetDirectoryName(paths.ConfigFile)!, paths.SolutionsDirectory, paths.LogDirectory, paths.RecoveryDirectory })
            Directory.CreateDirectory(folder);
        AtomicJsonFile.Write(paths.MarkerFile, JsonSerializer.Serialize(new { format = "SunEyeVisionData", schemaVersion = ApplicationDataPaths.SchemaVersion, dataId = Guid.NewGuid() }), overwrite: false);
        paths.ValidateAvailable(); return paths;
    }
    public void Remember(ApplicationDataPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths); paths.ValidateAvailable();
        var marker = JsonNode.Parse(File.ReadAllText(paths.MarkerFile))!.AsObject();
        if (File.Exists(paths.ConfigFile))
        { marker["configRequired"] = true; AtomicJsonFile.Write(paths.MarkerFile, marker.ToJsonString()); }
        AtomicJsonFile.Write(LocatorFile, JsonSerializer.Serialize(new { schemaVersion = ApplicationDataPaths.SchemaVersion,
            dataRoot = paths.Root, dataId = marker["dataId"]!.GetValue<string>() }));
    }
}
