using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SunEyeVision.Plugin.Infrastructure.Managers.Plugin;

/// <summary>
/// 检查单个插件运行目录的交付完整性与冗余副本。
/// </summary>
internal static class PluginDeploymentInspector
{
    internal static PluginDeploymentInspection Inspect(string mainAssemblyPath)
    {
        if (string.IsNullOrWhiteSpace(mainAssemblyPath))
            throw new ArgumentException("插件主程序集路径不能为空。", nameof(mainAssemblyPath));

        var fullPath = Path.GetFullPath(mainAssemblyPath);
        var directory = Path.GetDirectoryName(fullPath) ?? string.Empty;
        var simpleName = Path.GetFileNameWithoutExtension(fullPath);
        var requiredFiles = new[]
        {
            fullPath,
            Path.Combine(directory, simpleName + ".deps.json"),
        };

        var missingRequiredFiles = requiredFiles
            .Where(path => !File.Exists(path))
            .Select(path => Path.GetFileName(path)!)
            .ToArray();

        var redundantHostAssemblies = Directory.Exists(directory)
            ? Directory.EnumerateFiles(directory, "*.dll", SearchOption.TopDirectoryOnly)
                .Where(path => !string.Equals(path, fullPath, StringComparison.OrdinalIgnoreCase))
                .Select(path => Path.GetFileNameWithoutExtension(path)!)
                .Where(PluginAssemblyPolicy.IsHostProvided)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray()
            : Array.Empty<string>();

        return new PluginDeploymentInspection(
            fullPath,
            missingRequiredFiles,
            redundantHostAssemblies,
            File.Exists(Path.Combine(directory, simpleName + ".pdb")));
    }
}

internal sealed record PluginDeploymentInspection(
    string MainAssemblyPath,
    IReadOnlyList<string> MissingRequiredFiles,
    IReadOnlyList<string> RedundantHostAssemblies,
    bool HasSymbols)
{
    internal bool IsDeployable => MissingRequiredFiles.Count == 0;
}
