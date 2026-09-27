<#
.SYNOPSIS
    生成「新增厂商相机」的三件套骨架：发现器 / 适配器工厂 / 适配器。

.DESCRIPTION
    按项目既有范式（参照 src/DeviceDriver/Cameras/Hikvision/Mvs/）生成可编译的诚实骨架：
      - 发现器：默认返回空结果（未接 SDK 时不阻断其他发现器）；
      - 工厂  ：CanCreate 默认返回 false（未填判据前不接管任何设备）；Create 抛 DriverUnavailable；
      - 适配器：所有入口抛 DriverUnavailable，Dispose/GetParameterSchemaAsync 不抛异常。
    骨架只负责结构，业务实现必须按技能 references/02、references/03 补齐。

.PARAMETER Vendor
    厂商名（PascalCase，ASCII），例如 Basler、Daheng、Hikvision。

.PARAMETER SdkSubFolder
    SDK 子目录名；默认与 Vendor 相同（如 MVS 对应 Hikvision/Mvs）。

.PARAMETER Namespace
    生成的命名空间；默认 SunEyeVision.DeviceDriver.Cameras.<Vendor>。

.PARAMETER Identity
    ProviderId / AdapterId 的取值；默认 <vendor 小写>-<sdk 小写>。

.PARAMETER SdkMacro
    条件编译常量名；默认 <VENDOR 大写>_SDK_AVAILABLE。

.EXAMPLE
    pwsh -File scaffold-camera-vendor.ps1 -Vendor Basler -SdkMacro BASLER_SDK_AVAILABLE

.EXAMPLE
    pwsh -File scaffold-camera-vendor.ps1 -Vendor Daheng -SdkSubFolder Galaxy -Namespace SunEyeVision.DeviceDriver.Cameras.Daheng.Galaxy
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[A-Za-z][A-Za-z0-9]*$')]
    [string]$Vendor,

    [string]$SdkSubFolder,
    [string]$Namespace,
    [string]$Identity,
    [string]$SdkMacro,
    [string]$Manufacturer,
    [string]$Root = 'src/DeviceDriver/Cameras',
    [switch]$Force
)

$ErrorActionPreference = 'Stop'

if (-not $SdkSubFolder) { $SdkSubFolder = $Vendor }
if (-not $Namespace) { $Namespace = "SunEyeVision.DeviceDriver.Cameras.$Vendor" }
if (-not $Identity) { $Identity = ($Vendor.ToLowerInvariant() + '-' + $SdkSubFolder.ToLowerInvariant()) }
if (-not $SdkMacro) { $SdkMacro = ($Vendor.ToUpperInvariant() + '_SDK_AVAILABLE') }
if (-not $Manufacturer) { $Manufacturer = $Vendor }

$repoRoot = (Get-Location).Path
$targetDir = Join-Path (Join-Path $repoRoot $Root) (Join-Path $Vendor $SdkSubFolder)
$null = New-Item -ItemType Directory -Path $targetDir -Force

$discoveryPath = Join-Path $targetDir ($Vendor + 'CameraDiscoveryProvider.cs')
$factoryPath = Join-Path $targetDir ($Vendor + 'CameraAdapterFactory.cs')
$adapterPath = Join-Path $targetDir ($Vendor + 'CameraAdapter.cs')

foreach ($p in @($discoveryPath, $factoryPath, $adapterPath)) {
    if ((Test-Path $p) -and (-not $Force)) {
        throw "目标文件已存在：$p（如需覆盖请显式加 -Force）"
    }
}

# ---------- 模板（不插值，避免与 C# 语法冲突；用标记替换） ----------

$discoveryTemplate = @'
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SunEyeVision.Plugin.SDK.Devices.Camera;
using SunEyeVision.DeviceDriver.Cameras.Contracts;

namespace __NAMESPACE__;

