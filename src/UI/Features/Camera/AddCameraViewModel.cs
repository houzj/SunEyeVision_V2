using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using SunEyeVision.Core.Services.CameraDiscovery;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Plugin.SDK.Models;

namespace SunEyeVision.UI.ViewModels
{
    /// <summary>
    /// 添加相机 ViewModel
    /// </summary>
    public class AddCameraViewModel : ObservableObject
    {
        #region 属性
        
        /// <summary>
        /// 选中的相机类型（GigE/USB/IP）
        /// </summary>
        private CameraType _selectedType;
        public CameraType SelectedType
        {
            get => _selectedType;
            set => SetProperty(ref _selectedType, value, "相机类型");
        }
        
        /// <summary>
        /// 发现的相机列表
        /// </summary>
        private ObservableCollection<DiscoveredCamera> _discoveredCameras;
        public ObservableCollection<DiscoveredCamera> DiscoveredCameras
        {
            get => _discoveredCameras;
            set => SetProperty(ref _discoveredCameras, value);
        }
        
        /// <summary>
        /// 选中的相机
        /// </summary>
        private DiscoveredCamera? _selectedCamera;
        public DiscoveredCamera? SelectedCamera
        {
            get => _selectedCamera;
            set
            {
                if (SetProperty(ref _selectedCamera, value))
                {
                    // 当选中相机变化时，自动生成名称
                    if (value != null && string.IsNullOrEmpty(CameraName))
                    {
                        AutoGenerateCameraName();
                    }
                }
            }
        }
        
        /// <summary>
        /// 相机名称
        /// </summary>
        private string _cameraName;
        public string CameraName
        {
            get => _cameraName;
            set => SetProperty(ref _cameraName, value, "相机名称");
        }
        
        /// <summary>
        /// 用户名
        /// </summary>
        private string _username = "admin";
        public string Username
        {
            get => _username;
            set => SetProperty(ref _username, value, "用户名");
        }
        
        /// <summary>
        /// 密码
        /// </summary>
        private string _password;
        public string Password
        {
            get => _password;
            set => SetProperty(ref _password, value, "密码");
        }
        
        /// <summary>
        /// 端口
        /// </summary>
        private int _port = 554;
        public int Port
        {
            get => _port;
            set => SetProperty(ref _port, value, "端口");
        }
        
        /// <summary>
        /// 是否正在搜索
        /// </summary>
        private bool _isSearching;
        public bool IsSearching
        {
            get => _isSearching;
            set => SetProperty(ref _isSearching, value);
        }
        
        /// <summary>
        /// 是否使用厂商 SDK
        /// </summary>
        private bool _useVendorSdk;
        public bool UseVendorSdk
        {
            get => _useVendorSdk;
            set => SetProperty(ref _useVendorSdk, value);
        }
        
        /// <summary>
        /// 搜索状态文本
        /// </summary>
        private string _searchStatus;
        public string SearchStatus
        {
            get => _searchStatus;
            set => SetProperty(ref _searchStatus, value);
        }
        
        #endregion
        
        #region 命令
        
        public ICommand SearchCommand { get; }
        public ICommand StopCommand { get; }
        public ICommand ConfirmCommand { get; }
        public ICommand CancelCommand { get; }
        
        #endregion
        
        #region 字段
        
        private readonly CameraDiscoveryAggregator _discoveryAggregator;
        private readonly CameraManagerViewModel _cameraManager;
        private CancellationTokenSource? _cancellationTokenSource;
        
        #endregion
        
        public AddCameraViewModel(CameraDiscoveryAggregator discoveryAggregator, CameraManagerViewModel cameraManager)
        {
            _discoveryAggregator = discoveryAggregator;
            _cameraManager = cameraManager;
            
            // 初始化属性
            _selectedType = CameraType.GigE;
            _discoveredCameras = new ObservableCollection<DiscoveredCamera>();
            _searchStatus = "就绪";
            
            // 初始化命令
            SearchCommand = new RelayCommand(Search, CanSearch);
            StopCommand = new RelayCommand(Stop, CanStop);
            ConfirmCommand = new RelayCommand(Confirm, CanConfirm);
            CancelCommand = new RelayCommand(Cancel);
            
            VisionLogger.Instance.Log(LogLevel.Info, "添加相机 ViewModel 初始化完成", "AddCameraViewModel");
        }
        
        #region 搜索
        
        private bool CanSearch()
        {
            return !IsSearching;
        }
        
