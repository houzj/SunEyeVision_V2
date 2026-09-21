using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;
using SunEyeVision.Workflow;
using SunEyeVision.DeviceDriver.Cameras;
using SunEyeVision.Plugin.SDK.Logging;

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
        private string _statusText;
        private string _statusIcon;
        private string _ipAddress;
        private string _lastOperationTime;
        private bool _isSelected;
        private bool _isEnabled;
        private string _manufacturer;
        private string _model;
        private string _description;
        private string _port;
        private string _username;
        private string _password;
        private string _latency;
        private string _frameRate;
        private string _deviceId;

        public string DeviceId
        {
            get => _deviceId;
            set
            {
                SetProperty(ref _deviceId, value);
            }
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

        public string Port
        {
            get => _port;
            set
            {
                SetProperty(ref _port, value);
            }
        }

        public string Username
        {
            get => _username;
            set
            {
                SetProperty(ref _username, value);
            }
        }

        public string Password
        {
            get => _password;
            set
            {
                SetProperty(ref _password, value);
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
    public class CameraManagerViewModel : INotifyPropertyChanged
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
            _runtime = new CameraRuntimeComposition();
            CameraDetailViewModel = new CameraDetailViewModel(_runtime);
            
            LoadCameraList();
            
            // 初始化 Commands
            AddCameraCommand = new RelayCommand(AddCamera);
            DeleteCameraCommand = new RelayCommand(DeleteCamera, CanDeleteCamera);
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
            Cameras.Clear();
            UpdateStatistics();
        }

        private void UpdateStatistics()
        {
            TotalCount = Cameras.Count;
            ConnectedCount = Cameras.Count(c => c.StatusIcon == "✅");
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

        private void DeleteCamera()
        {
            if (SelectedCamera != null)
            {
                Cameras.Remove(SelectedCamera);
                StatusMessage = $"已删除相机: {SelectedCamera.Name}";
                UpdateStatistics();
            }
        }

        private bool CanDeleteCamera()
        {
            return SelectedCamera != null;
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
            foreach (var camera in selected)
            {
                camera.StatusText = "已连接";
                camera.StatusIcon = "✅";
            }
            StatusMessage = $"已连接 {selected.Count} 台相机";
            UpdateStatistics();
        }

        private void BatchDisconnect()
        {
            var selected = Cameras.Where(c => c.IsSelected).ToList();
            foreach (var camera in selected)
            {
                camera.StatusText = "断开";
                camera.StatusIcon = "❌";
            }
            StatusMessage = $"已断开 {selected.Count} 台相机";
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
    }
}
