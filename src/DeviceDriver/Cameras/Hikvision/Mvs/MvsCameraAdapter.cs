using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SunEyeVision.DeviceDriver.Cameras.Contracts;
using SunEyeVision.Plugin.SDK.Devices.Camera;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Plugin.SDK.Vision.Contracts;

#if MVS_SDK_AVAILABLE
using MvCamCtrl.NET;
#endif

namespace SunEyeVision.DeviceDriver.Cameras.Hikvision.Mvs;

public sealed class MvsCameraAdapter : ICameraAdapter
{
#if MVS_SDK_AVAILABLE
    private MyCamera.MV_CC_DEVICE_INFO _deviceInfo;
    private readonly MyCamera _camera = new();
    private readonly SemaphoreSlim _gate = new(1, 1);
    private CancellationTokenSource? _streamCancellation;
    private Task? _streamTask;
    private bool _opened;
    private bool _grabbing;
    private bool _disposed;
    private bool _softwareTriggerPreview;
    private long _streamFrameCount;
    private long _consecutiveNoDataCount;
    private long _lastNoDataLogUtcTicks;
    private readonly Dictionary<string, CameraParameterDescriptor> _parameterDescriptors = new(StringComparer.OrdinalIgnoreCase);
#endif

    public MvsCameraAdapter(CameraDescriptor descriptor
#if MVS_SDK_AVAILABLE
        , MyCamera.MV_CC_DEVICE_INFO deviceInfo
#endif
    )
    {
        Descriptor = descriptor;
#if MVS_SDK_AVAILABLE
        _deviceInfo = deviceInfo;
#endif
    }

    public string AdapterId => "hikvision-mvs";
    public CameraDescriptor Descriptor { get; }
    public CameraCapabilities Capabilities { get; } = new()
    {
        SupportsContinuousCapture = true,
        SupportsHardwareTrigger = true,
        SupportsSoftwareTrigger = true,
    };
    public CameraState State { get; private set; } = CameraState.Offline;

    public Task ConnectAsync(CameraConnectionOptions options, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
#if MVS_SDK_AVAILABLE
        ThrowIfDisposed();
        if (_opened) return Task.CompletedTask;
        var error = _camera.MV_CC_CreateDevice_NET(ref UnsafeCopyDeviceInfo());
        if (error != MyCamera.MV_OK)
            throw new CameraAdapterException(CameraErrorCode.ConnectionRejected, $"MVS 创建设备句柄失败: 0x{error:X8}");
        error = _camera.MV_CC_OpenDevice_NET();
        if (error != MyCamera.MV_OK)
        {
            _camera.MV_CC_DestroyDevice_NET();
            throw new CameraAdapterException(CameraErrorCode.ConnectionRejected, $"MVS 打开设备失败: 0x{error:X8}");
        }
        _opened = true;
        State = CameraState.Connected;
#else
        throw new CameraAdapterException(CameraErrorCode.DriverUnavailable, "未找到海康 MVS SDK");
#endif
        return Task.CompletedTask;
    }

    public Task DisconnectAsync(CancellationToken cancellationToken)
    {
#if MVS_SDK_AVAILABLE
        if (_grabbing) StopGrabbing();
        if (_opened)
        {
            _camera.MV_CC_CloseDevice_NET();
            _camera.MV_CC_DestroyDevice_NET();
            _opened = false;
        }
        State = CameraState.Offline;
#endif
        return Task.CompletedTask;
    }

