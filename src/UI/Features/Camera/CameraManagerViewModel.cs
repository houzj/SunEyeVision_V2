using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using SunEyeVision.Workflow;
using SunEyeVision.DeviceDriver.Cameras;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Plugin.SDK.Devices.Camera;

using SunEyeVision.UI.Core;

namespace SunEyeVision.UI.Features.Camera
{
    /// <summary>
    /// 相机设备模型
    /// </summary>
    public class CameraDevice : SunEyeVision.Plugin.SDK.Models.ObservableObject
    {
        private string _name;
        private string _transport;
        private string _statusText = "断开";
        private string _statusIcon = "●";
        private string _ipAddress;
        private string _lastOperationTime;
        private bool _isSelected;
        private bool _isEnabled;
        private bool _autoConnect = true;
        private string _manufacturer;
        private string _model;
        private string _description;
        private string _latency;
        private string _frameRate;
        private string _deviceId;
        private string _serialNumber;
        private string _macAddress;
        private CameraState _state = CameraState.Offline;
        private bool _isRenaming;
        private string _renameText = string.Empty;

        public string DeviceId
        {
            get => _deviceId;
            set
            {
                SetProperty(ref _deviceId, value);
            }
        }

        public string SerialNumber { get => _serialNumber; set => SetProperty(ref _serialNumber, value); }
        public string MacAddress { get => _macAddress; set => SetProperty(ref _macAddress, value); }

        public CameraState State
        {
            get => _state;
            set
            {
                if (!SetProperty(ref _state, value))
                    return;
                StatusText = value switch
                {
                    CameraState.Connected or CameraState.Capturing => "已连接",
                    CameraState.Connecting => "连接中",
                    CameraState.Faulted => "故障",
                    CameraState.Disposed => "已释放",
                    _ => "断开"
                };
                StatusIcon = value switch
                {
                    _ => "●"
                };
                OnPropertyChanged(nameof(IsConnected));
                OnPropertyChanged(nameof(CanToggleConnection));
                OnPropertyChanged(nameof(ConnectionSummary));
            }
        }

        public bool IsConnected => State is CameraState.Connected or CameraState.Capturing;

        public bool CanToggleConnection => State != CameraState.Disposed;

        public string ConnectionSummary
        {
            get
            {
                var identity = string.Join(" · ", new[] { Manufacturer, Transport }
                    .Where(value => !string.IsNullOrWhiteSpace(value)));
                return string.IsNullOrWhiteSpace(identity)
                    ? StatusText
                    : $"{identity} · {StatusText}";
            }
        }

        public bool IsRenaming
        {
            get => _isRenaming;
            set => SetProperty(ref _isRenaming, value);
        }

        public string RenameText
        {
            get => _renameText;
            set => SetProperty(ref _renameText, value);
        }

        public string Name
        {
            get => _name;
            set
            {
                SetProperty(ref _name, value);
            }
        }

        public string Transport
        {
            get => _transport;
            set => SetProperty(ref _transport, value);
        }

        public string StatusText
        {
            get => _statusText;
            set
            {
                SetProperty(ref _statusText, value);
            }
        }

        public string StatusIcon
        {
            get => _statusIcon;
            set
            {
                SetProperty(ref _statusIcon, value);
            }
        }

        public string IpAddress
        {
            get => _ipAddress;
            set
            {
                SetProperty(ref _ipAddress, value);
            }
        }

