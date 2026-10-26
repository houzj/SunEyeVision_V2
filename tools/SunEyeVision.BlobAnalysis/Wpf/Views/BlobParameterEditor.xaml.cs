using System.Windows;
using System.Windows.Controls;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;

namespace SunEyeVision.Tool.BlobAnalysis.Wpf.Views;

/// <summary>Tool-specific composition entry over shared declared field controls.
/// The document owner decides when to capture, commit, cancel and dispose.</summary>
public partial class BlobParameterEditor : UserControl,IToolSettingsView
{
    private readonly DeclaredSettingsView _editor;
    public BlobParameterEditor(IToolSettingsDraft draft,ToolSettingsViewOptions options)
    {InitializeComponent();_editor=new(draft,options);EditorHost.Content=_editor;}
    public FrameworkElement View=>this;
    public bool HasInvalidInput=>_editor.HasInvalidInput;
    public event Action? Changed {add=>_editor.Changed+=value;remove=>_editor.Changed-=value;}
    public bool TryCapture(out object settings,out IReadOnlyDictionary<string,ReferenceSetting> references,out string error)
        =>_editor.TryCapture(out settings,out references,out error);
    public void Dispose(){_editor.Dispose();EditorHost.Content=null;}
}
