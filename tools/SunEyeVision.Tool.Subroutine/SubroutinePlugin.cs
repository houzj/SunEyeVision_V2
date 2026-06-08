using System;
using System.Collections.Generic;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Tool.Subroutine.Models;
using SunEyeVision.Workflow;

namespace SunEyeVision.Tool.Subroutine
{
    /// <summary>
    /// 子程序插件 — 负责引擎注入和对话框服务。
    /// Condition 已迁移为 ConditionTool (IToolPlugin)，不再需要 IWorkflowControlPlugin。
    /// </summary>
    public class SubroutinePlugin
    {
        /// <summary>
        /// 初始化子程序插件，将 WorkflowEngine/ExecutionEngine 注入到 SubroutineTool
        /// </summary>
        public SubroutinePlugin(WorkflowEngine workflowEngine, WorkflowExecutionEngine? executionEngine = null)
        {
            if (workflowEngine == null)
                throw new ArgumentNullException(nameof(workflowEngine));

            SubroutineTool.WorkflowEngine = workflowEngine;
            SubroutineTool.WorkflowExecutionEngine = executionEngine;
        }

        /// <summary>
        /// 显示子程序参数设置对话框
        /// </summary>
        public static bool ShowParameterDialog(System.Windows.Window owner, string id, string name)
        {
            return ShowParameterDialog(owner, id, name, id, null, null, null, null);
        }

        /// <summary>
        /// 显示子程序参数设置对话框（含数据源绑定和参数）
        /// </summary>
        public static bool ShowParameterDialog(
            System.Windows.Window owner,
            string id,
            string name,
            string nodeId,
            IDataSourceQueryService? dataSourceQueryService,
            SubroutineParameters? parameters = null,
            List<AvailableDataSource>? subroutineInternalSources = null,
            Action? onOutputsChanged = null)
        {
            parameters ??= new SubroutineParameters();
            var dialog = new Views.SubroutineParameterDialog(id, name, nodeId, dataSourceQueryService, subroutineInternalSources, parameters, onOutputsChanged)
            {
                Owner = owner
            };
            return dialog.ShowDialog() == true;
        }
    }
}
