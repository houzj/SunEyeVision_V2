using System;
using System.Collections.Generic;
using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.Plugin.SDK.UI.Windows;

namespace SunEyeVision.Plugin.SDK.Execution.Parameters
{
    /// <summary>自定义Group参数控件接收宿主的独立候选；发布后重新绑定，不能写正式节点。</summary>
    public interface ISubroutineParameterEditor : IDisposable
    {
        void BindParameters(SubroutineParameters parameters,IDataSourceQueryService? queryService);
        void SetRunParameterEditor(System.Windows.Controls.UserControl editor);
    }

    public interface ISubroutineRunParameterEditor
    {
        void BindParameters(SubroutineParameters parameters);
    }

    public interface ISubroutineDebugService
    {
        DefaultDebugWindow CreateDebugWindow(
            System.Windows.Window owner,
            string subroutineId,
            string subroutineName,
            string nodeId,
            IDataSourceQueryService? queryService,
            SubroutineParameters parameters,
            List<AvailableDataSource>? subroutineInternalSources,
            Action<SubroutineOutputChange>? onOutputsChanged);
    }
}
