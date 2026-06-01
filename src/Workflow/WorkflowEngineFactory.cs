using System;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.Infrastructure.Infrastructure;
using SunEyeVision.Plugin.Infrastructure.Managers.Plugin;
using SunEyeVision.Plugin.SDK;

namespace SunEyeVision.Workflow
{
    /// <summary>
    /// 工作流引擎工厂 - 用于创建和初始化工作流引擎套件
    /// </summary>
    public static class WorkflowEngineFactory
    {
        /// <summary>
        /// 创建完整的工作流引擎套件
        /// </summary>
        public static (WorkflowEngine, WorkflowExecutionEngine, IPluginManager) CreateEngineSuite(ILogger logger)
        {
            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            // 1. 创建插件管理器
            var pluginManager = new PluginManager(logger);

            // 2. 创建工作流引擎
            var workflowEngine = new WorkflowEngine(logger);

            // 3. 创建执行引擎
            var executionEngine = new WorkflowExecutionEngine(workflowEngine, pluginManager, logger);

            // 子程序插件由 UI 层负责创建和注册

            return (workflowEngine, executionEngine, pluginManager);
        }

        /// <summary>
        /// 创建基础工作流引擎（无插件功能）
        /// </summary>
        public static WorkflowEngine CreateBasicEngine(ILogger logger)
        {
            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            return new WorkflowEngine(logger);
        }

        /// <summary>
        /// 创建执行引擎
        /// </summary>
        public static WorkflowExecutionEngine CreateExecutionEngine(
            WorkflowEngine workflowEngine,
            IPluginManager pluginManager,
            ILogger logger)
        {
            if (workflowEngine == null)
            {
                throw new ArgumentNullException(nameof(workflowEngine));
            }
            if (pluginManager == null)
            {
                throw new ArgumentNullException(nameof(pluginManager));
            }
            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            return new WorkflowExecutionEngine(workflowEngine, pluginManager, logger);
        }
    }
}
