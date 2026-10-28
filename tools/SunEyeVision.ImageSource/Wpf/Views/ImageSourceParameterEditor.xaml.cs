using System.Windows;
using System.Windows.Controls;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;

namespace SunEyeVision.Tool.ImageSource.Wpf.Views;

/// <summary>One actual parameter composition for designer and runtime; no parallel source model.</summary>
public partial class ImageSourceParameterEditor : UserControl, IToolSettingsView
{
    // DeclaredSettingsView.InputSection currently addresses a page identity, not a category caption.
    public const string InputPage = "基本参数";

    public ImageSourceParameterEditor() => InitializeComponent();

    public ImageSourceParameterEditor(IToolSettingsDraft draft, ToolSettingsViewOptions options) : this()
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(options);
        // Attachment and IDisposable ownership are entirely handled by the shared presenter.
        // If Bind fails before attachment, the caller still owns the unaccepted InputContent.
        Editor.Bind(draft, options with { InputSection = InputPage });
    }

    public FrameworkElement View => this;
    public bool HasInvalidInput => Editor.HasInvalidInput;
    public event Action? Changed { add => Editor.Changed += value; remove => Editor.Changed -= value; }
    public bool TryCapture(out object settings, out IReadOnlyDictionary<string, ReferenceSetting> references, out string error)
        => Editor.TryCapture(out settings, out references, out error);
    public void Dispose() => Editor.Dispose();
}
