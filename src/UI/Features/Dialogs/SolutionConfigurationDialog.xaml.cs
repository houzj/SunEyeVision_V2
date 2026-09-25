using System.Windows;
using SunEyeVision.Core.Versioning;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.UI.Core;
using SunEyeVision.Workflow;

namespace SunEyeVision.UI.Features.Dialogs;

/// <summary>
/// SolutionConfigurationDialog.xaml 的交互逻辑
/// </summary>
public partial class SolutionConfigurationDialog : Window
{
    private readonly SolutionConfigurationDialogViewModel _viewModel;

    /// <summary>
    /// 自定义无边框窗口 Chrome：CaptionHeight 覆盖标题栏高度，边缘可缩放
    /// </summary>
    public static System.Windows.Shell.WindowChrome DialogChrome { get; } = new System.Windows.Shell.WindowChrome
    {
        CaptionHeight = 64,
        ResizeBorderThickness = new Thickness(6),
        GlassFrameThickness = new Thickness(0),
        CornerRadius = new CornerRadius(0),
        UseAeroCaptionButtons = false
    };

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

        // 标题栏版本号：读程序集注入的产品版本（rule-022），禁止硬编码
        VersionTextBlock.Text = "v" + AppVersionInfo.Product;
    }

    /// <summary>
    /// 标题栏关闭按钮：仅关闭对话框（由调用方决定退出或回主窗口）
    /// </summary>
    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    /// <summary>
    /// 窗口关闭时
    /// </summary>
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        VisionLogger.Instance.Log(LogLevel.Info,
            $"[SolutionConfigTrace] dialog closing | isLaunched={_viewModel.IsLaunched}, launchResult={_viewModel.LaunchResult?.Id ?? "null"}, dialogResult={DialogResult?.ToString() ?? "null"}",
            "SolutionConfigurationDialog");

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

        base.OnClosing(e);
    }
}
