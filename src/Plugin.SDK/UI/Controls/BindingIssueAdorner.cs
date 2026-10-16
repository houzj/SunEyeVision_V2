using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace SunEyeVision.Plugin.SDK.UI.Controls;

/// <summary>Presentation only; never participates in WPF value validation or parameter persistence.</summary>
internal sealed class BindingIssueAdorner : Adorner
{
    internal BindingIssueAdorner(UIElement element) : base(element) => IsHitTestVisible = false;
    protected override void OnRender(DrawingContext drawingContext)
    {
        var bounds = new Rect(AdornedElement.RenderSize);
        var errorBrush = (AdornedElement as FrameworkElement)?.TryFindResource(Themes.SdkThemeKeys.ErrorBrush) as Brush ?? Brushes.OrangeRed;
        drawingContext.DrawRoundedRectangle(null, new Pen(errorBrush, 1.5), bounds, 3, 3);
        drawingContext.DrawEllipse(errorBrush, null, new Point(System.Math.Max(5, bounds.Width - 5), 5), 4, 4);
        if (AdornedElement is ParamBinding binding && string.IsNullOrEmpty(binding.BindingSource)
            && !binding.IsKeyboardFocusWithin && bounds.Width > 40 && bounds.Height > 12)
        {
            // Draw a watermark, never set TextBox.Text/BindingSource to a diagnostic message.
            var text = new FormattedText("⚠ " + binding.BindingIssue, System.Globalization.CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight, new Typeface("Segoe UI"), 12, errorBrush,
                VisualTreeHelper.GetDpi(this).PixelsPerDip)
            { MaxTextWidth = bounds.Width - 36, MaxLineCount = 1, Trimming = TextTrimming.CharacterEllipsis };
            drawingContext.DrawText(text, new Point(6, System.Math.Max(0, (bounds.Height - text.Height) / 2)));
        }
    }
}