    public async Task<CameraFrame> CaptureAsync(CameraCaptureRequest request, CancellationToken cancellationToken)
    {
        using var diagnosticScope = CameraDiagnosticScope.Begin(request.DiagnosticCorrelationId);
        cancellationToken.ThrowIfCancellationRequested();
#if MVS_SDK_AVAILABLE
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureConnected();
            EnsureGrabbing();
            State = CameraState.Capturing;
            var frame = new MyCamera.MV_FRAME_OUT { nReserved = new uint[16] };
            var timeout = (int)Math.Clamp(request.Timeout.TotalMilliseconds, 1, int.MaxValue);
            var captureTimer = Stopwatch.StartNew();
            var error = _camera.MV_CC_GetImageBuffer_NET(ref frame, timeout);
            if (error != MyCamera.MV_OK)
            {
                captureTimer.Stop();
                if (error == MyCamera.MV_E_NODATA)
                {
                    var noDataCount = Interlocked.Increment(ref _consecutiveNoDataCount);
                    var nowTicks = DateTimeOffset.UtcNow.UtcDateTime.Ticks;
                    var previousTicks = Interlocked.Read(ref _lastNoDataLogUtcTicks);
                    if (noDataCount == 1 || nowTicks - previousTicks >= TimeSpan.FromSeconds(5).Ticks)
                    {
                        Interlocked.Exchange(ref _lastNoDataLogUtcTicks, nowTicks);
                        LogCamera(LogLevel.Warning,
                            $"GetImageBuffer无数据/超时 sdk=0x{error:X8} waitMs={captureTimer.Elapsed.TotalMilliseconds:F1} timeoutMs={timeout} consecutive={noDataCount} requestMode={request.TriggerMode}");
                    }
                    throw new CameraAdapterException(CameraErrorCode.FrameTimeout, $"MVS 等待图像超时/无数据: 0x{error:X8}");
                }

                LogCamera(LogLevel.Error,
                    $"GetImageBuffer SDK失败 sdk=0x{error:X8} waitMs={captureTimer.Elapsed.TotalMilliseconds:F1} timeoutMs={timeout} requestMode={request.TriggerMode}");
                throw new CameraAdapterException(CameraErrorCode.Unknown, $"MVS 取帧 SDK 错误: 0x{error:X8}");
            }
            try
            {
                var info = frame.stFrameInfo;
                var length = checked((int)info.nFrameLen);
                var bytes = new byte[length];
                if (length > 0 && frame.pBufAddr == IntPtr.Zero)
                    throw new CameraAdapterException(CameraErrorCode.FrameTimeout, "MVS 返回空图像缓冲区");
                Marshal.Copy(frame.pBufAddr, bytes, 0, length);
                // MVS 可能返回 Mono/Bayer 原始单通道数据；以实际字节长度判断，
                // 避免把 width*height 的数据误标成 BGR24 后被 WPF 按三通道解释。
                var pixelCount = checked((long)info.nWidth * info.nHeight);
                var channels = length == pixelCount * 3 ? 3 : 1;
                var frameIndex = Interlocked.Increment(ref _streamFrameCount);
                var previousTimeouts = Interlocked.Exchange(ref _consecutiveNoDataCount, 0);
                if (previousTimeouts > 0)
                    LogCamera(LogLevel.Info, $"取帧恢复 frame={info.nFrameNum} consecutiveNoDataBeforeRecovery={previousTimeouts}");
                if (frameIndex == 1 || frameIndex % 120 == 0)
                    LogCamera(LogLevel.Info,
                        $"取帧成功 frame={info.nFrameNum} streamFrameIndex={frameIndex} size={info.nWidth}x{info.nHeight} pixelType={info.enPixelType} bytes={length} channels={channels} sampleMean8={CalculateSampleMean(bytes):F2} sdkWaitMs={captureTimer.Elapsed.TotalMilliseconds:F1}");
                var image = new MvsVisionImage((int)info.nWidth, (int)info.nHeight, channels, bytes);
                return new CameraFrame(Descriptor.CameraRef, info.nFrameNum, DateTimeOffset.UtcNow, image,
                    new Dictionary<string, object?> { ["pixelType"] = info.enPixelType.ToString(), ["source"] = "MVS" });
            }
            finally
            {
                _camera.MV_CC_FreeImageBuffer_NET(ref frame);
                State = CameraState.Connected;
            }
        }
        finally { _gate.Release(); }
#else
        await Task.Yield();
        throw new CameraAdapterException(CameraErrorCode.DriverUnavailable, "未找到海康 MVS SDK");
#endif
    }

    public Task StartStreamAsync(CameraCaptureRequest request, ICameraFrameSink sink, CancellationToken cancellationToken)
    {
        using var diagnosticScope = CameraDiagnosticScope.Begin(request.DiagnosticCorrelationId);
        ArgumentNullException.ThrowIfNull(sink);
#if MVS_SDK_AVAILABLE
        EnsureConnected();
        lock (this)
        {
            if (_streamTask is not null)
                throw new InvalidOperationException("MVS 连续采集已在运行。");
            EnsureGrabbing();
            Interlocked.Exchange(ref _streamFrameCount, 0);
            Interlocked.Exchange(ref _consecutiveNoDataCount, 0);
            Interlocked.Exchange(ref _lastNoDataLogUtcTicks, 0);
            _softwareTriggerPreview = request.TriggerMode == CameraTriggerMode.Software;
            _streamCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var streamToken = _streamCancellation.Token;
            LogCamera(LogLevel.Info,
                $"预览流启动 requestMode={request.TriggerMode} timeoutMs={request.Timeout.TotalMilliseconds:F0} softwareTriggerEnabled={_softwareTriggerPreview} parameters={FormatParameters(request.Parameters)}");
            _streamTask = Task.Run(() => RunStreamAsync(request, sink, streamToken), CancellationToken.None);
            return _streamTask;
        }
#else
        throw new CameraAdapterException(CameraErrorCode.DriverUnavailable, "未找到海康 MVS SDK");
#endif
    }

    public Task StopStreamAsync(CancellationToken cancellationToken)
    {
#if MVS_SDK_AVAILABLE
        Task? streamTask;
        lock (this)
        {
            _streamCancellation?.Cancel();
            streamTask = _streamTask;
            if (streamTask is not null)
                LogCamera(LogLevel.Info, "收到停止预览流请求");
        }
        if (streamTask is not null)
            return StopStreamAndWaitAsync(streamTask, cancellationToken);
#endif
        return Task.CompletedTask;
    }

    public async Task<CameraParameterSchema> GetParameterSchemaAsync(CancellationToken cancellationToken)
    {
#if MVS_SDK_AVAILABLE
        EnsureConnected();
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var parameters = new List<CameraParameterDescriptor>();
            _parameterDescriptors.Clear();

            var root = new MyCamera.MV_XML_NODE_FEATURE
            {
                strName = string.Empty,
                strDisplayName = string.Empty,
                strDescription = string.Empty,
                strToolTip = string.Empty,
                nReserved = new uint[4]
            };
            var rootError = _camera.MV_XML_GetRootNode_NET(ref root);
            if (rootError != MyCamera.MV_OK)
                throw new CameraAdapterException(CameraErrorCode.ParameterUnsupported,
                    $"读取 MVS 参数树根节点失败: 0x{rootError:X8}");

            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            EnumerateChildren(root, string.Empty, 0);
            return new CameraParameterSchema { AdapterId = AdapterId, Parameters = parameters };

            void EnumerateChildren(MyCamera.MV_XML_NODE_FEATURE parent, string categoryPath, int depth)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (depth > 32)
                    return;

                var children = new MyCamera.MV_XML_NODES_LIST
                {
                    stNodes = CreateEmptyNodeArray()
                };
                var error = _camera.MV_XML_GetChildren_NET(ref parent, ref children);
                if (error != MyCamera.MV_OK || children.stNodes is null)
                    throw new CameraAdapterException(CameraErrorCode.ParameterUnsupported,
                        $"读取相机参数类别“{categoryPath}”的子节点失败: 0x{error:X8}");
                if (children.nNodeNum > (uint)children.stNodes.Length)
                    throw new CameraAdapterException(CameraErrorCode.ParameterUnsupported,
                        $"相机参数类别“{categoryPath}”的子节点超过 SDK 单次枚举上限 {children.stNodes.Length}，不能声称已完整读取");

                var count = Math.Min((int)children.nNodeNum, children.stNodes.Length);
                for (var index = 0; index < count; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var node = children.stNodes[index];
                    if (string.IsNullOrWhiteSpace(node.strName))
                        continue;

                    var key = $"{categoryPath}/{node.strName}";
                    if (!visited.Add(key))
                        continue;

                    if (node.enType == MyCamera.MV_XML_InterfaceType.IFT_ICategory)
                    {
                        var categoryName = string.IsNullOrWhiteSpace(node.strDisplayName)
                            ? node.strName : node.strDisplayName;
                        var nextPath = string.IsNullOrWhiteSpace(categoryPath)
                            ? categoryName : $"{categoryPath}/{categoryName}";
                        EnumerateChildren(node, nextPath, depth + 1);
                        continue;
                    }

                    if (node.enType is MyCamera.MV_XML_InterfaceType.IFT_IBase
                        or MyCamera.MV_XML_InterfaceType.IFT_IValue
                        or MyCamera.MV_XML_InterfaceType.IFT_IEnumEntry
                        or MyCamera.MV_XML_InterfaceType.IFT_IPort)
                        continue;

                    var descriptor = DescribeNode(node, categoryPath);
                    _parameterDescriptors[descriptor.Name] = descriptor;
                    parameters.Add(descriptor);
                }
            }
        }
        finally
        {
            _gate.Release();
        }
