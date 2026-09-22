using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Collections.Generic;
using System.Windows.Input;
using System.Windows;
using SunEyeVision.DeviceDriver.Cameras;
using SunEyeVision.DeviceDriver.Cameras.Contracts;
using SunEyeVision.Plugin.SDK.Devices.Camera;

using SunEyeVision.UI.Core;

namespace SunEyeVision.UI.Features.Camera
{
    /// <summary>
    /// 相机详情 ViewModel - 用于 CameraDetailPanel
    /// </summary>
    public class CameraDetailViewModel : ViewModelBase
    {
        private CameraDevice _selectedCamera;
        private bool _isCameraSelected;
        private object _manufacturerParamsViewModel;
        private readonly CameraRuntimeComposition _runtime;
        private CameraSession? _session;
        private string _parameterStatus = "请选择相机后加载设备参数";

        public ObservableCollection<CameraParameterItemViewModel> Parameters { get; } = new();

        public string ParameterStatus
        {
            get => _parameterStatus;
            private set
            {
                SetProperty(ref _parameterStatus, value);
            }
        }

        public CameraDevice SelectedCamera
        {
            get => _selectedCamera;
            set
            {
                if (!SetProperty(ref _selectedCamera, value))
                    return;
                IsCameraSelected = value != null;

                // 根据厂商类型加载对应的参数视图
                LoadManufacturerParams();
                _ = LoadSchemaAsync();
            }
        }

        public bool IsCameraSelected
        {
            get => _isCameraSelected;
            set
            {
                SetProperty(ref _isCameraSelected, value);
            }
        }

        public object ManufacturerParamsViewModel
        {
            get => _manufacturerParamsViewModel;
            set
            {
                SetProperty(ref _manufacturerParamsViewModel, value);
            }
        }

        // Commands
        public ICommand ConnectCommand { get; }
        public ICommand DisconnectCommand { get; }
        public ICommand PreviewCommand { get; }
        public ICommand SaveParamsCommand { get; }
        public ICommand ResetParamsCommand { get; }

        public CameraDetailViewModel(CameraRuntimeComposition runtime)
        {
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            ConnectCommand = new RelayCommand(Connect);
            DisconnectCommand = new RelayCommand(Disconnect);
            PreviewCommand = new RelayCommand(Preview);
            SaveParamsCommand = new RelayCommand(SaveParams);
            ResetParamsCommand = new RelayCommand(ResetParams);
        }

        private async System.Threading.Tasks.Task LoadSchemaAsync()
        {
            Parameters.Clear();
            await ReleaseSessionAsync().ConfigureAwait(true);
            if (SelectedCamera == null || string.IsNullOrWhiteSpace(SelectedCamera.DeviceId))
            {
                ParameterStatus = "请选择已添加的设备";
                return;
            }

            try
            {
                var reference = new CameraDeviceRef(SelectedCamera.DeviceId);
                await using var session = await _runtime.Runtime.OpenAsync(
                    reference, CameraAccessMode.Diagnostic, cancellationToken: default).ConfigureAwait(true);
                var schema = await session.Adapter.GetParameterSchemaAsync(default).ConfigureAwait(true);
                var names = schema.Parameters.Select(item => item.Name).ToArray();
                var values = await session.Adapter.ReadParametersAsync(names, default).ConfigureAwait(true);
                foreach (var descriptor in schema.Parameters)
                {
                    values.TryGetValue(descriptor.Name, out var value);
                    Parameters.Add(new CameraParameterItemViewModel(descriptor, value ?? descriptor.Value));
                }
                await _runtime.Runtime.CloseAsync(reference).ConfigureAwait(true);
                ParameterStatus = $"已加载 {Parameters.Count} 个设备参数";
            }
            catch (Exception ex)
            {
                ParameterStatus = $"参数不可用：{ex.Message}";
            }
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
            _ = ConnectAsync();
        }

        private void Disconnect()
        {
            _ = DisconnectAsync();
        }

        private void Preview()
        {
            if (SelectedCamera == null || string.IsNullOrWhiteSpace(SelectedCamera.DeviceId))
                return;

            var window = new CameraPreviewWindow(_runtime, new CameraDeviceRef(SelectedCamera.DeviceId));
            var owner = Application.Current?.Windows.OfType<Window>()
                .FirstOrDefault(item => ReferenceEquals(item.DataContext, this));
            if (owner != null)
                window.Owner = owner;
            window.Show();
        }

        private void SaveParams()
        {
            _ = SaveParamsAsync();
        }

        private void ResetParams()
        {
            _ = LoadSchemaAsync();
        }

