using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using SunEyeVision.Application;
using SunEyeVision.Application.Editing;
using SunEyeVision.Domain.Solutions;
using SunEyeVision.Domain.Workflows;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Controls.Native.Composition.ImageSources;
using SunEyeVision.Plugin.Wpf.Presenters.Input.ImageSources;
using SunEyeVision.UI.Services.Images;

namespace SunEyeVision.UI.Services.Editing;

/// <summary>Image source settings hosted by one actual session, node and private editor draft.</summary>
public sealed class ImageSourceDraftContent : UserControl, IImageSourceSettingsContent, IDisposable
{
 private readonly DocumentSession _session;
 private readonly WorkflowDefinition _root;
 private readonly NodeDefinition _node;
 private readonly Func<NodeConfigurationEditDraft?> _nodeDraft;
 private readonly Func<bool> _isCurrent;
 private readonly Func<string,string?>? _cameraStatus;
 private readonly Action<string?>? _openCamera;
 private readonly ImageSourceThemeScope _theme;
 private readonly ImageSourceInputView _inputView;
 private readonly ComboBox _cameraPicker;
 private readonly TextBlock _status;
 private readonly DispatcherTimer _statusTimer;
 private IToolSettingsDraft? _draft;
 private NodeConfigurationEditDraft? _boundNodeDraft;
 private Action<FieldSettings>? _accept;
 private Action? _prepare;
 private bool _preparing, _disposed, _refreshing;
 public event Action? Changed;

