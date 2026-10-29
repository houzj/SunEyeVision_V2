using System.Windows;
using System.Windows.Controls;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;

namespace SunEyeVision.Tool.BlobAnalysis.Wpf.Views;

/// <summary>Tool-specific composition entry over shared declared field controls.
/// The document owner decides when to capture, commit, cancel and dispose.</summary>
public partial class BlobParameterEditor : UserControl,IToolSettingsView
{
    public BlobParameterEditor()
    {
        InitializeComponent();
    }
    public BlobParameterEditor(IToolSettingsDraft draft,ToolSettingsViewOptions options)
        :this()=>Editor.Bind(draft,options);
    public FrameworkElement View=>this;
    public bool HasInvalidInput=>Editor.HasInvalidInput;
    public event Action? Changed {add=>Editor.Changed+=value;remove=>Editor.Changed-=value;}
    public bool TryCapture(out FieldSettings fields,out string error)
        =>Editor.TryCapture(out fields,out error);
    public void Dispose()=>Editor.Dispose();
}
