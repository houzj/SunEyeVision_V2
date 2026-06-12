using System;
using System.Collections.Generic;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.UI.Windows;
using SunEyeVision.Tool.Subroutine.Models;
using SunEyeVision.Tool.Subroutine.Views;

namespace SunEyeVision.Tool.Subroutine
{
    /// <summary>
    /// 子程序插件 — 创建 DefaultDebugWindow 包裹 SubroutineDebugControl。
    /// 引擎注入已迁移为委托注入（SubroutineParameters.ExecuteSubWorkflowAsync），
    /// 不再需要静态属性注入。
    /// </summary>
    public class SubroutinePlugin
    {
        /// <summary>
        /// 打开子程序调试窗口（DefaultDebugWindow + SubroutineDebugControl），
        /// 参数通过绑定实时同步到 SubroutineParameters。
        /// </summary>
        public static DefaultDebugWindow ShowDebugWindow(
            System.Windows.Window owner,
            string subroutineId,
            string subroutineName,
            string nodeId,
            IDataSourceQueryService? dataSourceQueryService,
            SubroutineParameters parameters,
            List<AvailableDataSource>? subroutineInternalSources = null,
            Action? onOutputsChanged = null)
        {
            var control = new SubroutineDebugControl(
                subroutineId, subroutineName, nodeId,
                dataSourceQueryService, subroutineInternalSources,
                parameters, onOutputsChanged);

            var debugWindow = new DefaultDebugWindow(control)
            {
                Owner = owner
            };
            return debugWindow;
        }
    }
}
