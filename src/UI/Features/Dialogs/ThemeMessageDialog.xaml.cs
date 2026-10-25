using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.UI.Themes.Base;

namespace SunEyeVision.UI.Features.Dialogs
{
    /// <summary>
    /// 主题化确认/提示对话框，替代系统原生 MessageBox（原生弹窗不跟随亮/暗主题，样式与整体 UI 不搭）。
    /// 通过静态 <see cref="Show"/> 方法调用，返回 <see cref="MessageBoxResult"/> 兼容值，可无缝替换现有 MessageBox 逻辑。
    /// </summary>
    public partial class ThemeMessageDialog : Window
    {
        /// <summary>对话框消息类型</summary>
        public enum MessageIcon
        {
            /// <summary>信息</summary>
            Info,
            /// <summary>询问</summary>
            Question,
            /// <summary>警告</summary>
            Warning,
            /// <summary>错误</summary>
            Error,
        }

        private readonly MessageBoxResult _defaultResult;
        private MessageBoxResult? _result;

        private ThemeMessageDialog(string message, string title, MessageBoxButton button, MessageIcon icon)
        {
            InitializeComponent();

            Title = title;
            TitleText.Text = title;
            MessageText.Text = message;
            _defaultResult = button switch
            {
                MessageBoxButton.OK => MessageBoxResult.OK,
                MessageBoxButton.YesNo => MessageBoxResult.No,
                _ => MessageBoxResult.Cancel,
            };

            ConfigureButtons(button);
            ConfigureIcon(icon);

            // 默认聚焦主按钮（无边框窗口无系统默认焦点）
            Loaded += (s, e) =>
            {
                if (OkButton.Visibility == Visibility.Visible) OkButton.Focus();
                else if (YesButton.Visibility == Visibility.Visible) YesButton.Focus();
                else if (NoButton.Visibility == Visibility.Visible) NoButton.Focus();
                else if (CancelButton.Visibility == Visibility.Visible) CancelButton.Focus();
            };
        }

        /// <summary>
        /// 显示主题化对话框。
        /// </summary>
        /// <param name="message">消息正文</param>
        /// <param name="title">标题</param>
        /// <param name="button">按钮组合</param>
        /// <param name="icon">消息图标类型</param>
        /// <param name="owner">已显示且仍存活的宿主窗口（决定居中与所属窗口层级，可为 null）。关闭确认应先取消本次关闭，
        /// 再由 Dispatcher 延迟执行，此时继续传递原宿主；不要因关闭流程而丢弃 Owner。</param>
        /// <returns>用户选择结果（兼容 MessageBoxResult）</returns>
        public static MessageBoxResult Show(
            string message,
            string title,
            MessageBoxButton button = MessageBoxButton.OK,
            MessageIcon icon = MessageIcon.Info,
            Window? owner = null)
        {
            VisionLogger.Instance.Log(LogLevel.Info, $"[ThemeMessageDialog] 准备显示: title='{title}', button={button}, icon={icon}, ownerIsNull={owner == null}", "ThemeMessageDialog");
            var dialog = new ThemeMessageDialog(message, title, button, icon);
            if (owner != null)
            {
                dialog.Owner = owner;
            }
            else
            {
                dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }
            try
            {
                dialog.ShowDialog();
                VisionLogger.Instance.Log(LogLevel.Info, $"[ThemeMessageDialog] ShowDialog 已返回, result={dialog._result}", "ThemeMessageDialog");
            }
            catch (Exception ex)
            {
                VisionLogger.Instance.Log(LogLevel.Error, $"[ThemeMessageDialog] ShowDialog 抛异常: {ex}", "ThemeMessageDialog");
                throw;
            }
            return dialog._result ?? MessageBoxResult.None;
        }

        private void ConfigureButtons(MessageBoxButton button)
        {
            switch (button)
            {
                case MessageBoxButton.OK:
                    OkButton.Visibility = Visibility.Visible;
                    break;
                case MessageBoxButton.OKCancel:
                    OkButton.Visibility = Visibility.Visible;
                    CancelButton.Visibility = Visibility.Visible;
                    break;
                case MessageBoxButton.YesNo:
                    YesButton.Visibility = Visibility.Visible;
                    NoButton.Visibility = Visibility.Visible;
                    break;
                case MessageBoxButton.YesNoCancel:
                    YesButton.Visibility = Visibility.Visible;
                    NoButton.Visibility = Visibility.Visible;
                    CancelButton.Visibility = Visibility.Visible;
                    break;
            }
        }

        private void ConfigureIcon(MessageIcon icon)
        {
            (string glyph, Brush bg, Brush fg) = icon switch
            {
                MessageIcon.Info => ("i", GetBrush(ThemeKeys.AccentBrush), Brushes.White),
                MessageIcon.Question => ("?", GetBrush(ThemeKeys.AccentBrush), Brushes.White),
                MessageIcon.Warning => ("!", GetBrush(ThemeKeys.WarningBrush), Brushes.White),
                MessageIcon.Error => ("✕", GetBrush(ThemeKeys.ErrorBrush), Brushes.White),
                _ => ("i", GetBrush(ThemeKeys.AccentBrush), Brushes.White),
            };
            IconGlyph.Text = glyph;
            IconBackground.Fill = bg;
            IconGlyph.Foreground = fg;
        }

        private Brush GetBrush(string key)
        {
            if (FindResource(key) is Brush brush)
            {
                return brush;
            }
            throw new InvalidOperationException($"主题画笔未定义: {key}");
        }

        private void OnOkClick(object sender, RoutedEventArgs e)
        {
            _result = MessageBoxResult.OK;
            Close();
        }

        private void OnYesClick(object sender, RoutedEventArgs e)
        {
            _result = MessageBoxResult.Yes;
            Close();
        }

        private void OnNoClick(object sender, RoutedEventArgs e)
        {
            _result = MessageBoxResult.No;
            Close();
        }

        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            _result = MessageBoxResult.Cancel;
            Close();
        }

        /// <summary>用户点标题栏关闭按钮/按 Esc 时，返回取消/否默认值</summary>
        protected override void OnClosing(CancelEventArgs e)
        {
            if (_result == null)
            {
                _result = _defaultResult;
            }
            base.OnClosing(e);
        }
    }
}
