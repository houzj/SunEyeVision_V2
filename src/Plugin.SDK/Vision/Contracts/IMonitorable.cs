using System.Collections.Generic;

namespace SunEyeVision.Plugin.SDK.Vision.Contracts
{
    /// <summary>
    /// 监视协议 — 业务对象向监视面板暴露具名字段的自描述接口。
    /// 监视面板递归调用 GetMonitorFields()，将每个 (FieldName, FieldValue) 渲染为子节点。
    /// 字段名为中文，值可为标量/IEnumerable/IMonitorable 自身（递归）。
    /// 零侵入：未实现本接口的对象仍走默认 ToString() 渲染路径。
    /// </summary>
    public interface IMonitorable
    {
        /// <summary>
        /// 返回当前对象的监视字段列表。
        /// 字段名使用中文（与显示名一致）。
        /// </summary>
        IReadOnlyList<(string FieldName, object? FieldValue)> GetMonitorFields();
    }
}