#else
        await Task.CompletedTask;
        return new CameraParameterSchema { AdapterId = AdapterId, Parameters = Array.Empty<CameraParameterDescriptor>() };
#endif
    }

    public Task ExecuteSoftwareTriggerAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
#if MVS_SDK_AVAILABLE
        EnsureConnected();
        if (!_grabbing || !_softwareTriggerPreview)
            throw new InvalidOperationException("请先在预览窗口启动软件触发调试。");

        LogCamera(LogLevel.Info, "执行软件触发命令 node=TriggerSoftware");
        var error = _camera.MV_CC_SetCommandValue_NET("TriggerSoftware");
        if (error != MyCamera.MV_OK)
        {
            LogCamera(LogLevel.Error, $"软件触发命令失败 node=TriggerSoftware sdk=0x{error:X8}");
            throw new CameraAdapterException(CameraErrorCode.TriggerTimeout, $"MVS 软件触发失败: 0x{error:X8}");
        }
        LogCamera(LogLevel.Info, "软件触发命令成功 node=TriggerSoftware");
        return Task.CompletedTask;
#else
        throw new CameraAdapterException(CameraErrorCode.DriverUnavailable, "未找到海康 MVS SDK");
#endif
    }

#if MVS_SDK_AVAILABLE
    private CameraParameterDescriptor DescribeNode(MyCamera.MV_XML_NODE_FEATURE node, string categoryPath)
    {
        var interfaceType = node.enType;
        var typeError = _camera.MV_XML_GetNodeInterfaceType_NET(node.strName, ref interfaceType);
        var accessMode = MyCamera.MV_XML_AccessMode.AM_NI;
        var accessError = _camera.MV_XML_GetNodeAccessMode_NET(node.strName, ref accessMode);
        var kind = interfaceType switch
        {
            MyCamera.MV_XML_InterfaceType.IFT_IInteger => CameraParameterKind.Integer,
            MyCamera.MV_XML_InterfaceType.IFT_IFloat => CameraParameterKind.Double,
            MyCamera.MV_XML_InterfaceType.IFT_IBoolean => CameraParameterKind.Boolean,
            MyCamera.MV_XML_InterfaceType.IFT_IString => CameraParameterKind.String,
            MyCamera.MV_XML_InterfaceType.IFT_IEnumeration => CameraParameterKind.Enumeration,
            MyCamera.MV_XML_InterfaceType.IFT_ICommand => CameraParameterKind.Command,
            _ => CameraParameterKind.Custom
        };

        var readable = accessError == MyCamera.MV_OK
            && accessMode is MyCamera.MV_XML_AccessMode.AM_RO or MyCamera.MV_XML_AccessMode.AM_RW;
        var writable = accessError == MyCamera.MV_OK
            && accessMode is MyCamera.MV_XML_AccessMode.AM_WO or MyCamera.MV_XML_AccessMode.AM_RW;
        var implemented = typeError == MyCamera.MV_OK && accessMode != MyCamera.MV_XML_AccessMode.AM_NI;
        var available = implemented && accessMode != MyCamera.MV_XML_AccessMode.AM_NA;
        var displayName = string.IsNullOrWhiteSpace(node.strDisplayName) ? node.strName : node.strDisplayName;
        var description = string.IsNullOrWhiteSpace(node.strToolTip)
            ? node.strDescription : $"{node.strDescription} {node.strToolTip}".Trim();
        var descriptor = new CameraParameterDescriptor
        {
            Name = node.strName,
            DisplayName = displayName,
            Category = categoryPath,
            Kind = kind,
            IsReadOnly = !writable,
            IsReadable = readable,
            IsWritable = writable,
            IsImplemented = implemented,
            IsAvailable = available,
            Visibility = node.enVisivility.ToString(),
            Description = description
        };

        if (!available)
            return descriptor with
            {
                StatusMessage = accessError == MyCamera.MV_OK
                    ? $"设备访问状态：{DescribeAccessMode(accessMode)}"
                    : "无法查询设备访问状态"
            };

        if (kind == CameraParameterKind.Custom)
            return descriptor with { IsReadable = false, IsWritable = false, IsReadOnly = true, StatusMessage = $"SDK 原生节点类型 {interfaceType} 暂不支持通用读写" };

        if (kind == CameraParameterKind.Command)
            return descriptor with { Value = null, StatusMessage = "命令节点：点击按钮执行" };

        if (!readable)
            return descriptor with { StatusMessage = "设备节点不可读" };

        if (!TryReadNode(node.strName, kind, out var value, out var minimum, out var maximum, out var increment, out var enumValues, out var readError))
        {
            if (ShouldTraceParameter(node.strName))
                LogCamera(LogLevel.Warning, $"Schema节点当前值读取失败 node={node.strName} kind={kind} sdk=0x{readError:X8}");
            return descriptor with { StatusMessage = "SDK 未能读取该节点当前值或参数范围" };
        }

        return descriptor with
        {
            Value = value,
            Minimum = minimum,
            Maximum = maximum,
            Increment = increment,
            EnumValues = enumValues
        };
    }

    private static string DescribeAccessMode(MyCamera.MV_XML_AccessMode accessMode) => accessMode switch
    {
        MyCamera.MV_XML_AccessMode.AM_NI => "未实现",
        MyCamera.MV_XML_AccessMode.AM_NA => "当前不可用",
        MyCamera.MV_XML_AccessMode.AM_RO => "只读",
        MyCamera.MV_XML_AccessMode.AM_WO => "只写",
        MyCamera.MV_XML_AccessMode.AM_RW => "可读写",
        _ => accessMode.ToString()
    };

    private void LogCamera(LogLevel level, string message, Exception? exception = null)
    {
        var traceId = CameraDiagnosticScope.CurrentCorrelationId;
        var trace = string.IsNullOrWhiteSpace(traceId) ? string.Empty : $"[camera-trace={traceId}] ";
        VisionLogger.Instance.Log(level, $"{trace}camera={Descriptor.DeviceId} {message}", "MvsCameraAdapter", exception, LogTarget.All);
    }

    private bool TryReadNode(string name, CameraParameterKind kind, out object? value,
        out object? minimum, out object? maximum, out object? increment,
        out IReadOnlyList<CameraEnumValue> enumValues, out int sdkError)
    {
        value = minimum = maximum = increment = null;
        enumValues = Array.Empty<CameraEnumValue>();
        sdkError = MyCamera.MV_E_SUPPORT;
        switch (kind)
        {
            case CameraParameterKind.Integer:
            {
                var result = new MyCamera.MVCC_INTVALUE_EX { nReserved = new uint[16] };
                sdkError = _camera.MV_CC_GetIntValueEx_NET(name, ref result);
                if (sdkError != MyCamera.MV_OK) return false;
                value = result.nCurValue;
                minimum = result.nMin;
                maximum = result.nMax;
                increment = Math.Max(1, result.nInc);
                return true;
            }
            case CameraParameterKind.Double:
            {
                var result = new MyCamera.MVCC_FLOATVALUE { nReserved = new uint[4] };
                sdkError = _camera.MV_CC_GetFloatValue_NET(name, ref result);
                if (sdkError != MyCamera.MV_OK) return false;
                value = result.fCurValue;
                minimum = result.fMin;
                maximum = result.fMax;
                var range = Math.Abs(result.fMax - result.fMin);
                increment = range <= 10 ? 0.01 : range <= 1000 ? 0.1 : 1;
                return true;
            }
            case CameraParameterKind.Boolean:
            {
                var result = false;
                sdkError = _camera.MV_CC_GetBoolValue_NET(name, ref result);
                if (sdkError != MyCamera.MV_OK) return false;
                value = result;
                return true;
            }
            case CameraParameterKind.String:
            {
                var result = new MyCamera.MVCC_STRINGVALUE { chCurValue = string.Empty, nReserved = new uint[2] };
                sdkError = _camera.MV_CC_GetStringValue_NET(name, ref result);
                if (sdkError != MyCamera.MV_OK) return false;
                value = result.chCurValue;
                maximum = result.nMaxLength;
                return true;
            }
            case CameraParameterKind.Enumeration:
            {
                var result = new MyCamera.MVCC_ENUMVALUE
                {
                    nSupportValue = new uint[MyCamera.MV_MAX_XML_SYMBOLIC_NUM],
                    nReserved = new uint[4]
                };
                sdkError = _camera.MV_CC_GetEnumValue_NET(name, ref result);
                if (sdkError != MyCamera.MV_OK) return false;
                value = result.nCurValue.ToString(CultureInfo.InvariantCulture);
                var choices = new List<CameraEnumValue>();
                var count = Math.Min((int)result.nSupportedNum, result.nSupportValue.Length);
                for (var i = 0; i < count; i++)
                {
                    var numeric = result.nSupportValue[i];
                    var entry = new MyCamera.MVCC_ENUMENTRY
                    {
                        nValue = numeric,
                        chSymbolic = new byte[MyCamera.MV_MAX_SYMBOLIC_LEN],
                        nReserved = new uint[4]
                    };
                    var symbolic = _camera.MV_CC_GetEnumEntrySymbolic_NET(name, ref entry) == MyCamera.MV_OK
                        ? Encoding.ASCII.GetString(entry.chSymbolic).TrimEnd('\0')
                        : string.Empty;
                    var raw = numeric.ToString(CultureInfo.InvariantCulture);
                    choices.Add(new CameraEnumValue(raw, string.IsNullOrWhiteSpace(symbolic) ? raw : symbolic));
                }
                enumValues = choices;
                return true;
            }
        }

        return false;
    }

    private CameraParameterDescriptor? DescribeNodeByName(string name)
    {
        var interfaceType = MyCamera.MV_XML_InterfaceType.IFT_IValue;
        if (_camera.MV_XML_GetNodeInterfaceType_NET(name, ref interfaceType) != MyCamera.MV_OK)
            return null;
        var node = new MyCamera.MV_XML_NODE_FEATURE
        {
            enType = interfaceType,
            strName = name,
            strDisplayName = name,
            strDescription = string.Empty,
            strToolTip = string.Empty
        };
        return DescribeNode(node, string.Empty);
    }

    private static MyCamera.MV_XML_NODE_FEATURE[] CreateEmptyNodeArray()
    {
        var nodes = new MyCamera.MV_XML_NODE_FEATURE[MyCamera.MV_MAX_XML_NODE_NUM_C];
        for (var index = 0; index < nodes.Length; index++)
        {
            nodes[index] = new MyCamera.MV_XML_NODE_FEATURE
            {
                strName = string.Empty,
                strDisplayName = string.Empty,
                strDescription = string.Empty,
                strToolTip = string.Empty,
                nReserved = new uint[4]
            };
        }
        return nodes;
    }
