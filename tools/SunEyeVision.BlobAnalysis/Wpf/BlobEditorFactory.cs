using System.Windows;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Tool.BlobAnalysis.Views;

namespace SunEyeVision.Tool.BlobAnalysis.Wpf;

public sealed class BlobEditorFactory : IToolEditorFactory
{
    public FrameworkElement CreateDebugControl(IToolPlugin tool) => new BlobAnalysisToolDebugControl("BlobAnalysis", tool, null);
    public FrameworkElement CreateDataAnalysisControl() => new BlobDataAnalysisControl();
}
