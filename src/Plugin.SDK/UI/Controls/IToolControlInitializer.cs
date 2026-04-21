using System.Threading.Tasks;
using SunEyeVision.Plugin.SDK.Execution.Parameters;

namespace SunEyeVision.Plugin.SDK.UI.Controls
{
    /// <summary>
    /// 工具控件初始化接口 - 调试控件的标准化初始化协议
    /// </summary>
    /// <remarks>
    /// 所有需要接收框架上下文的调试控件必须实现此接口。
    /// 
    /// 初始化顺序（必须严格按此顺序调用）：
    /// 1. SetCurrentNode   - 设置节点上下文（节点ID、节点引用）
    /// 2. SetParameters    - 设置节点参数（ToolParameters 实例）
    /// 3. SetDataProvider  - 设置数据查询服务（用于获取上游节点输出）
    /// 4. SetMainImageControl - 设置主窗口图像控件（可选，区域编辑器用）
    /// 5. InitializeAsync  - 完成所有子控件初始化（在所有 Set 完成后调用）
    /// 
    /// 设计原则：
    /// - 编译时约束：接口确保调用方不会遗漏方法
    /// - 壳层透明：DefaultDebugWindow 完整转发所有方法
    /// - 生命周期明确：InitializeAsync 是初始化终点
    /// - 职责分离：每个方法只负责一类数据
    /// </remarks>
    public interface IToolControlInitializer
    {
        /// <summary>
        /// 设置当前节点（第一步）
        /// </summary>
        /// <remarks>
        /// 仅设置节点上下文信息（节点ID、节点引用）。
        /// 不应在此方法中提取或设置参数。
        /// 参数由 SetParameters 方法单独设置。
        /// </remarks>
        void SetCurrentNode(object node);

        /// <summary>
        /// 设置节点参数（第二步）
        /// </summary>
        /// <remarks>
        /// 参数从节点对象的 Parameters 属性中提取后传入。
        /// 控件在此方法中存储参数引用并执行参数相关的UI更新。
        /// 不应在此方法中执行依赖 MainImageControl 或 DataProvider 的初始化，
        /// 这些初始化应在 InitializeAsync 中完成。
        /// </remarks>
        void SetParameters(ToolParameters parameters);

        /// <summary>
        /// 设置数据查询服务（第三步）
        /// </summary>
        /// <remarks>
        /// 数据查询服务用于获取上游节点的输出数据，
        /// 用于填充图像源选择器、参数绑定等。
        /// </remarks>
        void SetDataProvider(object dataProvider);

        /// <summary>
        /// 设置主窗口图像控件（第四步，可选）
        /// </summary>
        /// <remarks>
        /// 仅用于区域编辑器等需要在主窗口图像上绘制 ROI 的场景。
        /// 不需要此功能的控件可以提供空实现。
        /// </remarks>
        void SetMainImageControl(ImageControl? imageControl);

        /// <summary>
        /// 异步初始化（最后一步）
        /// </summary>
        /// <remarks>
        /// 在所有 Set 方法完成后调用。
        /// 用于执行依赖多项数据的初始化逻辑。
        /// 例如：初始化区域编辑器（需要 Parameters + MainImageControl）。
        /// </remarks>
        Task InitializeAsync();
    }
}