#endif

    public async Task<IReadOnlyDictionary<string, object?>> ReadParametersAsync(IReadOnlyCollection<string> names, CancellationToken cancellationToken)
    {
#if MVS_SDK_AVAILABLE
        EnsureConnected();
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
        var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in names)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!_parameterDescriptors.TryGetValue(name, out var descriptor))
                descriptor = DescribeNodeByName(name);
            if (descriptor is null)
            {
                if (ShouldTraceParameter(name))
                    LogCamera(LogLevel.Warning, $"请求读取关键采集节点但未找到Schema node={name}");
                continue;
            }
            if (!descriptor.IsReadable)
            {
                if (ShouldTraceParameter(name))
                    LogCamera(LogLevel.Warning, $"关键采集节点不可读 node={name} access={descriptor.StatusMessage ?? "unknown"}");
                continue;
            }
            if (TryReadNode(name, descriptor.Kind, out var value, out _, out _, out _, out _, out var readError))
            {
                values[name] = value;
                if (ShouldTraceParameter(name))
                    LogCamera(LogLevel.Info, $"设备参数读回 node={name} kind={descriptor.Kind} value={Convert.ToString(value, CultureInfo.InvariantCulture)}");
            }
            else
            {
                if (ShouldTraceParameter(name))
                    LogCamera(LogLevel.Warning, $"设备参数SDK读取失败 node={name} kind={descriptor.Kind} sdk=0x{readError:X8}");
            }
        }
        return values;
        }
        finally { _gate.Release(); }
