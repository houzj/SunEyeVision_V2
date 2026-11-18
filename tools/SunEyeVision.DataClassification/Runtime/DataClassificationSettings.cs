using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.DataClassification.Runtime;

/// <summary>
/// 数据分类的唯一持久化设置。UI 面字段来自原厂控件反编译；算法语义未取证，故只校验设置自身完备性，
/// 不推断分类数值逻辑。默认空订阅/空分类是可进入参数页的初始态（与原厂新建模块一致）。
/// </summary>
public sealed record DataClassificationSettings : IToolSettings<DataClassificationSettings>
{
    public string InputSource { get; init; } = string.Empty;
    public string Subscriptions { get; init; } = string.Empty;
    public bool OpenMutex { get; init; }
    public string ClassificationSetting { get; init; } = string.Empty;

    public DataClassificationSettings Freeze() => this;

    public IReadOnlyList<Diagnostic> Validate()
    {
        // UI 骨架阶段只保证字段类型/引用合法；算法语义待补证，不在这里伪造判定逻辑。
        return Array.Empty<Diagnostic>();
    }
}
