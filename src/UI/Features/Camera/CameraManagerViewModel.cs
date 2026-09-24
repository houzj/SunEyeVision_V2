using System;
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
                
                // 同步到 CameraDetailViewModel
                if (CameraDetailViewModel != null)
                {
                    CameraDetailViewModel.SelectedCamera = value;
                }
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
            _runtime = new CameraRuntimeComposition();
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
        }

        private void LoadCameraList()
        {
            foreach (var camera in Cameras)
                camera.PropertyChanged -= Camera_PropertyChanged;
            Cameras.Clear();
            UpdateStatistics();
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
            if (e.PropertyName is nameof(CameraDevice.State) or nameof(CameraDevice.IsEnabled))
                UpdateStatistics();
        }

        private void UpdateStatistics()
        {
            TotalCount = Cameras.Count;
            ConnectedCount = Cameras.Count(c => c.State is CameraState.Connected or CameraState.Capturing);
            DisabledCount = Cameras.Count(c => !c.IsEnabled);
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
                StatusMessage = oldName == camera.Name ? $"名称未修改：{camera.Name}" : $"已重命名为：{camera.Name}";
            }
            camera.IsRenaming = false;
        }

        private void DeleteCamera(CameraDevice? camera)
        {
            if (camera != null && Cameras.Contains(camera))
            {
                Cameras.Remove(camera);
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
            if (CameraDetailViewModel != null)
                await CameraDetailViewModel.DisposeAsync().ConfigureAwait(true);
            await _runtime.DisposeAsync().ConfigureAwait(true);
        }
    }
}
