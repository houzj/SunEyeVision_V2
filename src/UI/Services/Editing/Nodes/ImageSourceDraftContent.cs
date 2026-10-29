using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using SunEyeVision.Plugin.Contracts;
using ConfigurationSetting=SunEyeVision.Plugin.Contracts.ParameterSetting;
using SunEyeVision.Plugin.Wpf.Controls.Native.Composition.ImageSources;
using SunEyeVision.Plugin.Wpf.Presenters.Input.ImageSources;
using SunEyeVision.UI.Services.Images;
using SunEyeVision.UI.Shared;
using SunEyeVision.Workflow;

namespace SunEyeVision.UI.Services.Editing;

/// <summary>Document adapter for the original source strip inside the sole tool settings draft.</summary>
public sealed class ImageSourceDraftContent : UserControl,IImageSourceSettingsContent,IDisposable
{
    private readonly string _nodeId;
    private readonly Func<bool> _isCurrent;
    private readonly SunEyeVision.Plugin.SDK.Execution.Parameters.IResourcePathResolver _paths;
    private readonly ImageInputSource _source;
    private readonly ImageSourceStrip _strip;
    private readonly ImageSourceStripSession _session;
    private readonly ImageSourceThemeScope _themeScope;
    private readonly ImageSourceThemeScope _cameraTheme;
    private IToolSettingsDraft? _draft;
    private Action<FieldSettings>? _accept;
    private Action? _prepare;
    private bool _preparing;
    private bool _disposed;
    private bool _refreshing;
    private readonly ImageSourceInputView _inputView;
    private readonly ComboBox _cameraPicker;
    private readonly TextBlock _cameraStatus;
    private readonly System.Windows.Threading.DispatcherTimer _statusTimer;
    public event Action? Changed;

