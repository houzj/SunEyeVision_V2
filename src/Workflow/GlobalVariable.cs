using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using SunEyeVision.Plugin.SDK.Models;

namespace SunEyeVision.Workflow;

/// <summary>
/// 全局变量
/// </summary>
/// <remarks>
/// 用于存储解决方案级别的全局变量，可以在所有工作流和节点中访问。
/// 
/// 设计原则（rule-010: 方案系统实现规范）：
/// - 弱类型 + 类型字符串（视觉软件行业标准）
/// - 使用 JsonConverter 保留类型信息
/// - 支持分组管理（多工位/多相机场景）
/// 
/// 支持的数据类型：
/// - 基础类型：Integer, Float, String, Boolean, Int64, Double, Byte
/// - 几何类型：Point, Rectangle, Size
/// - 集合类型：Array, List
/// - 图像类型：Image
/// 
/// JSON格式示例：
/// {
///   "Id": "var_001",
///   "Name": "阈值",
///   "Group": "参数设置",
///   "Index": 1,
///   "Type": "Integer",
///   "Value": { "Type": "Integer", "Value": 128 },
///   "Description": "二值化阈值",
///   "InputSource": "手动输入",
///   "OutputTarget": "无"
/// }
/// </remarks>
public class GlobalVariable : ObservableObject
{
    private string _name = "";
    private string _group = "默认分组";
    private int _index = 0;
    private object? _value = null;
    private string _type = "String";
    private string _description = "";
    private string _inputSource = "手动输入";
    private string _outputTarget = "无";
    private bool _isInitialized = false;

    /// <summary>
    /// 变量ID
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// 变量名称
    /// </summary>
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value, "变量名称");
    }

    /// <summary>
    /// 变量所属分组
    /// </summary>
    /// <remarks>
    /// 支持多工位/多相机场景的分组管理。
    /// 例如："工位1参数"、"工位2参数"、"相机1设置"、"相机2设置"
    /// </remarks>
    public string Group
    {
        get => _group;
        set => SetProperty(ref _group, value, "所属分组");
    }

    /// <summary>
    /// 变量在分组中的序号（用于UI显示）
    /// </summary>
    public int Index
    {
        get => _index;
        set => SetProperty(ref _index, value, "序号");
    }

    /// <summary>
    /// 变量值（使用转换器保留类型信息）
    /// </summary>
    /// <remarks>
    /// 使用 GlobalVariableValueConverter 保留类型信息，
    /// 序列化格式：{ "Type": "Integer", "Value": 128 }
    /// </remarks>
    [JsonConverter(typeof(GlobalVariableValueConverter))]
    public object? Value
    {
        get => _value;
        set => SetProperty(ref _value, value, "变量值");
    }

    /// <summary>
    /// 变量类型
    /// </summary>
    /// <remarks>
    /// 支持的类型见 GlobalVariableValueConverter.SupportedTypes
    /// </remarks>
    public string Type
    {
        get => _type;
        set => SetProperty(ref _type, value, "变量类型");
    }

    /// <summary>
    /// 变量描述/注释
    /// </summary>
    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value, "变量描述");
    }

    /// <summary>
    /// 输入来源
    /// </summary>
    /// <remarks>
    /// 值来源：手动输入、PLC通讯、串口通讯、数据库、文件等
    /// </remarks>
    public string InputSource
    {
        get => _inputSource;
        set => SetProperty(ref _inputSource, value, "输入来源");
    }

    /// <summary>
    /// 目标输出
    /// </summary>
    /// <remarks>
    /// 值输出目标：无、PLC通讯、串口通讯、数据库、文件等
    /// </remarks>
    public string OutputTarget
    {
        get => _outputTarget;
        set => SetProperty(ref _outputTarget, value, "目标输出");
    }

    /// <summary>
    /// 是否已初始化
    /// </summary>
    public bool IsInitialized
    {
        get => _isInitialized;
        set => SetProperty(ref _isInitialized, value, "是否已初始化");
    }

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
    public GlobalVariable()
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    public GlobalVariable(string name, object? value, string type = "String", string group = "默认分组")
    {
        Name = name;
        Value = value;
        Type = type;
        Group = group;
    }

    /// <summary>
    /// 克隆变量
    /// </summary>
    public GlobalVariable Clone()
    {
        return new GlobalVariable
        {
            Id = Guid.NewGuid().ToString(),
            Name = Name,
            Group = Group,
            Index = Index,
            Value = Value,  // 注意：值类型会被复制，引用类型只复制引用
            Type = Type,
            Description = Description,
            InputSource = InputSource,
            OutputTarget = OutputTarget,
            IsInitialized = IsInitialized,
            CreatedTime = CreatedTime,
            ModifiedTime = DateTime.Now
        };
    }

    /// <summary>
    /// 验证变量
    /// </summary>
    public (bool IsValid, List<string> Errors) Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(Name))
        {
            errors.Add("变量名称不能为空");
        }

        if (string.IsNullOrEmpty(Type))
        {
            errors.Add("变量类型不能为空");
        }

        // 验证类型是否有效
        if (!IsValidType(Type))
        {
            errors.Add($"不支持的变量类型: {Type}");
        }

        return (errors.Count == 0, errors);
    }

    /// <summary>
    /// 检查类型是否有效
    /// </summary>
    private static bool IsValidType(string type)
    {
        return type switch
        {
            GlobalVariableValueConverter.SupportedTypes.Integer => true,
            GlobalVariableValueConverter.SupportedTypes.Float => true,
            GlobalVariableValueConverter.SupportedTypes.String => true,
            GlobalVariableValueConverter.SupportedTypes.Boolean => true,
            GlobalVariableValueConverter.SupportedTypes.Int64 => true,
            GlobalVariableValueConverter.SupportedTypes.Double => true,
            GlobalVariableValueConverter.SupportedTypes.Byte => true,
            GlobalVariableValueConverter.SupportedTypes.Point => true,
            GlobalVariableValueConverter.SupportedTypes.Rectangle => true,
            GlobalVariableValueConverter.SupportedTypes.Size => true,
            GlobalVariableValueConverter.SupportedTypes.Array => true,
            GlobalVariableValueConverter.SupportedTypes.List => true,
            GlobalVariableValueConverter.SupportedTypes.Image => true,
            _ => false
        };
    }
}

