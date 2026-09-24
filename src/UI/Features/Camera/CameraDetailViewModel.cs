using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Collections.Generic;
using System.Windows.Input;
using System.Windows;
using System.Threading;
using System.Threading.Tasks;
using SunEyeVision.DeviceDriver.Cameras;
using SunEyeVision.DeviceDriver.Cameras.Contracts;
using SunEyeVision.Plugin.SDK.Devices.Camera;
using SunEyeVision.Plugin.SDK.Logging;

using SunEyeVision.UI.Core;

namespace SunEyeVision.UI.Features.Camera
{
    /// <summary>
    /// 相机详情 ViewModel - 用于 CameraDetailPanel
    /// </summary>
    public class CameraDetailViewModel : ViewModelBase
    {
        private static readonly Dictionary<string, string> CategoryNames = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Device Control"] = "设备控制", ["Device"] = "设备", ["Image Format Control"] = "图像格式",
            ["Acquisition Control"] = "采集控制", ["Analog Control"] = "模拟控制",
            ["Digital IO Control"] = "数字 I/O 控制", ["Digital I/O Control"] = "数字 I/O 控制",
            ["Color Transformation Control"] = "色彩转换", ["Color Transformation"] = "色彩转换",
            ["Color Space Control"] = "色彩空间", ["LUT Control"] = "查找表 (LUT)",
            ["Super Palette Control"] = "调色板控制", ["Encoder Control"] = "编码器控制",
            ["Frequency Converter Control"] = "频率转换器", ["Counter And Timer Control"] = "计数器与定时器",
            ["Counter/Timer Control"] = "计数器与定时器", ["Transport Layer Control"] = "传输层控制",
            ["Event Control"] = "事件控制", ["Chunk Data Control"] = "附加数据控制",
            ["File Access Control"] = "文件访问控制", ["User Set Control"] = "用户配置集",
            ["Action Control"] = "动作控制", ["GigE Vision"] = "GigE 网络参数",
            ["USB Control"] = "USB 控制", ["Other"] = "其他",
            ["Shading Correction"] = "阴影校正", ["Transfer Control"] = "传输控制",
            ["Color Transformation Control"] = "色彩转换", ["Digital I/O Control"] = "数字 I/O 控制"
        };

        private static readonly Dictionary<string, string> ParameterNames = new(StringComparer.OrdinalIgnoreCase)
        {
            ["DeviceVendorName"] = "设备厂商", ["DeviceModelName"] = "设备型号", ["DeviceVersion"] = "设备版本",
            ["DeviceSerialNumber"] = "设备序列号", ["DeviceUserID"] = "设备用户名称", ["DeviceID"] = "设备 ID",
            ["DeviceTemperature"] = "设备温度", ["Width"] = "图像宽度", ["Height"] = "图像高度",
            ["OffsetX"] = "水平偏移", ["OffsetY"] = "垂直偏移", ["ReverseX"] = "水平翻转", ["ReverseY"] = "垂直翻转",
            ["PixelFormat"] = "像素格式", ["SensorWidth"] = "传感器宽度", ["SensorHeight"] = "传感器高度",
            ["AcquisitionMode"] = "采集模式", ["AcquisitionStart"] = "开始采集", ["AcquisitionStop"] = "停止采集",
            ["AcquisitionFrameCount"] = "采集帧数", ["AcquisitionBurstFrameCount"] = "连续采集帧数",
            ["AcquisitionFrameRate"] = "采集帧率", ["AcquisitionFrameRateEnable"] = "启用帧率控制",
            ["AcquisitionLineRate"] = "采集行频", ["AcquisitionLineRateEnable"] = "启用行频控制",
            ["ResultingLineRate"] = "实际行频", ["ReferenceLineRate"] = "参考行频",
            ["DeviceType"] = "设备类型", ["DeviceScanType"] = "扫描类型",
            ["TriggerMode"] = "触发模式", ["TriggerSource"] = "触发源", ["TriggerSelector"] = "触发选择器",
            ["TriggerActivation"] = "触发沿", ["TriggerDelay"] = "触发延迟", ["TriggerSoftware"] = "软件触发",
            ["ExposureMode"] = "曝光模式", ["ExposureTime"] = "曝光时间", ["ExposureTimeRaw"] = "曝光时间",
            ["ExposureAuto"] = "自动曝光", ["Gain"] = "增益", ["GainRaw"] = "增益", ["GainAuto"] = "自动增益",
            ["BlackLevel"] = "黑电平", ["Gamma"] = "伽马", ["GammaEnable"] = "启用伽马校正",
            ["BalanceWhiteAuto"] = "自动白平衡", ["BalanceRatio"] = "白平衡比例", ["Saturation"] = "饱和度",
            ["Sharpness"] = "锐度", ["Contrast"] = "对比度", ["Brightness"] = "亮度",
            ["LUTEnable"] = "启用查找表", ["LUTSelector"] = "查找表选择", ["LUTIndex"] = "查找表索引",
            ["LUTValue"] = "查找表当前值", ["LUTValueAll"] = "查找表全部数据", ["LUTSave"] = "保存查找表",
            ["UserSetSelector"] = "用户配置选择", ["UserSetLoad"] = "加载用户配置", ["UserSetSave"] = "保存用户配置",
            ["ResultingFrameRate"] = "实际帧率", ["PayloadSize"] = "图像数据大小", ["DeviceLinkThroughputLimit"] = "传输带宽限制",
            ["GevSCPSPacketSize"] = "网络数据包大小", ["GevSCPD"] = "网络包间隔", ["GevCurrentIPAddress"] = "当前 IP 地址"
        };

