using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SunEyeVision.Workflow.Persistence;

namespace SunEyeVision.Workflow;

/// <summary>Windows 只保存可重建定位信息，重要配置保存在用户选择的数据目录。</summary>
public sealed class ApplicationDataLocationService
{
    public string LocatorFile { get; }
    public ApplicationDataLocationService(string? locatorFile = null) =>
        LocatorFile = locatorFile ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SunEyeVision", "data-location.json");

    public ApplicationDataPaths? Locate()
    {
        if (!File.Exists(LocatorFile)) return null;
        using var json = JsonDocument.Parse(File.ReadAllText(LocatorFile));
        var paths = new ApplicationDataPaths(json.RootElement.GetProperty("dataRoot").GetString()
            ?? throw new InvalidDataException("数据位置记录为空。"));
        paths.ValidateAvailable();
        if (json.RootElement.TryGetProperty("dataId", out var expected))
        {
            using var marker = JsonDocument.Parse(File.ReadAllText(paths.MarkerFile));
            if (marker.RootElement.GetProperty("dataId").GetString() != expected.GetString())
                throw new InvalidDataException("此路径的数据目录身份已改变，请重新选择确认，不能自动载入另一目录。");
        }
        return paths;
    }

    public ApplicationDataPaths Prepare(string root)
    {
        if (string.IsNullOrWhiteSpace(root)) throw new ArgumentException("请选择数据目录。");
        var paths = new ApplicationDataPaths(root);
        var installation = Path.GetFullPath(AppContext.BaseDirectory).TrimEnd(Path.DirectorySeparatorChar);
        if (AtomicJsonFile.SamePath(paths.Root, installation) ||
            paths.Root.StartsWith(installation + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new IOException("数据目录不能放在程序安装目录内。");
        if (File.Exists(paths.MarkerFile)) { paths.ValidateAvailable(); return paths; }
        Directory.CreateDirectory(paths.Root);
        ApplicationDataPaths.CheckWritable(paths.Root);
        if (File.Exists(paths.ConfigFile)) throw new InvalidDataException("已有配置但缺少目录标识，请选择正确目录，不会自动覆盖。");
        Directory.CreateDirectory(Path.GetDirectoryName(paths.ConfigFile)!);
        Directory.CreateDirectory(paths.SolutionsDirectory);
        Directory.CreateDirectory(paths.LogDirectory);
        Directory.CreateDirectory(paths.RecoveryDirectory);
        AtomicJsonFile.Write(paths.MarkerFile, JsonSerializer.Serialize(new { format = "SunEyeVisionData", schemaVersion = 1, dataId = Guid.NewGuid() }));
        paths.ValidateAvailable();
        return paths;
    }

    public void Remember(ApplicationDataPaths paths)
    {
        paths.ValidateAvailable();
        if (File.Exists(paths.ConfigFile))
        {
            var marker = JsonNode.Parse(File.ReadAllText(paths.MarkerFile))!.AsObject();
            marker["configRequired"] = true;
            AtomicJsonFile.Write(paths.MarkerFile, marker.ToJsonString());
        }
        using var identity = JsonDocument.Parse(File.ReadAllText(paths.MarkerFile));
        AtomicJsonFile.Write(LocatorFile, JsonSerializer.Serialize(new { schemaVersion = 1, dataRoot = paths.Root, dataId = identity.RootElement.GetProperty("dataId").GetString() }));
    }

    public void MigrateData(ApplicationDataPaths source, ApplicationDataPaths target)
    {
        source.ValidateAvailable();
        target.ValidateAvailable();
        if (AtomicJsonFile.SamePath(source.Root, target.Root)) return;
        if (target.Root.StartsWith(source.Root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
            source.Root.StartsWith(target.Root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new IOException("新旧数据目录不能相互包含。");
        if (File.Exists(target.ConfigFile) || Directory.EnumerateFiles(target.SolutionsDirectory, "*", SearchOption.AllDirectories).Any())
            throw new IOException("目标已有配置或方案，请选择空数据目录，避免覆盖。");
        var configuration = new SolutionSettings();
        configuration.SetBasePath(source.Root);
        if (File.Exists(source.ConfigFile) && !configuration.Load(source.ConfigFile)) throw new InvalidDataException("源配置无法读取。");
        // Source paths inside its root relocate; external paths remain untouched.
        foreach (var metadata in configuration.GetKnownSolutions())
        {
            if (metadata.FilePath.StartsWith(source.SolutionsDirectory.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                metadata.FilePath = Path.Combine(target.Root, Path.GetRelativePath(source.Root, metadata.FilePath));
                metadata.DirectoryPath = Path.GetDirectoryName(metadata.FilePath)!;
            }
            configuration.AddKnownSolution(metadata);
        }
        var copied = new List<string>();
        var originalMarker = File.ReadAllText(target.MarkerFile);
        string? writtenConfiguration = null;
        try
        {
            foreach (var folder in new[] { "Solutions", ".recovery" })
            {
                var from = Path.Combine(source.Root, folder);
                if (!Directory.Exists(from)) continue;
                foreach (var path in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories))
                {
                    var destination = Path.Combine(target.Root, Path.GetRelativePath(source.Root, path));
                    Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                    File.Copy(path, destination, false);
                    copied.Add(destination);
                    if (Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))) != Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(destination))))
                        throw new IOException("迁移内容校验失败：" + path);
                }
            }
            configuration.SetBasePath(target.Root);
            if (!configuration.Save(target.ConfigFile)) throw new IOException("目标配置保存失败。");
            writtenConfiguration = File.ReadAllText(target.ConfigFile);
            Remember(target);
        }
        catch
        {
            // Only files created by this migration are removed; source is never moved/deleted.
            foreach (var path in copied) if (File.Exists(path)) File.Delete(path);
            if (writtenConfiguration != null && File.Exists(target.ConfigFile) && File.ReadAllText(target.ConfigFile) == writtenConfiguration)
                File.Delete(target.ConfigFile);
            AtomicJsonFile.Write(target.MarkerFile, originalMarker);
            throw;
        }
    }

