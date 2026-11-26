namespace SunEyeVision.Plugin.Contracts.Manifest;

/// <summary>
/// 输出端口随配置变化的工具实现本接口。对应原件 <c>DynamicVmIO</c>（动态输出端口）：
/// 端口由操作创建并加入 Outputs，可见性/可订阅性由平台控制。
/// </summary>
/// <remarks>
/// 与 <see cref="IToolDynamicSettings"/> 分工：
/// 后者展开**设置**端口（原件 IDynamicChild），本接口只展开**输出**端口。
/// 实现必须是确定性的，只能读传入的当前字段，不得产生副作用。
/// </remarks>
public interface IToolDynamicOutputs
{
    /// <summary>按当前配置展开输出端口；结构未变化返回 null（避免无谓重建）。</summary>
    IReadOnlyList<Port>? ExpandOutputs(FieldSettings current);

    /// <summary>为新增端口补齐默认值；仅在结构增长时调用。</summary>
    IReadOnlyDictionary<string, ParameterSetting> ExpandOutputDefaults(
        IReadOnlyList<Port> expanded, FieldSettings current);
}
