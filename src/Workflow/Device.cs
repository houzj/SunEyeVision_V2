using System.Linq;
using System;
using System.Collections.Generic;
using SunEyeVision.Plugin.SDK.Models;

namespace SunEyeVision.Workflow;

/// <summary>
/// 设备类型枚举
/// </summary>
public enum DeviceType
{
    /// <summary>
    /// 相机
    /// </summary>
    Camera,

    /// <summary>
    /// 光源
    /// </summary>
    LightSource,

    /// <summary>
    /// PLC
    /// </summary>
    PLC,

    /// <summary>
    /// 传感器
    /// </summary>
    Sensor,

    /// <summary>
    /// 执行机构
    /// </summary>
    Actuator,

    /// <summary>
    /// 其他
    /// </summary>
    Other
}

/// <summary>
/// 设备状态枚举
/// </summary>
public enum DeviceStatus
{
    /// <summary>
    /// 未知
    /// </summary>
    Unknown,

    /// <summary>
    /// 已连接
    /// </summary>
    Connected,

    /// <summary>
    /// 已断开
    /// </summary>
    Disconnected,

    /// <summary>
    /// 错误
    /// </summary>
    Error,

    /// <summary>
    /// 初始化中
    /// </summary>
    Initializing
}

/// <summary>
/// 设备模型
/// </summary>
/// <remarks>
/// 用于管理解决方案中的所有设备，包括相机、光源、PLC等。
/// </remarks>
public class Device : ObservableObject
{
    [System.Text.Json.Serialization.JsonExtensionData]
    public System.Collections.Generic.Dictionary<string, System.Text.Json.JsonElement>? AdditionalData { get; set; }

    private string _name = "";
    private string _manufacturer = "";
    private string _model = "";
    private string _serialNumber = "";
    private string _ipAddress = "";
    private int _port = 0;
    private DeviceStatus _status = DeviceStatus.Unknown;
    private bool _enabled = true;
    private string? _workflowRef = null;
    private string _description = "";

    /// <summary>
    /// 设备ID
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// 设备名称
    /// </summary>
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value, "设备名称");
    }

    /// <summary>
    /// 设备类型
    /// </summary>
    public DeviceType Type { get; set; } = DeviceType.Camera;

    /// <summary>
    /// 制造商
    /// </summary>
    public string Manufacturer
    {
        get => _manufacturer;
        set => SetProperty(ref _manufacturer, value, "制造商");
    }

    /// <summary>
    /// 型号
    /// </summary>
    public string Model
    {
        get => _model;
        set => SetProperty(ref _model, value, "型号");
    }

    /// <summary>
    /// 序列号
    /// </summary>
    public string SerialNumber
    {
        get => _serialNumber;
        set => SetProperty(ref _serialNumber, value, "序列号");
    }

    /// <summary>
    /// IP地址
    /// </summary>
    public string IpAddress
    {
        get => _ipAddress;
        set => SetProperty(ref _ipAddress, value, "IP地址");
    }

    /// <summary>
    /// 端口号
    /// </summary>
    public int Port
    {
        get => _port;
        set => SetProperty(ref _port, value, "端口号");
    }

    /// <summary>
    /// 设备状态
    /// </summary>
    public DeviceStatus Status
    {
        get => _status;
        set => SetProperty(ref _status, value, "设备状态");
    }

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled
    {
        get => _enabled;
        set => SetProperty(ref _enabled, value, "是否启用");
    }

    /// <summary>
    /// 引用的工作流ID
    /// </summary>
    public string? WorkflowRef
    {
        get => _workflowRef;
        set => SetProperty(ref _workflowRef, value, "引用工作流");
    }

    /// <summary>
    /// 设备描述
    /// </summary>
    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value, "设备描述");
    }

    /// <summary>
    /// 设备设置（JSON格式）
    /// </summary>
    public Dictionary<string, object> Settings { get; set; } = new();

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedTime { get; set; } = DateTime.Now;

    /// <summary>
    /// 修改时间
    /// </summary>
    public DateTime ModifiedTime { get; set; } = DateTime.Now;

    /// <summary>
    /// 无参构造函数
    /// </summary>
    public Device()
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    public Device(string name, DeviceType type)
    {
        Name = name;
        Type = type;
    }

    /// <summary>
    /// 克隆设备
    /// </summary>
    public Device Clone()
    {
        return new Device
        {
            Id = Guid.NewGuid().ToString(),
            AdditionalData = AdditionalData?.ToDictionary(p => p.Key, p => p.Value.Clone()),
            Name = Name,
            Type = Type,
            Manufacturer = Manufacturer,
            Model = Model,
            SerialNumber = SerialNumber,
            IpAddress = IpAddress,
            Port = Port,
            Status = Status,
            IsEnabled = IsEnabled,
            WorkflowRef = WorkflowRef,
            Description = Description,
            Settings = new Dictionary<string, object>(Settings),
            CreatedTime = CreatedTime,
            ModifiedTime = DateTime.Now
        };
    }

    /// <summary>
    /// 验证设备
    /// </summary>
    public (bool IsValid, List<string> Errors) Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(Name))
        {
            errors.Add("设备名称不能为空");
        }

        if (Type == DeviceType.Camera && string.IsNullOrWhiteSpace(IpAddress) && Port == 0)
        {
            errors.Add("相机设备必须配置IP地址或端口");
        }

        return (errors.Count == 0, errors);
    }

    /// <summary>
    /// 获取连接字符串
    /// </summary>
    public string GetConnectionString()
    {
        if (!string.IsNullOrWhiteSpace(IpAddress) && Port > 0)
        {
            return $"{IpAddress}:{Port}";
        }
        else if (!string.IsNullOrWhiteSpace(SerialNumber))
        {
            return SerialNumber;
        }
        else
        {
            return Name;
        }
    }
}
