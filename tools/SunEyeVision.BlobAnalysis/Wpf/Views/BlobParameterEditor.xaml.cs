using System.Windows;
using System.Windows.Controls;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using SunEyeVision.Plugin.Wpf.Presenters.Regions;

namespace SunEyeVision.Tool.BlobAnalysis.Wpf.Views;

/// <summary>Tool-specific composition entry over shared declared field controls.
/// The document owner decides when to capture, commit, cancel and dispose.</summary>
public partial class BlobParameterEditor : UserControl,IToolSettingsView
{
    private NativeAssistUiDraftControls? _assistUi;
    public BlobParameterEditor()
    {
        InitializeComponent();
    }
    public BlobParameterEditor(IToolSettingsDraft draft,ToolSettingsViewOptions options)
        :this()
    {
        // The saved XAML page is also used for Visual Studio preview. Runtime
        // editing always starts at the image/ROI input page.
        Editor.SetCurrentValue(DeclaredSettingsView.SelectedPageIndexProperty,0);
        if(options.InputContent is DeclaredInputFieldsView inputs)
        {
            var pose=BlobRoiUi.Add(inputs);
            _assistUi=pose as NativeAssistUiDraftControls;
            const string position="field:c95f3a26edf55097863a7ec47d17cce5";
            options=options with {AdditionalAfterField=position,AdditionalAfterContent=pose,AdditionalAfterCondition=new(position,true),
                HeaderlessSectionAnchors=new HashSet<string>{position}};
            if(_assistUi?.GraphicVisibilitySource is {} source)
            {
                var gates=options.FieldVisibilitySources?.ToDictionary(item=>item.Key,item=>item.Value,StringComparer.Ordinal)
                    ??new Dictionary<string,FrameworkElement>(StringComparer.Ordinal);
                gates[position]=source;options=options with{FieldVisibilitySources=gates};
            }
        }
        Editor.Bind(draft,BlobDisplayUi.AddUnavailableRows(BlobRunUi.AddUnavailableRows(options),draft.Schema));
    }
    public FrameworkElement View=>this;
    public bool HasInvalidInput=>Editor.HasInvalidInput;
    public bool HasPendingEdits=>Editor.HasPendingEdits;
    public long AcceptedRevision=>Editor.AcceptedRevision;
    public FieldSettings CaptureAcceptedFields()=>Editor.CaptureAcceptedFields();
    public event Action? Changed {add=>Editor.Changed+=value;remove=>Editor.Changed-=value;}
    public bool TryCapture(out FieldSettings fields,out string error)
    {
        if(_assistUi is {Visibility:Visibility.Visible,IsUnimplementedMode:true})
        {
            fields=null!;error="按点和按坐标的位置修正尚未接入，请切回按信息。";return false;
        }
        return Editor.TryCapture(out fields,out error);
    }
    public void Dispose()=>Editor.Dispose();
}
