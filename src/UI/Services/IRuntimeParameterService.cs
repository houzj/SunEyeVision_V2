using System.Collections.Generic;

namespace SunEyeVision.UI.Services
{
    /// <summary>
    /// 运行时参数服务（编辑态 UI 层运行时参数）。
    /// 承载主界面编辑态设置的运行时参数（如当前图像路径），
    /// 执行时由 WorkflowExecutionManager 读取。与执行态 WorkflowContext 的
    /// 运行时参数是两套独立存储。
    /// </summary>
    public interface IRuntimeParameterService
    {
        /// <summary>
        /// 设置运行时参数
        /// </summary>
        void Set<T>(string key, T value);

        /// <summary>
        /// 获取运行时参数
        /// </summary>
        T? Get<T>(string key);

        /// <summary>
        /// 获取所有运行时参数的快照
        /// </summary>
        Dictionary<string, object> GetAll();

        /// <summary>
        /// 清除所有运行时参数
        /// </summary>
        void Clear();
    }
}
