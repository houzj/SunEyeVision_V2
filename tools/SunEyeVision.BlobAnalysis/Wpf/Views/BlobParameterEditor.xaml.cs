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
    private BlobSupportedWindowState? _supportedState;
    private NativeRoiUiDraftControls? _roiUi;
    private SunEyeVision.Plugin.Wpf.Presenters.Display.NativeDisplayUiDraftRows? _displayUi;
    private Action? _uiOnlyChanged;
    private bool _disposed;
    private bool HasUiOnlyChanges=>_roiUi?.HasUiChanges==true||_assistUi?.HasUiChanges==true||_displayUi?.HasUiChanges==true;
    private string? ValidateSupportedState()=>HasUiOnlyChanges?"尚未接入的界面设置已修改，请恢复原状态后再确认。":_supportedState?.Validate();
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
            var pose=BlobRoiUi.Add(inputs,supportedWindow:true,observe:value=>_roiUi=value);
            _supportedState=new BlobSupportedWindowState(draft,inputs){InformationHost=pose};
            _supportedState.Refresh();
            _assistUi=pose as NativeAssistUiDraftControls;
            const string position="field:c95f3a26edf55097863a7ec47d17cce5";
            options=options with {AdditionalAfterField=position,AdditionalAfterContent=pose,
                HeaderlessSectionAnchors=new HashSet<string>{position}};
            if(_assistUi?.GraphicVisibilitySource is {} source)
            {
                var gates=options.FieldVisibilitySources?.ToDictionary(item=>item.Key,item=>item.Value,StringComparer.Ordinal)
                    ??new Dictionary<string,FrameworkElement>(StringComparer.Ordinal);
                gates[position]=source;options=options with{FieldVisibilitySources=gates};
            }
        }
        Editor.Bind(draft,BlobDisplayUi.AddUnavailableRows(BlobRunUi.AddUnavailableRows(options,_supportedState is null?null:ValidateSupportedState),draft.Schema,supportedWindow:true,observe:value=>_displayUi=value));
        Editor.Changed+=RefreshSupportedState;
        if(_roiUi is not null)_roiUi.Changed+=OnUiOnlyChanged;
        if(_assistUi is not null)_assistUi.Changed+=OnUiOnlyChanged;
        if(_displayUi is not null)_displayUi.Changed+=OnUiOnlyChanged;
    }
    public FrameworkElement View=>this;
    public bool HasInvalidInput=>Editor.HasInvalidInput||HasUiOnlyChanges;
    public bool HasPendingEdits=>Editor.HasPendingEdits||HasUiOnlyChanges;
    public long AcceptedRevision=>Editor.AcceptedRevision;
    public IReadOnlyList<string> AcceptedFieldOrder=>Editor.AcceptedFieldOrder;
    public FieldSettings CaptureAcceptedFields()=>Editor.CaptureAcceptedFields();
    public event Action? Changed {add{Editor.Changed+=value;_uiOnlyChanged+=value;}remove{Editor.Changed-=value;_uiOnlyChanged-=value;}}
    public bool TryCapture(out FieldSettings fields,out string error)
    {
        if(_assistUi is {Visibility:Visibility.Visible,IsUnimplementedMode:true})
        {
            fields=null!;error="按点和按坐标的位置修正尚未接入，请切回按信息。";return false;
        }
        return Editor.TryCapture(out fields,out error);
    }
    private void RefreshSupportedState()=>_supportedState?.Refresh();
    private void OnUiOnlyChanged(){if(!_disposed)_uiOnlyChanged?.Invoke();}
    public void Dispose()
    {
        if(_disposed)return;_disposed=true;
        Editor.Changed-=RefreshSupportedState;
        if(_roiUi is not null)_roiUi.Changed-=OnUiOnlyChanged;
        if(_assistUi is not null)_assistUi.Changed-=OnUiOnlyChanged;
        if(_displayUi is not null)_displayUi.Changed-=OnUiOnlyChanged;_uiOnlyChanged=null;
        try{_supportedState?.Dispose();}finally{Editor.Dispose();}
    }
}
