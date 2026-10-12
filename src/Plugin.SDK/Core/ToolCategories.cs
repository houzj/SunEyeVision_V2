using System;
using System.Collections.Generic;
using System.Linq;

namespace SunEyeVision.Plugin.SDK.Core;

/// <summary>Shared category names, semantic icon keys and workflow-oriented ordering.</summary>
public static class ToolCategories
{
    public const string Acquisition = "图像采集", Preprocessing = "图像预处理", Location = "特征定位",
        Region = "区域分析", Measurement = "几何测量", Calibration = "坐标标定",
        Communication = "通信工具", Control = "流程控制", Script = "脚本工具";
    public static IReadOnlyList<ToolCategoryDefinition> Definitions { get; } = Array.AsReadOnly(new[]
    {
        new ToolCategoryDefinition(Acquisition, 10, "camera"), new(Preprocessing, 20, "sliders"),
        new(Location, 30, "locate"), new(Region, 40, "region"), new(Measurement, 50, "ruler"),
        new(Calibration, 60, "axes"), new(Communication, 70, "exchange"),
        new(Control, 80, "branch"), new(Script, 90, "code")
    });
    public static ToolCategoryDefinition GetDefinition(string name) =>
        Definitions.FirstOrDefault(d => d.Name == name) ?? new(name, 1000, "generic");
}

public sealed record ToolCategoryDefinition(string Name, int Order, string IconKey);