#else
        await Task.CompletedTask;
        return new Dictionary<string, object?>();
#endif
    }

    public async Task ApplyParametersAsync(IReadOnlyDictionary<string, object?> values, CancellationToken cancellationToken)
    {
#if MVS_SDK_AVAILABLE
        EnsureConnected();
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            LogCamera(LogLevel.Info, $"SDK参数写入开始 count={values.Count} values={FormatParameters(values)}");
            // MVS 官方 BasicDemo 在手动设置曝光/增益前先关闭自动控制；否则自动算法会在后续帧
            // 覆盖手动值，导致参数读回变化但画面亮度看起来没有对应变化。
            if (values.ContainsKey("ExposureTime") || values.ContainsKey("ExposureTimeRaw"))
                DisableAutoControlForManualValue("ExposureAuto", values.ContainsKey("ExposureAuto"));
            if (values.ContainsKey("Gain") || values.ContainsKey("GainRaw"))
                DisableAutoControlForManualValue("GainAuto", values.ContainsKey("GainAuto"));

            foreach (var item in values)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var descriptor = _parameterDescriptors.TryGetValue(item.Key, out var cached)
                    ? cached : DescribeNodeByName(item.Key);
                if (descriptor is null || !descriptor.IsWritable || item.Value is null)
                    throw new CameraAdapterException(CameraErrorCode.ParameterUnsupported, $"MVS 参数 {item.Key} 不可写或未发现");

                var error = descriptor.Kind switch
                {
                    CameraParameterKind.Integer => _camera.MV_CC_SetIntValueEx_NET(item.Key, Convert.ToInt64(item.Value, CultureInfo.InvariantCulture)),
                    CameraParameterKind.Double => _camera.MV_CC_SetFloatValue_NET(item.Key, Convert.ToSingle(item.Value, CultureInfo.InvariantCulture)),
                    CameraParameterKind.Boolean => _camera.MV_CC_SetBoolValue_NET(item.Key, Convert.ToBoolean(item.Value, CultureInfo.InvariantCulture)),
                    CameraParameterKind.String => _camera.MV_CC_SetStringValue_NET(item.Key, Convert.ToString(item.Value, CultureInfo.InvariantCulture) ?? string.Empty),
                    CameraParameterKind.Enumeration => _camera.MV_CC_SetEnumValue_NET(item.Key, Convert.ToUInt32(item.Value, CultureInfo.InvariantCulture)),
                    _ => MyCamera.MV_E_SUPPORT
                };
                if (error != MyCamera.MV_OK)
                {
                    LogCamera(LogLevel.Error,
                        $"SDK参数写入失败 node={item.Key} kind={descriptor.Kind} value={Convert.ToString(item.Value, CultureInfo.InvariantCulture)} sdk=0x{error:X8}");
                    throw new CameraAdapterException(CameraErrorCode.ParameterUnsupported, $"MVS 参数 {item.Key} 写入失败: 0x{error:X8}");
                }
                LogCamera(LogLevel.Info,
                    $"SDK参数写入成功 node={item.Key} kind={descriptor.Kind} value={Convert.ToString(item.Value, CultureInfo.InvariantCulture)}");
            }
            LogCamera(LogLevel.Info, $"SDK参数写入结束 count={values.Count}");
        }
        finally
        {
            _gate.Release();
        }
