using System;
using System.Collections.Generic;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Tool.Subroutine.Models;

namespace SunEyeVision.Tool.Subroutine
{
    /// <summary>
    /// 子程序插件 — 只负责对话框服务。
    /// 引擎注入已迁移为委托注入（SubroutineParameters.ExecuteSubWorkflowAsync），
    /// 不再需要静态属性注入。
    /// </summary>
    public class SubroutinePlugin
    {
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