        private static readonly Dictionary<string, string> EnumDisplayNames = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Continuous"] = "连续", ["SingleFrame"] = "单帧", ["MultiFrame"] = "多帧",
            ["Off"] = "关闭", ["On"] = "开启", ["Once"] = "单次", ["Software"] = "软件触发",
            ["Line0"] = "线路 0", ["Line1"] = "线路 1", ["Line2"] = "线路 2", ["Line3"] = "线路 3",
            ["Transmitter"] = "发射端", ["Receiver"] = "接收端", ["AreaScan"] = "面阵扫描",
            ["Arescan"] = "面阵扫描", ["Linescan"] = "线阵扫描", ["LineScan"] = "线阵扫描",
            ["RisingEdge"] = "上升沿", ["FallingEdge"] = "下降沿", ["AnyEdge"] = "任意边沿",
            ["Mono8"] = "灰度 8 位", ["Mono10"] = "灰度 10 位", ["Mono12"] = "灰度 12 位",
            ["RGB8"] = "RGB 8 位", ["BGR8"] = "BGR 8 位", ["BayerRG8"] = "Bayer RG 8 位"
        };

        private CameraDevice _selectedCamera;
        private bool _isCameraSelected;
        private object _manufacturerParamsViewModel;
        private readonly CameraRuntimeComposition _runtime;
        private CameraSession? _session;
        private CameraPreviewWindow? _previewWindow;
        private bool _previewOwnsSession;
        private bool _connectionBusy;
        private bool _parameterSchemaLoaded;
        private string _parameterStatus = "请选择相机后加载设备参数";
        private string _parameterSearchText = string.Empty;
        private CameraParameterGroupViewModel? _selectedParameterCategory;
        private CancellationTokenSource? _schemaLoadCts;
        private CancellationTokenSource? _autoApplyCts;
        private readonly SemaphoreSlim _parameterApplyGate = new(1, 1);

        private static readonly HashSet<string> CommonStandardFeatures = new(StringComparer.OrdinalIgnoreCase)
        {
            "acquisition.mode", "acquisition.frame-rate", "acquisition.frame-rate-enable",
            "trigger.mode", "trigger.source", "exposure.mode", "exposure.time", "exposure.auto",
            "gain.value", "gain.auto", "image.black-level", "image.gamma", "image.gamma-enable",
            "image.white-balance-auto", "image.width", "image.height", "image.offset-x",
            "image.offset-y", "image.pixel-format", "transport.packet-size",
            "transport.packet-delay", "transport.pause-frame"
        };

        public ObservableCollection<CameraParameterItemViewModel> Parameters { get; } = new();
        public ObservableCollection<CameraParameterGroupViewModel> ParameterCategories { get; } = new();
        public ObservableCollection<CameraParameterItemViewModel> SelectedParameters { get; } = new();
        public string ParameterSearchText
        {
            get => _parameterSearchText;
            set
            {
                if (SetProperty(ref _parameterSearchText, value ?? string.Empty))
                    RefreshSelectedParameters();
            }
        }

        public CameraParameterGroupViewModel? SelectedParameterCategory
        {
            get => _selectedParameterCategory;
            set
            {
                if (SetProperty(ref _selectedParameterCategory, value))
                {
                    OnPropertyChanged(nameof(SelectedCategoryTitle));
                    RefreshSelectedParameters();
                }
            }
        }

        public string SelectedCategoryTitle => !string.IsNullOrWhiteSpace(ParameterSearchText)
            ? "搜索结果"
            : SelectedParameterCategory?.Name ?? "参数";

        public string ParameterStatus
        {
            get => _parameterStatus;
            private set
            {
                SetProperty(ref _parameterStatus, value);
            }
        }

        public void ReportOperationStatus(string status) => ParameterStatus = status ?? string.Empty;

        public CameraDevice SelectedCamera
        {
            get => _selectedCamera;
            set
            {
                if (!SetProperty(ref _selectedCamera, value))
                    return;
                IsCameraSelected = value != null;
                _parameterSchemaLoaded = false;
                _schemaLoadCts?.Cancel();
                _schemaLoadCts?.Dispose();
                _schemaLoadCts = new CancellationTokenSource();

                // 根据厂商类型加载对应的参数视图
                LoadManufacturerParams();
                _ = LoadSchemaAsync(_schemaLoadCts.Token);
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
        public ICommand ToggleConnectionCommand { get; }
        public ICommand PreviewCommand { get; }
        public ICommand ResetParamsCommand { get; }
        public ICommand ExecuteParameterCommand { get; }

        public bool IsConnected => _session != null;
        public string ConnectionActionText => _connectionBusy
            ? "连接中..."
            : IsConnected ? "断开" : "连接";

        public CameraDetailViewModel(CameraRuntimeComposition runtime)
        {
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            ConnectCommand = new RelayCommand(Connect);
            DisconnectCommand = new RelayCommand(Disconnect);
            ToggleConnectionCommand = new RelayCommand(ToggleConnection);
            PreviewCommand = new RelayCommand(Preview);
            ResetParamsCommand = new RelayCommand(ResetParams);
            ExecuteParameterCommand = new RelayCommand<CameraParameterItemViewModel>(
                item => _ = ExecuteParameterAsync(item),
                item => item is { IsCommand: true, IsAvailable: true, IsWritable: true });
        }

        private async Task LoadSchemaAsync(CancellationToken cancellationToken)
        {
            ClearParameterItems();
            ParameterCategories.Clear();
            SelectedParameters.Clear();
            SelectedParameterCategory = null;
            await ReleaseSessionAsync().ConfigureAwait(true);
            if (SelectedCamera == null || string.IsNullOrWhiteSpace(SelectedCamera.DeviceId))
            {
                ParameterStatus = "请选择已添加的设备";
                return;
            }

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var camera = SelectedCamera;
                var reference = new CameraDeviceRef(camera.DeviceId);
                var session = await _runtime.Runtime.OpenAsync(
                    reference, CameraAccessMode.Diagnostic, cancellationToken: cancellationToken).ConfigureAwait(true);
                try
                {
                    var schema = await session.Adapter.GetParameterSchemaAsync(cancellationToken).ConfigureAwait(true);
                    var names = schema.Parameters
                        .Where(item => item.Value is null && item.IsReadable && item.Kind != CameraParameterKind.Command)
                        .Select(item => item.Name)
                        .ToArray();
                    var values = await session.Adapter.ReadParametersAsync(names, cancellationToken).ConfigureAwait(true);
                    if (!ReferenceEquals(SelectedCamera, camera))
                        return;
                    foreach (var descriptor in schema.Parameters)
                    {
                        values.TryGetValue(descriptor.Name, out var value);
                        var item = new CameraParameterItemViewModel(descriptor, value ?? descriptor.Value);
                        item.PropertyChanged += OnParameterValueChanged;
                        Parameters.Add(item);
                    }
                    BuildParameterTree();
                    _parameterSchemaLoaded = true;
                    var readableCount = Parameters.Count(item => item.IsReadable && item.HasValue);
                    var unavailableCount = Parameters.Count(item => !item.IsAvailable || !item.IsImplemented);
                    var editableCount = Parameters.Count(item => item.IsEditable);
                    ParameterStatus = $"已加载 {Parameters.Count - unavailableCount} 项可用参数 · {readableCount} 项可读 · {editableCount} 项可编辑";
                }
                finally
                {
                    // 清理不能被“切换相机”的取消信号打断，保证旧会话一定释放。
                    await _runtime.Runtime.CloseAsync(reference, CancellationToken.None).ConfigureAwait(true);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // 切换相机时取消旧的参数加载，不显示为错误。
            }
            catch (Exception ex)
            {
                _parameterSchemaLoaded = false;
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

        private void BuildParameterTree(string? preferredViewKind = null, string? preferredCategoryPath = null)
        {
            ParameterCategories.Clear();
            var groups = new Dictionary<string, CameraParameterGroupViewModel>(StringComparer.OrdinalIgnoreCase);
            var native = new CameraParameterGroupViewModel("属性树", viewKind: "NativeRoot") { IsExpanded = true };
            var common = new CameraParameterGroupViewModel("常用属性", viewKind: "Common") { IsExpanded = true };
            var advanced = new CameraParameterGroupViewModel("高级功能", viewKind: "AdvancedFeatures") { IsExpanded = true };
            ParameterCategories.Add(native);
            ParameterCategories.Add(common);
            ParameterCategories.Add(advanced);
            var commonGroups = new Dictionary<string, CameraParameterGroupViewModel>(StringComparer.OrdinalIgnoreCase);
            var advancedGroups = new Dictionary<string, CameraParameterGroupViewModel>(StringComparer.OrdinalIgnoreCase);

            foreach (var parameter in Parameters.Where(item => item.IsAvailable && item.IsImplemented))
            {
                var path = string.IsNullOrWhiteSpace(parameter.Category) ? "其他" : parameter.Category;
                var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (segments.Length == 0)
                    segments = new[] { "其他" };

                var parent = native;
                var currentPath = string.Empty;
                foreach (var segment in segments)
                {
                    currentPath = currentPath.Length == 0 ? segment : $"{currentPath}/{segment}";
                    if (!groups.TryGetValue(currentPath, out var group))
                    {
                        group = new CameraParameterGroupViewModel(LocalizeCategory(segment), currentPath)
                        {
                            IsExpanded = false,
                            SourceOrder = parameter.SourceOrder
                        };
                        groups[currentPath] = group;
                        parent.Children.Add(group);
                    }
                    parent = group;
                }

                if (parameter.StandardFeature is { } feature
                    && CommonStandardFeatures.Contains(feature)
                    && !parameter.IsCommand
                    && !IsAcquisitionToolbarCommand(feature))
                {
                    common.Parameters.Add(parameter);
                    var commonGroupName = GetCommonGroupName(feature);
                    if (!commonGroups.TryGetValue(commonGroupName, out var commonGroup))
                    {
                        commonGroup = new CameraParameterGroupViewModel(
                            commonGroupName, $"common/{commonGroupName}", "Common")
                        {
                            IsExpanded = false,
                            SourceOrder = parameter.SourceOrder
                        };
                        commonGroups[commonGroupName] = commonGroup;
                        common.Children.Add(commonGroup);
                    }
                    commonGroup.Parameters.Add(parameter);
                }
                parent.Parameters.Add(parameter);

                var advancedGroupName = GetAdvancedFeatureGroup(parameter);
                if (advancedGroupName is not null)
                {
                    advanced.Parameters.Add(parameter);
                    if (!advancedGroups.TryGetValue(advancedGroupName, out var advancedGroup))
                    {
                        advancedGroup = new CameraParameterGroupViewModel(
                            advancedGroupName, $"advanced/{advancedGroupName}", "AdvancedFeature")
                        {
                            IsExpanded = false,
                            SourceOrder = parameter.SourceOrder
                        };
                        advancedGroups[advancedGroupName] = advancedGroup;
                        advanced.Children.Add(advancedGroup);
                    }
                    advancedGroup.Parameters.Add(parameter);
                }
            }

            var preferred = preferredViewKind switch
            {
                "NativeRoot" => native,
                "Common" => common,
                "AdvancedFeatures" when string.IsNullOrEmpty(preferredCategoryPath) => advanced,
                "AdvancedFeature" => advancedGroups.GetValueOrDefault(preferredCategoryPath),
                _ => common
            };
            SelectedParameterCategory = preferred ?? native;
            RefreshSelectedParameters();
        }

        private void RefreshSelectedParameters()
        {
            SelectedParameters.Clear();
            var query = ParameterSearchText.Trim();
            IEnumerable<CameraParameterItemViewModel> items = Parameters.Where(item => item.IsAvailable && item.IsImplemented);
            if (!string.IsNullOrEmpty(query))
            {
                items = items.Where(item => item.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || item.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || item.Category.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || item.Category.Split('/', StringSplitOptions.RemoveEmptyEntries)
                        .Select(LocalizeCategory).Any(categoryName => categoryName.Contains(query, StringComparison.OrdinalIgnoreCase)));
            }
            else if (SelectedParameterCategory is { } category)
            {
                items = category.ViewKind switch
                {
                    "Common" => category.Parameters,
                    "NativeRoot" => items,
                    "AdvancedFeatures" => category.Parameters,
                    "AdvancedFeature" => category.Parameters,
                    _ => items.Where(item => string.Equals(item.Category, category.CategoryPath, StringComparison.OrdinalIgnoreCase)
                        || item.Category.StartsWith(category.CategoryPath + "/", StringComparison.OrdinalIgnoreCase))
                };
            }

            foreach (var item in items.OrderBy(item => item.SourceOrder)
                .ThenBy(item => item.DisplayName, StringComparer.CurrentCulture))
                SelectedParameters.Add(item);
            OnPropertyChanged(nameof(SelectedCategoryTitle));
        }

        private static bool IsAcquisitionToolbarCommand(string feature) => feature is
            "acquisition.start" or "acquisition.stop" or "trigger.software";

        private static string GetCommonGroupName(string feature) => feature switch
        {
            "image.gamma" or "image.gamma-enable" or "image.black-level"
                or "image.white-balance-auto" => "ISP",
            "transport.packet-size" or "transport.packet-delay" or "transport.pause-frame"
                => "传输层控制",
            _ => "基本属性"
        };

        private static string? GetAdvancedFeatureGroup(CameraParameterItemViewModel parameter)
        {
            var name = parameter.Name;
            var category = parameter.Category;
            if (name.Contains("LUT", StringComparison.OrdinalIgnoreCase)
                || name.Contains("UserLut", StringComparison.OrdinalIgnoreCase))
                return "查找表";
            if (name.Contains("Chunk", StringComparison.OrdinalIgnoreCase)
                || category.Contains("Chunk", StringComparison.OrdinalIgnoreCase))
                return "Chunk 数据";
            if (name.Contains("Event", StringComparison.OrdinalIgnoreCase)
                || category.Contains("Event", StringComparison.OrdinalIgnoreCase))
                return "事件控制信息";
            if (name.Contains("UserSet", StringComparison.OrdinalIgnoreCase)
                || category.Contains("User Set", StringComparison.OrdinalIgnoreCase))
                return "用户集操作";
            if (name.Contains("Aoi", StringComparison.OrdinalIgnoreCase)
                || name.Contains("AutoFunction", StringComparison.OrdinalIgnoreCase)
                || name.Contains("AutoExposure", StringComparison.OrdinalIgnoreCase))
                return "AOI";
            if (category.Contains("Image Format", StringComparison.OrdinalIgnoreCase)
                && (name.Equals("Width", StringComparison.OrdinalIgnoreCase)
                    || name.Equals("Height", StringComparison.OrdinalIgnoreCase)
                    || name.Equals("OffsetX", StringComparison.OrdinalIgnoreCase)
                    || name.Equals("OffsetY", StringComparison.OrdinalIgnoreCase)
                    || name.Equals("RegionSelector", StringComparison.OrdinalIgnoreCase)))
                return "ROI";
            return null;
        }

        private async Task ExecuteParameterAsync(CameraParameterItemViewModel? item)
        {
            if (item is not { IsCommand: true, IsAvailable: true, IsWritable: true, CanExecuteCommand: true }
                || SelectedCamera is null)
                return;

            var traceId = Guid.NewGuid().ToString("N");
            var cameraId = SelectedCamera.DeviceId;
            using var diagnosticScope = CameraDiagnosticScope.Begin(traceId);
            CameraSession? temporarySession = null;
            try
            {
                var reference = new CameraDeviceRef(cameraId);
                var session = _session;
                if (session is null)
                {
                    temporarySession = await _runtime.Runtime.OpenAsync(reference, CameraAccessMode.Diagnostic);
                    session = temporarySession;
                }
                VisionLogger.Instance.Log(LogLevel.Info,
                    $"[camera-trace={traceId}] 相机命令开始 camera={cameraId} adapter={session.Adapter.AdapterId} node={item.Name}",
                    "CameraParameterTrace", target: LogTarget.All);
                await session.Adapter.ExecuteCommandAsync(item.Name, CancellationToken.None);
                VisionLogger.Instance.Log(LogLevel.Info,
                    $"[camera-trace={traceId}] 相机命令完成 camera={cameraId} node={item.Name}",
                    "CameraParameterTrace", target: LogTarget.All);
                ParameterStatus = $"命令已执行：{item.DisplayName}";
            }
            catch (Exception ex)
            {
                VisionLogger.Instance.Log(LogLevel.Error,
                    $"[camera-trace={traceId}] 相机命令失败 camera={cameraId} node={item.Name}: {ex.Message}",
                    "CameraParameterTrace", ex, LogTarget.All);
                ParameterStatus = $"命令执行失败：{ex.Message}";
            }
            finally
            {
                if (temporarySession is not null)
                    await _runtime.Runtime.CloseAsync(temporarySession.CameraRef);
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

        private void ToggleConnection()
        {
            if (_connectionBusy)
                return;
            if (IsConnected)
                Disconnect();
            else
                Connect();
        }

        private async void Preview()
        {
            if (_connectionBusy)
                return;
            if (SelectedCamera == null || string.IsNullOrWhiteSpace(SelectedCamera.DeviceId))
                return;

            if (_previewWindow is { IsVisible: true })
            {
                _previewWindow.Activate();
                return;
            }

            var wasConnectedBeforePreview = IsConnected;
            await ConnectAsync();
            if (_session is null)
            {
                MessageBox.Show(ParameterStatus, "相机连接失败", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // 只有预览前未连接时，预览窗口才拥有这次会话的生命周期。
            // 预览前已经由设备列表连接的相机，关闭预览后必须保持连接。
            _previewOwnsSession = !wasConnectedBeforePreview;

            // 选中设备时可能因被厂商相机软件占用而无法读取参数树；连接成功后用当前会话重试，
            // 不要求用户删除并重新添加设备。使用当前会话，避免再开第二个 SDK 句柄。
            if (!_parameterSchemaLoaded)
            {
                ParameterStatus = "设备已连接，正在重新读取参数…";
                try
                {
                    await RefreshParameterSchemaAsync(
                        _session.Adapter,
                        SelectedParameterCategory?.ViewKind,
                        SelectedParameterCategory?.CategoryPath).ConfigureAwait(true);
                    _parameterSchemaLoaded = true;
                    var availableCount = Parameters.Count(item => item.IsAvailable && item.IsImplemented);
                    var readableCount = Parameters.Count(item => item.IsReadable && item.HasValue);
                    var editableCount = Parameters.Count(item => item.IsEditable);
                    ParameterStatus = $"已加载 {availableCount} 项可用参数 · {readableCount} 项可读 · {editableCount} 项可编辑";
                }
                catch (Exception ex)
                {
                    _parameterSchemaLoaded = false;
                    ParameterStatus = $"已连接，但参数读取失败：{ex.Message}";
                }
            }

            var window = new CameraPreviewWindow(_runtime, new CameraDeviceRef(SelectedCamera.DeviceId), _session, this);
            _previewWindow = window;
            window.Closed += (_, _) =>
            {
                if (ReferenceEquals(_previewWindow, window))
                    _previewWindow = null;
            };
            var owner = Application.Current?.Windows.OfType<Window>()
                .FirstOrDefault(item => item.DataContext is CameraManagerViewModel manager
                    && ReferenceEquals(manager.CameraDetailViewModel, this));
            if (owner != null)
                window.Owner = owner;
            window.ShowDialog();
        }

        public async Task ClosePreviewSessionAsync()
        {
            _autoApplyCts?.Cancel();
            _autoApplyCts?.Dispose();
            _autoApplyCts = null;
            await _parameterApplyGate.WaitAsync().ConfigureAwait(true);
            try
            {
            if (_session is null)
            {
                _previewOwnsSession = false;
                return;
            }

            var camera = SelectedCamera;
            if (!_previewOwnsSession)
            {
                if (camera is not null)
                    camera.State = CameraState.Connected;
                ParameterStatus = "预览已关闭，设备保持连接";
                OnPropertyChanged(nameof(IsConnected));
                OnPropertyChanged(nameof(ConnectionActionText));
                return;
            }

            await ReleaseSessionAsync().ConfigureAwait(true);
            _previewOwnsSession = false;
            if (camera is not null)
                camera.State = CameraState.Offline;
            ParameterStatus = "预览已关闭，相机连接已释放";
            OnPropertyChanged(nameof(IsConnected));
            OnPropertyChanged(nameof(ConnectionActionText));
            }
            finally
            {
                _parameterApplyGate.Release();
            }
        }

        public Task<bool> ApplyPendingParametersAsync() => SaveParamsAsync();

        public async Task SetConnectionAsync(CameraDevice camera, bool shouldConnect)
        {
            if (camera == null)
                return;

            SelectedCamera = camera;
            if (shouldConnect)
                await ConnectAsync().ConfigureAwait(true);
            else
                await DisconnectAsync().ConfigureAwait(true);
        }

        private void OnParameterValueChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(CameraParameterItemViewModel.Value)
                || sender is not CameraParameterItemViewModel item
                || !item.IsDirty)
                return;

            ParameterStatus = $"{item.DisplayName} 已修改，正在写入设备…";
            VisionLogger.Instance.Log(LogLevel.Info,
                $"[CameraParameterTrace] UI参数变更，已排队自动写入 camera={SelectedCamera?.DeviceId} node={item.Name} value={FormatDiagnosticValue(item.Value)} requiresStopped={item.RequiresStopped} affectsSchema={item.AffectsSchema}",
                "CameraParameterTrace", target: LogTarget.All);

            _autoApplyCts?.Cancel();
            _autoApplyCts?.Dispose();
            _autoApplyCts = new CancellationTokenSource();
            _ = ApplyParameterAfterEditAsync(_autoApplyCts.Token);
        }

        private async Task ApplyParameterAfterEditAsync(CancellationToken cancellationToken)
        {
            try
            {
                // NumericUpDown / text editing often emits several intermediate values; merge them into one SDK write.
                await Task.Delay(350, cancellationToken).ConfigureAwait(true);
                await SaveParamsAsync().ConfigureAwait(true);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
        }

        private void ResetParams()
        {
            _ = ReloadParametersAsync();
        }

        private async Task ReloadParametersAsync()
        {
            if (_session is null)
            {
                _schemaLoadCts?.Cancel();
                await LoadSchemaAsync(_schemaLoadCts?.Token ?? CancellationToken.None).ConfigureAwait(true);
                return;
            }

            try
            {
                await RefreshParameterSchemaAsync(_session.Adapter,
                    SelectedParameterCategory?.ViewKind, SelectedParameterCategory?.CategoryPath).ConfigureAwait(true);
                ParameterStatus = "已重新读取相机当前参数";
            }
            catch (Exception ex)
            {
                ParameterStatus = $"重新读取失败：{ex.Message}";
            }
        }

        private async System.Threading.Tasks.Task ConnectAsync()
        {
            if (SelectedCamera == null || string.IsNullOrWhiteSpace(SelectedCamera.DeviceId))
                return;

            try
            {
                if (_session != null)
                    return;

                _connectionBusy = true;
                OnPropertyChanged(nameof(ConnectionActionText));
                SelectedCamera.State = CameraState.Connecting;

                var reference = new CameraDeviceRef(SelectedCamera.DeviceId);
                if (_runtime.Runtime.TryGetSession(reference, out var existingSession))
                {
                    _session = existingSession;
                    SelectedCamera.State = CameraState.Connected;
                    ParameterStatus = "设备已连接";
                    return;
                }

                _session = await _runtime.Runtime.OpenAsync(reference, CameraAccessMode.Diagnostic);
                SelectedCamera.State = CameraState.Connected;
                ParameterStatus = "设备已连接";
            }
            catch (Exception ex)
            {
                _session = null;
                SelectedCamera.State = CameraState.Faulted;
                ParameterStatus = $"连接失败：{ex.Message}";
            }
            finally
            {
                _connectionBusy = false;
                OnPropertyChanged(nameof(IsConnected));
                OnPropertyChanged(nameof(ConnectionActionText));
            }
        }

        private async System.Threading.Tasks.Task DisconnectAsync()
        {
            if (SelectedCamera == null || string.IsNullOrWhiteSpace(SelectedCamera.DeviceId))
                return;

            try
            {
                _connectionBusy = true;
                OnPropertyChanged(nameof(ConnectionActionText));
                SelectedCamera.State = CameraState.Connecting;
                await _runtime.Runtime.CloseAsync(new CameraDeviceRef(SelectedCamera.DeviceId));
                _session = null;
                SelectedCamera.State = CameraState.Offline;
                ParameterStatus = "设备已断开";
            }
            catch (Exception ex)
            {
                SelectedCamera.State = _session != null ? CameraState.Connected : CameraState.Faulted;
                ParameterStatus = $"断开失败：{ex.Message}";
            }
            finally
            {
                _connectionBusy = false;
                OnPropertyChanged(nameof(IsConnected));
                OnPropertyChanged(nameof(ConnectionActionText));
            }
        }

        private async System.Threading.Tasks.Task ReleaseSessionAsync()
        {
            var session = _session;
            _session = null;
            if (session != null)
                await _runtime.Runtime.CloseAsync(session.CameraRef).ConfigureAwait(true);
        }

        public async ValueTask DisposeAsync()
        {
            _schemaLoadCts?.Cancel();
            _schemaLoadCts?.Dispose();
            _schemaLoadCts = null;
            _autoApplyCts?.Cancel();
            _autoApplyCts?.Dispose();
            _autoApplyCts = null;
            await _parameterApplyGate.WaitAsync().ConfigureAwait(true);
            try { await ReleaseSessionAsync().ConfigureAwait(true); }
            finally { _parameterApplyGate.Release(); }
        }

        private async Task<bool> SaveParamsAsync()
        {
            if (SelectedCamera == null || string.IsNullOrWhiteSpace(SelectedCamera.DeviceId))
                return false;

            await _parameterApplyGate.WaitAsync().ConfigureAwait(true);

            var traceId = Guid.NewGuid().ToString("N");
            var cameraId = SelectedCamera.DeviceId;
            using var diagnosticScope = CameraDiagnosticScope.Begin(traceId);
            CameraSession? temporarySession = null;
            CameraSession? session = null;
            var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            var writeAttempted = false;
            var wasCapturing = false;
            var pausedForParameterApply = false;
            var previousViewKind = SelectedParameterCategory?.ViewKind;
            var previousCategoryPath = SelectedParameterCategory?.CategoryPath;
            try
            {
                var reference = new CameraDeviceRef(SelectedCamera.DeviceId);
                session = _session;
                if (session == null)
                {
                    temporarySession = await _runtime.Runtime.OpenAsync(reference, CameraAccessMode.Diagnostic);
                    session = temporarySession;
                }

                foreach (var item in Parameters)
                {
                    if (!item.TryGetWritableValue(out var value)
                        || item.Kind is CameraParameterKind.Command)
                        continue;
                    values[item.Name] = value;
                }

                var exposureAutoWasEnabled = IsAutoControlEnabled("ExposureAuto");
                var gainAutoWasEnabled = IsAutoControlEnabled("GainAuto");

                if (values.Count == 0)
                {
                    ParameterStatus = "没有待应用的参数修改";
                    return true;
                }

                var requiresStopped = values.Keys.Any(name => Parameters.FirstOrDefault(item =>
                    string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase))?.RequiresStopped == true);
                wasCapturing = _previewWindow?.IsAcquiring == true;
                if (requiresStopped && wasCapturing && _previewWindow is not null)
                {
                    ParameterStatus = "该参数需要停止采集；正在暂停取流后应用";
                    await _previewWindow.StopAcquisitionAsync();
                    pausedForParameterApply = true;
                }

                var submittedSummary = string.Join("; ", values.Select(pair =>
                {
                    var item = Parameters.FirstOrDefault(parameter =>
                        string.Equals(parameter.Name, pair.Key, StringComparison.OrdinalIgnoreCase));
                    return $"{pair.Key}[{item?.Kind}] {FormatDiagnosticValue(item?.OriginalValue)} -> {FormatDiagnosticValue(pair.Value)}";
                }));
                VisionLogger.Instance.Log(LogLevel.Info,
                    $"[camera-trace={traceId}] 参数提交开始 camera={reference.Value} adapter={session.Adapter.AdapterId} count={values.Count} changes={submittedSummary}",
                    "CameraParameterTrace", target: LogTarget.All);
                writeAttempted = true;
                await session.Adapter.ApplyParametersAsync(values, CancellationToken.None);
                VisionLogger.Instance.Log(LogLevel.Info,
                    $"[camera-trace={traceId}] 适配器参数写入调用完成 camera={reference.Value} count={values.Count}",
                    "CameraParameterTrace", target: LogTarget.All);

                // 普通参数只回读已写节点，避免每次改曝光/增益都重建整棵参数树；Selector/模式参数才刷新完整 Schema。
                IReadOnlyDictionary<string, object?> readbackValues;
                var affectsSchema = values.Keys.Any(name => Parameters.FirstOrDefault(item =>
                    string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase))?.AffectsSchema == true);
                if (affectsSchema)
                    readbackValues = await RefreshParameterSchemaAsync(session.Adapter, previousViewKind, previousCategoryPath);
                else
                {
                    readbackValues = await session.Adapter.ReadParametersAsync(values.Keys.ToArray(), CancellationToken.None);
                    foreach (var pair in values)
                    {
                        if (readbackValues.TryGetValue(pair.Key, out var actual))
                            Parameters.FirstOrDefault(item => string.Equals(item.Name, pair.Key, StringComparison.OrdinalIgnoreCase))
                                ?.AcceptReadback(actual, pair.Value);
                    }
                }
                var readCount = Parameters.Count(item => item.IsReadable && item.HasValue);
                var readbackSummary = string.Join("; ", values.Keys.Select(name =>
                    $"{name}={(readbackValues.TryGetValue(name, out var actualValue) ? FormatDiagnosticValue(actualValue) : "<read-failed-or-unavailable>")}"));
                VisionLogger.Instance.Log(LogLevel.Info,
                    $"[camera-trace={traceId}] 参数设备回读完成 camera={reference.Value} readCount={readCount} values={readbackSummary}",
                    "CameraParameterTrace", target: LogTarget.All);
                var adjustmentNote = GetAutomaticControlNote(values.Keys)
                    + GetManualAutoTransitionNote(values, session.Adapter.AdapterId,
                        exposureAutoWasEnabled, gainAutoWasEnabled);
                // 只刷新设备实际采集配置，不打断可实时应用参数的取流。
                await (_previewWindow?.RefreshConfigurationAsync(traceId) ?? Task.CompletedTask);
                if (pausedForParameterApply && wasCapturing && _previewWindow is not null)
                {
                    await _previewWindow.StartAcquisitionAsync();
                    pausedForParameterApply = false;
                }
                VisionLogger.Instance.Log(LogLevel.Info,
                    $"[camera-trace={traceId}] 参数应用流程完成 camera={reference.Value} previewOpen={_previewWindow is not null}",
                    "CameraParameterTrace", target: LogTarget.All);
                ParameterStatus = $"参数已实时写入并回读：{values.Count} 项{adjustmentNote}";
                return true;
            }
            catch (Exception ex)
            {
                VisionLogger.Instance.Log(LogLevel.Error,
                    $"[camera-trace={traceId}] 参数应用流程失败 camera={cameraId} submitted={string.Join(",", values.Keys)}: {ex.Message}",
                    "CameraParameterTrace", ex, LogTarget.All);
                var refreshNote = string.Empty;
                if (writeAttempted && session is not null)
                {
                    try
                    {
                        await RefreshParameterSchemaAsync(session.Adapter, previousViewKind, previousCategoryPath);
                        refreshNote = "；设备当前值已重新读取";
                    }
                    catch (Exception refreshException)
                    {
                        refreshNote = $"；状态刷新也失败：{refreshException.Message}";
                    }
                }
                ParameterStatus = $"参数写入失败：{ex.Message}{refreshNote}";
                return false;
            }
            finally
            {
                if (pausedForParameterApply && wasCapturing && _previewWindow is not null)
                {
                    try
                    {
                        await _previewWindow.RefreshConfigurationAsync(traceId);
                        await _previewWindow.StartAcquisitionAsync();
                    }
                    catch (Exception resumeException)
                    {
                        ParameterStatus += $"；恢复采集失败：{resumeException.Message}";
                    }
                }
                if (temporarySession != null)
                    await _runtime.Runtime.CloseAsync(temporarySession.CameraRef);
                _parameterApplyGate.Release();
            }
        }

        private static string FormatDiagnosticValue(object? value)
        {
            var text = Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? "<null>";
            return text.Length <= 96 ? text : text[..93] + "...";
        }

        private async Task<IReadOnlyDictionary<string, object?>> RefreshParameterSchemaAsync(
            ICameraAdapter adapter, string? preferredViewKind, string? preferredCategoryPath)
        {
            var schema = await adapter.GetParameterSchemaAsync(CancellationToken.None);
            var names = schema.Parameters
                .Where(item => item.Value is null && item.IsReadable && item.Kind != CameraParameterKind.Command)
                .Select(item => item.Name)
                .ToArray();
            var values = await adapter.ReadParametersAsync(names, CancellationToken.None);
            ClearParameterItems();
            foreach (var descriptor in schema.Parameters)
            {
                values.TryGetValue(descriptor.Name, out var value);
                var item = new CameraParameterItemViewModel(descriptor, value ?? descriptor.Value);
                item.PropertyChanged += OnParameterValueChanged;
                Parameters.Add(item);
            }
            BuildParameterTree(preferredViewKind, preferredCategoryPath);
            _parameterSchemaLoaded = true;
            return values;
        }

        private void ClearParameterItems()
        {
            foreach (var item in Parameters)
                item.PropertyChanged -= OnParameterValueChanged;
            Parameters.Clear();
        }

        private string GetAutomaticControlNote(IEnumerable<string> changedNames)
        {
            var changed = new HashSet<string>(changedNames, StringComparer.OrdinalIgnoreCase);
            var notes = new List<string>();
            if ((changed.Contains("ExposureTime") || changed.Contains("ExposureTimeRaw"))
                && IsAutoControlEnabled("ExposureAuto"))
                notes.Add("自动曝光仍开启，手动曝光值可能不会直接决定每帧亮度");
            if ((changed.Contains("Gain") || changed.Contains("GainRaw"))
                && IsAutoControlEnabled("GainAuto"))
                notes.Add("自动增益仍开启，手动增益值可能不会直接决定每帧亮度");
            return notes.Count == 0 ? string.Empty : "；注意：" + string.Join("；", notes);
        }

        private bool IsAutoControlEnabled(string parameterName)
        {
            var value = Parameters.FirstOrDefault(item => string.Equals(item.Name, parameterName, StringComparison.OrdinalIgnoreCase))?.Value;
            if (value is null)
                return false;
            if (value is bool boolean)
                return boolean;
            var text = Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
            if (text is "0" || string.Equals(text, "Off", StringComparison.OrdinalIgnoreCase)
                || string.Equals(text, "关闭", StringComparison.OrdinalIgnoreCase))
                return false;
            return true;
        }

        private string GetManualAutoTransitionNote(
            IReadOnlyDictionary<string, object?> changedValues,
            string adapterId,
            bool exposureAutoWasEnabled,
            bool gainAutoWasEnabled)
        {
            if (!string.Equals(adapterId, "hikvision-mvs", StringComparison.OrdinalIgnoreCase))
                return string.Empty;

            var notes = new List<string>();
            if ((changedValues.ContainsKey("ExposureTime") || changedValues.ContainsKey("ExposureTimeRaw"))
                && !changedValues.ContainsKey("ExposureAuto")
                && exposureAutoWasEnabled && !IsAutoControlEnabled("ExposureAuto"))
                notes.Add("已按海康示例关闭自动曝光以应用手动曝光");
            if ((changedValues.ContainsKey("Gain") || changedValues.ContainsKey("GainRaw"))
                && !changedValues.ContainsKey("GainAuto")
                && gainAutoWasEnabled && !IsAutoControlEnabled("GainAuto"))
                notes.Add("已按海康示例关闭自动增益以应用手动增益");
            return notes.Count == 0 ? string.Empty : "；" + string.Join("；", notes);
        }

        public static string LocalizeCategory(string value)
        {
            if (CategoryNames.TryGetValue(value, out var localized))
                return localized;

            var normalized = value.Replace('_', ' ').Trim();
            return CategoryNames.TryGetValue(normalized, out localized) ? localized : normalized;
        }

        public static string LocalizeParameter(string name, string displayName)
        {
            if (ParameterNames.TryGetValue(name, out var localized))
                return localized;
            if (string.IsNullOrWhiteSpace(displayName))
                return name;

            // SDK 未提供本地化名称时，优先展示可读的中文映射；未知节点保留原名，避免误译。
            return TranslateKnownTerms(displayName);
        }

        public static string LocalizeEnumDisplay(string value)
        {
            return EnumDisplayNames.TryGetValue(value, out var translated) ? translated : TranslateKnownTerms(value);
        }

        private static string TranslateKnownTerms(string text)
        {
            var normalized = text.Trim();
            var words = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Acquisition"] = "采集", ["Frame"] = "帧", ["Count"] = "数量", ["Burst"] = "连续",
                ["Line"] = "行", ["Rate"] = "频率", ["Resulting"] = "实际", ["Reference"] = "参考",
                ["Enable"] = "启用", ["Mode"] = "模式", ["Device"] = "设备", ["Type"] = "类型",
                ["Scan"] = "扫描", ["Format"] = "格式", ["Pixel"] = "像素", ["Trigger"] = "触发",
                ["Source"] = "源", ["Selector"] = "选择", ["Exposure"] = "曝光", ["Time"] = "时间",
                ["Gain"] = "增益", ["Auto"] = "自动", ["Width"] = "宽度", ["Height"] = "高度",
                ["Offset"] = "偏移", ["Control"] = "控制", ["Value"] = "值", ["Index"] = "索引",
                ["Save"] = "保存", ["Load"] = "加载", ["Temperature"] = "温度", ["Status"] = "状态"
            };
            var result = System.Text.RegularExpressions.Regex.Replace(normalized, "([a-z])([A-Z])", "$1 $2");
            var tokens = result.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0)
                return text;
            var translated = tokens.Select(token => words.TryGetValue(token, out var value) ? value : token).ToArray();
            return string.Join(string.Empty, translated);
        }
    }

    public sealed class CameraParameterItemViewModel : ViewModelBase
    {
        private object? _value;
        private object? _originalValue;
        private readonly object? _minimum;
        private readonly object? _maximum;
        private readonly object? _increment;

        public CameraParameterItemViewModel(CameraParameterDescriptor descriptor, object? value)
        {
            Name = descriptor.Name;
            DisplayName = CameraDetailViewModel.LocalizeParameter(descriptor.Name, descriptor.DisplayName);
            SourceOrder = descriptor.SourceOrder;
            StandardFeature = descriptor.StandardFeature;
            Category = descriptor.Category;
            Kind = descriptor.Kind;
            IsAvailable = descriptor.IsAvailable;
            IsImplemented = descriptor.IsImplemented;
            IsReadable = descriptor.IsReadable && descriptor.IsAvailable && descriptor.IsImplemented;
            IsWritable = descriptor.IsWritable && descriptor.IsAvailable && descriptor.IsImplemented;
            RequiresStopped = descriptor.RequiresStopped;
            AffectsSchema = descriptor.AffectsSchema;
            StatusMessage = descriptor.StatusMessage;
            if (value is null && IsReadable && Kind != CameraParameterKind.Command)
                StatusMessage ??= "当前值读取失败";
            EnumValues = descriptor.EnumValues
                .Select(item => item with { DisplayName = CameraDetailViewModel.LocalizeEnumDisplay(item.DisplayName) })
                .ToArray();
            Unit = descriptor.Description ?? string.Empty;
            _minimum = descriptor.Minimum;
            _maximum = descriptor.Maximum;
            _increment = descriptor.Increment;
            _value = value;
            _originalValue = value;
        }

        public string Name { get; }
        public int SourceOrder { get; }
        public bool IsExpanded { get; set; }
        public string DisplayName { get; }
        public string? StandardFeature { get; }
        public string Category { get; }
        public CameraParameterKind Kind { get; }
        public bool IsReadOnly => !IsEditable;
        public bool IsAvailable { get; }
        public bool IsImplemented { get; }
        public bool IsReadable { get; }
        public bool IsWritable { get; }
        public bool RequiresStopped { get; }
        public bool AffectsSchema { get; }
        public string? StatusMessage { get; private set; }
        public bool IsCommand => Kind == CameraParameterKind.Command;
        public bool CanExecuteCommand => IsCommand && IsAvailable && IsWritable
            && StandardFeature is not ("acquisition.start" or "acquisition.stop" or "trigger.software");
        public string CommandActionText => CanExecuteCommand ? "执行" : "由上方操作栏控制";
        public bool IsEditable => IsWritable && !IsCommand && Value is not null
            && (Kind != CameraParameterKind.Enumeration || EnumValues.Count > 1)
            && Kind != CameraParameterKind.Custom;
        public bool HasValue => Value is not null;
        public bool IsBoolean => Kind == CameraParameterKind.Boolean && IsEditable;
        public bool IsEnumeration => Kind == CameraParameterKind.Enumeration && EnumValues.Count > 1 && IsEditable;
        public bool IsNumeric => (Kind is CameraParameterKind.Integer or CameraParameterKind.Double) && IsEditable;
        public bool IsReadOnlyValue => !IsEditable && Value is not null
            && Kind is CameraParameterKind.Boolean or CameraParameterKind.Integer or CameraParameterKind.Double or CameraParameterKind.Enumeration;
        public bool IsTextValue => Kind is CameraParameterKind.String or CameraParameterKind.Custom
            || (Kind == CameraParameterKind.Enumeration && EnumValues.Count == 0)
            || (Value is null && !IsCommand)
            || IsReadOnlyValue;
        public bool IsDirty => !Equals(Value, _originalValue);
        public object? OriginalValue => _originalValue;
        public string ValueText
        {
            get
            {
                if (Value is null)
                    return StatusMessage ?? "不可读取";
                if (Kind == CameraParameterKind.Boolean)
                    return Convert.ToBoolean(Value, System.Globalization.CultureInfo.InvariantCulture) ? "开启" : "关闭";
                if (Kind == CameraParameterKind.Enumeration)
                {
                    var raw = Convert.ToString(Value, System.Globalization.CultureInfo.InvariantCulture);
                    return EnumValues.FirstOrDefault(item => string.Equals(item.Value, raw, StringComparison.OrdinalIgnoreCase))?.DisplayName
                        ?? raw ?? string.Empty;
                }
                return Convert.ToString(Value, System.Globalization.CultureInfo.CurrentCulture) ?? string.Empty;
            }
            set => Value = value;
        }
        public string ToolTipText
        {
            get
            {
                var status = StatusMessage;
                if (string.IsNullOrWhiteSpace(status) && !IsWritable)
                    status = "设备将此参数标记为只读";
                else if (string.IsNullOrWhiteSpace(status) && Kind == CameraParameterKind.Enumeration && EnumValues.Count <= 1)
                    status = EnumValues.Count == 0
                        ? "设备未提供可选枚举项，当前仅显示实际值"
                        : "设备当前仅提供一个可选值，无需下拉选择";
                return string.IsNullOrWhiteSpace(status) ? Unit : $"{Unit}{Environment.NewLine}{status}";
            }
        }

        public bool BooleanValue
        {
            get => Convert.ToBoolean(Value, System.Globalization.CultureInfo.InvariantCulture);
            set => Value = value;
        }
        public IReadOnlyList<CameraEnumValue> EnumValues { get; }
        public string Unit { get; }
        public double MinimumValue => ToDouble(_minimum, 0);
        public double MaximumValue => ToDouble(_maximum, 1_000_000);
        public double IncrementValue => Math.Max(0.0001, ToDouble(_increment, Kind == CameraParameterKind.Integer ? 1 : 0.1));
        public double LargeIncrementValue => Math.Max(IncrementValue, IncrementValue * 10);
        public int DecimalPlaces => CalculateDecimalPlaces(IncrementValue);
        public double NumericValue
        {
            get => ToDouble(Value, 0);
            set => Value = Kind == CameraParameterKind.Integer ? (long)Math.Round(value) : value;
        }
        public object? Value
        {
            get => _value;
            set
            {
                if (SetProperty(ref _value, value))
                {
                    OnPropertyChanged(nameof(BooleanValue));
                    OnPropertyChanged(nameof(NumericValue));
                    OnPropertyChanged(nameof(ValueText));
                    OnPropertyChanged(nameof(IsDirty));
                    OnPropertyChanged(nameof(HasValue));
                    OnPropertyChanged(nameof(IsEditable));
                    OnPropertyChanged(nameof(IsReadOnly));
                    OnPropertyChanged(nameof(IsBoolean));
                    OnPropertyChanged(nameof(IsEnumeration));
                    OnPropertyChanged(nameof(IsNumeric));
                    OnPropertyChanged(nameof(IsReadOnlyValue));
                    OnPropertyChanged(nameof(IsTextValue));
                }
            }
        }

        private static double ToDouble(object? value, double fallback)
            => value is null ? fallback : Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture);

        private static int CalculateDecimalPlaces(double value)
        {
            var text = value.ToString("0.########", System.Globalization.CultureInfo.InvariantCulture);
            var separator = text.IndexOf('.');
            return separator < 0 ? 0 : text.Length - separator - 1;
        }

        public bool TryGetWritableValue(out object? value)
        {
            value = null;
            if (!IsEditable || Value is null || !IsDirty)
                return false;

            if (Kind != CameraParameterKind.String && Value is string text && string.IsNullOrWhiteSpace(text))
                return false;

            try
            {
                value = Kind switch
                {
                    CameraParameterKind.Integer => Convert.ToInt64(Value, System.Globalization.CultureInfo.InvariantCulture),
                    CameraParameterKind.Double => Convert.ToDouble(Value, System.Globalization.CultureInfo.InvariantCulture),
                    CameraParameterKind.Boolean => Convert.ToBoolean(Value, System.Globalization.CultureInfo.InvariantCulture),
                    _ => Value
                };
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
            catch (InvalidCastException)
            {
                return false;
            }
        }

        public void AcceptReadback(object? actualValue, object? submittedValue)
        {
            _originalValue = actualValue;
            if (ValuesEqual(Value, submittedValue))
                Value = actualValue;
            OnPropertyChanged(nameof(OriginalValue));
            OnPropertyChanged(nameof(IsDirty));
        }

        private static bool ValuesEqual(object? left, object? right)
            => left is null || right is null
                ? left is null && right is null
                : string.Equals(Convert.ToString(left, System.Globalization.CultureInfo.InvariantCulture),
                    Convert.ToString(right, System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);

        public void SetDeviceValue(object? value)
        {
            if (value is null)
                return;

            Value = Kind == CameraParameterKind.Enumeration
                ? Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)
                : value;
            _originalValue = Value;
            OnPropertyChanged(nameof(IsDirty));
        }
    }

    public sealed class CameraParameterGroupViewModel : ViewModelBase
    {
        private bool _isExpanded;
        private bool _isSelected;

        public CameraParameterGroupViewModel(
            string name, string? categoryPath = null, string? viewKind = null, int sourceOrder = int.MaxValue)
        {
            Name = name;
            CategoryPath = categoryPath;
            ViewKind = viewKind;
            SourceOrder = sourceOrder;
        }

        public string Name { get; }
        public string? CategoryPath { get; }
        public string? ViewKind { get; }
        public int SourceOrder { get; set; }
        public bool IsExpanded
        {
            get => _isExpanded;
            set => SetProperty(ref _isExpanded, value);
        }
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }
        public ObservableCollection<CameraParameterGroupViewModel> Children { get; } = new();
        public ObservableCollection<CameraParameterItemViewModel> Parameters { get; } = new();
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
