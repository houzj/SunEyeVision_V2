using System;
using System.ComponentModel;
using System.Windows.Input;

namespace SunEyeVision.UI.ViewModels
{
    /// <summary>
    /// 相机详情 ViewModel - 用于 CameraDetailPanel
    /// </summary>
    public class CameraDetailViewModel : INotifyPropertyChanged
    {
        private CameraDevice _selectedCamera;
        private bool _isCameraSelected;
        private object _manufacturerParamsViewModel;

        public CameraDevice SelectedCamera
        {
            get => _selectedCamera;
            set
            {
                _selectedCamera = value;
                IsCameraSelected = value != null;
                OnPropertyChanged(nameof(SelectedCamera));

                // 根据厂商类型加载对应的参数视图
                LoadManufacturerParams();
            }
        }

        public bool IsCameraSelected
        {
            get => _isCameraSelected;
            set
            {
                _isCameraSelected = value;
                OnPropertyChanged(nameof(IsCameraSelected));
            }
        }

        public object ManufacturerParamsViewModel
        {
            get => _manufacturerParamsViewModel;
            set
            {
                _manufacturerParamsViewModel = value;
                OnPropertyChanged(nameof(ManufacturerParamsViewModel));
            }
        }

        // Commands
        public ICommand ConnectCommand { get; }
        public ICommand DisconnectCommand { get; }
        public ICommand PreviewCommand { get; }
        public ICommand SaveParamsCommand { get; }
        public ICommand ResetParamsCommand { get; }

        public CameraDetailViewModel()
        {
            ConnectCommand = new RelayCommand(Connect);
            DisconnectCommand = new RelayCommand(Disconnect);
            PreviewCommand = new RelayCommand(Preview);
            SaveParamsCommand = new RelayCommand(SaveParams);
            ResetParamsCommand = new RelayCommand(ResetParams);
        }

        private void LoadManufacturerParams()
        {
            // 根据相机厂商类型加载对应的参数视图
            // 这里使用一个简单的字典来映射
            // 实际应用中可以从服务容器中获取
            if (SelectedCamera != null)
            {
                // 根据制造商选择对应的 ViewModel
                // 这里暂时使用 GenericParamsViewModel
                ManufacturerParamsViewModel = new GenericParamsViewModel();
            }
        }

        private void Connect()
        {
            if (SelectedCamera != null)
            {
                SelectedCamera.StatusText = "已连接";
                SelectedCamera.StatusIcon = "✅";
                SelectedCamera.Latency = "23";
                SelectedCamera.FrameRate = "30";
            }
        }

        private void Disconnect()
        {
            if (SelectedCamera != null)
            {
                SelectedCamera.StatusText = "断开";
                SelectedCamera.StatusIcon = "❌";
                SelectedCamera.Latency = "-";
                SelectedCamera.FrameRate = "-";
            }
        }

        private void Preview()
        {
            // TODO: 实现预览功能
        }

        private void SaveParams()
        {
            // TODO: 实现保存参数功能
        }

        private void ResetParams()
        {
            // TODO: 实现恢复默认参数功能
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// 通用参数 ViewModel - 复用SDK控件
    /// </summary>
    public class GenericParamsViewModel : ViewModelBase
    {
        private string _exposureMode = "自动";
        private string _exposureTime = "10000 us";
        private string _gain = "50 dB";
        private bool _enableMotionDetection = true;
        private bool _enableBorderDetection = false;
        private bool _enableRegionIntrusion = false;
        private string _wideDynamic = "启用";
        private double _wideDynamicLevel = 50.0;
        private string _compressionType = "H.264";
        private string _bitrate = "4096 Kbps";
        private string _iframeInterval = "50";

        // 下拉选项
        public List<string> ExposureModes { get; } = new List<string> { "自动", "手动" };
        public List<string> WideDynamicOptions { get; } = new List<string> { "启用", "关闭" };
        public List<string> CompressionTypes { get; } = new List<string> { "H.264", "H.265", "MJPEG" };

        public string ExposureMode
        {
            get => _exposureMode;
            set => SetProperty(ref _exposureMode, value);
        }

        public string ExposureTime
        {
            get => _exposureTime;
            set => SetProperty(ref _exposureTime, value);
        }

        public string Gain
        {
            get => _gain;
            set => SetProperty(ref _gain, value);
        }

        public bool EnableMotionDetection
        {
            get => _enableMotionDetection;
            set => SetProperty(ref _enableMotionDetection, value);
        }

        public bool EnableBorderDetection
        {
            get => _enableBorderDetection;
            set => SetProperty(ref _enableBorderDetection, value);
        }

        public bool EnableRegionIntrusion
        {
            get => _enableRegionIntrusion;
            set => SetProperty(ref _enableRegionIntrusion, value);
        }

        public string WideDynamic
        {
            get => _wideDynamic;
            set => SetProperty(ref _wideDynamic, value);
        }

        public double WideDynamicLevel
        {
            get => _wideDynamicLevel;
            set => SetProperty(ref _wideDynamicLevel, value);
        }

        public string CompressionType
        {
            get => _compressionType;
            set => SetProperty(ref _compressionType, value);
        }

        public string Bitrate
        {
            get => _bitrate;
            set => SetProperty(ref _bitrate, value);
        }

        public string IframeInterval
        {
            get => _iframeInterval;
            set => SetProperty(ref _iframeInterval, value);
        }

        protected override string? GetLogSource()
        {
            return "GenericParamsViewModel";
        }
    }
}
