using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SunEyeVision.Plugin.Wpf.Adapters.Fields;

internal static class NativeTextInheritance
{
    /// <summary>ContentPresenter-generated text can take an application implicit style
    /// across the popup boundary. Keep the original label's inherited properties;
    /// never replace its text, color, font, explicit style or visual template.</summary>
    public static void Restore(DependencyObject root)
    {
        if (root is TextBlock text && text.TemplatedParent is ContentPresenter
            && text.ReadLocalValue(FrameworkElement.StyleProperty) == DependencyProperty.UnsetValue
            && DependencyPropertyHelper.GetValueSource(text, FrameworkElement.StyleProperty).BaseValueSource
                == BaseValueSource.ImplicitStyleReference)
            text.Style = null;
        if (root is not Visual) return;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            Restore(VisualTreeHelper.GetChild(root, i));
    }
}
