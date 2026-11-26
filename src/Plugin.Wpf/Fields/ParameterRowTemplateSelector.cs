using System;
using System.Windows;
using System.Windows.Controls;

namespace SunEyeVision.Plugin.Wpf.Fields;

/// <summary>
/// 按 <see cref="FieldRowModel"/> 解析出的模板键选取原参数行 DataTemplate。
///
/// 对齐原厂 <c>ParamUserControl</c> 的 ContentTemplateSelector 语义：键来自
/// <see cref="DeclaredFieldTemplateRegistry.ResolveTemplateKey"/>，在宿主资源树中查找；
/// 找不到直接抛错（不允许静默渲染成空行）。
/// </summary>
public sealed class ParameterRowTemplateSelector : DataTemplateSelector
{
    public static ParameterRowTemplateSelector Instance { get; } = new();

    public override DataTemplate? SelectTemplate(object item, DependencyObject container)
    {
        if (item is not FieldRowModel model)
            throw new ArgumentException("参数行模板选择器只接受 FieldRowModel 行视图模型。", nameof(item));

        var host = (FrameworkElement)container;
        // 原厂查找顺序：宿主（平台）资源优先，其次才是合并进来的工具字典。
        var template = host.TryFindResource(model.TemplateKey) as DataTemplate
            ?? ParameterTemplateResources.Find(model.TemplateKey)
            ?? throw new InvalidOperationException(
                $"未声明的原参数行模板：{model.TemplateKey}（字段 {model.FieldId}）" +
                "；工具自定义模板需按约定路径贡献 DataTemplate/ParamUserControlDataTemplate.xaml，或显式登记。");
        return template;
    }
}
