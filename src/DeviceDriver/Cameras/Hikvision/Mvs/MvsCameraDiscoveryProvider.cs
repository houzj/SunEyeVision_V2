using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SunEyeVision.DeviceDriver.Cameras.Contracts;
using SunEyeVision.Plugin.SDK.Devices.Camera;

#if MVS_SDK_AVAILABLE
using MvCamCtrl.NET;
#endif

namespace SunEyeVision.DeviceDriver.Cameras.Hikvision.Mvs;

/// <summary>
/// 海康 MVS 官方 .NET SDK 发现器。
/// 发现阶段只读取设备描述，不创建长期句柄；句柄由 MvsCameraAdapter 按会话创建和释放。
/// </summary>
public sealed class MvsCameraDiscoveryProvider : ICameraDiscoveryProvider
{
    private readonly Dictionary<string, object> _deviceInfos = new(StringComparer.OrdinalIgnoreCase);
    public const uint GigE = 0x00000001;
    public const uint Usb3 = 0x00000004;

    public string ProviderId => "hikvision-mvs";

    public Task<IReadOnlyList<CameraDescriptor>> DiscoverAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

#if MVS_SDK_AVAILABLE
        return Task.FromResult(DiscoverWithMvs(cancellationToken));
#else
        // 没有安装 MVS 时，该供应商是可选能力，不阻断其他发现器。
        return Task.FromResult<IReadOnlyList<CameraDescriptor>>(Array.Empty<CameraDescriptor>());
#endif
    }

#if MVS_SDK_AVAILABLE
    private IReadOnlyList<CameraDescriptor> DiscoverWithMvs(CancellationToken cancellationToken)
    {
        var result = new List<CameraDescriptor>();
        var list = new MyCamera.MV_CC_DEVICE_INFO_LIST();
        var error = MyCamera.MV_CC_EnumDevices_NET(GigE | Usb3, ref list);
        if (error != MyCamera.MV_OK)
        {
            return result;
        }

        for (var i = 0; i < list.nDeviceNum; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var info = Marshal.PtrToStructure<MyCamera.MV_CC_DEVICE_INFO>(list.pDeviceInfo[i]);
            var descriptor = CreateDescriptor(info);
            _deviceInfos[descriptor.DeviceId] = info;
            result.Add(descriptor);
        }

        return result;
    }

    internal bool TryGetDeviceInfo(string deviceId, out MyCamera.MV_CC_DEVICE_INFO info)
    {
        if (_deviceInfos.TryGetValue(deviceId, out var value) && value is MyCamera.MV_CC_DEVICE_INFO typed)
        {
            info = typed;
            return true;
        }

        info = default;
        return false;
    }

    internal static CameraDescriptor CreateDescriptor(MyCamera.MV_CC_DEVICE_INFO info)
    {
        string manufacturer;
        string model;
        string serial;
        string userName;
        string? ip = null;
        string? mac = null;
        CameraTransportKind transport;

        if (info.nTLayerType == MyCamera.MV_GIGE_DEVICE)
        {
            var gige = MyCamera.ByteToStruct(info.SpecialInfo.stGigEInfo, typeof(MyCamera.MV_GIGE_DEVICE_INFO_EX));
            var value = (MyCamera.MV_GIGE_DEVICE_INFO_EX)gige;
            manufacturer = value.chManufacturerName ?? "Hikvision";
            model = value.chModelName ?? string.Empty;
            serial = value.chSerialNumber ?? string.Empty;
            userName = Decode(value.chUserDefinedName);
            ip = ToIpAddress(value.nCurrentIp);
            transport = CameraTransportKind.GigEVision;
        }
        else
        {
            var usb = (MyCamera.MV_USB3_DEVICE_INFO_EX)MyCamera.ByteToStruct(
                info.SpecialInfo.stUsb3VInfo, typeof(MyCamera.MV_USB3_DEVICE_INFO_EX));
            manufacturer = usb.chManufacturerName ?? "Hikvision";
            model = usb.chModelName ?? string.Empty;
            serial = usb.chSerialNumber ?? string.Empty;
            userName = Decode(usb.chUserDefinedName);
            transport = CameraTransportKind.Usb3Vision;
        }

        var stableId = string.IsNullOrWhiteSpace(serial)
            ? $"hikvision-mvs:{transport}:{model}:{ip ?? userName}"
            : $"hikvision-mvs:{serial}";

        return new CameraDescriptor
        {
            DeviceId = stableId,
            CameraRef = new CameraDeviceRef(stableId),
            DisplayName = string.IsNullOrWhiteSpace(userName) ? $"{manufacturer} {model}" : userName,
            Manufacturer = manufacturer,
            Model = model,
            SerialNumber = serial,
            IpAddress = ip,
            MacAddress = mac,
            Transport = transport,
            Protocol = CameraProtocolKind.GenICam,
            Metadata = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["sdk"] = "MVS",
                ["adapter"] = "hikvision-mvs"
            }
        };
    }

    private static string? ToIpAddress(uint value)
        => value == 0 ? null : string.Join('.', BitConverter.GetBytes(value));

    private static string Decode(byte[]? value)
        => value is null || value.Length == 0
            ? string.Empty
            : Encoding.Default.GetString(value).TrimEnd('\0', ' ');
#endif
}