#endif
#if !MVS_SDK_AVAILABLE
        await Task.CompletedTask;
#endif
    }

#if MVS_SDK_AVAILABLE
    private void DisableAutoControlForManualValue(string autoNodeName, bool explicitlyConfiguredInBatch)
    {
        if (explicitlyConfiguredInBatch)
            return;

        var current = new MyCamera.MVCC_ENUMVALUE();
        var readError = _camera.MV_CC_GetEnumValue_NET(autoNodeName, ref current);
        if (readError != MyCamera.MV_OK)
        {
            LogCamera(LogLevel.Warning, $"手动参数前读取自动控制状态失败 node={autoNodeName} sdk=0x{readError:X8}");
            return;
        }
        if (current.nCurValue == 0)
        {
            LogCamera(LogLevel.Info, $"自动控制当前已关闭 node={autoNodeName} current={current.nCurValue}");
            return;
        }

        var error = _camera.MV_CC_SetEnumValue_NET(autoNodeName, 0);
        if (error != MyCamera.MV_OK)
        {
            LogCamera(LogLevel.Error, $"关闭自动控制失败 node={autoNodeName} previous={current.nCurValue} sdk=0x{error:X8}");
            throw new CameraAdapterException(CameraErrorCode.ParameterUnsupported,
                $"手动设置参数前关闭 {autoNodeName} 失败: 0x{error:X8}");
        }
        LogCamera(LogLevel.Info, $"手动参数前已关闭自动控制 node={autoNodeName} previous={current.nCurValue} current=0");
    }