    public string ImportLegacy(string legacyConfig, ApplicationDataPaths paths)
    {
        if (File.Exists(paths.ConfigFile)) return "已有新配置，保留原配置，未导入旧记录。";
        var settings = new SolutionSettings();
        settings.SetBasePath(Path.GetDirectoryName(Path.GetFullPath(legacyConfig))!);
        if (!settings.Load(legacyConfig)) throw new InvalidDataException("旧配置无法读取。");
        var legacyJson = File.ReadAllText(legacyConfig);
        var recoveryConfig = Path.Combine(paths.RecoveryDirectory, "legacy-settings.json");
        if (File.Exists(recoveryConfig) && File.ReadAllText(recoveryConfig) != legacyJson)
            throw new IOException("已有不同的迁移恢复配置，不会覆盖，请选择新的数据目录。");
        if (!File.Exists(recoveryConfig)) AtomicJsonFile.Write(recoveryConfig, legacyJson, overwrite: false);
        var changed = new List<(string Path, string Original, string Updated)>();
        try
        {
            foreach (var metadata in settings.GetKnownSolutions())
            {
                if (!File.Exists(metadata.FilePath)) continue;
                var disk = new SolutionRepository(paths.RecoveryDirectory).LoadMetadata(metadata.FilePath);
                if (disk == null || disk.Id != metadata.Id)
                    throw new InvalidDataException("旧配置与方案身份不一致或格式不支持，请先确认记录，不会猜测覆盖：" + metadata.FilePath);
                var original = File.ReadAllText(metadata.FilePath);
                var document = JsonNode.Parse(original)?.AsObject() ?? throw new InvalidDataException("方案格式错误：" + metadata.FilePath);
                if (string.IsNullOrWhiteSpace(document["Name"]?.GetValue<string>())) document["Name"] = metadata.Name;
                if (string.IsNullOrWhiteSpace(document["Description"]?.GetValue<string>())) document["Description"] = metadata.Description;
                var updated = document.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
                if (updated != original)
                {
                    var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(metadata.FilePath).ToUpperInvariant())));
                    AtomicJsonFile.Write(Path.Combine(paths.RecoveryDirectory, key + ".previous.solution"), original);
                    AtomicJsonFile.Write(metadata.FilePath, updated);
                    changed.Add((metadata.FilePath, original, updated));
                }
                var legacyBackup = metadata.FilePath + ".bak";
                if (File.Exists(legacyBackup))
                {
                    var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(metadata.FilePath).ToUpperInvariant())));
                    AtomicJsonFile.Write(Path.Combine(paths.RecoveryDirectory, key + ".imported.solution"), File.ReadAllText(legacyBackup));
                }
            }
            settings.SetBasePath(paths.Root);
            var report = $"旧配置：{legacyConfig}\n记录数：{settings.GetKnownSolutions().Count}\n方案及旧配置原位置保留。";
            AtomicJsonFile.Write(Path.Combine(paths.Root, "Config", "migration-result.json"), JsonSerializer.Serialize(new { source = legacyConfig, records = settings.GetKnownSolutions().Count, changedFiles = changed.Count }));
            if (!settings.Save(paths.ConfigFile)) throw new IOException("迁移配置保存失败。");
            return report;
        }
        catch
        {
            foreach (var change in changed.AsEnumerable().Reverse())
                if (File.Exists(change.Path) && File.ReadAllText(change.Path) == change.Updated)
                    AtomicJsonFile.Write(change.Path, change.Original);
            throw;
        }
    }
}
