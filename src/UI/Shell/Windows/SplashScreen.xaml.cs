using System;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using SunEyeVision.Core.Versioning;

namespace SunEyeVision.UI.Shell.Windows
{
    /// <summary>
    /// 应用启动页 — 在插件加载/服务初始化/解决方案加载等启动耗时阶段提供品牌反馈。
    /// 简洁视觉软件风格：Logo + 应用名 + 版本 + 进度条 + 单行加载状态 + 实时耗时。
    /// 启动过程通过进度条位置与单行状态体现（不列步骤列表）。
    /// 启动页显示失败不阻塞应用（由 App.OnStartup 以 try-catch 保护调用）。
    /// </summary>
    public partial class SplashScreen : Window
    {
        private readonly DispatcherTimer _elapsedTimer = new();
        private readonly DateTime _startTime;

        // 泵消息保护标志：防止 UpdateStatus/UpdateProgress 在泵消息期间被重入（如 DispatcherTimer tick 间接触发）导致死循环。
        private bool _isPumping;

        /// <summary>
        /// 创建并显示启动页（无边框、居中、不抢焦点）
        /// </summary>
        public SplashScreen()
        {
            InitializeComponent();
            _startTime = DateTime.Now;

            // 实时耗时计时器（100ms 刷新）
            _elapsedTimer.Interval = TimeSpan.FromMilliseconds(100);
            _elapsedTimer.Tick += (_, _) => UpdateElapsed();
            _elapsedTimer.Start();

            // 不抢主窗口焦点
            ShowActivated = false;

            // 版本自报（rule-022）：读程序集注入的产品版本，禁止硬编码
            VersionText.Text = "版本 " + AppVersionInfo.Product;
        }

        /// <summary>
        /// 更新单行加载状态文字（如"正在加载模块..."），随启动阶段切换
        /// </summary>
        public void UpdateStatus(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            StatusText.Text = message;
            PumpForRender();
        }

        /// <summary>
        /// 更新进度条（0~100）
        /// </summary>
        public void UpdateProgress(double value)
        {
            Progress.Value = Math.Clamp(value, 0, 100);
            PumpForRender();
        }

        /// <summary>
        /// 同时更新启动阶段、进度和阶段说明。
        /// </summary>
        public void UpdateStage(string stage, double progress, string? detail = null)
        {
            if (!string.IsNullOrWhiteSpace(stage))
                StatusText.Text = stage;
            if (detail != null)
                DetailText.Text = detail;
            Progress.Value = Math.Clamp(progress, 0, 100);
            PumpForRender();
        }

        /// <summary>
        /// 泵 UI 消息队列，强制启动页渲染出中间阶段。
        /// OnStartup 在 UI 线程同步执行耗时初始化，期间若不泵消息，启动页的
        /// 状态文字/进度条即便更新了属性也不会真正重绘。这里在处理完 Render
        /// 优先级后立即返回，让启动页逐阶段可见。
        /// </summary>
        private void PumpForRender()
        {
            // 重入保护：泵消息期间其它 UI 路径（如 DispatcherTimer tick）不应再次进入。
            if (_isPumping) return;
            // 当前不在 UI 线程（理论上不会发生），跳过。
            if (Dispatcher == null || !Dispatcher.CheckAccess()) return;

            _isPumping = true;
            try
            {
                // 泵消息直到 ContextIdle（最低优先级）处理完毕。
                // ContextIdle 低于 Render：PushFrame 在处理到 ContextIdle 前，会先
                // 完成 Render 级重绘（WPF 渲染在 Render 优先级进行），从而让启动页
                // 真正渲染出当前阶段后本方法才返回。
                var frame = new DispatcherFrame();
                Dispatcher.BeginInvoke(
                    DispatcherPriority.ContextIdle,
                    new Action(() => frame.Continue = false));
                Dispatcher.PushFrame(frame);
            }
            finally
            {
                _isPumping = false;
            }
        }

        /// <summary>
        /// 更新已用耗时显示
        /// </summary>
        private void UpdateElapsed()
        {
            var elapsed = DateTime.Now - _startTime;
            ElapsedText.Text = $"已用 {elapsed.TotalSeconds:F1}s";
        }

        /// <summary>
        /// 淡出后关闭启动页（主窗口显示后调用）
        /// </summary>
        public void CloseWithFade()
        {
            if (!IsVisible)
                return;
            _elapsedTimer.Stop();
            var fade = new DoubleAnimation(Opacity, 0, TimeSpan.FromMilliseconds(200));
            fade.Completed += (_, _) => Close();
            BeginAnimation(OpacityProperty, fade);
        }

        /// <summary>
        /// 进入需要用户交互的配置窗口前立即关闭启动页。
        /// </summary>
        public void CloseImmediately()
        {
            _elapsedTimer.Stop();
            if (IsVisible)
                Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            _elapsedTimer.Stop();
            base.OnClosed(e);
        }
    }
}
