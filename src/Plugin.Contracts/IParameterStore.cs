namespace SunEyeVision.Plugin.Contracts;

/// <summary>节点参数值的唯一存放处。声明与值分离，界面和代码读写同一份值。</summary>
public interface IParameterStore
{
    /// <summary>登记一个节点的参数声明。重复登记相同声明幂等，声明不同直接抛异常。</summary>
    void Declare(Guid nodeId, ToolSchema schema);

    /// <summary>读取当前值。从未写入过的字段返回 Unset。</summary>
    ParameterSetting Read(Guid nodeId, string fieldId);

    /// <summary>写入值。节点或字段未声明、类型不符、超出声明范围都直接抛异常。</summary>
    void Write(Guid nodeId, string fieldId, ParameterSetting setting);

    /// <summary>只读快照，供算法执行与方案保存使用。</summary>
    FieldSettings Snapshot(Guid nodeId);

    /// <summary>整体替换节点值，用于方案加载与撤销重做。</summary>
    void Restore(Guid nodeId, FieldSettings settings);

    /// <summary>某个节点的某个字段值发生变化。</summary>
    event Action<Guid, string>? Changed;
}