#endif

    public async Task ExecuteCommandAsync(string name, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
#if MVS_SDK_AVAILABLE
        EnsureConnected();
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
        var descriptor = _parameterDescriptors.TryGetValue(name, out var cached) ? cached : DescribeNodeByName(name);
        if (descriptor?.Kind != CameraParameterKind.Command || !descriptor.IsWritable)
            throw new CameraAdapterException(CameraErrorCode.ParameterUnsupported, $"MVS 命令 {name} 不可执行或未发现");
        var error = _camera.MV_CC_SetCommandValue_NET(name);
        if (error != MyCamera.MV_OK)
        {
            LogCamera(LogLevel.Error, $"参数命令执行失败 node={name} sdk=0x{error:X8}");
            throw new CameraAdapterException(CameraErrorCode.ParameterUnsupported, $"MVS 命令 {name} 执行失败: 0x{error:X8}");
        }
        LogCamera(LogLevel.Info, $"参数命令执行成功 node={name}");
        }
        finally { _gate.Release(); }
#else
        throw new CameraAdapterException(CameraErrorCode.DriverUnavailable, "未找到海康 MVS SDK");
#endif
    }

    public async ValueTask DisposeAsync()
    {
#if MVS_SDK_AVAILABLE
        if (_disposed) return;
        _disposed = true;
        await DisconnectAsync(CancellationToken.None).ConfigureAwait(false);
        _gate.Dispose();
#else
        await Task.CompletedTask;
#endif
        State = CameraState.Disposed;
    }