        public string LastOperationTime
        {
            get => _lastOperationTime;
            set
            {
                SetProperty(ref _lastOperationTime, value);
            }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                SetProperty(ref _isSelected, value);
            }
        }

        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                SetProperty(ref _isEnabled, value);
            }
        }

        public bool AutoConnect
        {
            get => _autoConnect;
            set => SetProperty(ref _autoConnect, value);
        }

        public string Manufacturer
        {
            get => _manufacturer;
            set
            {
                SetProperty(ref _manufacturer, value);
            }
        }

        public string Model
        {
            get => _model;
            set
            {
                SetProperty(ref _model, value);
            }
        }

        public string Description
        {
            get => _description;
            set
            {
                SetProperty(ref _description, value);
            }
        }

        public string Latency
        {
            get => _latency;
            set
            {
                SetProperty(ref _latency, value);
            }
        }

        public string FrameRate
        {
            get => _frameRate;
            set
            {
                SetProperty(ref _frameRate, value);
            }
        }

    }

    /// <summary>
    /// 相机管理器 ViewModel
    /// </summary>
    public class CameraManagerViewModel : INotifyPropertyChanged, IAsyncDisposable
    {
        private CameraDevice _selectedCamera;
        private string _statusMessage;
        private int _connectedCount;
        private int _disabledCount;
        private int _totalCount;
        private CameraDetailViewModel _cameraDetailViewModel;
        private readonly CameraRuntimeComposition _runtime;

        public ObservableCollection<CameraDevice> Cameras { get; set; }

        public CameraDevice SelectedCamera
        {
            get => _selectedCamera;
            set
            {
                _selectedCamera = value;
                OnPropertyChanged(nameof(SelectedCamera));
            }
        }

        public CameraDetailViewModel CameraDetailViewModel
        {
            get => _cameraDetailViewModel;
            set
            {
                _cameraDetailViewModel = value;
                OnPropertyChanged(nameof(CameraDetailViewModel));
            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                _statusMessage = value;
                OnPropertyChanged(nameof(StatusMessage));
            }
        }

        public int ConnectedCount
        {
            get => _connectedCount;
            set
            {
                _connectedCount = value;
                OnPropertyChanged(nameof(ConnectedCount));
            }
        }

        public int DisabledCount
        {
            get => _disabledCount;
            set
            {
                _disabledCount = value;
                OnPropertyChanged(nameof(DisabledCount));
            }
        }

        public int TotalCount
        {
            get => _totalCount;
            set
            {
                _totalCount = value;
                OnPropertyChanged(nameof(TotalCount));
            }
        }

        // Commands
        public ICommand AddCameraCommand { get; }
        public ICommand DeleteCameraCommand { get; }
        public ICommand RenameCameraCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand ExportCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand GlobalSettingsCommand { get; }

        // 批量操作 Commands
        public ICommand BatchConnectCommand { get; }
        public ICommand BatchDisconnectCommand { get; }
        public ICommand BatchEnableCommand { get; }
        public ICommand BatchDisableCommand { get; }

        private SolutionManager _solutionManager;

        public CameraManagerViewModel(SolutionManager solutionManager)
        {
            _solutionManager = solutionManager;
            Cameras = new ObservableCollection<CameraDevice>();
            Cameras.CollectionChanged += Cameras_CollectionChanged;
            _runtime = SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters.ServiceInitializer.CameraRuntime;
            CameraDetailViewModel = new CameraDetailViewModel(_runtime);
            
            LoadCameraList();
            
            // 初始化 Commands
            AddCameraCommand = new RelayCommand(AddCamera);
            DeleteCameraCommand = new RelayCommand<CameraDevice>(DeleteCamera, CanDeleteCamera);
            RenameCameraCommand = new RelayCommand<CameraDevice>(BeginRename, camera => camera != null);
            RefreshCommand = new RelayCommand(Refresh);
            ExportCommand = new RelayCommand(Export);
            SaveCommand = new RelayCommand(Save);
            GlobalSettingsCommand = new RelayCommand(GlobalSettings);
            
            BatchConnectCommand = new RelayCommand(BatchConnect);
            BatchDisconnectCommand = new RelayCommand(BatchDisconnect);
            BatchEnableCommand = new RelayCommand(BatchEnable);
            BatchDisableCommand = new RelayCommand(BatchDisable);
            
            StatusMessage = "就绪";
            RefreshRuntimeConnectionStates();
        }

        /// <summary>
        /// 方案加载完成后恢复设备连接。相机管理器本身只负责展示和手动操作，
        /// 不再在窗口构造时触发发现或自动连接。
        /// </summary>
        public static async Task RestoreConnectionsForCurrentSolutionAsync(SolutionManager solutionManager)
        {
            var manager = new CameraManagerViewModel(solutionManager);
            try
            {
                await manager.RestoreAutoConnectionsAsync().ConfigureAwait(true);
            }
            finally
            {
                await manager.DisposeAsync().ConfigureAwait(true);
            }
        }

        private void LoadCameraList()
        {
            foreach (var camera in Cameras)
                camera.PropertyChanged -= Camera_PropertyChanged;
            Cameras.Clear();

            var savedDevices = _solutionManager.CurrentSolution?.Devices
                ?.Where(device => device.Type == DeviceType.Camera)
                .ToList() ?? new List<Device>();
            foreach (var device in savedDevices)
            {
                Cameras.Add(new CameraDevice
                {
                    Name = device.Name,
                    Manufacturer = device.Manufacturer,
                    Model = device.Model,
                    SerialNumber = device.SerialNumber,
                    IpAddress = device.IpAddress,
                    IsEnabled = device.IsEnabled,
                    AutoConnect = ReadSetting(device, "AutoConnect", true),
                    Description = device.Description,
                    DeviceId = ReadSetting(device, "DeviceId", device.Id),
                    MacAddress = ReadSetting(device, "MacAddress", string.Empty),
                    Transport = ReadSetting(device, "Transport", string.Empty),
                    State = CameraState.Offline
                });
            }
            UpdateStatistics();
        }

        public void PersistCameraList()
        {
            var solution = _solutionManager.CurrentSolution;
            if (solution == null)
            {
                StatusMessage = "当前没有打开的方案，设备配置暂不能保存";
                return;
            }

            solution.Devices.RemoveAll(device => device.Type == DeviceType.Camera);
            foreach (var camera in Cameras)
            {
                var device = new Device(camera.Name, DeviceType.Camera)
                {
                    Manufacturer = camera.Manufacturer,
                    Model = camera.Model,
                    SerialNumber = camera.SerialNumber,
                    IpAddress = camera.IpAddress,
                    IsEnabled = camera.IsEnabled,
                    Description = camera.Description,
                    Status = DeviceStatus.Disconnected,
                    ModifiedTime = DateTime.Now
                };
                device.Id = string.IsNullOrWhiteSpace(camera.DeviceId) ? device.Id : camera.DeviceId;
                device.Settings["DeviceId"] = camera.DeviceId ?? string.Empty;
                device.Settings["MacAddress"] = camera.MacAddress ?? string.Empty;
                device.Settings["Transport"] = camera.Transport ?? string.Empty;
                device.Settings["AutoConnect"] = camera.AutoConnect;
                solution.Devices.Add(device);
            }

            SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters.ServiceInitializer.CommunicationService.RefreshAutomationConfiguration(solution);
            if (string.IsNullOrWhiteSpace(solution.FilePath))
            {
                StatusMessage = "设备配置已更新，保存方案后将写入本地文件";
            }
            else if (_solutionManager.SaveSolution(solution.FilePath))
                StatusMessage = "设备配置已保存到当前方案";
        }

        private async Task RestoreAutoConnectionsAsync()
        {
            if (Cameras.Count == 0)
            {
                VisionLogger.Instance.Log(LogLevel.Info, "当前方案没有相机，跳过相机发现和自动连接", "CameraManagerViewModel");
                return;
            }

            IReadOnlyList<CameraDescriptor> discovered = Array.Empty<CameraDescriptor>();
            try
            {
                // 每次打开管理器都会创建新的运行时注册表；先重新发现设备，
                // 否则持久化的 DeviceId 虽然存在，但 Runtime.OpenAsync 会认为设备未注册。
                // 恢复时只扫描方案中实际保存的接口，避免默认模拟发现器把“模拟相机”
                // 混入真实相机列表。
                var transports = Cameras
                    .Select(camera => camera.Transport)
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(value => Enum.TryParse<CameraTransportKind>(value, true, out var transport)
                        ? (CameraTransportKind?)transport
                        : null)
                    .Where(transport => transport.HasValue)
                    .Select(transport => transport!.Value)
                    .ToHashSet();
                discovered = transports.Count == 0
                    ? await _runtime.Discovery.DiscoverAsync().ConfigureAwait(true)
                    : await _runtime.Discovery.DiscoverAsync(transports).ConfigureAwait(true);
                // DiscoveryCoordinator 只负责聚合发现结果，不会修改运行时注册表；
                // 重启后的自动连接必须显式注册本次发现到的设备描述。
                _runtime.Runtime.RegisterDevices(discovered);
                VisionLogger.Instance.Log(
                    LogLevel.Info,
                    $"启动恢复相机：持久化设备={Cameras.Count}，重新发现设备={discovered.Count}，" +
                    $"运行时注册={_runtime.Runtime.Devices.Count}，扫描接口={string.Join(',', transports)}",
                    "CameraManagerViewModel");
            }
            catch (Exception ex)
            {
                StatusMessage = $"设备重新发现失败：{ex.Message}";
                VisionLogger.Instance.Log(
                    LogLevel.Error,
                    $"启动恢复相机时重新发现失败: {ex}",
                    "CameraManagerViewModel",
                    ex);
            }

            var identityChanged = false;
            foreach (var camera in Cameras.ToList())
            {
                var descriptor = FindMatchingDescriptor(camera, discovered);
                if (descriptor is not null && !string.Equals(camera.DeviceId, descriptor.DeviceId, StringComparison.OrdinalIgnoreCase))
                {
                    var oldDeviceId = camera.DeviceId;
                    camera.DeviceId = descriptor.DeviceId;
                    camera.SerialNumber = descriptor.SerialNumber;
                    camera.MacAddress = descriptor.MacAddress ?? camera.MacAddress;
                    camera.IpAddress = descriptor.IpAddress ?? camera.IpAddress;
                    camera.Transport = descriptor.Transport.ToString();
                    identityChanged = true;
                    VisionLogger.Instance.Log(
                        LogLevel.Info,
                        $"启动恢复相机重新绑定: name={camera.Name}, oldDeviceId={oldDeviceId}, newDeviceId={camera.DeviceId}",
                        "CameraManagerViewModel");
                }

                var runtimeDeviceId = descriptor?.DeviceId ?? camera.DeviceId;
                if (_runtime.Runtime.TryGetSession(new CameraDeviceRef(runtimeDeviceId), out _))
                    camera.State = CameraState.Connected;
                else
                    camera.State = CameraState.Offline;
            }

            if (identityChanged)
                PersistCameraList();

            foreach (var camera in Cameras.Where(item => item.AutoConnect && item.IsEnabled).ToList())
            {
                try
                {
                    VisionLogger.Instance.Log(
                        LogLevel.Info,
                        $"启动自动连接开始: name={camera.Name}, deviceId={camera.DeviceId}, " +
                        $"discovered={discovered.Any(item => string.Equals(item.DeviceId, camera.DeviceId, StringComparison.OrdinalIgnoreCase))}",
                        "CameraManagerViewModel");
                    await SetCameraConnectionAsync(camera, shouldConnect: true).ConfigureAwait(true);
                    VisionLogger.Instance.Log(
                        camera.IsConnected ? LogLevel.Success : LogLevel.Error,
                        $"启动自动连接结束: name={camera.Name}, deviceId={camera.DeviceId}, state={camera.State}, status={camera.StatusText}",
                        "CameraManagerViewModel");
                }
                catch (Exception ex)
                {
                    StatusMessage = $"自动连接失败：{camera.Name}（{ex.Message}）";
                    VisionLogger.Instance.Log(
                        LogLevel.Error,
                        $"启动自动连接异常: name={camera.Name}, deviceId={camera.DeviceId}, state={camera.State}, error={ex}",
                        "CameraManagerViewModel",
                        ex);
                }
            }
        }

        private static CameraDescriptor? FindMatchingDescriptor(
            CameraDevice camera,
            IReadOnlyList<CameraDescriptor> descriptors)
        {
            static bool Same(string? left, string? right)
                => !string.IsNullOrWhiteSpace(left)
                   && !string.IsNullOrWhiteSpace(right)
                   && string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);

            return descriptors.FirstOrDefault(descriptor =>
                Same(camera.DeviceId, descriptor.DeviceId)
                || (Same(camera.SerialNumber, descriptor.SerialNumber)
                    && string.Equals(camera.Manufacturer, descriptor.Manufacturer, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(camera.Transport, descriptor.Transport.ToString(), StringComparison.OrdinalIgnoreCase))
                || (Same(camera.MacAddress, descriptor.MacAddress)
                    && string.Equals(camera.Transport, descriptor.Transport.ToString(), StringComparison.OrdinalIgnoreCase)));
        }

        private static T ReadSetting<T>(Device device, string key, T fallback)
        {
            if (!device.Settings.TryGetValue(key, out var value) || value == null)
                return fallback;
            if (value is T typed)
                return typed;
            if (value is System.Text.Json.JsonElement element)
            {
                try { return System.Text.Json.JsonSerializer.Deserialize<T>(element.GetRawText()) ?? fallback; }
                catch { return fallback; }
            }
            try { return (T)Convert.ChangeType(value, typeof(T)); }
            catch { return fallback; }
        }

        private void Cameras_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
                foreach (CameraDevice camera in e.OldItems)
                    camera.PropertyChanged -= Camera_PropertyChanged;
            if (e.NewItems != null)
                foreach (CameraDevice camera in e.NewItems)
                    camera.PropertyChanged += Camera_PropertyChanged;
            UpdateStatistics();
        }

        private void Camera_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(CameraDevice.State))
                UpdateStatistics();
            else if (e.PropertyName is nameof(CameraDevice.IsEnabled) or nameof(CameraDevice.AutoConnect))
            {
                UpdateStatistics();
                PersistCameraList();
            }
        }

        private void UpdateStatistics()
        {
            TotalCount = Cameras.Count;
            ConnectedCount = Cameras.Count(c => c.State is CameraState.Connected or CameraState.Capturing);
            DisabledCount = Cameras.Count(c => !c.IsEnabled);
        }

        private void RefreshRuntimeConnectionStates()
        {
            foreach (var camera in Cameras)
            {
                camera.State = _runtime.Runtime.TryGetSession(new CameraDeviceRef(camera.DeviceId), out _)
                    ? CameraState.Connected
                    : CameraState.Offline;
            }
            UpdateStatistics();
        }

        #region Commands Implementation

        private void AddCamera()
        {
            try
            {
                VisionLogger.Instance.Log(LogLevel.Info, "打开添加相机对话框", "CameraManagerViewModel");
                
                // 创建相机发现聚合服务
                var runtime = _runtime;
                
                // 打开添加相机对话框
                var addCameraDialog = new SunEyeVision.UI.Features.Camera.AddCameraDialog(runtime, this);
                var result = addCameraDialog.ShowDialog();
                
                if (result == true)
                {
                    UpdateStatistics();
                }
            }
            catch (Exception ex)
            {
                VisionLogger.Instance.Log(LogLevel.Error, $"打开添加相机对话框失败: {ex.Message}", "CameraManagerViewModel", ex);
                StatusMessage = "打开添加相机对话框失败";
            }
        }

        private void BeginRename(CameraDevice? camera)
        {
            if (camera is null)
                return;
            camera.RenameText = camera.Name;
            camera.IsRenaming = true;
            StatusMessage = $"正在重命名：{camera.Name}";
        }

        public void CommitRename(CameraDevice? camera, bool cancel = false)
        {
            if (camera is null)
                return;
            if (!cancel && !string.IsNullOrWhiteSpace(camera.RenameText))
            {
                var oldName = camera.Name;
                camera.Name = camera.RenameText.Trim();
                PersistCameraList();
                StatusMessage = oldName == camera.Name ? $"名称未修改：{camera.Name}" : $"已重命名为：{camera.Name}";
            }
            camera.IsRenaming = false;
        }

        private void DeleteCamera(CameraDevice? camera)
        {
            if (camera != null && Cameras.Contains(camera))
            {
                Cameras.Remove(camera);
                PersistCameraList();
                if (ReferenceEquals(SelectedCamera, camera))
                    SelectedCamera = Cameras.FirstOrDefault();
                StatusMessage = $"已删除相机: {camera.Name}";
                UpdateStatistics();
            }
        }

        private bool CanDeleteCamera(CameraDevice? camera)
        {
            return camera != null && Cameras.Contains(camera);
        }

        private void Refresh()
        {
            LoadCameraList();
            StatusMessage = "已刷新相机列表";
        }

        private void Export()
        {
            StatusMessage = "导出配置功能待实现";
        }

        private void Save()
        {
            StatusMessage = "保存配置功能待实现";
        }

        private void GlobalSettings()
        {
            StatusMessage = "全局设置功能待实现";
        }

        private void BatchConnect()
        {
            var selected = Cameras.Where(c => c.IsSelected).ToList();
            StatusMessage = selected.Count == 0
                ? "请先选择相机"
                : "批量连接暂未接入运行时，请在右侧详情面板连接相机";
            UpdateStatistics();
        }

        private void BatchDisconnect()
        {
            var selected = Cameras.Where(c => c.IsSelected).ToList();
            StatusMessage = selected.Count == 0
                ? "请先选择相机"
                : "批量断开暂未接入运行时，请在右侧详情面板断开相机";
            UpdateStatistics();
        }

        public async Task SetCameraConnectionAsync(CameraDevice camera, bool shouldConnect)
        {
            if (camera == null || !Cameras.Contains(camera) || CameraDetailViewModel == null)
                return;

            SelectedCamera = camera;
            if (camera.IsConnected == shouldConnect)
                return;

            await CameraDetailViewModel.SetConnectionAsync(camera, shouldConnect);
            StatusMessage = shouldConnect
                ? (camera.IsConnected ? $"已连接相机: {camera.Name}" : $"相机连接失败: {camera.Name}")
                : $"相机已断开: {camera.Name}";
            UpdateStatistics();
        }

        /// <summary>
        /// 从设备卡片打开现有预览窗口。设备列表选中动作本身不再触发参数读取，
        /// 只有用户明确点击预览时才把设备交给详情/预览会话。
        /// </summary>
        public void OpenPreview(CameraDevice camera)
        {
            if (camera == null || !Cameras.Contains(camera) || CameraDetailViewModel == null)
                return;

            SelectedCamera = camera;
            CameraDetailViewModel.SelectedCamera = camera;
            if (CameraDetailViewModel.PreviewCommand.CanExecute(null))
                CameraDetailViewModel.PreviewCommand.Execute(null);
        }

        private void BatchEnable()
        {
            var selected = Cameras.Where(c => c.IsSelected).ToList();
            foreach (var camera in selected)
            {
                camera.IsEnabled = true;
            }
            StatusMessage = $"已启用 {selected.Count} 台相机";
            UpdateStatistics();
        }

        private void BatchDisable()
        {
            var selected = Cameras.Where(c => c.IsSelected).ToList();
            foreach (var camera in selected)
            {
                camera.IsEnabled = false;
            }
            StatusMessage = $"已禁用 {selected.Count} 台相机";
            UpdateStatistics();
        }

        #endregion

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public async ValueTask DisposeAsync()
        {
            // 管理器是临时视图，关闭窗口不能释放方案级相机运行时和连接会话。
            // 运行时由 ServiceInitializer 持有，在应用退出或方案切换时统一释放。
            await Task.CompletedTask.ConfigureAwait(true);
        }
    }
}