        private async System.Threading.Tasks.Task ConnectAsync()
        {
            if (SelectedCamera == null || string.IsNullOrWhiteSpace(SelectedCamera.DeviceId))
                return;

            try
            {
                if (_session != null)
                    return;

                var reference = new CameraDeviceRef(SelectedCamera.DeviceId);
                _session = await _runtime.Runtime.OpenAsync(reference, CameraAccessMode.Diagnostic);
                SelectedCamera.StatusText = "已连接";
                SelectedCamera.StatusIcon = "✅";
                ParameterStatus = "设备已连接";
            }
            catch (Exception ex)
            {
                _session = null;
                SelectedCamera.StatusText = "连接失败";
                SelectedCamera.StatusIcon = "⚠️";
                ParameterStatus = $"连接失败：{ex.Message}";
            }
        }

        private async System.Threading.Tasks.Task DisconnectAsync()
        {
            if (SelectedCamera == null || string.IsNullOrWhiteSpace(SelectedCamera.DeviceId))
                return;

            try
            {
                await _runtime.Runtime.CloseAsync(new CameraDeviceRef(SelectedCamera.DeviceId));
                _session = null;
                SelectedCamera.StatusText = "断开";
                SelectedCamera.StatusIcon = "❌";
                ParameterStatus = "设备已断开";
            }
            catch (Exception ex)
            {
                ParameterStatus = $"断开失败：{ex.Message}";
            }
        }

        private async System.Threading.Tasks.Task ReleaseSessionAsync()
        {
            var session = _session;
            _session = null;
            if (session != null)
                await _runtime.Runtime.CloseAsync(session.CameraRef).ConfigureAwait(true);
        }

        private async System.Threading.Tasks.Task SaveParamsAsync()
        {
            if (SelectedCamera == null || string.IsNullOrWhiteSpace(SelectedCamera.DeviceId))
                return;

            CameraSession? temporarySession = null;
            try
            {
                var reference = new CameraDeviceRef(SelectedCamera.DeviceId);
                var session = _session;
                if (session == null)
                {
                    temporarySession = await _runtime.Runtime.OpenAsync(reference, CameraAccessMode.Diagnostic);
                    session = temporarySession;
                }

                var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                foreach (var item in Parameters)
                {
                    if (item.Value is null || item.Kind is CameraParameterKind.Command)
                        continue;
                    values[item.Name] = ConvertParameterValue(item);
                }

                await session.Adapter.ApplyParametersAsync(values, default);
                ParameterStatus = $"已写入 {values.Count} 个参数";
            }
            catch (Exception ex)
            {
                ParameterStatus = $"参数写入失败：{ex.Message}";
            }
            finally
            {
                if (temporarySession != null)
                    await _runtime.Runtime.CloseAsync(temporarySession.CameraRef);
            }
        }

        private static object? ConvertParameterValue(CameraParameterItemViewModel item)
        {
            if (item.Kind == CameraParameterKind.Integer)
                return Convert.ToInt64(item.Value, System.Globalization.CultureInfo.InvariantCulture);
            if (item.Kind == CameraParameterKind.Double)
                return Convert.ToDouble(item.Value, System.Globalization.CultureInfo.InvariantCulture);
            if (item.Kind == CameraParameterKind.Boolean)
                return Convert.ToBoolean(item.Value, System.Globalization.CultureInfo.InvariantCulture);
            return item.Value;
        }

    }

    public sealed class CameraParameterItemViewModel : ViewModelBase
    {
        private object? _value;

        public CameraParameterItemViewModel(CameraParameterDescriptor descriptor, object? value)
        {
            Name = descriptor.Name;
            DisplayName = descriptor.DisplayName;
            Kind = descriptor.Kind;
            IsReadOnly = descriptor.IsReadOnly || !descriptor.IsAvailable;
            EnumValues = descriptor.EnumValues;
            Unit = descriptor.Description ?? string.Empty;
            _value = value;
        }

        public string Name { get; }
        public string DisplayName { get; }
        public CameraParameterKind Kind { get; }
        public bool IsReadOnly { get; }
        public bool IsEditable => !IsReadOnly;
        public bool IsBoolean => Kind == CameraParameterKind.Boolean;
        public bool IsEnumeration => Kind == CameraParameterKind.Enumeration;
        public bool IsTextValue => !IsBoolean && !IsEnumeration;
        public IReadOnlyList<CameraEnumValue> EnumValues { get; }
        public string Unit { get; }
        public object? Value
        {
            get => _value;
            set
            {
                SetProperty(ref _value, value);
            }
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
