using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SunEyeVision.Plugin.Wpf.Adapters.Resources;

namespace SunEyeVision.Plugin.Wpf.Presenters.Input;

/// <summary>Loads original color keys locally. Popup templates remain in their owning native family.</summary>
public static class NativeInputFactory
{
    private const string Root="/SunEyeVision.Plugin.Wpf;component/";
    private static readonly DependencyProperty EnumerationTextScopeProperty=DependencyProperty.RegisterAttached(
        "EnumerationTextScope",typeof(NativeEnumerationTextScope),typeof(NativeInputFactory),new PropertyMetadata(null));
    public static ComboBox Enumeration()
    {
        var combo=new ComboBox{Height=30,IsEditable=false,HorizontalAlignment=HorizontalAlignment.Stretch};
        combo.Resources.MergedDictionaries.Add(OriginalThemeResources.Create());
        foreach(var name in new[]{"SourceScrollBars.xaml","SourceComboBoxes.xaml"})
            combo.Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri(Root+"Themes/Native/Input/VisionUi/"+name,UriKind.Relative)});
        combo.SetResourceReference(FrameworkElement.StyleProperty,"ListViewComboBoxStyle");
        combo.SetValue(EnumerationTextScopeProperty,new NativeEnumerationTextScope(combo));
        return combo;
    }
    internal static void ReleaseEnumeration(ComboBox combo)
    {
        if(combo.GetValue(EnumerationTextScopeProperty) is not NativeEnumerationTextScope scope)return;
        try{scope.Dispose();}finally{combo.ClearValue(EnumerationTextScopeProperty);}
    }
    public static Image BooleanImage(bool value)
    {
        // Both faces must resolve even before the inactive Image is attached to a visual tree.
        var bitmap=new BitmapImage(new Uri("pack://application:,,,"+Root+
            "Themes/Assets/Native/Composition/Apps.XmlUI/resources/"+(value?"swith_on.png":"swith_off.png"),UriKind.Absolute));
        bitmap.Freeze();
        return new Image{Stretch=Stretch.None,Source=bitmap};
    }

}