/// <summary>
/// __VENDOR__ 设备发现器。
/// 范式见技能 references/02-paradigm-discovery.md；参考实现见 Cameras/Hikvision/Mvs/MvsCameraDiscoveryProvider.cs。
/// </summary>
public sealed class __VENDOR__CameraDiscoveryProvider : ICameraDiscoveryProvider
{
    public string ProviderId => "__IDENTITY__";

    // TODO(Step3) 按本 Provider 实际支持的传输类型填写掩码（参考 MVS 的 GigE=0x1 / Usb3=0x4）
    private static readonly CameraTransportKind[] SupportedTransports =
    {
        CameraTransportKind.Unknown,
    };

    public Task<IReadOnlyList<CameraDescriptor>> DiscoverAsync(
        CancellationToken cancellationToken,
        IReadOnlySet<CameraTransportKind>? transports = null)
    {
        if (!IsRequested(transports))
        {
            // 未请求本 Provider 支持的传输类型时不做无谓枚举
            return Task.FromResult<IReadOnlyList<CameraDescriptor>>(Array.Empty<CameraDescriptor>());
        }

        // TODO(Step3) 在此调用 __VENDOR__ SDK 枚举设备（同步 SDK 接口用 Task.Run 包裹），
        //             并用 CreateDescriptor 生成 CameraDescriptor；不要向上暴露 SDK 对象。
        //             未接 SDK 时返回空数组，不抛异常、不阻断其他发现器。
        return Task.FromResult<IReadOnlyList<CameraDescriptor>>(Array.Empty<CameraDescriptor>());
    }

    /// <summary>
    /// 生成稳定身份。规则见技能 references/02 §3：
    /// 有序列号 &#8594; "&lt;providerId&gt;:&lt;serial&gt;"；否则 &#8594; "&lt;providerId&gt;:&lt;transport&gt;:&lt;model&gt;:&lt;ip 或 mac 或 userName&gt;"。
    /// </summary>
    internal static CameraDescriptor CreateDescriptor(string manufacturer, string model, string serialNumber, string ipOrMac)
    {
        var stableId = string.IsNullOrWhiteSpace(serialNumber)
            ? "__IDENTITY__:" + manufacturer + ":" + model + ":" + ipOrMac
            : "__IDENTITY__:" + serialNumber;

        // 注意：CameraDescriptor.Metadata 的类型是 IReadOnlyDictionary<string, object?>
        var metadata = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["sdk"] = "__VENDOR__",
            ["adapter"] = "__IDENTITY__",
        };

        return new CameraDescriptor
        {
            DeviceId = stableId,
            CameraRef = new CameraDeviceRef(stableId),
            DisplayName = string.IsNullOrWhiteSpace(serialNumber) ? model : serialNumber,
            Manufacturer = manufacturer,
            Model = model,
            SerialNumber = serialNumber,
            IpAddress = null,
            MacAddress = null,
            Transport = CameraTransportKind.Unknown,
            Protocol = CameraProtocolKind.GenICam,
            Metadata = metadata,
        };
    }

    private static bool IsRequested(IReadOnlySet<CameraTransportKind>? transports)
    {
        if (transports is null || transports.Count == 0) return true;
        foreach (var t in SupportedTransports)
        {
            if (transports.Contains(t)) return true;
        }
        return false;
    }
}
'@

$factoryTemplate = @'
using System;
using System.Collections.Generic;
using SunEyeVision.Plugin.SDK.Devices.Camera;
using SunEyeVision.DeviceDriver.Cameras.Contracts;

namespace __NAMESPACE__;

/// <summary>
/// __VENDOR__ 适配器工厂。
/// 范式见技能 references/02-paradigm-discovery.md §6 与 references/03。
/// </summary>
public sealed class __VENDOR__CameraAdapterFactory : ICameraAdapterFactory
{
    private readonly __VENDOR__CameraDiscoveryProvider _discovery;

    public __VENDOR__CameraAdapterFactory(__VENDOR__CameraDiscoveryProvider discovery)
    {
        ArgumentNullException.ThrowIfNull(discovery);
        _discovery = discovery;
    }

    public string AdapterId => "__IDENTITY__";

