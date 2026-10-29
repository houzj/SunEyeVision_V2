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
            ["Analog Controls"] = "模拟控制", ["Image Format Controls"] = "图像格式",
            ["AOI Controls"] = "感兴趣区域 (AOI)", ["Image Quality Controls"] = "图像质量",
            ["Acquisition Controls"] = "采集控制", ["Digital I/O Controls"] = "数字 I/O 控制",
            ["Sequence Control"] = "序列控制", ["Counter and Timer Controls"] = "计数器与定时器",
            ["Basler Light Control"] = "巴斯勒光源控制", ["LUT Controls"] = "查找表 (LUT)",
            ["User Defined Values"] = "用户自定义值", ["Device Information"] = "设备信息",
            ["Chunk Data Streams"] = "附加数据流", ["Events Generation"] = "事件生成",
            ["Configuration Sets"] = "配置集", ["Transport Layer"] = "传输层",
            ["Auto Functions"] = "自动功能", ["Remove Parameter Limits"] = "解除参数限制",
            ["Expert Feature Access"] = "专家功能访问"
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
        private readonly CameraRuntimeManager _runtime;
        private CameraSession? _session;
        private CameraPreviewWindow? _previewWindow;
        private bool _previewOpening;
        private bool _changingCamera;
        private bool _hasPendingCamera;
        private CameraDevice? _pendingCamera;
        private bool _connectionBusy;
        private int _connectionOperationVersion;
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
                if (_changingCamera)
                {
                    // Synchronous observers may select again while the old tree is retiring.
                    // Keep the latest intent and apply it before reopening edit commands.
                    _pendingCamera = value;
                    _hasPendingCamera = true;
                    return;
                }
                if (ReferenceEquals(_selectedCamera, value)) return;
                _changingCamera = true;
                try
                {
                    var next = value;
                    while (true)
                    {
                        _hasPendingCamera = false;
                        if (!ReferenceEquals(_selectedCamera, next))
                        {
                            var oldAutoApply = _autoApplyCts;
                            _autoApplyCts = null;
                            var oldSchemaLoad = _schemaLoadCts;
                            _schemaLoadCts = null;
                            oldAutoApply?.Cancel(); oldAutoApply?.Dispose();
                            oldSchemaLoad?.Cancel(); oldSchemaLoad?.Dispose();
                            _schemaLoadCts = new CancellationTokenSource();
                            ClearParameterItems();
                            ParameterCategories.Clear();
                            SelectedParameters.Clear();
                            SelectedParameterCategory = null;
                            _parameterSchemaLoaded = false;
                            SetProperty(ref _selectedCamera, next);
                            IsCameraSelected = next != null;
                            OnPropertyChanged(nameof(IsConnected));
                            OnPropertyChanged(nameof(ConnectionActionText));
                            LoadManufacturerParams();
                        }
                        if (!_hasPendingCamera) break;
                        next = _pendingCamera!;
                    }
                }
                finally
                {
                    _pendingCamera = null;
                    _hasPendingCamera = false;
                    _changingCamera = false;
                }
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

        public bool IsConnected => _session is { } session && SelectedCamera is { } camera
            && string.Equals(session.CameraRef.Value, camera.DeviceId, StringComparison.OrdinalIgnoreCase)
            && _runtime.TryGetSession(session.CameraRef, out var current) && ReferenceEquals(session, current);
        public string ConnectionActionText => _connectionBusy
            ? "连接中..."
            : IsConnected ? "断开" : "连接";

        public CameraDetailViewModel(CameraRuntimeManager runtime)
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
                var session = await _runtime.OpenAsync(
                    reference, CameraAccessMode.Diagnostic, cancellationToken: cancellationToken).ConfigureAwait(true);
                try
                {
                    using var operation = _runtime.AcquireSessionOperationLease(session, "读取设备参数");
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
                    await session.DisposeAsync().ConfigureAwait(true);
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
            if (_changingCamera || item is not { IsCommand: true, IsAvailable: true, IsWritable: true, CanExecuteCommand: true }
                || SelectedCamera is null)
                return;

            var camera = SelectedCamera;
            var traceId = Guid.NewGuid().ToString("N");
            var cameraId = camera.DeviceId;
            using var diagnosticScope = CameraDiagnosticScope.Begin(traceId);
            CameraSession? temporarySession = null;
            try
            {
                var reference = new CameraDeviceRef(cameraId);
                if (!_runtime.TryGetSession(reference, out var session))
                {
                    temporarySession = await _runtime.OpenAsync(reference, CameraAccessMode.Diagnostic);
                    session = temporarySession;
                }
                using var operation = _runtime.AcquireSessionOperationLease(session, "执行设备命令");
                EnsureParameterSessionCurrent(camera, session);
                VisionLogger.Instance.Log(LogLevel.Info,
                    $"[camera-trace={traceId}] 相机命令开始 camera={cameraId} adapter={session.Adapter.AdapterId} node={item.Name}",
                    "CameraParameterTrace", target: LogTarget.All);
                await session.Adapter.ExecuteCommandAsync(item.Name, CancellationToken.None);
                VisionLogger.Instance.Log(LogLevel.Info,
                    $"[camera-trace={traceId}] 相机命令完成 camera={cameraId} node={item.Name}",
                    "CameraParameterTrace", target: LogTarget.All);
                if (ReferenceEquals(SelectedCamera, camera)) ParameterStatus = $"命令已执行：{item.DisplayName}";
            }
            catch (Exception ex)
            {
                VisionLogger.Instance.Log(LogLevel.Error,
                    $"[camera-trace={traceId}] 相机命令失败 camera={cameraId} node={item.Name}: {ex.Message}",
                    "CameraParameterTrace", ex, LogTarget.All);
                if (ReferenceEquals(SelectedCamera, camera)) ParameterStatus = $"命令执行失败：{ex.Message}";
            }
            finally
            {
                if (temporarySession is not null)
                {
                    try { await temporarySession.DisposeAsync(); }
                    catch (Exception cleanup)
                    {
                        VisionLogger.Instance.Log(LogLevel.Error, "相机命令临时连接关闭失败：" + cleanup.Message,
                            "CameraParameterTrace", cleanup, LogTarget.All);
                        if (ReferenceEquals(SelectedCamera, camera)) ParameterStatus += "；临时连接关闭失败：" + cleanup.Message;
                    }
                }
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

        private void Preview() => _ = PreviewAsync();

        internal async Task PreviewAsync()
        {
            if (_previewWindow is { IsVisible: true }) { _previewWindow.Activate(); return; }
            if (_changingCamera || _previewOpening || _connectionBusy) return;
            var camera = SelectedCamera;
            if (camera == null || string.IsNullOrWhiteSpace(camera.DeviceId)) return;
            _previewOpening = true;
            CameraSession? session = null;
            CameraPreviewWindow? window = null;
            var ownsSession = false;
            try
            {
                var reference = new CameraDeviceRef(camera.DeviceId);
                (session, ownsSession) = await ConnectAsync().ConfigureAwait(true);
                if (session is null)
                {
                    if (ReferenceEquals(SelectedCamera, camera))
                        MessageBox.Show(ParameterStatus, "相机连接失败", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                if (!ReferenceEquals(SelectedCamera, camera))
                {
                    if (ownsSession) await CloseAbandonedPreviewAsync(camera, session).ConfigureAwait(true);
                    return;
                }

                if (!_parameterSchemaLoaded)
                {
                    ParameterStatus = "设备已连接，正在重新读取参数…";
                    try
                    {
                        await RefreshParameterSchemaAsync(session, camera,
                            SelectedParameterCategory?.ViewKind, SelectedParameterCategory?.CategoryPath).ConfigureAwait(true);
                        if (!ReferenceEquals(SelectedCamera, camera))
                        {
                            if (ownsSession) await CloseAbandonedPreviewAsync(camera, session).ConfigureAwait(true);
                            return;
                        }
                        _parameterSchemaLoaded = true;
                        var availableCount = Parameters.Count(item => item.IsAvailable && item.IsImplemented);
                        var readableCount = Parameters.Count(item => item.IsReadable && item.HasValue);
                        var editableCount = Parameters.Count(item => item.IsEditable);
                        ParameterStatus = $"已加载 {availableCount} 项可用参数 · {readableCount} 项可读 · {editableCount} 项可编辑";
                    }
                    catch (Exception ex)
                    {
                        if (ReferenceEquals(SelectedCamera, camera))
                        {
                            _parameterSchemaLoaded = false;
                            ParameterStatus = $"已连接，但参数读取失败：{ex.Message}";
                        }
                    }
                }
                if (!ReferenceEquals(SelectedCamera, camera)
                    || !_runtime.TryGetSession(reference, out var current) || !ReferenceEquals(current, session))
                {
                    if (ownsSession) await CloseAbandonedPreviewAsync(camera, session).ConfigureAwait(true);
                    return;
                }

                window = new CameraPreviewWindow(_runtime, reference, session, camera, ownsSession, this);
                _previewWindow = window;
                window.Closed += (_, _) =>
                {
                    if (ReferenceEquals(_previewWindow, window)) _previewWindow = null;
                };
                var owner = Application.Current?.Windows.OfType<Window>()
                    .FirstOrDefault(item => item.DataContext is CameraManagerViewModel manager
                        && ReferenceEquals(manager.CameraDetailViewModel, this));
                if (owner != null) window.Owner = owner;
                try { window.ShowDialog(); }
                finally { await window.CloseResourcesAsync().ConfigureAwait(true); }
            }
            catch (Exception error)
            {
                Exception failure = error;
                if (window is not null)
                {
                    try { await window.CloseResourcesAsync().ConfigureAwait(true); }
                    catch (Exception cleanup) { if (!ReferenceEquals(error, cleanup)) failure = new AggregateException(error, cleanup); }
                }
                else if (session is not null && ownsSession)
                    await CloseAbandonedPreviewAsync(camera, session).ConfigureAwait(true);
                VisionLogger.Instance.Log(LogLevel.Error, "相机预览打开失败：" + failure.Message,
                    "CameraPreviewTrace", failure, LogTarget.All);
                if (ReferenceEquals(SelectedCamera, camera)) ParameterStatus = "预览打开失败：" + failure.Message;
            }
            finally
            {
                if (ReferenceEquals(_previewWindow, window)) _previewWindow = null;
                _previewOpening = false;
            }
        }

        private async Task CloseAbandonedPreviewAsync(CameraDevice camera, CameraSession session)
        {
            try { await ClosePreviewSessionAsync(camera, session, true).ConfigureAwait(true); }
            catch (Exception error)
            {
                VisionLogger.Instance.Log(LogLevel.Error, "预览取消后的相机连接清理失败：" + error.Message,
                    "CameraPreviewTrace", error, LogTarget.All);
                if (ReferenceEquals(SelectedCamera, camera)) ParameterStatus = "预览连接清理失败：" + error.Message;
            }
        }

        public async Task ClosePreviewSessionAsync(CameraDevice camera, CameraSession session, bool ownsSession)
        {
            if (ReferenceEquals(SelectedCamera, camera) && ReferenceEquals(_session, session))
            {
                _autoApplyCts?.Cancel();
                _autoApplyCts?.Dispose();
                _autoApplyCts = null;
            }
            await _parameterApplyGate.WaitAsync().ConfigureAwait(true);
            try
            {
                if (ownsSession)
                {
                    try { await session.DisposeAsync().ConfigureAwait(true); }
                    finally
                    {
                        if (ReferenceEquals(_session, session)
                            && (!_runtime.TryGetSession(session.CameraRef, out var current)
                                || !ReferenceEquals(current, session))) _session = null;
                    }
                }
                var stillConnected = _runtime.TryGetSession(session.CameraRef, out var retained);
                if (stillConnected)
                {
                    if (ReferenceEquals(retained, session)) camera.State = retained.Adapter.State;
                }
                else camera.State = CameraState.Offline;
                if (ReferenceEquals(SelectedCamera, camera))
                    ParameterStatus = stillConnected ? "预览已关闭，设备保持连接" : "预览已关闭，相机连接已释放";
                OnPropertyChanged(nameof(IsConnected));
                OnPropertyChanged(nameof(ConnectionActionText));
            }
            finally { _parameterApplyGate.Release(); }
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
            var camera = SelectedCamera;
            if (camera is null || string.IsNullOrWhiteSpace(camera.DeviceId)) return;
            var reference = new CameraDeviceRef(camera.DeviceId);
            if (!_runtime.TryGetSession(reference, out var session))
            {
                // A selected device must never borrow the previously selected device's session.
                if (_session is not null && !string.Equals(_session.CameraRef.Value, reference.Value, StringComparison.OrdinalIgnoreCase))
                    _session = null;
                _schemaLoadCts?.Cancel();
                _schemaLoadCts?.Dispose();
                _schemaLoadCts = new CancellationTokenSource();
                await LoadSchemaAsync(_schemaLoadCts.Token).ConfigureAwait(true);
                return;
            }
            _session = session;
            OnPropertyChanged(nameof(IsConnected));
            OnPropertyChanged(nameof(ConnectionActionText));
            try
            {
                await RefreshParameterSchemaAsync(session, camera,
                    SelectedParameterCategory?.ViewKind, SelectedParameterCategory?.CategoryPath).ConfigureAwait(true);
                if (ReferenceEquals(SelectedCamera, camera)) ParameterStatus = "已重新读取相机当前参数";
            }
            catch (Exception ex)
            {
                if (ReferenceEquals(SelectedCamera, camera)) ParameterStatus = $"重新读取失败：{ex.Message}";
            }
        }

        private async Task<(CameraSession? Session, bool Opened)> ConnectAsync()
        {
            var camera = SelectedCamera;
            if (camera == null || string.IsNullOrWhiteSpace(camera.DeviceId)) return (null, false);
            var reference = new CameraDeviceRef(camera.DeviceId);
            var operation = ++_connectionOperationVersion;
            try
            {
                _connectionBusy = true;
                OnPropertyChanged(nameof(ConnectionActionText));
                camera.State = CameraState.Connecting;
                var opened = !_runtime.TryGetSession(reference, out var existing);
                var session = opened
                    ? await _runtime.OpenAsync(reference, CameraAccessMode.Diagnostic).ConfigureAwait(true)
                    : existing;
                if (_connectionOperationVersion != operation)
                {
                    if (opened)
                    {
                        try { await session.DisposeAsync().ConfigureAwait(true); }
                        catch (Exception error)
                        {
                            VisionLogger.Instance.Log(LogLevel.Error, "过期相机连接清理失败：" + error.Message,
                                "CameraPreviewTrace", error, LogTarget.All);
                        }
                    }
                    return (null, false);
                }
                camera.State = CameraState.Connected;
                if (ReferenceEquals(SelectedCamera, camera))
                {
                    _session = session;
                    ParameterStatus = "设备已连接";
                }
                return (session, opened);
            }
            catch (Exception ex)
            {
                camera.State = CameraState.Faulted;
                if (ReferenceEquals(SelectedCamera, camera))
                {
                    if (_session is { } old && string.Equals(old.CameraRef.Value, reference.Value, StringComparison.OrdinalIgnoreCase))
                        _session = null;
                    ParameterStatus = $"连接失败：{ex.Message}";
                }
                return (null, false);
            }
            finally
            {
                if (_connectionOperationVersion == operation) _connectionBusy = false;
                OnPropertyChanged(nameof(IsConnected));
                OnPropertyChanged(nameof(ConnectionActionText));
            }
        }

        private async System.Threading.Tasks.Task DisconnectAsync()
        {
            var camera = SelectedCamera;
            if (camera == null || string.IsNullOrWhiteSpace(camera.DeviceId)) return;
            var reference = new CameraDeviceRef(camera.DeviceId);
            var session = _runtime.TryGetSession(reference, out var current) ? current : null;
            var operation = ++_connectionOperationVersion;
            try
            {
                _connectionBusy = true;
                OnPropertyChanged(nameof(ConnectionActionText));
                camera.State = CameraState.Connecting;
                if (session is not null) await session.DisposeAsync().ConfigureAwait(true);
                if (ReferenceEquals(_session, session)) _session = null;
                camera.State = CameraState.Offline;
                if (ReferenceEquals(SelectedCamera, camera)) ParameterStatus = "设备已断开";
            }
            catch (Exception ex)
            {
                var retained = _runtime.TryGetSession(reference, out var live) && ReferenceEquals(session, live);
                if (!retained && ReferenceEquals(_session, session)) _session = null;
                camera.State = retained ? session!.Adapter.State : CameraState.Faulted;
                if (ReferenceEquals(SelectedCamera, camera)) ParameterStatus = $"断开失败：{ex.Message}";
            }
            finally
            {
                if (_connectionOperationVersion == operation) _connectionBusy = false;
                OnPropertyChanged(nameof(IsConnected));
                OnPropertyChanged(nameof(ConnectionActionText));
            }
        }

        private async System.Threading.Tasks.Task ReleaseSessionAsync()
        {
            var session = _session;
            if (session != null)
            {
                try { await session.DisposeAsync().ConfigureAwait(true); }
                finally
                {
                    if (ReferenceEquals(_session, session)
                        && (!_runtime.TryGetSession(session.CameraRef, out var current)
                            || !ReferenceEquals(current, session))) _session = null;
                }
            }
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
            var camera = SelectedCamera;
            if (_changingCamera || camera == null || string.IsNullOrWhiteSpace(camera.DeviceId))
                return false;

            await _parameterApplyGate.WaitAsync().ConfigureAwait(true);

            var traceId = Guid.NewGuid().ToString("N");
            var cameraId = camera.DeviceId;
            CameraSession? temporarySession = null;
            CameraSession? session = null;
            CameraPreviewWindow? preview = null;
            var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            var writeAttempted = false;
            var wasCapturing = false;
            var pausedForParameterApply = false;
            var captureConfigurationChanged = false;
            var previousViewKind = SelectedParameterCategory?.ViewKind;
            var previousCategoryPath = SelectedParameterCategory?.CategoryPath;
            try
            {
                using var diagnosticScope = CameraDiagnosticScope.Begin(traceId);
                if (!ReferenceEquals(SelectedCamera, camera)) return false;
                var reference = new CameraDeviceRef(cameraId);
                if (_runtime.TryGetSession(reference, out var currentSession))
                {
                    session = currentSession;
                    _session = currentSession;
                }
                else
                {
                    temporarySession = await _runtime.OpenAsync(reference, CameraAccessMode.Diagnostic);
                    session = temporarySession;
                }
                using var operation = _runtime.AcquireSessionOperationLease(session, "应用设备参数");
                EnsureParameterSessionCurrent(camera, session);
                if (_previewWindow is { } currentPreview && currentPreview.Matches(camera, session))
                    preview = currentPreview;

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

                captureConfigurationChanged = values.Keys.Any(name => name is
                    "AcquisitionMode" or "TriggerMode" or "TriggerSource" or "TriggerSelector");
                var requiresStopped = captureConfigurationChanged || values.Keys.Any(name => Parameters.FirstOrDefault(item =>
                    string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase))?.RequiresStopped == true);
                wasCapturing = preview?.IsAcquiring == true;
                if (requiresStopped && wasCapturing && preview is not null)
                {
                    ParameterStatus = captureConfigurationChanged
                        ? "触发或采集模式已修改；正在停止旧取流并重新读取设备配置"
                        : "该参数需要停止采集；正在暂停取流后应用";
                    VisionLogger.Instance.Log(LogLevel.Info,
                        $"[camera-trace={traceId}] 参数应用前暂停取流 camera={reference.Value} captureConfigurationChanged={captureConfigurationChanged}",
                        "CameraParameterTrace", target: LogTarget.All);
                    await preview.StopAcquisitionAsync();
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
                EnsureParameterSessionCurrent(camera, session);
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
                    readbackValues = await RefreshParameterSchemaAsync(session, camera, previousViewKind, previousCategoryPath);
                else
                {
                    readbackValues = await session.Adapter.ReadParametersAsync(values.Keys.ToArray(), CancellationToken.None);
                    EnsureParameterSessionCurrent(camera, session);
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
                // Topology changes have already stopped the old request; ordinary live parameters do not interrupt it.
                await (preview?.RefreshConfigurationAsync(traceId) ?? Task.CompletedTask);
                if (pausedForParameterApply && wasCapturing && preview is not null)
                {
                    // Do not let the error path retry a failed restart with an uncertain configuration.
                    pausedForParameterApply = false;
                    await preview.StartAcquisitionAsync();
                    VisionLogger.Instance.Log(LogLevel.Info,
                        $"[camera-trace={traceId}] 参数回读后已按新配置恢复取流 camera={reference.Value}",
                        "CameraParameterTrace", target: LogTarget.All);
                }
                VisionLogger.Instance.Log(LogLevel.Info,
                    $"[camera-trace={traceId}] 参数应用流程完成 camera={reference.Value} previewOpen={preview is not null}",
                    "CameraParameterTrace", target: LogTarget.All);
                if (ReferenceEquals(SelectedCamera, camera)) ParameterStatus = $"参数已实时写入并回读：{values.Count} 项{adjustmentNote}";
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
                        await RefreshParameterSchemaAsync(session, camera, previousViewKind, previousCategoryPath);
                        if (captureConfigurationChanged && preview is not null)
                            await preview.RefreshConfigurationAsync(traceId);
                        refreshNote = "；设备当前值已重新读取";
                    }
                    catch (Exception refreshException)
                    {
                        refreshNote = $"；状态刷新也失败：{refreshException.Message}";
                    }
                }
                if (ReferenceEquals(SelectedCamera, camera)) ParameterStatus = $"参数写入失败：{ex.Message}{refreshNote}";
                return false;
            }
            finally
            {
                try
                {
                    if (pausedForParameterApply && wasCapturing && preview is not null
                        && !captureConfigurationChanged)
                    {
                        try
                        {
                            await preview.RefreshConfigurationAsync(traceId);
                            await preview.StartAcquisitionAsync();
                        }
                        catch (Exception resumeException)
                        {
                            if (ReferenceEquals(SelectedCamera, camera)) ParameterStatus += $"；恢复采集失败：{resumeException.Message}";
                        }
                    }
                    if (temporarySession != null)
                    {
                        try { await temporarySession.DisposeAsync(); }
                        catch (Exception cleanup)
                        {
                            if (ReferenceEquals(SelectedCamera, camera)) ParameterStatus += $"；临时相机关闭失败：{cleanup.Message}";
                            throw;
                        }
                    }
                }
                finally { _parameterApplyGate.Release(); }
            }
        }

        private static string FormatDiagnosticValue(object? value)
        {
            var text = Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? "<null>";
            return text.Length <= 96 ? text : text[..93] + "...";
        }

        private void EnsureParameterSessionCurrent(CameraDevice camera, CameraSession session)
        {
            if (!ReferenceEquals(SelectedCamera, camera)
                || !string.Equals(camera.DeviceId, session.CameraRef.Value, StringComparison.OrdinalIgnoreCase)
                || !_runtime.TryGetSession(session.CameraRef, out var current) || !ReferenceEquals(current, session))
                throw new OperationCanceledException("相机选择或连接已改变，旧参数读取已取消。");
        }

        private async Task<IReadOnlyDictionary<string, object?>> RefreshParameterSchemaAsync(
            CameraSession session, CameraDevice camera, string? preferredViewKind, string? preferredCategoryPath)
        {
            using var operation = _runtime.AcquireSessionOperationLease(session, "刷新设备参数");
            EnsureParameterSessionCurrent(camera, session);
            var adapter = session.Adapter;
            var schema = await adapter.GetParameterSchemaAsync(CancellationToken.None);
            var names = schema.Parameters
                .Where(item => item.Value is null && item.IsReadable && item.Kind != CameraParameterKind.Command)
                .Select(item => item.Name)
                .ToArray();
            var values = await adapter.ReadParametersAsync(names, CancellationToken.None);
            EnsureParameterSessionCurrent(camera, session);
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
            // 原生类别中的 '/' 已由适配器转义为全角斜杠，避免被当成层级分隔符。
            var categoryName = value.Replace('／', '/').Trim();
            if (CategoryNames.TryGetValue(categoryName, out var localized))
                return localized;

            var normalized = categoryName.Replace('_', ' ');
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
                {
                    return TryReadBoolean(Value, out var booleanValue)
                        ? (booleanValue ? "开启" : "关闭")
                        : Convert.ToString(Value, System.Globalization.CultureInfo.InvariantCulture) ?? "不可读取";
                }
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
            // DataTemplate 会为所有参数创建 ToggleSwitch，即使控件随后被折叠，
            // WPF 仍可能先读取绑定属性，因此不能无条件转换枚举/数值参数。
            get => Kind == CameraParameterKind.Boolean
                && TryReadBoolean(Value, out var booleanValue)
                && booleanValue;
            set
            {
                if (Kind == CameraParameterKind.Boolean)
                    Value = value;
            }
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

        private static bool TryReadBoolean(object? value, out bool result)
        {
            result = false;
            if (value is bool boolean)
            {
                result = boolean;
                return true;
            }

            if (value is string text)
            {
                if (bool.TryParse(text, out result))
                    return true;
                if (text == "0")
                    return true;
                if (text == "1")
                {
                    result = true;
                    return true;
                }
                return false;
            }

            // 仅接受 0/1 数值，不能把 PixelFormat 的 17301505 等任意非零值当作 true。
            if (value is byte or sbyte or short or ushort or int or uint or long or ulong)
            {
                try
                {
                    var number = Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture);
                    if (number == 0 || number == 1)
                    {
                        result = number == 1;
                        return true;
                    }
                }
                catch (OverflowException)
                {
                    // 非法数值按无效布尔值处理。
                }
            }

            return false;
        }

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
                    CameraParameterKind.Boolean when TryReadBoolean(Value, out var booleanValue) => booleanValue,
                    CameraParameterKind.Boolean => throw new FormatException("相机布尔参数的当前值不是有效的 true/false 或 0/1"),
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
