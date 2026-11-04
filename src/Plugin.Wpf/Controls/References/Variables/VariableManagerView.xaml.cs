using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Native.Common.Localization;

namespace SunEyeVision.Plugin.Wpf.Controls.References.Variables;

/// <summary>Original manager composition. The owner supplies its draft and handles editing actions.</summary>
public partial class VariableManagerView : UserControl
{
    public event Action<string, object, RoutedEventArgs>? EditorAction;
    public VariableManagerView()
    {
        InitializeComponent();
        Resources.MergedDictionaries.Add(LocalizationResourceDictionary.Instance);
        Loaded+=(_,_)=>{tabControl.ApplyTemplate();UpdateGroupButton();};
    }
    private void Forward(string action, object sender, RoutedEventArgs args) => EditorAction?.Invoke(action, sender, args);
    private void Editor_Loaded(object sender, RoutedEventArgs e) => Forward("Loaded", sender, e);
    private void Name_TextChanged(object sender, TextChangedEventArgs e) => Forward("NameChanged", sender, e);
    private void Value_TextChanged(object sender, TextChangedEventArgs e) => Forward("ValueChanged", sender, e);
    private void Editor_LostFocus(object sender, RoutedEventArgs e) => Forward("LostFocus", sender, e);
    private void Editor_KeyDown(object sender, KeyEventArgs e) => Forward("KeyDown", sender, e);
    private void Type_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if(sender is ComboBox {IsLoaded:true,IsKeyboardFocusWithin:true}) Forward("TypeChanged",sender,e);
    }
    private void Group_PreviewLostFocus(object sender, KeyboardFocusChangedEventArgs e) => Forward("GroupLostFocus", sender, e);
    private void Group_TextChanged(object sender, TextChangedEventArgs e) => Forward("GroupChanged",sender,e);
    private void Group_KeyDown(object sender, KeyEventArgs e) => Forward("GroupKeyDown", sender, e);
    private void Array_Click(object sender, RoutedEventArgs e) => Forward("Array", sender, e);
    private void Delete_Click(object sender, RoutedEventArgs e) => Forward("Delete", sender, e);
    private void Delete_OnHandler(object sender, RoutedEventArgs e) => Forward("Delete", sender, e);
    private void RenameGroup_Click(object sender, RoutedEventArgs e) => Forward("RenameGroup", sender, e);
    private void DeleteGroup_Click(object sender, RoutedEventArgs e) => Forward("DeleteGroup", sender, e);
    private void Search_Click(object sender, RoutedEventArgs e) => Forward("Search", sender, e);
    private void Sv_ScrollChanged(object sender, ScrollChangedEventArgs e)
        =>UpdateGroupButton();
    private void UpdateGroupButton()
    {
        var tab=tabControl;
        if(tab?.Template?.FindName("scroll",tab) is not ScrollViewer scroll)return;
        var left = tab.Template.FindName("ComContent", tab) as ContentPresenter;
        var right = tab.Template.FindName("subComContent", tab) as ContentPresenter;
        if (left is null || right is null) return;
        var content = left.Content ?? right.Content ?? tab.TryFindResource("Com");
        var overflow = scroll.ScrollableWidth > 0;
        if (overflow) { left.Content = null; right.Content = content; }
        else { right.Content = null; left.Content = content; }
    }
}

public sealed class VariableManagerValueTextProxy : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => Format(value);
    public static string Format(object? value) => value switch
    {
        null => "",
        IReadOnlyStoredRaster => "[图像]",
        RecordValue => "[组合值]",
        SequenceValue => "[组合序列]",
        BinaryValue binary => binary.ToString(),
        Array array => string.Join(", ", array.Cast<object?>().Select(Format)),
        bool boolean => boolean.ToString(),
        IFormattable scalar => scalar.ToString(null, CultureInfo.InvariantCulture),
        string text => text,
        _ => "[不支持的值]"
    };
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}
public sealed class VariableManagerArrayVisibilityConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        var details = values.Length > 1 && values[0] is Array array && (array.Length != 1 || values[1] is string type && type.EndsWith("[]",StringComparison.Ordinal));
        return (parameter as string == "Text" ? !details : details) ? Visibility.Visible : Visibility.Collapsed;
    }
    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
public sealed class VariableManagerValueReadOnlyConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is not string type || type is not ("Integer" or "Double" or "String" or "Boolean");
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}
