using System.Windows;

namespace SunEyeVision.UI.Shell.Windows;

/// <summary>
/// DeleteSolutionDialog.xaml 的交互逻辑。
/// 主题化无边框确认框：用户选择「仅从列表移除」或「同时删除源文件」。
/// </summary>
public partial class DeleteSolutionDialog : Window
{
    /// <summary>
    /// 用户是否选择同时删除源文件
    /// </summary>
    public bool DeleteFile => DeleteFileRadio?.IsChecked == true;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="solutionName">解决方案名称</param>
    public DeleteSolutionDialog(string solutionName)
    {
        InitializeComponent();

        PromptTextBlock.Text = $"确定要删除解决方案「{solutionName}」吗？";

        // 设置焦点到"仅从列表移除"选项
        Loaded += (s, e) => RemoveOnlyRadio.Focus();
    }

    /// <summary>
    /// 取消按钮点击事件
    /// </summary>
    private void OnCancelButtonClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    /// <summary>
    /// 删除按钮点击事件
    /// </summary>
    private void OnDeleteButtonClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}
