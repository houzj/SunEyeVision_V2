using System;
using System.Collections.Generic;
using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.Plugin.SDK.UI.Windows;

namespace SunEyeVision.Plugin.SDK.Execution.Parameters
{
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
