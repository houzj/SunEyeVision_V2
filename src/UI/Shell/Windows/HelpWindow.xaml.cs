using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace SunEyeVision.UI.Views.Windows
{
    /// <summary>
    /// 帮助窗口 - 支持显示内容和外部 HTML 文档
    /// </summary>
    public partial class HelpWindow : Window
    {
        private readonly string _helpDirectory;

        public HelpWindow()
        {
            InitializeComponent();

            // 获取文档目录 - 使用工作目录
            _helpDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Help");
        }

        /// <summary>
        /// 窗口加载时显示帮助
        /// </summary>
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // 当前窗口已打开，显示简帮助
            // 用户通过按钮可外部查看文档
        }

        /// <summary>
        /// 打开文档按钮点击事件
        /// </summary>
        private void OpenFullDocsButton_Click(object sender, RoutedEventArgs e)
        {
            // 先尝试在可执行文件路径
            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            string indexPath = Path.Combine(basePath, "Help", "index.html");

            // 如果文件不存在，尝试解决方案路径
            if (!File.Exists(indexPath))
            {
                // 从当前目录向上查找
                string solutionDir = Path.GetFullPath(Path.Combine(basePath, "..", "..", "..", ".."));
                indexPath = Path.Combine(solutionDir, "Help", "Output", "index.html");
            }

            // 调试信息
            string message = $"当前目录: {basePath}\n路径: {indexPath}\n文件存在: {File.Exists(indexPath)}";

            if (File.Exists(indexPath))
            {
                try
                {
                    // 使用默认浏览器打开 HTML 文档
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = indexPath,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"无法打开帮助文档:\n{ex.Message}\n\n{message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MessageBox.Show($"帮助文档未找到!\n{indexPath}\n\n请确保 Help 文件夹存在并包含帮助文档\n\n{message}", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        /// <summary>
        /// 加载帮助内容
        /// </summary>
        private void LoadHelpContent()
        {
            // 先尝试 CHM 文件
            string chmPath = Path.Combine(_helpDirectory, "SunEyeVision.chm");

            if (File.Exists(chmPath))
            {
                // 使用 CHM 文件
                ShowChmHelp(chmPath);
            }
            else
            {
                // 使用 HTML 文件
                ShowHtmlHelp();
            }
        }

        /// <summary>
        /// 显示 CHM 文档
        /// </summary>
        private void ShowChmHelp(string chmPath)
        {
            try
            {
                // 使用 Windows Help API
                var psi = new ProcessStartInfo
                {
                    FileName = "hh.exe",
                    Arguments = chmPath,
                    UseShellExecute = true
                };
                Process.Start(psi);
                this.Close(); // 关闭窗口,CHM 独立打开
            }
            catch (Exception ex)
            {
                MessageBox.Show($"无法打开 CHM 文档: {ex.Message}\n将使用 HTML 文档。", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                ShowHtmlHelp();
            }
        }

        /// <summary>
        /// 显示 HTML 文档
        /// </summary>
        private void ShowHtmlHelp()
        {
            string indexPath = Path.Combine(_helpDirectory, "index.html");

            if (File.Exists(indexPath))
            {
                try
                {
                    // 使用默认浏览器打开 HTML 文档
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = indexPath,
                        UseShellExecute = true
                    });

                    this.Close(); // 关闭窗口,HTML 在浏览器打开
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"无法打开 HTML 文档: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    ShowFallbackHelp();
                }
            }
            else
            {
                ShowFallbackHelp();
            }
        }

        /// <summary>
        /// 显示简帮助信息(文档文件缺失时)
        /// </summary>
        private void ShowFallbackHelp()
        {
            // 显示简帮助
            HelpTitle.Text = "SunEyeVision 帮助";

            string fallbackContent = GetFallbackContent();
            HelpContent.Inlines.Clear();
            HelpContent.Inlines.Add(new System.Windows.Documents.Run(fallbackContent));
        }

        /// <summary>
        /// 获取简帮助内容
        /// </summary>
        private string GetFallbackContent()
        {
            return "SunEyeVision 帮助文档\n\n" +
                   "欢迎使用 SunEyeVision 视觉算法平台!\n\n" +
                   "文档说明:\n" +
                   "- 按 F1 快速打开帮助\n" +
                   "- 支持 CHM 和 HTML 两种格式\n" +
                   "- 文档位于 Help/Output 目录\n\n" +
                   "文档包含:\n" +
                   "- 用户手册 - 使用指南\n" +
                   "- 功能架构 - 系统设计和架构说明\n" +
                   "- 开发计划 - 未来规划\n" +
                   "- 更新日志 - 当前状态\n" +
                   "- API 文档 - 开发者参考\n\n" +
                   "注意: 当前文档文件未找到\n" +
                   "请运行 tools\\GenerateHelpDocumentation.ps1 脚本生成帮助文档\n\n" +
                   "技术支持: support@suneyevision.com";
        }

        /// <summary>
        /// 显示指定主题的帮助
        /// </summary>
        public void ShowTopic(string topic)
        {
            string chmPath = Path.Combine(_helpDirectory, "SunEyeVision.chm");

            if (File.Exists(chmPath))
            {
                try
                {
                    // 使用 Windows Help API 显示特定主题
                    var psi = new ProcessStartInfo
                    {
                        FileName = "hh.exe",
                        Arguments = $"\"{chmPath}::{topic}\"",
                        UseShellExecute = true
                    };
                    Process.Start(psi);
                }
                catch
                {
                    ShowHtmlHelp();
                }
            }
            else
            {
                ShowHtmlHelp();
            }
        }

        /// <summary>
        /// 搜索帮助关键词
        /// </summary>
        public void SearchHelp(string keyword)
        {
            string chmPath = Path.Combine(_helpDirectory, "SunEyeVision.chm");

            if (File.Exists(chmPath))
            {
                try
                {
                    // 使用 Windows Help API 搜索
                    var psi = new ProcessStartInfo
                    {
                        FileName = "hh.exe",
                        Arguments = $"\"{chmPath}\"::/Keyword,{keyword}",
                        UseShellExecute = true
                    };
                    Process.Start(psi);
                }
                catch
                {
                    ShowHtmlHelp();
                }
            }
            else
            {
                ShowHtmlHelp();
            }
        }
    }
}
