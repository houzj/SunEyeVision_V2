using SunEyeVision.Plugin.SDK.Execution.Results;

namespace SunEyeVision.Plugin.SDK.UI.Controls
{
    /// <summary>
    /// 工具控件数据分析接口 - 数据分析控件的结果更新协议
    /// </summary>
    /// <remarks>
    /// 用于主界面右下角"数据分析"Tab中的控件。
    /// 
    /// 与 IToolControlInitializer 的区别：
    /// - IToolControlInitializer → 调试窗口控件（需要完整初始化流程：SetNode/SetParams/SetDataProvider/Init）
    /// - IToolControlDataAnalysis → 数据分析控件（仅需接收执行结果更新）
    /// 
    /// 使用方式：
    /// 1. 工具在 CreateDataAnalysisControl() 中返回实现此接口的控件
    /// 2. 框架在工具执行完成后调用 OnResultUpdated() 更新数据
    /// </remarks>
    public interface IToolControlDataAnalysis
    {
        /// <summary>
        /// 工具执行完成后，更新数据分析结果
        /// </summary>
        /// <param name="result">工具执行结果</param>
        void OnResultUpdated(ToolResults result);
    }
}
