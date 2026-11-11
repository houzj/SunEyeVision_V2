using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using SunEyeVision.Tool.BlobAnalysis.Runtime;

namespace SunEyeVision.Tool.BlobAnalysis.Wpf.Views;

/// <summary>
/// VM 参数树中的运行参数页接缝。
///
/// 运行参数已经由 BlobParameters.xml、BlobParameterDefinition 和
/// DeclaredFieldsView 组成；这里仅挂接运行态能力，不再保留一套按代码
/// 逐行创建的备用参数界面。这样运行参数的字段身份、模板和释放路径只有
/// 一个来源，避免旧元数据分支重新引入旧控件组合。
/// </summary>
internal static class BlobRunUi
{
    internal static ToolSettingsViewOptions Configure(ToolSettingsViewOptions options)
    {
        var definition=BlobParameterDefinition.Instance;
        var controller=definition.SettingsFieldId(nameof(BlobSettings.SegmentationMode));
        var conditions=options.ParentConditions?.ToDictionary(pair=>pair.Key,pair=>pair.Value,StringComparer.Ordinal)
            ??new Dictionary<string,IReadOnlyList<FieldCondition>>(StringComparer.Ordinal);
        // Original ThresholdType triggers select the active controls. Apply the
        // same activity before materialization, including both range endpoints.
        foreach(var property in BlobThresholdCapabilities.ControlledFields)
        {
            var id=definition.SettingsFieldId(property);
            var hidden=Enum.GetValues<BlobSegmentationMode>()
                .Where(mode=>!BlobThresholdCapabilities.IsThresholdFieldActive(mode,property))
                .Select(mode=>new FieldCondition(controller,mode.ToString(),true));
            conditions[id]=(conditions.GetValueOrDefault(id)??Array.Empty<FieldCondition>()).Concat(hidden).Distinct().ToArray();
        }
        // The original advanced expander never exposes OverlapRatio.
        var suppressed = new HashSet<string>(options.SuppressedFieldRows ?? Enumerable.Empty<string>(), StringComparer.Ordinal)
        {
            definition.SettingsFieldId(nameof(BlobFindParameters.OverlapRatio))
        };
        return options with { ParentConditions = conditions, SuppressedFieldRows = suppressed };
    }

}
