using SunEyeVision.UI.Shared.Events;

namespace SunEyeVision.UI.Services.Logging
{
    /// <summary>
    /// 参数变更日志记录器接口
    /// </summary>
    public interface IParameterChangeLogger
    {
        /// <summary>
        /// 记录参数变更
        /// </summary>
        void LogParameterChange(ParameterChangeEventArgs e);

        /// <summary>
        /// 记录批量参数变更
        /// </summary>
        void LogBatchParameterChange(string nodeName, int changeCount);
    }
}
