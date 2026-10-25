using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using SunEyeVision.Plugin.Infrastructure;
using SunEyeVision.Plugin.SDK;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Metadata;
using SunEyeVision.Plugin.SDK.UI;
using SunEyeVision.Plugin.SDK.Validation;
using SunEyeVision.Plugin.SDK.UI.Controls;
using SunEyeVision.Plugin.Infrastructure.Managers.Tool;

namespace SunEyeVision.UI.Features.WorkflowUI
{
    /// <summary>
    /// 工具调试窗口工厂 - 优先使用工具自定义调试窗口
    /// </summary>
    /// <remarks>
    /// 创建调试窗口的优先级：
    /// 1. toolMetadata.DebugWindowType - 工具元数据指定（推荐）
    /// 2. IToolEditorFactory.CreateDebugWindow() - 工具自己创建
    /// 3. 返回 null - 不再提供通用调试窗口
    /// </remarks>
    public static class ToolDebugWindowFactory
    {
        /// <summary>
        /// 创建工具调试窗口 - 优先使用 IToolEditorFactory.CreateDebugWindow()
        /// </summary>
        /// <param name="toolId">工具ID</param>
        /// <param name="toolPlugin">工具插件（可选，用于兼容旧版本）</param>
        /// <param name="toolMetadata">工具元数据</param>
        /// <param name="mainImageControl">主窗口的ImageControl（用于区域编辑器绑定）</param>
        /// <returns>调试窗口实例，无窗口工具返回 null</returns>
        public static Window? CreateDebugWindow(string toolId, IToolPlugin? toolPlugin, ToolMetadata toolMetadata, ImageControl? mainImageControl = null)
        {
            // ★ 优先级1：使用 toolMetadata.DebugWindowType（新增）
            if (toolMetadata.DebugWindowType != null)
            {
                try
                {
                    var window = Activator.CreateInstance(toolMetadata.DebugWindowType) as Window;
                    if (window != null)
                    {
                        // 初始化调试窗口
                        InitializeDebugWindow(window, toolId, toolPlugin, toolMetadata);
return window;
                    }
                }
                catch (Exception ex)
                {
}
            }

            // ★ 优先级2：通过 ToolRegistry 创建工具实例
            var tool = ToolRegistry.CreateToolInstance(toolId);
            if (ToolRegistry.CreateEditorFactory(tool) is { } editorFactory)
            {
                // 检查工具是否支持调试窗口
                if (!editorFactory.HasDebugWindow)
                {
return null;
                }

                try
                {
                    var window = editorFactory.CreateDebugWindow();
                    if (window != null)
                    {
                        // ★ 初始化调试窗口（传递 toolId, toolPlugin, toolMetadata）
                        InitializeDebugWindow(window, toolId, toolPlugin, toolMetadata);
return window;
                    }
                }
                catch (Exception ex)
                {
}
            }

            // The host supplies the basic declaration editor when the specialized companion is absent.
            return tool is not null && SunEyeVision.UI.Services.Editing.ToolConfigurationEditors.CreateControl(tool) is System.Windows.Controls.UserControl control
                ? new SunEyeVision.Plugin.SDK.UI.Windows.DefaultDebugWindow(control) : null;
        }

        /// <summary>
        /// 检查工具是否支持调试窗口（运行时检查）
        /// </summary>
        /// <param name="toolId">工具ID</param>
        /// <param name="toolPlugin">工具插件（可选）</param>
        /// <returns>是否支持调试窗口</returns>
        public static bool HasDebugWindow(string toolId, IToolPlugin? toolPlugin)
        {
            // 运行时检查：使用 ToolRegistry 创建工具实例检查
            var tool = ToolRegistry.CreateToolInstance(toolId);
            if (ToolRegistry.CreateEditorFactory(tool) is { } editorFactory)
            {
                return editorFactory.HasDebugWindow;
            }

            return tool is not null&&ToolRegistry.GetComponentMetadata().Any(c=>c.ToolId==toolId&&c.Role==SunEyeVision.Plugin.Contracts.ToolComponentRole.Runtime);
        }

        /// <summary>
        /// 初始化调试窗口及其 ViewModel
        /// </summary>
        private static void InitializeDebugWindow(Window window, string toolId, IToolPlugin? toolPlugin, ToolMetadata? toolMetadata)
        {
            // 尝试调用窗口的 Initialize 方法
            var windowInitMethod = window.GetType().GetMethod("Initialize",
                new[] { typeof(string), typeof(IToolPlugin), typeof(ToolMetadata) });
            if (windowInitMethod != null)
            {
                try
                {
                    windowInitMethod.Invoke(window, new object?[] { toolId, toolPlugin, toolMetadata });
}
                catch (Exception ex)
                {
}
            }

            // 尝试初始化 DataContext (ViewModel)
            var dataContext = window.DataContext;
            if (dataContext != null)
            {
                var vmInitMethod = dataContext.GetType().GetMethod("Initialize",
                    new[] { typeof(string), typeof(IToolPlugin), typeof(ToolMetadata) });
                if (vmInitMethod != null)
                {
                    try
                    {
                        vmInitMethod.Invoke(dataContext, new object?[] { toolId, toolPlugin, toolMetadata });
}
                    catch (Exception ex)
                    {
}
                }
            }
        }
    }

    /// <summary>
    /// 默认工具插件 - 用于工具调试窗口工厂的兼容性
    /// </summary>
    internal class DefaultToolPlugin : IToolPlugin
    {
        public Type ParamsType => typeof(GenericToolParameters);
        public Type ResultType => typeof(GenericToolResults);
        public IReadOnlyList<OutputVariable> OutputVariables => Array.Empty<OutputVariable>();

        public ToolResults Run(ToolParameters parameters)
        {
            return new GenericToolResults();
        }

        public bool HasDebugWindow => false;
        public System.Windows.Window? CreateDebugWindow() => null;
        public ToolParameters GetDefaultParameters() => new GenericToolParameters();
    }
}
