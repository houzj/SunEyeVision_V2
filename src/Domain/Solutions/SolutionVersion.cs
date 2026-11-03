using System;
using System.Collections.Generic;
using System.Linq;

namespace SunEyeVision.Domain.Solutions;

/// <summary>
/// 解决方案版本
/// </summary>
/// <remarks>
/// 用于记录解决方案的版本历史，支持版本追踪和回滚。
/// </remarks>
public class SolutionVersion
{
    /// <summary>
    /// 版本号
    /// </summary>
    public string Version { get; set; } = "";

    /// <summary>
    /// 时间戳
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.Now;

    /// <summary>
    /// 版本描述
    /// </summary>
    public string Description { get; set; } = "";

    /// <summary>
    /// 变更内容
    /// </summary>
    public List<string> Changes { get; set; } = new();

    /// <summary>
    /// 作者
    /// </summary>
    public string Author { get; set; } = "";

    /// <summary>
    /// 无参构造函数
    /// </summary>
    public SolutionVersion()
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    public SolutionVersion(string version, string description)
    {
        Version = version;
        Description = description;
        Timestamp = DateTime.Now;
    }

    /// <summary>
    /// 构造函数（带变更列表）
    /// </summary>
    public SolutionVersion(string version, string description, List<string> changes)
    {
        Version = version;
        Description = description;
        Changes = changes ?? new List<string>();
        Timestamp = DateTime.Now;
    }

    /// <summary>
    /// 添加变更项
    /// </summary>
    public void AddChange(string change)
    {
        if (!string.IsNullOrWhiteSpace(change))
        {
            Changes.Add(change);
        }
    }

    /// <summary>
    /// 获取变更摘要
    /// </summary>
    public string GetChangeSummary()
    {
        if (Changes.Count == 0)
        {
            return Description;
        }

        return $"{Description} ({Changes.Count} 项变更)";
    }

    /// <summary>
    /// 验证版本信息
    /// </summary>
    public (bool IsValid, List<string> Errors) Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(Version))
        {
            errors.Add("版本号不能为空");
        }

        return (errors.Count == 0, errors);
    }
}


