using System.Windows;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Tool.BlobAnalysis.Views;

namespace SunEyeVision.Tool.BlobAnalysis.Wpf;

public sealed class BlobEditorFactory : IToolEditorFactory
{
    // Configuration is provided completely by the host's declaration/ROI editor.
    // This optional companion currently supplies the specialized read-only result view only.
    public FrameworkElement CreateDataAnalysisControl() => new BlobDataAnalysisControl();
}
