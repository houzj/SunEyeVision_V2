using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SunEyeVision.Plugin.Wpf.Native.Numeric.ParameterPresentation.ParamTabControl;

namespace SunEyeVision.Plugin.Wpf.Presenters.Parameters;

/// <summary>
/// 匹配类工具模板窗口底栏：匹配中心 FixX/FixY + 锁定 + 确定。
/// 逐字等价迁移自反编译原件 hpfeaturepatmatchdialoguibasestyle.xaml:16-45（BottomBorder DataTemplate）：
///   Border(#C2C2C2 顶边) → Grid(#E6E6E6) → StatusBar(高30/透明/#808080/13) →
///   左：匹配中心 :( FixX , FixY ) [锁定勾选] 锁定文字；右：确定按钮。
/// 等价替换：原厂数值输入框 → TemplateTextBox + 转换器；遮罩由窗口层 Grid 承担；
/// 样式 CheckBoxStyle1/BottomButtonStyle 直接复用已激活字典。
/// 被 HPFeaturepatMatchDialogUI 系的轮廓/灰度/灰度VA/快速特征匹配共用（L3 公共层）。
/// </summary>
public sealed class NativeMatchCenterBar : Control
{
    // 属性名与原厂绑定路径一致（FixPointX/FixPointY），严格照 hpfeaturepatmatchdialoguibasestyle.xaml:24/28。
    public static readonly DependencyProperty FixPointXProperty = DependencyProperty.Register(
        nameof(FixPointX), typeof(double), typeof(NativeMatchCenterBar), new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
    public static readonly DependencyProperty FixPointYProperty = DependencyProperty.Register(
        nameof(FixPointY), typeof(double), typeof(NativeMatchCenterBar), new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
    public static readonly DependencyProperty IsLockedProperty = DependencyProperty.Register(
        nameof(IsLocked), typeof(bool), typeof(NativeMatchCenterBar), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
    public static readonly DependencyProperty ConfirmCommandProperty = DependencyProperty.Register(
        nameof(ConfirmCommand), typeof(System.Windows.Input.ICommand), typeof(NativeMatchCenterBar), new PropertyMetadata(null));

    public double FixPointX { get => (double)GetValue(FixPointXProperty); set => SetValue(FixPointXProperty, value); }
    public double FixPointY { get => (double)GetValue(FixPointYProperty); set => SetValue(FixPointYProperty, value); }
    public bool IsLocked { get => (bool)GetValue(IsLockedProperty); set => SetValue(IsLockedProperty, value); }
    public System.Windows.Input.ICommand? ConfirmCommand { get => (System.Windows.Input.ICommand?)GetValue(ConfirmCommandProperty); set => SetValue(ConfirmCommandProperty, value); }

    static NativeMatchCenterBar()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(NativeMatchCenterBar),
            new FrameworkPropertyMetadata(typeof(NativeMatchCenterBar)));
    }
}