        private async void Search()
        {
            try
            {
                IsSearching = true;
                SearchStatus = "搜索中...";
                DiscoveredCameras.Clear();
                
                VisionLogger.Instance.Log(LogLevel.Info, $"开始搜索 {SelectedType} 相机...", "AddCameraViewModel");
                
                _cancellationTokenSource = new CancellationTokenSource();
                
                var cameras = await _discoveryAggregator.DiscoverAsync(SelectedType, _cancellationTokenSource.Token);
                
                foreach (var camera in cameras)
                {
                    DiscoveredCameras.Add(camera);
                }
                
                SearchStatus = $"搜索完成，发现 {cameras.Count} 台相机";
                VisionLogger.Instance.Log(LogLevel.Success, $"搜索完成，发现 {cameras.Count} 台相机", "AddCameraViewModel");
                
                // 自动选中第一台相机
                if (cameras.Count > 0)
                {
                    SelectedCamera = cameras[0];
                    AutoGenerateCameraName();
                }
            }
            catch (OperationCanceledException)
            {
                SearchStatus = "搜索已取消";
                VisionLogger.Instance.Log(LogLevel.Info, "搜索已取消", "AddCameraViewModel");
            }
            catch (Exception ex)
            {
                SearchStatus = "搜索失败";
                VisionLogger.Instance.Log(LogLevel.Error, $"搜索失败: {ex.Message}", "AddCameraViewModel", ex);
            }
            finally
            {
                IsSearching = false;
            }
        }
        
        private bool CanStop()
        {
            return IsSearching;
        }
        
        private void Stop()
        {
            _cancellationTokenSource?.Cancel();
            VisionLogger.Instance.Log(LogLevel.Info, "停止搜索", "AddCameraViewModel");
        }
        
        #endregion
        
        #region 自动生成名称
        
        private void AutoGenerateCameraName()
        {
            if (SelectedCamera != null)
            {
                // 自动生成名称: 制造商-型号-001
                var manufacturer = SelectedCamera.Manufacturer;
                var model = SelectedCamera.Model;
                var index = _cameraManager.Cameras.Count + 1;
                CameraName = $"{manufacturer}-{model}-{index:D3}";
                
                VisionLogger.Instance.Log(LogLevel.Info, $"自动生成相机名称: {CameraName}", "AddCameraViewModel");
            }
        }
        
        #endregion
        
        #region 确认添加
        
        private bool CanConfirm()
        {
            return SelectedCamera != null && !string.IsNullOrEmpty(CameraName);
        }
        
        private void Confirm()
        {
            try
            {
                if (SelectedCamera == null)
                {
                    VisionLogger.Instance.Log(LogLevel.Warning, "请先选择相机", "AddCameraViewModel");
                    return;
                }
                
                // 创建新的相机设备
                var newCamera = new CameraDevice
                {
                    Name = CameraName,
                    CameraType = SelectedCamera.CameraType.ToString(),
                    Manufacturer = SelectedCamera.Manufacturer,
                    Model = SelectedCamera.Model,
                    IpAddress = SelectedCamera.IpAddress,
                    Port = SelectedCamera.Port == 0 ? Port.ToString() : SelectedCamera.Port.ToString(),
                    Username = Username,
                    Password = Password,
                    StatusText = "断开",
                    StatusIcon = "❌",
                    IsSelected = false,
                    IsEnabled = true,
                    LastOperationTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm")
                };
                
                // 添加到相机管理器
                _cameraManager.Cameras.Add(newCamera);
                
                VisionLogger.Instance.Log(LogLevel.Success, $"相机添加成功: {CameraName}", "AddCameraViewModel");
                _cameraManager.StatusMessage = $"相机添加成功: {CameraName}";
                
                // 关闭窗口
                RequestClose?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                VisionLogger.Instance.Log(LogLevel.Error, $"相机添加失败: {ex.Message}", "AddCameraViewModel", ex);
            }
        }
        
        #endregion
        
        #region 取消
        
        private void Cancel()
        {
            VisionLogger.Instance.Log(LogLevel.Info, "取消添加相机", "AddCameraViewModel");
            
            // 停止搜索
            Stop();
            
            // 关闭窗口
            RequestClose?.Invoke(this, EventArgs.Empty);
        }
        
        #endregion
        
        #region 事件
        
        public event EventHandler? RequestClose;
        
        #endregion
    }
}
