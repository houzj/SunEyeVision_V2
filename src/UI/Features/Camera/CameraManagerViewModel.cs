using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;
using SunEyeVision.Workflow;
using SunEyeVision.Core.Services.CameraDiscovery;
using SunEyeVision.Plugin.SDK.Logging;

namespace SunEyeVision.UI.ViewModels
{
    /// <summary>
    /// 相机设备模型
    /// </summary>
    public class CameraDevice : INotifyPropertyChanged
    {
        private string _name;
        private string _cameraType;
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

        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                OnPropertyChanged(nameof(Name));
            }
        }

        public string CameraType
        {
            get => _cameraType;
            set
            {
                _cameraType = value;
                OnPropertyChanged(nameof(CameraType));
            }
        }

        public string StatusText
        {
            get => _statusText;
            set
            {
                _statusText = value;
                OnPropertyChanged(nameof(StatusText));
            }
        }

        public string StatusIcon
        {
            get => _statusIcon;
            set
            {
                _statusIcon = value;
                OnPropertyChanged(nameof(StatusIcon));
            }
        }

        public string IpAddress
        {
            get => _ipAddress;
            set
            {
                _ipAddress = value;
                OnPropertyChanged(nameof(IpAddress));
            }
        }

        public string LastOperationTime
        {
            get => _lastOperationTime;
            set
            {
                _lastOperationTime = value;
                OnPropertyChanged(nameof(LastOperationTime));
            }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                OnPropertyChanged(nameof(IsSelected));
            }
        }

        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                _isEnabled = value;
                OnPropertyChanged(nameof(IsEnabled));
            }
        }

        public string Manufacturer
        {
            get => _manufacturer;
            set
            {
                _manufacturer = value;
                OnPropertyChanged(nameof(Manufacturer));
            }
        }

        public string Model
        {
            get => _model;
            set
            {
                _model = value;
                OnPropertyChanged(nameof(Model));
            }
        }

        public string Description
        {
            get => _description;
            set
            {
                _description = value;
                OnPropertyChanged(nameof(Description));
            }
        }

        public string Port
        {
            get => _port;
            set
            {
                _port = value;
                OnPropertyChanged(nameof(Port));
            }
        }

        public string Username
        {
            get => _username;
            set
            {
                _username = value;
                OnPropertyChanged(nameof(Username));
            }
        }

        public string Password
        {
            get => _password;
            set
            {
                _password = value;
                OnPropertyChanged(nameof(Password));
            }
        }

        public string Latency
        {
            get => _latency;
            set
            {
                _latency = value;
                OnPropertyChanged(nameof(Latency));
            }
        }

        public string FrameRate
        {
            get => _frameRate;
            set
            {
                _frameRate = value;
                OnPropertyChanged(nameof(FrameRate));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
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
            CameraDetailViewModel = new CameraDetailViewModel();
            
            // 初始化示例数据
            LoadSampleData();
            
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

        private void LoadSampleData()
        {
            Cameras.Clear();
            
            // 示例数据
            Cameras.Add(new CameraDevice
            {
                Name = "相机1",
                CameraType = "IP",
                StatusText = "已连接",
                StatusIcon = "✅",
                IpAddress = "192.168.1.100",
                LastOperationTime = "2024-04-08 10:30",
                IsSelected = false,
                IsEnabled = true,
                Manufacturer = "海康威视",
                Model = "DS-2CD2043D-IWD",
                Description = "室内监控相机",
                Port = "8000",
                Username = "admin",
                Password = "********",
                Latency = "23",
                FrameRate = "30"
            });
            
            Cameras.Add(new CameraDevice
            {
                Name = "相机2",
                CameraType = "USB",
                StatusText = "警告",
                StatusIcon = "⚠️",
                IpAddress = "-",
                LastOperationTime = "2024-04-07 15:20",
                IsSelected = false,
                IsEnabled = true,
                Manufacturer = "大华",
                Model = "DH-IPC-HFW1230S",
                Description = "USB 接口相机",
                Port = "-",
                Username = "-",
                Password = "-",
                Latency = "-",
                FrameRate = "-"
            });
            
            Cameras.Add(new CameraDevice
            {
                Name = "相机3",
                CameraType = "GigE",
                StatusText = "断开",
                StatusIcon = "❌",
                IpAddress = "192.168.1.101",
                LastOperationTime = "2024-04-08 09:15",
                IsSelected = false,
                IsEnabled = false,
                Manufacturer = "海康威视",
                Model = "DS-2CD3T45",
                Description = "工业级 GigE 相机",
                Port = "554",
                Username = "admin",
                Password = "********",
                Latency = "-",
                FrameRate = "-"
            });
            
            Cameras.Add(new CameraDevice
            {
                Name = "相机4",
                CameraType = "IP",
                StatusText = "已连接",
                StatusIcon = "✅",
                IpAddress = "192.168.1.102",
                LastOperationTime = "2024-04-08 11:00",
                IsSelected = false,
                IsEnabled = true,
                Manufacturer = "大华",
                Model = "DH-IPC-HDBW1431",
                Description = "室外监控相机",
                Port = "37777",
                Username = "admin",
                Password = "********",
                Latency = "45",
                FrameRate = "25"
            });
            
            Cameras.Add(new CameraDevice
            {
                Name = "相机5",
                CameraType = "USB",
                StatusText = "断开",
                StatusIcon = "❌",
                IpAddress = "-",
                LastOperationTime = "2024-04-08 08:45",
                IsSelected = false,
                IsEnabled = true,
                Manufacturer = "通用",
                Model = "Generic-Cam",
                Description = "通用 USB 相机",
                Port = "-",
                Username = "-",
                Password = "-",
                Latency = "-",
                FrameRate = "-"
            });
            
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
                var discoveryAggregator = new CameraDiscoveryAggregator(
                    new SunEyeVision.Core.Services.CameraDiscovery.GigeCameraDiscoveryService(),
                    new SunEyeVision.Core.Services.CameraDiscovery.UsbCameraDiscoveryService(),
                    new SunEyeVision.Core.Services.CameraDiscovery.IpCameraDiscoveryService()
                );
                
                // 打开添加相机对话框
                var addCameraDialog = new Views.Windows.AddCameraDialog(discoveryAggregator, this);
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
            LoadSampleData();
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
