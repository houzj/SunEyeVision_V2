namespace SunEyeVision.Tool.FrameMean.Runtime;

/// <summary>
/// 帧均值的稳定字段身份与枚举取值（工具内共享）。
/// 参数页、结果、投影的声明在 <see cref="FrameMeanManifest"/>；
/// 这里只放被设置校验、编辑器与清单共同引用的常量，不再自带 Schema/读写。
/// </summary>
public static class FrameMeanFields
{
    public const string ProcStep = "proc.step";

    /// <summary>原厂 AlgorithmTab.xml 三项命令 DisplayTitle：统计当前图像 / 不统计当前图像 / 清空统计图像。</summary>
    public static readonly string[] ProcStepKeys = ["1", "2", "3"];
    public static readonly string[] ProcStepLabels = ["统计当前图像", "不统计当前图像", "清空统计图像"];
}
