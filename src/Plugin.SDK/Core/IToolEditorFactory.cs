namespace SunEyeVision.Plugin.SDK.Core;

/// <summary>
/// 工具的 WPF 编辑器工厂。算法运行层只依赖 <see cref="IToolPlugin"/>。
/// </summary>
public interface IToolEditorFactory
{
    bool HasDebugWindow => true;

    System.Windows.Window? CreateDebugWindow() => null;

    System.Windows.FrameworkElement? CreateDebugControl(IToolPlugin tool) => null;

    System.Windows.FrameworkElement? CreateDataAnalysisControl() => null;
}
