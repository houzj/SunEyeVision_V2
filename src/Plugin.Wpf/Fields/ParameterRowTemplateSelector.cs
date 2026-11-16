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
        var template = host.TryFindResource(model.TemplateKey) as DataTemplate
            ?? throw new InvalidOperationException($"未声明的原参数行模板：{model.TemplateKey}（字段 {model.FieldId}）");
        return template;
    }
}
