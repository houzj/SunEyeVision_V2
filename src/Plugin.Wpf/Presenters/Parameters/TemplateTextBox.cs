using System.Windows.Controls;
using System.Windows.Input;

namespace SunEyeVision.Plugin.Wpf.Presenters.Parameters;

/// <summary>
/// 模板窗口文本框：Enter 键将焦点移到下一控件（底栏 FixPointX/FixPointY 输入框）。
/// 行为逐字迁移自反编译原件（docs/07-工具逆向/decompiled/PcUi/...），按项目规范改为中性命名。
/// </summary>
public class TemplateTextBox : TextBox
{
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (e.Key == Key.Return)
        {
            MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
            e.Handled = true;
        }
    }
}
