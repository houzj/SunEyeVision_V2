using System;
using System.Linq;
using System.Reflection;

namespace SunEyeVision.Core.Versioning;

/// <summary>
/// 产品版本的唯一读取入口（rule-022）。
/// 数据全部来自构建期注入的程序集特性，运行时不读任何外部文件：
/// <list type="bullet">
///   <item><see cref="AssemblyInformationalVersionAttribute"/>：四段产品版本 + "+提交 SHA"（SDK 由 SourceRevisionId 自动附加，SHA 属构建元数据、不参与版本比较）</item>
///   <item><c>AssemblyMetadata("Channel")</c> / <c>AssemblyMetadata("ReleaseDate")</c>：由 Directory.Build.props 从版本真源 version.json 注入</item>
/// </list>
/// 界面与脚本禁止硬编码版本号，一律读本类。
/// </summary>
public static class AppVersionInfo
{
    /// <summary>四段产品版本（MAJOR.MINOR.PATCH.BUILD），例如 2.0.0.1。取不到时为空串。</summary>
    public static string Product { get; } = GetInformationalPart(0);

    /// <summary>构建时的提交 SHA（构建元数据）。取不到时为空串。</summary>
    public static string Commit { get; } = GetInformationalPart(1);

    /// <summary>渠道标识：alpha / beta / rc / stable / local。未注入时为空串。</summary>
    public static string Channel { get; } = GetMetadata("Channel");

    /// <summary>发布日期（yyyy-MM-dd，来自版本真源）。未注入时为空串。</summary>
    public static string ReleaseDate { get; } = GetMetadata("ReleaseDate");

    /// <summary>面向界面的版本显示串：产品版本 + 渠道中文名（无渠道或正是版时只显示版本号）。</summary>
    public static string Display { get; } = BuildDisplay();

    /// <summary>完整版本串：产品版本 + "+" + 提交 SHA（用于诊断输出）。</summary>
    public static string Full { get; } = BuildFull();

    private static string Informational =>
        typeof(AppVersionInfo).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? string.Empty;

    private static string GetInformationalPart(int index)
    {
        var parts = Informational.Split('+', 2);
        if (index == 0) { return parts.Length > 0 ? parts[0].Trim() : string.Empty; }
        return parts.Length > 1 ? parts[1].Trim() : string.Empty;
    }

    private static string GetMetadata(string key) =>
        typeof(AppVersionInfo).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => string.Equals(attribute.Key, key, StringComparison.Ordinal))
            ?.Value ?? string.Empty;

    private static string BuildDisplay()
    {
        var version = string.IsNullOrEmpty(Product) ? "未知版本" : Product;
        var label = Channel switch
        {
            "alpha" => "内测版",
            "beta" => "公测版",
            "rc" => "候选版",
            _ => string.Empty
        };
        return string.IsNullOrEmpty(label) ? version : version + "（" + label + "）";
    }

    private static string BuildFull() =>
        string.IsNullOrEmpty(Commit) ? Product : Product + "+" + Commit;
}
