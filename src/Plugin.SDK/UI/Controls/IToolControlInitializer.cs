using System.Threading.Tasks;
using SunEyeVision.Plugin.SDK.Execution.Parameters;

namespace SunEyeVision.Plugin.SDK.UI.Controls
{
    /// <summary>
    /// 工具控件初始化接口 - 定义控件初始化阶段的标准方法
    /// 规范调用顺序：SetCurrentNode → SetParameters → SetDataProvider → SetMainImageControl → InitializeAsync
    /// </summary>
    public interface IToolControlInitializer
    {
        void SetCurrentNode(object node);
        void SetParameters(ToolParameters parameters);
        void SetDataProvider(object dataProvider);
        void SetMainImageControl(HalconImageControl? imageControl);
        Task InitializeAsync();
    }
}