    /// <summary>同厂商发现器：Step 4 需要它缓存的厂商设备信息。</summary>
    internal __VENDOR__CameraDiscoveryProvider Discovery => _discovery;

    /// <summary>
    /// TODO(Step5) 填写判据（参考 MVS：Protocol == GenICam 且 Manufacturer / DeviceId 前缀 / Metadata["adapter"] 命中）。
    /// 未填判据前返回 false，确保不误接管其他厂商设备。
    /// </summary>
    public bool CanCreate(CameraDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        return false;
    }

    public ICameraAdapter Create(CameraDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        // TODO(Step4) 有 SDK 时：从发现器缓存取厂商设备信息并 new __VENDOR__CameraAdapter(...)；
        //             取不到设备信息时抛 CameraNotFound；无 SDK 时保持下面的 DriverUnavailable。
        throw new CameraAdapterException(
            CameraErrorCode.DriverUnavailable,
            "未接入 __VENDOR__ SDK：请先按技能 Step 7 配置 SDK 探测与条件引用");
    }
}
'@

$adapterTemplate = @'
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SunEyeVision.Plugin.SDK.Devices.Camera;
using SunEyeVision.DeviceDriver.Cameras.Contracts;

namespace __NAMESPACE__;

/// <summary>
/// __VENDOR__ 相机适配器骨架。
/// 实现范式见技能 references/03-paradigm-adapter.md；参考实现见 Cameras/Hikvision/Mvs/MvsCameraAdapter.cs。
/// 约定：未接入 SDK 时所有操作抛 DriverUnavailable；Dispose 与 GetParameterSchemaAsync 不得抛异常。
/// </summary>
public sealed class __VENDOR__CameraAdapter : ICameraAdapter
{
    private static readonly CameraCapabilities EmptyCapabilities = new()
    {
        SupportedFeatures = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
    };

    // 范式：参数/命令串行化与取帧串行化分离（参考 MvsCameraAdapter.cs:28-29）
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly SemaphoreSlim _captureGate = new(1, 1);

    public __VENDOR__CameraAdapter(CameraDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        Descriptor = descriptor;
    }

    public string AdapterId => "__IDENTITY__";

    public CameraDescriptor Descriptor { get; }

    public CameraCapabilities Capabilities => EmptyCapabilities;

    public CameraState State { get; private set; } = CameraState.Offline;

    public Task ConnectAsync(CameraConnectionOptions options, CancellationToken cancellationToken)
        => throw DriverUnavailable();

    public Task DisconnectAsync(CancellationToken cancellationToken)
        => throw DriverUnavailable();

    public Task<CameraFrame> CaptureAsync(CameraCaptureRequest request, CancellationToken cancellationToken)
        => throw DriverUnavailable();

    public Task StartStreamAsync(CameraCaptureRequest request, ICameraFrameSink sink, CancellationToken cancellationToken)
        => throw DriverUnavailable();

    public Task StopStreamAsync(CancellationToken cancellationToken)
        => throw DriverUnavailable();

    public Task ExecuteSoftwareTriggerAsync(CancellationToken cancellationToken)
        => throw DriverUnavailable();

    /// <summary>未接入 SDK 时返回空 schema（不抛异常）。</summary>
    public Task<CameraParameterSchema> GetParameterSchemaAsync(CancellationToken cancellationToken)
        => Task.FromResult(new CameraParameterSchema
        {
            AdapterId = AdapterId,
            Parameters = Array.Empty<CameraParameterDescriptor>(),
        });

    public Task<CameraCaptureConfiguration> ReadCaptureConfigurationAsync(CancellationToken cancellationToken)
        => throw DriverUnavailable();

    public Task<IReadOnlyDictionary<string, object?>> ReadParametersAsync(
        IReadOnlyCollection<string> names,
        CancellationToken cancellationToken)
        => throw DriverUnavailable();

    public Task ApplyParametersAsync(
        IReadOnlyDictionary<string, object?> values,
        CancellationToken cancellationToken)
        => throw DriverUnavailable();

