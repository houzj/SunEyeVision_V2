using System;
using System.Collections.Generic;
using System.Linq;

namespace SunEyeVision.Plugin.SDK.Execution.Parameters
{
    /// <summary>
    /// 参数设置容器
    /// </summary>
    /// <remarks>
    /// 管理一个节点或工具的所有参数设置配置。
    /// 提供设置的增删改查、验证和序列化功能。
    /// 
    /// 核心功能：
    /// 1. 管理多个参数设置
    /// 2. 支持快速查找和更新
    /// 3. 批量验证
    /// 4. 序列化支持
    /// 
    /// 使用示例：
    /// <code>
    /// var container = new ParamSettingContainer();
    /// 
    /// // 添加常量设置
    /// container.SetSetting(ParamSetting.CreateConstant("Threshold", 128));
    /// 
    /// // 添加动态绑定设置
    /// container.SetSetting(ParamSetting.CreateBinding("MinRadius", "node_001", "Radius"));
    /// 
    /// // 获取设置
    /// var thresholdSetting = container.GetSetting("Threshold");
    /// 
    /// // 验证所有设置
    /// var validationResult = container.ValidateAll();
    /// </code>
    /// </remarks>
    public class ParamSettingContainer
    {
        private readonly Dictionary<string, ParamSetting> _settings = new Dictionary<string, ParamSetting>();

        /// <summary>
        /// 节点ID（可选）
        /// </summary>
        public string? NodeId { get; set; }

        /// <summary>
        /// 工具名称（可选）
        /// </summary>
        public string? ToolName { get; set; }

        /// <summary>
        /// 所有设置数量
        /// </summary>
        public int Count => _settings.Count;

        /// <summary>
        /// 所有参数名称
        /// </summary>
        public IEnumerable<string> ParameterNames => _settings.Keys;

        /// <summary>
        /// 所有设置
        /// </summary>
        public IEnumerable<ParamSetting> Settings => _settings.Values;

        /// <summary>
        /// 获取或设置参数设置
        /// </summary>
        /// <param name="parameterName">参数名称</param>
        /// <returns>参数设置，如果不存在则返回null</returns>
        public ParamSetting? this[string parameterName]
        {
            get => GetSetting(parameterName);
            set
            {
                if (value != null)
                {
                    SetSetting(value);
                }
                else
                {
                    RemoveSetting(parameterName);
                }
            }
        }

        /// <summary>
        /// 设置参数
        /// </summary>
        /// <param name="setting">参数设置</param>
        public void SetSetting(ParamSetting setting)
        {
            if (setting == null)
                throw new ArgumentNullException(nameof(setting));

            _settings[setting.ParameterName] = setting;
        }

        /// <summary>
        /// 设置常量参数
        /// </summary>
        /// <param name="parameterName">参数名称</param>
        /// <param name="value">常量值</param>
        public void SetConstantSetting(string parameterName, object? value)
        {
            SetSetting(ParamSetting.CreateConstant(parameterName, value));
        }

        /// <summary>
        /// 设置动态绑定参数
        /// </summary>
        /// <param name="parameterName">参数名称</param>
        /// <param name="sourceNodeId">源节点ID</param>
        /// <param name="sourceProperty">源属性名称</param>
        public void SetDynamicSetting(
            string parameterName,
            string sourceNodeId,
            string sourceProperty)
        {
            SetSetting(ParamSetting.CreateBinding(parameterName, sourceNodeId, sourceProperty));
        }

        /// <summary>
        /// 获取参数设置
        /// </summary>
        /// <param name="parameterName">参数名称</param>
        /// <returns>参数设置，如果不存在则返回null</returns>
        public ParamSetting? GetSetting(string parameterName)
        {
            _settings.TryGetValue(parameterName, out var setting);
            return setting;
        }

        /// <summary>
        /// 检查是否存在参数设置
        /// </summary>
        /// <param name="parameterName">参数名称</param>
        /// <returns>是否存在</returns>
        public bool HasSetting(string parameterName)
        {
            return _settings.ContainsKey(parameterName);
        }

        /// <summary>
        /// 移除参数设置
        /// </summary>
        /// <param name="parameterName">参数名称</param>
        /// <returns>是否成功移除</returns>
        public bool RemoveSetting(string parameterName)
        {
            return _settings.Remove(parameterName);
        }

