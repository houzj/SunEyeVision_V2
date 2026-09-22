using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using SunEyeVision.DeviceDriver.Cameras;
using SunEyeVision.Plugin.SDK.Devices.Camera;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Plugin.SDK.Models;

using SunEyeVision.UI.Core;

namespace SunEyeVision.UI.Features.Camera
{
    /// <summary>
    /// 添加相机 ViewModel
    /// </summary>
    public class AddCameraViewModel : ObservableObject
    {
        #region 属性
        
        /// <summary>
        /// <summary>
        /// 发现的相机列表
        /// </summary>
        private ObservableCollection<CameraDiscoveryItem> _discoveredCameras;
        public ObservableCollection<CameraDiscoveryItem> DiscoveredCameras
        {
            get => _discoveredCameras;
            set => SetProperty(ref _discoveredCameras, value);
        }
        
        /// <summary>
        /// 选中的相机
        /// </summary>
        private CameraDiscoveryItem? _selectedCamera;
        public CameraDiscoveryItem? SelectedCamera
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
        /// 是否正在搜索
        /// </summary>
        private bool _isSearching;
        public bool IsSearching
        {
            get => _isSearching;
            set => SetProperty(ref _isSearching, value);
        }
        
        /// <summary>
        /// GigE 接口是否参与自动发现
        /// </summary>
        private bool _isGigEVisionSelected = true;
        public bool IsGigEVisionSelected
        {
            get => _isGigEVisionSelected;
            set => SetInterfaceSelection(ref _isGigEVisionSelected, value, IsUsb3VisionSelected, IsSimulationSelected);
        }

        private bool _isUsb3VisionSelected;
        public bool IsUsb3VisionSelected
        {
            get => _isUsb3VisionSelected;
            set => SetInterfaceSelection(ref _isUsb3VisionSelected, value, IsGigEVisionSelected, IsSimulationSelected);
        }

        private bool _isSimulationSelected;
        public bool IsSimulationSelected
        {
            get => _isSimulationSelected;
            set => SetInterfaceSelection(ref _isSimulationSelected, value, IsGigEVisionSelected, IsUsb3VisionSelected);
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
        
        private readonly CameraRuntimeComposition _runtime;
        private readonly CameraManagerViewModel _cameraManager;
        private CancellationTokenSource? _cancellationTokenSource;
        
        #endregion
        
        public AddCameraViewModel(CameraRuntimeComposition runtime, CameraManagerViewModel cameraManager)
        {
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            _cameraManager = cameraManager;
            
            // 初始化属性
            _discoveredCameras = new ObservableCollection<CameraDiscoveryItem>();
            _searchStatus = "就绪";
            
            // 初始化命令
            SearchCommand = new RelayCommand(Search, CanSearch);
            StopCommand = new RelayCommand(Stop, CanStop);
            ConfirmCommand = new RelayCommand(Confirm, CanConfirm);
            CancelCommand = new RelayCommand(Cancel);
            
            VisionLogger.Instance.Log(LogLevel.Info, "添加相机 ViewModel 初始化完成", "AddCameraViewModel");
        }

        private void SetInterfaceSelection(ref bool field, bool value, bool otherOne, bool otherTwo)
        {
            // 至少保留一个接口，避免点击搜索时产生“什么都没搜”的隐式行为。
            if (!value && !otherOne && !otherTwo)
            {
                return;
            }

            SetProperty(ref field, value);
        }

        private IReadOnlySet<CameraTransportKind> GetSelectedTransports()
        {
            var transports = new HashSet<CameraTransportKind>();
            if (IsGigEVisionSelected) transports.Add(CameraTransportKind.GigEVision);
            if (IsUsb3VisionSelected) transports.Add(CameraTransportKind.Usb3Vision);
            if (IsSimulationSelected) transports.Add(CameraTransportKind.Simulation);
            return transports;
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
                
                VisionLogger.Instance.Log(LogLevel.Info, "开始自动发现相机...", "AddCameraViewModel");
                
                _cancellationTokenSource = new CancellationTokenSource();
                
                var descriptors = await _runtime.Discovery.DiscoverAsync(
                    GetSelectedTransports(), _cancellationTokenSource.Token);
                _runtime.Runtime.RegisterDevices(descriptors);
                var cameras = descriptors.Select(ToDiscoveredCamera).ToList();
                
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

        private static CameraDiscoveryItem ToDiscoveredCamera(CameraDescriptor descriptor)
        {
            return new CameraDiscoveryItem
            {
                Name = descriptor.DisplayName,
                Manufacturer = descriptor.Manufacturer,
                Model = descriptor.Model,
                IpAddress = descriptor.IpAddress ?? string.Empty,
                SerialNumber = descriptor.SerialNumber,
                MacAddress = descriptor.MacAddress,
                DeviceId = descriptor.DeviceId,
                Transport = descriptor.Transport,
                IsAvailable = true
            };
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
                    Transport = SelectedCamera.Transport.ToString(),
                    Manufacturer = SelectedCamera.Manufacturer,
                    Model = SelectedCamera.Model,
                    IpAddress = SelectedCamera.IpAddress,
                    DeviceId = SelectedCamera.DeviceId,
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
