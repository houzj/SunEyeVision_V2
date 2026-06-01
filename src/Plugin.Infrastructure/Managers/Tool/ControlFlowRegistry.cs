using System;
using System.Collections.Generic;
using System.Linq;
using SunEyeVision.Core.Models;
using SunEyeVision.Plugin.SDK.Logging;

namespace SunEyeVision.Plugin.Infrastructure.Managers.Tool
{
    /// <summary>
    /// 控制流节点注册中心 - 管理所有控制流节点类型（子程序、条件分支等）
    /// 与 ToolRegistry（算法工具注册中心）职责分离
    /// </summary>
    public static class ControlFlowRegistry
    {
        private static readonly Dictionary<string, ControlFlowRegistration> _flows = new(StringComparer.OrdinalIgnoreCase);
        private static readonly object _lock = new();

        /// <summary>
        /// 静态构造器：确保内置控制流节点类型在使用前已注册
        /// </summary>
        static ControlFlowRegistry()
        {
            Register("Subroutine", new("子程序", "🔁", ControlFlowType.Subroutine));
            Register("Condition", new("条件判断", "🔀", ControlFlowType.Condition));
        }

        /// <summary>
        /// 控制流节点注册信息
        /// </summary>
        /// <param name="DisplayName">显示名称</param>
        /// <param name="Icon">图标</param>
        /// <param name="ControlFlowType">控制流类型</param>
        public sealed record ControlFlowRegistration(
            string DisplayName,
            string Icon,
            ControlFlowType ControlFlowType)
        {
            public ControlFlowRegistration() : this(string.Empty, string.Empty, Core.Models.ControlFlowType.Subroutine) { }
        }

        /// <summary>
        /// 注册控制流节点类型
        /// </summary>
        /// <param name="id">类型ID（如 "Subroutine", "Condition"）</param>
        /// <param name="registration">注册信息</param>
        public static void Register(string id, ControlFlowRegistration registration)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("ID 不能为空", nameof(id));
            if (registration == null)
                throw new ArgumentNullException(nameof(registration));

            lock (_lock)
            {
                _flows[id] = registration;
            }

            VisionLogger.Instance.Log(LogLevel.Success,
                $"控制流节点注册成功: {id} ({registration.DisplayName})",
                "ControlFlowRegistry");
        }

        /// <summary>
        /// 获取控制流节点注册信息
        /// </summary>
        public static ControlFlowRegistration? Get(string id)
        {
            lock (_lock)
            {
                return _flows.TryGetValue(id, out var info) ? info : null;
            }
        }

        /// <summary>
        /// 判断是否为注册的控制流节点类型
        /// </summary>
        public static bool IsControlFlow(string id)
        {
            lock (_lock)
            {
                return !string.IsNullOrEmpty(id) && _flows.ContainsKey(id);
            }
        }

        /// <summary>
        /// 获取所有已注册的控制流节点列表
        /// </summary>
        public static List<(string Id, string DisplayName, string Icon)> GetAll()
        {
            lock (_lock)
            {
                return _flows.Select(kvp => (kvp.Key, kvp.Value.DisplayName, kvp.Value.Icon)).ToList();
            }
        }
    }
}
