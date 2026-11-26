using System.Windows.Controls;

namespace SunEyeVision.Plugin.TemplateStudio.Views;

/// <summary>
/// 模板列表面板（对应原厂 <c>FeatureMatchConfigPanal</c>）。
/// 代码侧只有 DataContext 赋值：布局在 XAML，交互在 <see cref="TemplateConfigPanelModel"/> 的命令里，
/// 这里不出现任何事件处理——与原厂 Panal 一致（原厂同样只写 Command 绑定）。
/// </summary>
public partial class TemplateConfigPanel : UserControl
{
    public TemplateConfigPanel()
    {
        InitializeComponent();
    }

    public TemplateConfigPanel(TemplateConfigPanelModel model) : this()
    {
        DataContext = model;
    }

    /// <summary>当前绑定的面板模型。</summary>
    public TemplateConfigPanelModel? Model => DataContext as TemplateConfigPanelModel;
}
