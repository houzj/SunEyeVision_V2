using SunEyeVision.Plugin.Wpf.Native.Dependencies.VisualElements;
using SunEyeVision.Plugin.Wpf.Interop.Windows;
namespace SunEyeVision.Plugin.Wpf.Interop.Windows;

/// <summary>Non-persistent UI event projection of the three members consumed by the original text entry.</summary>
public sealed class TextBoxFocusEventArgs : EventArgs
{
    public string? Text { get; set; }
    public bool IsFocus { get; set; }
    public int SelectionStart { get; set; }
}