 public ImageSourceDraftContent(DocumentSession session, WorkflowDefinition root, NodeDefinition node,
  Func<NodeConfigurationEditDraft?> currentNodeDraft, Func<bool> isCurrent,
  Func<string,string?>? cameraStatus = null, Action<string?>? openCamera = null)
 {
  _session = session ?? throw new ArgumentNullException(nameof(session));
  _root = root ?? throw new ArgumentNullException(nameof(root));
  _node = node ?? throw new ArgumentNullException(nameof(node));
  _nodeDraft = currentNodeDraft ?? throw new ArgumentNullException(nameof(currentNodeDraft));
  _isCurrent = isCurrent ?? throw new ArgumentNullException(nameof(isCurrent));
  _cameraStatus = cameraStatus; _openCamera = openCamera;
  _inputView = new(); _cameraPicker = _inputView.CameraPicker; _status = _inputView.CameraStatus;
  _theme = new(_cameraPicker); _cameraPicker.SelectionChanged += CameraSelected;
  _inputView.OpenCameraButton.IsEnabled = openCamera is not null;
  if (openCamera is null) _inputView.OpenCameraButton.ToolTip = "相机管理尚未接入当前方案会话。";
  _inputView.OpenCameraButton.Click += OpenCameraManager;
  Content = _inputView;
  _statusTimer = new(TimeSpan.FromSeconds(1), DispatcherPriority.Background, (_, _) => RefreshCameraStatus(), Dispatcher);
  _statusTimer.Stop();
  Loaded += OnLoaded; Unloaded += OnUnloaded;
 }
 private void OnLoaded(object sender, RoutedEventArgs args)
 { if (!_disposed) { RefreshCameraStatus(); _statusTimer.Start(); } }
 private void OnUnloaded(object sender, RoutedEventArgs args) => _statusTimer.Stop();
 public void BindSourceDraft(IToolSettingsDraft draft, Action prepareEdit, Action<FieldSettings> acceptCandidate)
 {
  ObjectDisposedException.ThrowIf(_disposed, this);
  ArgumentNullException.ThrowIfNull(draft); ArgumentNullException.ThrowIfNull(prepareEdit);
  ArgumentNullException.ThrowIfNull(acceptCandidate);
  var candidate = _nodeDraft(); EnsureOwner(candidate);
  _draft = draft; _prepare = prepareEdit; _accept = acceptCandidate; _boundNodeDraft = candidate;
  EnsureCurrent(); RefreshSourceDraft();
 }
 /// <summary>Adopt a rebased node draft without rebuilding sibling controls or discarding raw text.</summary>
 internal void RebaseDraft(NodeConfigurationEditDraft candidate)
 {
  ArgumentNullException.ThrowIfNull(candidate);
  if (_draft is null || !ReferenceEquals(_nodeDraft(), candidate))
   throw new InvalidOperationException("图像源参数页未绑定当前重基草稿。");
  EnsureOwner(candidate);
  static byte[] Canonical(IReadOnlyDictionary<string, ParameterSetting> fields)
  {
   using var stream = new System.IO.MemoryStream();
   using (var writer = new System.Text.Json.Utf8JsonWriter(stream)) ParameterSettingCanonical.Write(writer, fields);
   return stream.ToArray();
  }
  var displayed = _draft.CaptureFields();
  var rebased = candidate.Configuration.Fields.Select(displayed.Declarations.Values);
  if (!Canonical(displayed.Capture()).SequenceEqual(Canonical(rebased.Capture())))
   throw new InvalidOperationException("图像源参数页与重基草稿的已接纳字段不一致，请先解决编辑冲突。");
  _boundNodeDraft = candidate;
 }
 private InputValues ReadValues()
 {
  EnsureCurrent();
  return _draft!.CaptureFields().Materialize(new Dictionary<string, object?>());
 }
 public void RefreshSourceDraft()
 {
  ObjectDisposedException.ThrowIf(_disposed, this);
  if (_draft is null || _preparing) return;
  var values = ReadValues();
  var deviceId = values.Get<string>("camera.device");
  _inputView.IsCamera = values.Get<string>("source.kind") == "camera";
  _refreshing = true;
  try
  {
   _cameraPicker.Items.Clear();
   _cameraPicker.Items.Add(new ComboBoxItem { Content = "请选择方案中的相机", Tag = string.Empty });
   foreach (var device in _session.Document.Devices.Where(device => device.Type == DeviceType.Camera))
    _cameraPicker.Items.Add(new ComboBoxItem { Content = device.Name, Tag = device.Id });
   var selected = _cameraPicker.Items.OfType<ComboBoxItem>().FirstOrDefault(item =>
    string.Equals(item.Tag as string, deviceId, StringComparison.OrdinalIgnoreCase));
   if (selected is null && !string.IsNullOrWhiteSpace(deviceId))
   { selected = new ComboBoxItem { Content = "设备已移除：" + deviceId, Tag = deviceId }; _cameraPicker.Items.Add(selected); }
   _cameraPicker.SelectedItem = selected ?? _cameraPicker.Items[0];
  }
  finally { _refreshing = false; }
  RefreshCameraStatus();
 }
 private void CameraSelected(object sender, SelectionChangedEventArgs args)
 {
  if (_refreshing || _disposed || _draft is null) return;
  try
  {
   EnsureCurrent(); PrepareEdit(); var draft = _draft;
   draft.ApplyFields(new Dictionary<string, ParameterSetting> {
    ["camera.device"] = new ConstantSetting((_cameraPicker.SelectedItem as ComboBoxItem)?.Tag as string ?? string.Empty)
   }, candidate => { EnsureCurrent(); _accept!(candidate); EnsureCurrent(); });
   Changed?.Invoke(); RefreshCameraStatus();
  }
  catch (Exception error)
  {
   try { RefreshSourceDraft(); } catch (InvalidOperationException) { }
   _status.Text = error.Message;
  }
 }
 private void RefreshCameraStatus()
 {
  if (_disposed || _draft is null) return;
  try
  {
   var id = ReadValues().Get<string>("camera.device");
   _status.Text = string.IsNullOrWhiteSpace(id) ? "设备状态：未选择相机" :
    _cameraStatus is null ? "设备状态：当前会话尚未接入" : "设备状态：" + (_cameraStatus(id) ?? "未知");
  }
  catch (Exception error) { _status.Text = "设备状态不可用：" + error.Message; }
 }
 private void OpenCameraManager(object sender, RoutedEventArgs args)
 {
  EnsureCurrent();
  (_openCamera ?? throw new InvalidOperationException("相机管理尚未接入当前方案会话。"))(ReadValues().Get<string>("camera.device"));
  if (!_disposed && _isCurrent()) RefreshSourceDraft();
 }
 private void PrepareEdit()
 {
  if (_preparing) throw new InvalidOperationException("图像源编辑准备不能重入。");
  _preparing = true; try { _prepare!(); EnsureCurrent(); } finally { _preparing = false; }
 }
 private void EnsureCurrent()
 {
  if (_disposed || !_isCurrent() || _draft is null || _accept is null)
   throw new InvalidOperationException("图像源参数页已失效，请使用当前编辑会话。");
  var candidate = _nodeDraft();
  if (!ReferenceEquals(candidate, _boundNodeDraft)) throw new InvalidOperationException("图像源编辑草稿已重建，请重新绑定。");
  EnsureOwner(candidate);
 }
 private void EnsureOwner(NodeConfigurationEditDraft? candidate)
 {
  if (_disposed || !_isCurrent() || candidate is null || candidate.EditSessionId != _session.EditSessionId || candidate.WorkflowId != _root.Id ||
      !ReferenceEquals(candidate.Node, _node) || _root.IsSubroutine ||
      !_session.GetWorkflows().Any(flow => ReferenceEquals(flow, _root)) ||
      !_session.GetNodes(_root.Id).Any(node => ReferenceEquals(node, _node)))
   throw new InvalidOperationException("图像源所属方案、流程、节点或草稿已变化。");
 }
 public void Dispose()
 {
  if (_disposed) return; _disposed = true;
  _statusTimer.Stop(); Loaded -= OnLoaded; Unloaded -= OnUnloaded;
  _cameraPicker.SelectionChanged -= CameraSelected; _inputView.OpenCameraButton.Click -= OpenCameraManager;
  _theme.Dispose(); _draft = null; _boundNodeDraft = null; _prepare = null; _accept = null; Changed = null; Content = null;
 }
}
