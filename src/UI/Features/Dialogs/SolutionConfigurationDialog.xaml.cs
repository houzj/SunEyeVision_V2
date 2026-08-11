using System.Windows;
using SunEyeVision.UI.ViewModels;
using SunEyeVision.Workflow;

namespace SunEyeVision.UI.Views.Windows;

/// <summary>
/// SolutionConfigurationDialog.xaml 的交互逻辑
/// </summary>
public partial class SolutionConfigurationDialog : Window
{
    private readonly SolutionConfigurationDialogViewModel _viewModel;

    /// <summary>
    /// 获取启动结果
    /// </summary>
    public Solution? LaunchResult => _viewModel.LaunchResult;

    /// <summary>
    /// 是否成功启动
    /// </summary>
    public bool IsLaunched => _viewModel.IsLaunched;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="solutionManager">解决方案管理器</param>
    /// <param name="preselectSolutionId">预选中的解决方案ID</param>
    public SolutionConfigurationDialog(SolutionManager solutionManager, string? preselectSolutionId = null)
    {
        InitializeComponent();

        _viewModel = new SolutionConfigurationDialogViewModel(solutionManager, preselectSolutionId);
        DataContext = _viewModel;

        // 设置所有者窗口，使双击时能正确关闭
        _viewModel.SetOwnerWindow(this);
    }

    /// <summary>
    /// 窗口关闭时
    /// </summary>
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        // ✅ 统一保存设置（包括 SkipStartupConfig）
        // 无论通过什么方式关闭，都统一保存设置
        _viewModel.SaveSettings();

        // 如果已启动（双击或点击启动按钮），直接关闭
        if (_viewModel.IsLaunched)
        {
            base.OnClosing(e);
            return;
        }

        // 保存当前解决方案（确保修改的名称和描述被保存）
        _viewModel.SaveCurrentSolution();

        // 如果用户没有点击启动或跳过，确认是否关闭
        var result = MessageBox.Show(
            "确定要关闭配置界面吗？",
            "确认",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.No)
        {
            e.Cancel = true;
        }

        base.OnClosing(e);
    }
}