    public ImageSourceDraftContent(string nodeId,Func<bool> isCurrent,
        SunEyeVision.Plugin.SDK.Execution.Parameters.IResourcePathResolver paths)
    {
        _nodeId=nodeId;_isCurrent=isCurrent;_paths=paths;
        _source=new(nodeId);
        _source.ConfigurationWriter=proposal=>
        {
            EnsureCurrent();
            PrepareEdit();
            var draft=_draft!;
            var policy=ImageSourcePolicy.ReadFields(_nodeId,draft.CaptureFields().Materialize(new System.Collections.Generic.Dictionary<string,object?>()));
            policy.UpdateLocalInput(proposal.Configuration);
            draft.ApplyFields(policy.ToFieldValues(draft.Schema).ToDictionary(pair=>pair.Key,pair=>(ConfigurationSetting)new ConstantSetting(pair.Value),StringComparer.Ordinal),candidate=>
            {
                EnsureCurrent();_accept!(candidate);EnsureCurrent();
            });
        };
        _source.ConfigurationEdited+=SourceEdited;
        _session=new(_source,()=>!_disposed&&_isCurrent(),dispatcher:Dispatcher,dialogOwner:()=>Window.GetWindow(this));
        _inputView=new();_strip=_inputView.SourceStrip;_strip.Session=_session;
        _cameraPicker=_inputView.CameraPicker;_cameraStatus=_inputView.CameraStatus;
        _themeScope=new(_strip);
        _cameraTheme=new(_cameraPicker);
        _cameraPicker.SelectionChanged+=CameraSelected;
        _inputView.OpenCameraButton.Click+=OpenCameraManager;Content=_inputView;
        _statusTimer=new(TimeSpan.FromSeconds(1),System.Windows.Threading.DispatcherPriority.Background,
            (_,_)=>RefreshCameraStatus(),Dispatcher);
        _statusTimer.Stop();
        Loaded+=(_,_)=>{if(!_disposed){RefreshCameraStatus();_statusTimer.Start();}};
        Unloaded+=(_,_)=>_statusTimer.Stop();
    }
    public void BindSourceDraft(IToolSettingsDraft draft,Action prepareEdit,Action<FieldSettings> acceptCandidate)
    {
        ObjectDisposedException.ThrowIf(_disposed,this);
        _draft=draft;_prepare=prepareEdit;_accept=acceptCandidate;RefreshSourceDraft();
    }
    public void RefreshSourceDraft()
    {
        ObjectDisposedException.ThrowIf(_disposed,this);
        if(_draft is null||_preparing)return;
        var policy=ImageSourcePolicy.ReadFields(_nodeId,_draft.CaptureFields().Materialize(new System.Collections.Generic.Dictionary<string,object?>()));
        _source.LoadFromPolicy(policy,_paths);
        _inputView.IsCamera=policy.SourceKind==SunEyeVision.Plugin.SDK.Vision.Contracts.ImageSourceKind.Camera;
        _refreshing=true;
        try
        {
            _cameraPicker.Items.Clear();
            _cameraPicker.Items.Add(new ComboBoxItem{Content="请选择方案中的相机",Tag=string.Empty});
            var devices=SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters.ServiceInitializer.SolutionManager.CurrentSolution?.Devices;
            foreach(var device in devices?.Where(device=>device.Type==DeviceType.Camera)??Enumerable.Empty<Device>())
                _cameraPicker.Items.Add(new ComboBoxItem{Content=device.Name,Tag=device.Id});
            var selected=_cameraPicker.Items.OfType<ComboBoxItem>().FirstOrDefault(item=>
                string.Equals(item.Tag as string,policy.CameraDeviceId,StringComparison.OrdinalIgnoreCase));
            if(selected is null&&!string.IsNullOrWhiteSpace(policy.CameraDeviceId))
            {_cameraPicker.Items.Add(selected=new ComboBoxItem{Content="设备已移除："+policy.CameraDeviceId,Tag=policy.CameraDeviceId});}
            _cameraPicker.SelectedItem=selected??_cameraPicker.Items[0];
        }
        finally{_refreshing=false;}
        RefreshCameraStatus();
    }
    private void CameraSelected(object sender,SelectionChangedEventArgs args)
    {
        if(_refreshing||_disposed||_draft is null)return;
        try
        {
            EnsureCurrent();PrepareEdit();var draft=_draft;
            draft.ApplyFields(new System.Collections.Generic.Dictionary<string,ConfigurationSetting>{
                ["camera.device"]=new ConstantSetting((_cameraPicker.SelectedItem as ComboBoxItem)?.Tag as string??string.Empty)},
                candidate=>{EnsureCurrent();_accept!(candidate);EnsureCurrent();});
            Changed?.Invoke();RefreshCameraStatus();
        }
        catch(Exception error){RefreshSourceDraft();_cameraStatus.Text=error.Message;}
    }
    private void RefreshCameraStatus()
    {
        if(_disposed||_draft is null)return;
        var id=ImageSourcePolicy.ReadFields(_nodeId,_draft.CaptureFields().Materialize(new System.Collections.Generic.Dictionary<string,object?>())).CameraDeviceId;
        if(string.IsNullOrWhiteSpace(id)){_cameraStatus.Text="设备状态：未选择相机";return;}
        if(!SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters.ServiceInitializer.CameraRuntime.Runtime.TryGetSession(
            new SunEyeVision.Plugin.SDK.Devices.Camera.CameraDeviceRef(id),out var session))
        {_cameraStatus.Text="设备状态：未连接";return;}
        _cameraStatus.Text="设备状态："+(session.Adapter.State switch
        {
            SunEyeVision.Plugin.SDK.Devices.Camera.CameraState.Offline=>"离线",
            SunEyeVision.Plugin.SDK.Devices.Camera.CameraState.Connecting=>"连接中",
            SunEyeVision.Plugin.SDK.Devices.Camera.CameraState.Connected=>"已连接",
            SunEyeVision.Plugin.SDK.Devices.Camera.CameraState.StartingCapture=>"正在启动采集",
            SunEyeVision.Plugin.SDK.Devices.Camera.CameraState.Capturing=>"采集中",
            SunEyeVision.Plugin.SDK.Devices.Camera.CameraState.StoppingCapture=>"正在停止采集",
            SunEyeVision.Plugin.SDK.Devices.Camera.CameraState.Reconnecting=>"重连中",
            SunEyeVision.Plugin.SDK.Devices.Camera.CameraState.Faulted=>"故障",
            SunEyeVision.Plugin.SDK.Devices.Camera.CameraState.Disposed=>"已释放",
            _=>"未知"
        });
    }
    private void OpenCameraManager(object sender,RoutedEventArgs args)
    {
        EnsureCurrent();
        var manager=new SunEyeVision.UI.Features.Camera.CameraManagerViewModel(
            SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters.ServiceInitializer.SolutionManager);
        var id=ImageSourcePolicy.ReadFields(_nodeId,_draft!.CaptureFields().Materialize(new System.Collections.Generic.Dictionary<string,object?>())).CameraDeviceId;
        manager.SelectedCamera=manager.Cameras.FirstOrDefault(camera=>camera.DeviceId==id);
        var dialog=new SunEyeVision.UI.Features.Camera.CameraManagerDialog(manager){Owner=Window.GetWindow(this)};
        dialog.ShowDialog();
        if(!_disposed&&_isCurrent())RefreshSourceDraft();
    }
    private void SourceEdited(object? sender,EventArgs args){EnsureCurrent();Changed?.Invoke();}
    private void PrepareEdit()
    {
        if(_preparing)throw new InvalidOperationException("图像源编辑准备不能重入。");
        _preparing=true;
        try{_prepare!();EnsureCurrent();}
        finally{_preparing=false;}
    }
    private void EnsureCurrent()
    {
        if(_disposed||!_isCurrent()||_draft is null||_accept is null)
            throw new InvalidOperationException("图像源参数页已失效，请使用当前编辑会话。");
    }
    public void Dispose()
    {
        if(_disposed)return;_disposed=true;
        _statusTimer.Stop();_cameraPicker.SelectionChanged-=CameraSelected;
        _inputView.OpenCameraButton.Click-=OpenCameraManager;
        _source.ConfigurationEdited-=SourceEdited;
        _source.ConfigurationWriter=null;_strip.Session=null;_session.Dispose();
        _themeScope.Dispose();
        _cameraTheme.Dispose();
        _draft=null;_prepare=null;_accept=null;Changed=null;Content=null;
    }
}
