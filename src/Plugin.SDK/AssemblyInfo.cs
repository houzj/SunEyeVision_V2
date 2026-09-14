using System.Windows;

// ★ 必须声明本特性：告诉 WPF "本程序集的默认样式（通用主题字典）就在本程序集内"。
//
// 为什么必需（实测根因）：
//   SDK 控件都是自定义控件（构造函数里 OverrideMetadata(DefaultStyleKeyProperty)），其默认样式
//   由 WPF 从**程序集的主题字典** themes/generic.xaml 中查找。若程序集未声明 ThemeInfo，
//   WPF 不会去查该字典 ⇒ 控件拿不到样式/模板 ⇒ 渲染为**空白**。
//
// 影响面（修复前实测）：
//   · XAML 设计器 / 独立开发环境（无宿主注入主题）：SDK 控件全部不可见
//     （探针实测：同样位置放纯 TextBlock 可见，放 ToggleSwitch/LabeledControl/RangeInputControl 则空白）；
//   · 主程序内之所以正常：宿主在 Application 级显式合并了 SDK 的样式库。
//
// 修复后：样式由 WPF 自动回退（设计器/独立环境也能渲染），宿主注入的亮/暗主题优先级更高、照常覆盖。
[assembly: ThemeInfo(
    ResourceDictionaryLocation.None,             // 本程序集无"主题特定"字典（亮/暗由宿主注入）
    ResourceDictionaryLocation.SourceAssembly)]  // 通用字典 themes/generic.xaml 在本程序集内