    public Task ExecuteCommandAsync(string name, CancellationToken cancellationToken)
        => throw DriverUnavailable();

    public ValueTask DisposeAsync()
    {
        // 未接入 SDK 时释放为空实现（不抛异常）
        _gate.Dispose();
        _captureGate.Dispose();
        State = CameraState.Disposed;
        return ValueTask.CompletedTask;
    }

    private static CameraAdapterException DriverUnavailable()
        => new(
            CameraErrorCode.DriverUnavailable,
            "__VENDOR__ SDK 未接入或不可用：请按技能 Step 4/Step 7 完成适配器实现与 SDK 条件编译");
}
'@

function Write-Template([string]$template, [string]$path) {
    $text = $template
    $text = $text.Replace('__NAMESPACE__', $Namespace)
    $text = $text.Replace('__VENDOR__', $Vendor)
    $text = $text.Replace('__IDENTITY__', $Identity)
    [System.IO.File]::WriteAllText($path, $text, (New-Object System.Text.UTF8Encoding($false)))
    Write-Output ('   生成 ' + $path.Replace($repoRoot + [System.IO.Path]::DirectorySeparatorChar, ''))
}

Write-Output ('=== 生成 ' + $Vendor + ' 相机骨架 ===')
Write-Template $discoveryTemplate $discoveryPath
Write-Template $factoryTemplate $factoryPath
Write-Template $adapterTemplate $adapterPath

Write-Output ''
Write-Output '=== 后续必做（技能步骤对应） ==='
Write-Output '  1) Step 3：实现发现器枚举与 CreateDescriptor 稳定身份（references/02）'
Write-Output '  2) Step 4：实现连接/采集/流/触发/参数读写与错误就地映射（references/03）'
Write-Output '  3) Step 5：填写工厂 CanCreate 判据（不得误接管其他厂商设备）'
Write-Output '  4) Step 6：在 src/DeviceDriver/Cameras/CameraRuntimeComposition.cs 构造内登记（唯一装配点）：'
Write-Output ''
Write-Output ('        var ' + $Vendor.Substring(0,1).ToLowerInvariant() + $Vendor.Substring(1) + 'Discovery = new ' + $Vendor + 'CameraDiscoveryProvider();')
Write-Output '        Runtime = new CameraRuntimeManager(new ICameraAdapterFactory[]'
Write-Output '        {'
Write-Output ('            new ' + $Vendor + 'CameraAdapterFactory(' + $Vendor.Substring(0,1).ToLowerInvariant() + $Vendor.Substring(1) + 'Discovery),')
Write-Output '        });'
Write-Output '        Discovery = new CameraDiscoveryCoordinator(new ICameraDiscoveryProvider[]'
Write-Output '        {'
Write-Output ('            ' + $Vendor.Substring(0,1).ToLowerInvariant() + $Vendor.Substring(1) + 'Discovery,')
Write-Output '        });'
Write-Output ''
Write-Output '  5) Step 7（如使用厂商 SDK）：在 src/DeviceDriver/SunEyeVision.DeviceDriver.csproj 增加 SDK 根探测 + 条件 Reference + DefineConstants，'
Write-Output ('        条件常量名建议：' + $SdkMacro + '；并把本文件的 SDK 调用包进 #if ' + $SdkMacro + ' / #else 分支。')
Write-Output '  6) Step 8：验证方案保存 → 重启 → 自动重新发现与绑定（身份需可被 DeviceId/序列号+厂商+传输/MAC+传输 匹配）'
Write-Output '  7) Step 9：补契约单测 + 运行时语义测试（模拟相机）+ 真机诊断测试（Trait = RealHardware）'
Write-Output '  8) 构建与测试：dotnet build SunEyeVision.slnx -c Debug / dotnet test SunEyeVision.slnx -c Debug'
Write-Output ''
Write-Output ('提示：本骨架在未完成实现前不会接管任何设备（CanCreate=false），所有调用都会明确报 DriverUnavailable，不存在静默兜底。')
