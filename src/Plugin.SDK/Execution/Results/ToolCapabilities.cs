using System;
using System.Reflection;

namespace SunEyeVision.Plugin.SDK.Execution.Results;

/// <summary>
/// 工具能力位 —— 框架自动生成的契约条目所依赖的能力。
/// </summary>
/// <remarks>
/// ★ 设计决策（P0.6 能力层根治）：<b>能力是集合，继承是树</b>。用"继承某基类"表达"使用了某能力"，
///   必然"为了要 A 被迫带上 B"——测量工具只需要「引导/卡尺」，却因继承 <c>RoiParameters</c>
///   而在结果树与绑定下拉里凭空多出「检测区图形 / 屏蔽区图形」。
///   故能力的<b>唯一判定源</b>是显式声明（本枚举 + <see cref="ToolCapabilityAttribute"/>），
///   禁止用类身份推断（旧实现 <c>typeof(RoiParameters).IsAssignableFrom(ParamsType)</c> 对 14/14 工具恒真，门槛形同虚设）。
/// </remarks>
[Flags]
public enum ToolCapabilities
{
    /// <summary>未声明任何能力（默认值）。框架不为未声明的能力产出任何框架级契约条目。</summary>
    None = 0,

    /// <summary>图像输入能力（图像源选择 / 图像输入契约）。</summary>
    ImageSource = 1 << 0,

    /// <summary>区域输入能力（检测区 / 屏蔽区几何）。</summary>
    RoiInput = 1 << 1,

    /// <summary>测量引导能力（引导直线 / 引导圆 + 卡尺测量带）。</summary>
    MeasureGuide = 1 << 2,

    /// <summary>位置修正能力（上游位姿修正源）。</summary>
    PositionCorrection = 1 << 3,
}

/// <summary>
/// 工具能力声明 —— 标注在<b>结果类</b>（<see cref="ToolResults"/> 派生类）上。
/// </summary>
/// <remarks>
/// <para><b>为什么标在结果类</b>：结果类在两个阶段都可达，无需任何注入机制——</para>
/// <list type="bullet">
/// <item>运行期：<c>ToolResults.GetOutputVariables()</c> 有 <c>this</c>，直接读特性；</item>
/// <item>设计期：<c>IToolPlugin.ResultType</c> 携带结果类型，读同一特性。</item>
/// </list>
/// <para>→ <b>单一来源，两阶段同源判定</b>，彻底消除"设计期恒注册、运行期看值过滤"的结构抖动。</para>
/// <para><b>与参数承载层的关系</b>：承载层（<c>MeasureGuideParameters</c> / <c>RoiParameters</c>）决定
/// "参数是否物理存在"，本特性决定"契约条目是否产出"。二者必须一致，由守卫契约测试校验
/// （声明了能力却无对应参数 → 可见报错，而非静默空值）。</para>
/// <para><b>默认 <see cref="ToolCapabilities.None"/></b>：忘记声明的工具不会暴露任何框架能力 ——
/// 宁可"少产出 + 报错可见"，也不要"多产出 + 静默误导"。</para>
/// <para><b>为什么放在本命名空间</b>：能力是对<b>结果契约结构</b>的声明，
/// 与 <see cref="ToolResults"/> / <see cref="FrameworkContractSchema"/> 同处一层；
/// 结果类天然可见该命名空间，标注无需额外 <c>using</c>（降低"忘记声明"的概率）。</para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class ToolCapabilityAttribute : Attribute
{
    /// <summary>声明的能力集合。</summary>
    public ToolCapabilities Capabilities { get; }

    /// <param name="capabilities">能力位组合，如 <c>ToolCapabilities.ImageSource | ToolCapabilities.RoiInput</c>。</param>
    public ToolCapabilityAttribute(ToolCapabilities capabilities) => Capabilities = capabilities;
}

/// <summary>
/// 能力读取入口（设计期 / 运行期共用）。
/// </summary>
public static class ToolCapability
{
    /// <summary>读取类型上声明的能力；未标注返回 <see cref="ToolCapabilities.None"/>。</summary>
    public static ToolCapabilities Resolve(Type? type)
        => type?.GetCustomAttribute<ToolCapabilityAttribute>(inherit: false)?.Capabilities ?? ToolCapabilities.None;

    /// <summary>读取实例所属类型声明的能力；实例为空返回 <see cref="ToolCapabilities.None"/>。</summary>
    public static ToolCapabilities Resolve(object? instance) => Resolve(instance?.GetType());
}
