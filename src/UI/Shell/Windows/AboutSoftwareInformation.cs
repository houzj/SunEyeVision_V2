using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using SunEyeVision.Core.Versioning;

namespace SunEyeVision.UI.Shell.Windows;

/// <summary>只读取当前运行程序的构建元数据，不依赖安装包旁的清单或外部版本文件。</summary>
public sealed class AboutSoftwareInformation
{
    public sealed record InformationRow(string Label, string Value);

    public IReadOnlyList<InformationRow> Rows { get; } = Array.AsReadOnly(new[]
    {
        new InformationRow("产品名称", "SunEyeVision"),
        new InformationRow("发布版本", ReleaseIdentity),
        new InformationRow("发布渠道", ChannelLabel),
        new InformationRow("发布日期", Known(AppVersionInfo.ReleaseDate)),
        new InformationRow("文件版本", Known(typeof(AppVersionInfo).Assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version)),
        new InformationRow("代码基线", Known(AppVersionInfo.Commit)),
        new InformationRow("运行环境", RuntimeInformation.FrameworkDescription + " / " + RuntimeInformation.ProcessArchitecture),
        new InformationRow("操作系统", RuntimeInformation.OSDescription),
        new InformationRow("程序目录", AppContext.BaseDirectory)
    });

    public static string ReleaseIdentity => string.IsNullOrWhiteSpace(AppVersionInfo.Channel) || AppVersionInfo.Channel == "stable"
        ? Known(AppVersionInfo.Product)
        : Known(AppVersionInfo.Product) + "_" + AppVersionInfo.Channel;

    private static string ChannelLabel => AppVersionInfo.Channel switch
    {
        "alpha" => "alpha（内测）", "beta" => "beta（公测）", "rc" => "rc（候选）",
        "stable" => "stable（正式）", "local" => "local（本地测试）", _ => Known(AppVersionInfo.Channel)
    };

    private static string Known(string? value) => string.IsNullOrWhiteSpace(value) ? "未提供" : value;

    public string ToDiagnosticText() => string.Join(Environment.NewLine,
        Rows.Select(row => row.Label + "：" + row.Value)) + Environment.NewLine +
        "说明：提交号表示代码基线，不代表源码没有未提交修改。";
}
