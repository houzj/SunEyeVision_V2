namespace SunEyeVision.Plugin.Wpf.Native.Dependencies.Apps.UIHelper.Extern;

/// <summary>Non-persistent UI event projection of the three members consumed by the original text entry.</summary>
public sealed class TextBoxFocusEventArgs : EventArgs
{
    public string? Text { get; set; }
    public bool IsFocus { get; set; }
    public int SelectionStart { get; set; }
}
