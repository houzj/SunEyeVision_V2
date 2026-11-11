using System.Globalization;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Plugin.Wpf.Controls.Native.Composition.Results.PropertyList;

/// <summary>
/// 主结果树（ResultPresentationPresenter）与只读属性树（DeclaredPropertyList）共用的
/// 语义文本投影。仅当显示声明名与标量原值同时可识别时返回原厂显示文本，否则返回 null
/// 交回通用投影，不改变任何值、端口身份或 publication 合同。
/// 键与原值来自 Blob 输出声明（tools/SunEyeVision.BlobAnalysis/Runtime/Results/BlobOutput.cs：
/// 模块状态/单体状态 0/1、阈值方式 SegmentationModeUsed、极性 TypeUsed=Binary/BinaryInv）；
/// 映射依据登记在 docs/collaboration/evidence/Blob结果格式语义投影原件对照-20261111.json。
/// </summary>
public static class ResultSemanticText
{
    public static string? TryProject(string displayName, object? scalar) => displayName switch
    {
        "模块状态" or "单体状态" when scalar is long status
            => status == 1 ? "成功" : status == 0 ? "失败" : status.ToString(CultureInfo.InvariantCulture),
        "阈值方式" when scalar is string mode => mode switch
        {
            "SingleThreshold" => "单阈值",
            "DoubleThreshold" => "双阈值",
            "AutoThreshold" => "自动阈值",
            "FixedSoftThreshold" => "软阈值（固定）",
            "RelativeSoftThreshold" => "软阈值（相对）",
            _ => null
        },
        "极性" when scalar is string polarity => polarity switch
        {
            "Binary" => "亮于背景",
            "BinaryInv" => "暗于背景",
            _ => null
        },
        _ => null
    };

    /// <summary>
    /// 原厂 Blob 数值为单精度自然位数（C03 原证：11217.29、0.4195392），不是固定位数；
    /// 仅 Float32 存储适用，其他存储返回 null 走各自视图的既有合同。
    /// </summary>
    public static string? TryProjectFloat32(NumericStorage? storage, double value)
        => storage == NumericStorage.Float32 ? ((float)value).ToString(CultureInfo.InvariantCulture) : null;
}
