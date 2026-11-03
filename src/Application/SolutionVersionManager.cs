using System;
using System.Collections.Generic;
using System.Linq;

using SunEyeVision.Domain.Solutions;

namespace SunEyeVision.Application;

/// <summary>
/// 解决方案版本管理器
/// </summary>
public class SolutionVersionManager
{
    private readonly List<SolutionVersion> _versions = new();
    private const int MaxVersions = 50;

    /// <summary>
    /// 获取所有版本
    /// </summary>
    public List<SolutionVersion> GetAll()
    {
        return _versions.ToList();
    }

    /// <summary>
    /// 获取最新版本
    /// </summary>
    public SolutionVersion? GetLatest()
    {
        return _versions.Count > 0 ? _versions[0] : null;
    }

    /// <summary>
    /// 获取指定版本
    /// </summary>
    public SolutionVersion? GetVersion(string version)
    {
        return _versions.FirstOrDefault(v => v.Version == version);
    }

    /// <summary>
    /// 添加新版本
    /// </summary>
    public void AddVersion(SolutionVersion version)
    {
        if (version == null)
            throw new ArgumentNullException(nameof(version));

        // 插入到开头
        _versions.Insert(0, version);

        // 限制版本数量
        while (_versions.Count > MaxVersions)
        {
            _versions.RemoveAt(_versions.Count - 1);
        }
    }

    /// <summary>
    /// 创建新版本
    /// </summary>
    public SolutionVersion CreateVersion(string description, List<string>? changes = null, string author = "")
    {
        var currentVersion = GetLatest();
        var newVersionNumber = IncrementVersion(currentVersion?.Version ?? "1.0");

        var newVersion = new SolutionVersion
        {
            Version = newVersionNumber,
            Description = description,
            Changes = changes ?? new List<string>(),
            Author = author
        };

        AddVersion(newVersion);
        return newVersion;
    }

    /// <summary>
    /// 移除版本
    /// </summary>
    public bool RemoveVersion(string version)
    {
        var versionToRemove = _versions.FirstOrDefault(v => v.Version == version);
        if (versionToRemove == null)
            return false;

        return _versions.Remove(versionToRemove);
    }

    /// <summary>
    /// 清空所有版本
    /// </summary>
    public void Clear()
    {
        _versions.Clear();
    }

    /// <summary>
    /// 版本号递增
    /// </summary>
    private string IncrementVersion(string version)
    {
        if (string.IsNullOrWhiteSpace(version))
            return "1.0";

        var parts = version.Split('.');
        if (parts.Length >= 2)
        {
            if (int.TryParse(parts[1], out var minorVersion))
            {
                return $"{parts[0]}.{minorVersion + 1}";
            }
        }

        return version;
    }
}