#if MVS_SDK_AVAILABLE
    private ref MyCamera.MV_CC_DEVICE_INFO UnsafeCopyDeviceInfo()
    {
        // 结构体在构造时已由官方 SDK 初始化，保留独立副本避免被调用方修改。
        return ref _deviceInfo;
    }

    private void EnsureConnected()
    {
        ThrowIfDisposed();
        if (!_opened) throw new CameraAdapterException(CameraErrorCode.ConnectionRejected, "相机尚未连接");
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
    private void StopGrabbing()
    {
        if (!_grabbing) return;
        var error = _camera.MV_CC_StopGrabbing_NET();
        if (error != MyCamera.MV_OK)
            LogCamera(LogLevel.Warning, $"StopGrabbing返回错误 sdk=0x{error:X8}");
        else
            LogCamera(LogLevel.Info, "StopGrabbing成功");
        _grabbing = false;
        _softwareTriggerPreview = false;
    }

    private void EnsureGrabbing()
    {
        if (_grabbing) return;
        var error = _camera.MV_CC_StartGrabbing_NET();
        if (error != MyCamera.MV_OK)
        {
            LogCamera(LogLevel.Error, $"StartGrabbing失败 sdk=0x{error:X8}");
            throw new CameraAdapterException(CameraErrorCode.ConnectionRejected, $"MVS 开始采集失败: 0x{error:X8}");
        }
        _grabbing = true;
        LogCamera(LogLevel.Info, "StartGrabbing成功");
    }

    private static string FormatParameters(IReadOnlyDictionary<string, object?> parameters)
        => parameters.Count == 0
            ? "<none>"
            : string.Join(",", parameters.Select(item => $"{item.Key}={Convert.ToString(item.Value, CultureInfo.InvariantCulture)}"));

    private static bool ShouldTraceParameter(string name)
        => name is "AcquisitionMode" or "TriggerMode" or "TriggerSource"
            or "ExposureTime" or "ExposureTimeRaw" or "ExposureAuto"
            or "Gain" or "GainRaw" or "GainAuto";

    private static double CalculateSampleMean(byte[] bytes)
    {
        if (bytes.Length == 0)
            return 0;

        const int maxSamples = 2048;
        var stride = Math.Max(1, bytes.Length / maxSamples);
        long sum = 0;
        var count = 0;
        for (var index = 0; index < bytes.Length; index += stride)
        {
            sum += bytes[index];
            count++;
        }
        return count == 0 ? 0 : (double)sum / count;
    }

    private async Task StopStreamAndWaitAsync(Task streamTask, CancellationToken cancellationToken)
    {
        try
        {
            await streamTask.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            lock (this)
            {
                StopGrabbing();
                _streamTask = null;
                _streamCancellation?.Dispose();
                _streamCancellation = null;
                if (!_disposed) State = CameraState.Connected;
            }
        }
    }

    private async Task RunStreamAsync(CameraCaptureRequest request, ICameraFrameSink sink, CancellationToken cancellationToken)
    {
        using var diagnosticScope = CameraDiagnosticScope.Begin(request.DiagnosticCorrelationId);
        State = CameraState.Capturing;
        LogCamera(LogLevel.Info,
            $"采集循环进入 requestMode={request.TriggerMode} triggerSource={FormatParameters(request.Parameters)} timeoutMs={request.Timeout.TotalMilliseconds:F0}");
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                CameraFrame frame;
                try
                {
                    frame = await CaptureAsync(request, cancellationToken).ConfigureAwait(false);
                }
                catch (CameraAdapterException ex) when (
                    ex.ErrorCode == CameraErrorCode.FrameTimeout
                    && !cancellationToken.IsCancellationRequested)
                {
                    // 超时表示本次等待区间没有帧，不等于采集已结束：可能是触发未到、曝光较长，
                    // 也可能是连续采集中的暂时空档。继续取帧，直到收到新帧或显式停止。
                    continue;
                }
                try
                {
                    await sink.OnFrameAsync(frame, cancellationToken).ConfigureAwait(false);
                }
                catch
                {
                    frame.Dispose();
                    throw;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            LogCamera(LogLevel.Error, $"采集循环异常 requestMode={request.TriggerMode}: {ex.Message}", ex);
            throw;
        }
        finally
        {
            LogCamera(LogLevel.Info,
                $"采集循环退出 requestMode={request.TriggerMode} frames={Interlocked.Read(ref _streamFrameCount)} remainingNoData={Interlocked.Read(ref _consecutiveNoDataCount)} cancelled={cancellationToken.IsCancellationRequested}");
            if (!_disposed) State = CameraState.Connected;
        }
    }

    private sealed class MvsVisionImage : IVisionImage
    {
        private byte[]? _data;
        public MvsVisionImage(int width, int height, int channels, byte[] data) { Width = width; Height = height; Channels = channels; _data = data; }
        public int Width { get; }
        public int Height { get; }
        public int Channels { get; }
        public bool IsEmpty => _data is null || _data.Length == 0;
        public byte[] GetPixelData() => _data is null ? throw new ObjectDisposedException(nameof(MvsVisionImage)) : (byte[])_data.Clone();
        public IVisionImage Clone() => new MvsVisionImage(Width, Height, Channels, GetPixelData());
        public void Dispose() { _data = null; }
    }
#endif
}
