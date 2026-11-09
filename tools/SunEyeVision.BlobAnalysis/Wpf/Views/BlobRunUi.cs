using System.IO;
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
    internal static ToolSettingsViewOptions AddRows(
        ToolSettingsViewOptions options,
        IToolSettingsDraft draft,
        DeclaredSettingsView settingsView,
        Func<string?> validateSupportedState,
        Action<BlobRunUiState>? observe = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(settingsView);
        ArgumentNullException.ThrowIfNull(validateSupportedState);

        // These fields are deliberately resolved from the formal VM metadata.
        // The guard makes a missing/partial resource fail at the current contract
        // boundary instead of silently falling back to a second visual tree.
        if (!BlobParameterDefinition.Instance.Contains("HoleMinArea"))
            throw new InvalidDataException("Blob VM 运行参数元数据不完整：缺少 HoleMinArea。");

        var supportedSort = new HashSet<string>(
            new[]
            {
                "SortFeatureArea",
                "SortFeaturePerimeter",
                "SortFeatureCircularity",
                "SortFeatureRect",
                "SortFeatureCentroidX",
                "SortFeatureCentroidY",
                "SortFeatureRectX",
                "SortFeatureRectY"
            },
            StringComparer.Ordinal);

        var formalState = new BlobRunUiState(draft, settingsView, supportedSort);
        observe?.Invoke(formalState);

        return options with
        {
            ValidateUiState = () =>
            {
                var previous = options.ValidateUiState?.Invoke();
                return string.IsNullOrEmpty(previous) ? validateSupportedState() : previous;
            }
        };
    }
}
