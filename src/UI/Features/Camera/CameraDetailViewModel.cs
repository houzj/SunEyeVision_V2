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
            ["USB Control"] = "USB 控制", ["Other"] = "其他"
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
        private bool _connectionBusy;
        private string _parameterStatus = "请选择相机后加载设备参数";
        private string _parameterSearchText = string.Empty;
        private CameraParameterGroupViewModel? _selectedParameterCategory;
        private CancellationTokenSource? _schemaLoadCts;

        private static readonly HashSet<string> CommonParameterKeys = new(StringComparer.OrdinalIgnoreCase)
        {
            "Width", "Height", "OffsetX", "OffsetY", "PixelFormat", "AcquisitionMode",
            "AcquisitionFrameRate", "AcquisitionFrameRateEnable", "ExposureMode", "ExposureTime",
            "ExposureTimeRaw", "ExposureAuto", "Gain", "GainRaw", "GainAuto", "BlackLevel",
            "Gamma", "GammaEnable", "BalanceWhiteAuto", "TriggerMode", "TriggerSource"
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

        public CameraDevice SelectedCamera
        {
            get => _selectedCamera;
            set
            {
                if (!SetProperty(ref _selectedCamera, value))
                    return;
                IsCameraSelected = value != null;
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
        public ICommand SaveParamsCommand { get; }
        public ICommand ResetParamsCommand { get; }
        public ICommand ExecuteParameterCommand { get; }
        public ICommand ExpandParameterTreeCommand { get; }
        public ICommand CollapseParameterTreeCommand { get; }

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
            SaveParamsCommand = new RelayCommand(SaveParams);
            ResetParamsCommand = new RelayCommand(ResetParams);
            ExpandParameterTreeCommand = new RelayCommand(() => SetGroupsExpanded(ParameterCategories, true));
            CollapseParameterTreeCommand = new RelayCommand(() => SetGroupsExpanded(ParameterCategories, false));
            ExecuteParameterCommand = new RelayCommand<CameraParameterItemViewModel>(
                item => _ = ExecuteParameterAsync(item),
                item => item is { IsCommand: true, IsAvailable: true, IsWritable: true });
        }

        private async Task LoadSchemaAsync(CancellationToken cancellationToken)
        {
            Parameters.Clear();
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
                        Parameters.Add(new CameraParameterItemViewModel(descriptor, value ?? descriptor.Value));
                    }
                    BuildParameterTree();
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
            var common = new CameraParameterGroupViewModel("常用参数", viewKind: "Common") { IsExpanded = true };
            var all = new CameraParameterGroupViewModel("全部属性", viewKind: "All") { IsExpanded = true, IsSelected = true };
            ParameterCategories.Add(common);
            ParameterCategories.Add(all);

            foreach (var parameter in Parameters.Where(item => item.IsAvailable && item.IsImplemented))
            {
                var path = string.IsNullOrWhiteSpace(parameter.Category) ? "其他" : parameter.Category;
                var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (segments.Length == 0)
                    segments = new[] { "其他" };

                var parent = all;
                var currentPath = string.Empty;
                foreach (var segment in segments)
                {
                    currentPath = currentPath.Length == 0 ? segment : $"{currentPath}/{segment}";
                    if (!groups.TryGetValue(currentPath, out var group))
                    {
                        group = new CameraParameterGroupViewModel(LocalizeCategory(segment), currentPath)
                        {
                            IsExpanded = false
                        };
                        groups[currentPath] = group;
                        parent.Children.Add(group);
                    }
                    parent = group;
                }

                var isCommon = CommonParameterKeys.Contains(parameter.Name) || IsTriggerParameter(parameter);
                if (isCommon)
                    common.Parameters.Add(parameter);
                parent.Parameters.Add(parameter);
            }

            var preferred = preferredViewKind switch
            {
                "Common" => common,
                "All" when string.IsNullOrEmpty(preferredCategoryPath) => all,
                "All" => groups.GetValueOrDefault(preferredCategoryPath),
                _ => null
            };
            SelectedParameterCategory = preferred ?? all;
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
                    "All" => items,
                    _ => items.Where(item => string.Equals(item.Category, category.CategoryPath, StringComparison.OrdinalIgnoreCase)
                        || item.Category.StartsWith(category.CategoryPath + "/", StringComparison.OrdinalIgnoreCase))
                };
            }

            foreach (var item in items.OrderBy(item => item.DisplayName, StringComparer.CurrentCulture))
                SelectedParameters.Add(item);
            OnPropertyChanged(nameof(SelectedCategoryTitle));
        }

        private static bool IsTriggerParameter(CameraParameterItemViewModel item) =>
            item.Name.StartsWith("Trigger", StringComparison.OrdinalIgnoreCase)
            || item.Category.Contains("trigger", StringComparison.OrdinalIgnoreCase)
            || item.Category.Contains("触发", StringComparison.OrdinalIgnoreCase);

        private static void SetGroupsExpanded(IEnumerable<CameraParameterGroupViewModel> groups, bool expanded)
        {
            foreach (var group in groups)
            {
                group.IsExpanded = expanded;
                SetGroupsExpanded(group.Children, expanded);
            }
        }

        private async Task ExecuteParameterAsync(CameraParameterItemViewModel? item)
        {
            if (item is not { IsCommand: true, IsAvailable: true, IsWritable: true }
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

        private void Preview()
        {
            if (SelectedCamera == null || string.IsNullOrWhiteSpace(SelectedCamera.DeviceId))
                return;

            if (_previewWindow is { IsVisible: true })
            {
                _previewWindow.Activate();
                return;
            }

            var window = new CameraPreviewWindow(_runtime, new CameraDeviceRef(SelectedCamera.DeviceId), _session);
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
            window.Show();
        }

        private void SaveParams()
        {
            _ = SaveParamsAsync();
        }

        private void ResetParams()
        {
            _schemaLoadCts?.Cancel();
            _ = LoadSchemaAsync(_schemaLoadCts?.Token ?? CancellationToken.None);
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

                var reference = new CameraDeviceRef(SelectedCamera.DeviceId);
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
                await _runtime.Runtime.CloseAsync(new CameraDeviceRef(SelectedCamera.DeviceId));
                _session = null;
                SelectedCamera.State = CameraState.Offline;
                ParameterStatus = "设备已断开";
            }
            catch (Exception ex)
            {
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
            await ReleaseSessionAsync().ConfigureAwait(true);
        }

        private async System.Threading.Tasks.Task SaveParamsAsync()
        {
            if (SelectedCamera == null || string.IsNullOrWhiteSpace(SelectedCamera.DeviceId))
                return;

            var traceId = Guid.NewGuid().ToString("N");
            var cameraId = SelectedCamera.DeviceId;
            using var diagnosticScope = CameraDiagnosticScope.Begin(traceId);
            CameraSession? temporarySession = null;
            CameraSession? session = null;
            var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            var writeAttempted = false;
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
                    ParameterStatus = "没有可写入的有效参数";
                    return;
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

                // selector/模式类参数会改变其他节点的可用性、范围和选项；提交后重新读取完整设备参数，
                // 以设备当前状态为准，避免界面继续显示旧权限和旧选项。
                var readbackValues = await RefreshParameterSchemaAsync(session.Adapter, previousViewKind, previousCategoryPath);
                var readCount = Parameters.Count(item => item.IsReadable && item.HasValue);
                var readbackSummary = string.Join("; ", values.Keys.Select(name =>
                    $"{name}={(readbackValues.TryGetValue(name, out var actualValue) ? FormatDiagnosticValue(actualValue) : "<read-failed-or-unavailable>")}"));
                VisionLogger.Instance.Log(LogLevel.Info,
                    $"[camera-trace={traceId}] 参数设备回读完成 camera={reference.Value} readCount={readCount} values={readbackSummary}",
                    "CameraParameterTrace", target: LogTarget.All);
                var adjustmentNote = GetAutomaticControlNote(values.Keys)
                    + GetManualAutoTransitionNote(values, session.Adapter.AdapterId,
                        exposureAutoWasEnabled, gainAutoWasEnabled);
                // 预览行为取相机读回的完整采集配置；任何参数应用后都让预览重新读设备并开始新帧，
                // 尤其覆盖 AcquisitionMode 变化（不能只盯 TriggerMode/TriggerSource）。
                await (_previewWindow?.RefreshFromDeviceParametersAsync(traceId) ?? Task.CompletedTask);
                VisionLogger.Instance.Log(LogLevel.Info,
                    $"[camera-trace={traceId}] 参数应用流程完成 camera={reference.Value} previewOpen={_previewWindow is not null}",
                    "CameraParameterTrace", target: LogTarget.All);
                ParameterStatus = $"设备参数已刷新：{readCount} 项可读；已提交 {values.Count} 项修改{adjustmentNote}";
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
            }
            finally
            {
                if (temporarySession != null)
                    await _runtime.Runtime.CloseAsync(temporarySession.CameraRef);
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
            Parameters.Clear();
            foreach (var descriptor in schema.Parameters)
            {
                values.TryGetValue(descriptor.Name, out var value);
                Parameters.Add(new CameraParameterItemViewModel(descriptor, value ?? descriptor.Value));
            }
            BuildParameterTree(preferredViewKind, preferredCategoryPath);
            return values;
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
            Category = descriptor.Category;
            Kind = descriptor.Kind;
            IsAvailable = descriptor.IsAvailable;
            IsImplemented = descriptor.IsImplemented;
            IsReadable = descriptor.IsReadable && descriptor.IsAvailable && descriptor.IsImplemented;
            IsWritable = descriptor.IsWritable && descriptor.IsAvailable && descriptor.IsImplemented;
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
        public bool IsExpanded { get; set; }
        public string DisplayName { get; }
        public string Category { get; }
        public CameraParameterKind Kind { get; }
        public bool IsReadOnly => !IsEditable;
        public bool IsAvailable { get; }
        public bool IsImplemented { get; }
        public bool IsReadable { get; }
        public bool IsWritable { get; }
        public string? StatusMessage { get; private set; }
        public bool IsCommand => Kind == CameraParameterKind.Command;
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

        public CameraParameterGroupViewModel(string name, string? categoryPath = null, string? viewKind = null)
        {
            Name = name;
            CategoryPath = categoryPath;
            ViewKind = viewKind;
        }

        public string Name { get; }
        public string? CategoryPath { get; }
        public string? ViewKind { get; }
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
