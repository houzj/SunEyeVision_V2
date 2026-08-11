using System.Windows.Media.Imaging;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;

namespace SunEyeVision.UI.Services.Workflow
{
    /// <summary>
    /// 节点结果接收器 - NodeResultManager 与 UI 显示层之间的解耦接口。
    /// 由承载节点结果显示的 ViewModel（当前为 MainWindowViewModel / 将来的 ImageResultViewModel）实现。
    /// 通过该接口，NodeResultManager 不再持有 MainWindowViewModel 强引用，改为面向接口编程。
    /// </summary>
    public interface INodeResultSink
    {
        /// <summary>
        /// 更新模块结果树（分组可折叠显示）
        /// </summary>
        void UpdateModuleResults(ToolResults? result, string? nodeName, string? toolId = null, string? nodeId = null, ToolParameters? parameters = null);

        /// <summary>
        /// 获取当前选中的图像数据源
        /// </summary>
        AvailableDataSource? GetSelectedImageSource();

        /// <summary>
        /// 从位图设置显示图像
        /// </summary>
        void SetDisplayImageFromBitmap(BitmapSource bitmapSource);

        /// <summary>
        /// 当前显示图像（null 表示无图像）
        /// </summary>
        BitmapSource? DisplayImage { get; set; }

        /// <summary>
        /// 清空全部结果展示（计算结果 + 模块结果树 + 结果标记）
        /// </summary>
        void ClearResultDisplay();

        /// <summary>
        /// 是否有模块结果（用于空状态显示）
        /// </summary>
        bool HasModuleResults { get; set; }
    }
}
