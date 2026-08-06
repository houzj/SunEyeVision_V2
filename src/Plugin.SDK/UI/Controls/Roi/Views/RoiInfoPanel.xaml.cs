using System.Windows.Controls;

namespace SunEyeVision.Plugin.SDK.UI.Controls.Roi.Views
{
    /// <summary>
    /// 区域信息面板
    /// </summary>
    public partial class RoiInfoPanel : UserControl
    {
        public RoiInfoPanel()
        {
            // 设计时跳过初始化
            if (System.ComponentModel.DesignerProperties.GetIsInDesignMode(this))
            {
                InitializeComponent();
                return;
            }

            InitializeComponent();
        }
    }
}