        /// <summary>
        /// 清空所有设置
        /// </summary>
        public void Clear()
        {
            _settings.Clear();
        }

        /// <summary>
        /// 验证所有设置
        /// </summary>
        /// <returns>批量验证结果</returns>
        public ContainerValidationResult ValidateAll()
        {
            var result = new ContainerValidationResult { IsValid = true };

            foreach (var setting in _settings.Values)
            {
                var settingResult = setting.Validate();
                if (!settingResult.IsValid)
                {
                    result.IsValid = false;
                    result.SettingErrors[setting.ParameterName] = settingResult.Errors;
                }
            }

            return result;
        }

        /// <summary>
        /// 获取所有动态绑定设置
        /// </summary>
        /// <returns>动态绑定设置列表</returns>
        public IEnumerable<ParamSetting> GetDynamicSettings()
        {
            return _settings.Values.Where(s => s.BindingType == BindingType.Binding);
        }

        /// <summary>
        /// 获取所有常量设置
        /// </summary>
        /// <returns>常量设置列表</returns>
        public IEnumerable<ParamSetting> GetConstantSettings()
        {
            return _settings.Values.Where(s => s.BindingType == BindingType.Constant);
        }

        /// <summary>
        /// 获取指定源节点的设置
        /// </summary>
        /// <param name="sourceNodeId">源节点ID</param>
        /// <returns>设置列表</returns>
        public IEnumerable<ParamSetting> GetSettingsBySourceNode(string sourceNodeId)
        {
            return _settings.Values.Where(s => s.SourceNodeId == sourceNodeId);
        }

        /// <summary>
        /// 克隆容器
        /// </summary>
        /// <returns>克隆的容器</returns>
        public ParamSettingContainer Clone()
        {
            var cloned = new ParamSettingContainer
            {
                NodeId = NodeId,
                ToolName = ToolName
            };

            foreach (var setting in _settings.Values)
            {
                cloned.SetSetting(setting.Clone());
            }

            return cloned;
        }

        /// <summary>
        /// 转换为字典（用于序列化）
        /// </summary>
        public Dictionary<string, object> ToDictionary()
        {
            var dict = new Dictionary<string, object>();

            if (!string.IsNullOrEmpty(NodeId))
                dict["NodeId"] = NodeId;

            if (!string.IsNullOrEmpty(ToolName))
                dict["ToolName"] = ToolName;

            // 始终序列化 Settings 列表，即使为空
            var settingsList = new List<Dictionary<string, object>>();
            foreach (var setting in _settings.Values)
            {
                settingsList.Add(setting.ToDictionary());
            }
            dict["Settings"] = settingsList;

            return dict;
        }

        /// <summary>
        /// 从字典创建容器（用于反序列化）
        /// </summary>
        public static ParamSettingContainer FromDictionary(Dictionary<string, object> dict)
        {
            var container = new ParamSettingContainer();

            if (dict.TryGetValue("NodeId", out var nodeId))
                container.NodeId = nodeId?.ToString();

            if (dict.TryGetValue("ToolName", out var toolName))
                container.ToolName = toolName?.ToString();

            if (dict.TryGetValue("Settings", out var settingsObj) && settingsObj is List<object> settingsList)
            {
                foreach (var settingObj in settingsList)
                {
                    if (settingObj is Dictionary<string, object> settingDict)
                    {
                        var setting = ParamSetting.FromDictionary(settingDict);
                        container.SetSetting(setting);
                    }
                }
            }

            return container;
        }

        /// <summary>
        /// 获取描述字符串
        /// </summary>
        public override string ToString()
        {
            var desc = $"ParamSettingContainer({Count} settings)";
            if (!string.IsNullOrEmpty(NodeId))
                desc += $" [Node: {NodeId}]";
            if (!string.IsNullOrEmpty(ToolName))
                desc += $" [Tool: {ToolName}]";
            return desc;
        }
    }

    /// <summary>
    /// 容器验证结果
    /// </summary>
    public class ContainerValidationResult
    {
        /// <summary>
        /// 是否有效
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// 各参数的错误信息
        /// </summary>
        public Dictionary<string, List<string>> SettingErrors { get; set; } = new Dictionary<string, List<string>>();

        /// <summary>
        /// 所有错误信息
        /// </summary>
        public IEnumerable<string> AllErrors => SettingErrors.Values.SelectMany(e => e);
    }
}
