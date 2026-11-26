using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Plugin.Wpf.Adapters.Regions;

/// <summary>
/// ROI 文档对宿主字段的最小读写接缝。参数页由 DeclaredInputFieldsView 承载，
/// 节点级常驻文档由宿主按 NodeDefinition.Configuration 实现，两者共用同一 ROI 文档逻辑。
/// </summary>
public interface IRoiInputAccess
{
    ParameterSetting Read(string fieldId);
    void Write(string fieldId, ParameterSetting setting);
}