/// <summary>
/// 全局变量管理器
/// </summary>
public class GlobalVariableManager
{
    private readonly Dictionary<string, GlobalVariable> _variables = new();

    /// <summary>
    /// 获取所有变量
    /// </summary>
    public List<GlobalVariable> GetAll()
    {
        return _variables.Values.ToList();
    }

    /// <summary>
    /// 获取变量
    /// </summary>
    public GlobalVariable? Get(string name)
    {
        return _variables.TryGetValue(name, out var variable) ? variable : null;
    }

    /// <summary>
    /// 添加变量
    /// </summary>
    public void Add(GlobalVariable variable)
    {
        if (string.IsNullOrWhiteSpace(variable.Name))
            throw new ArgumentException("变量名称不能为空");

        _variables[variable.Name] = variable;
    }

    /// <summary>
    /// 移除变量
    /// </summary>
    public bool Remove(string name)
    {
        return _variables.Remove(name);
    }

    /// <summary>
    /// 更新变量
    /// </summary>
    public bool Update(GlobalVariable variable)
    {
        if (string.IsNullOrWhiteSpace(variable.Name))
            return false;

        if (!_variables.ContainsKey(variable.Name))
            return false;

        _variables[variable.Name] = variable;
        return true;
    }

    /// <summary>
    /// 设置变量值
    /// </summary>
    public void SetValue(string name, object? value)
    {
        if (!_variables.TryGetValue(name, out var variable))
            return;

        variable.Value = value;
        variable.ModifiedTime = DateTime.Now;
    }

    /// <summary>
    /// 获取变量值
    /// </summary>
    public object? GetValue(string name)
    {
        return _variables.TryGetValue(name, out var variable) ? variable.Value : null;
    }

    /// <summary>
    /// 检查变量是否存在
    /// </summary>
    public bool Contains(string name)
    {
        return _variables.ContainsKey(name);
    }

    /// <summary>
    /// 清空所有变量
    /// </summary>
    public void Clear()
    {
        _variables.Clear();
    }
}
