using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using SunEyeVision.Plugin.Wpf.Controls;

namespace SunEyeVision.Plugin.Wpf.Fields;

/// <summary>Selection updates the owning window's private draft; outer confirm/cancel controls persistence.</summary>
[TemplatePart(Name="PART_ColorPicker",Type=typeof(VmColorPicker))]
[TemplatePart(Name="PART_ColorPickerPopup",Type=typeof(Popup))]
public sealed class ColorField : Control
{
    private Border? _surface, _preview;
    private TextBlock? _rgb;
    private VmColorPicker? _picker;
    private Popup? _popup;
    private Button? _apply, _cancel;
    public static readonly DependencyProperty SelectedColorProperty=DependencyProperty.Register(
        nameof(SelectedColor),typeof(Color),typeof(ColorField),new FrameworkPropertyMetadata(Colors.Red,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,OnColorChanged));
    public Color SelectedColor { get=>(Color)GetValue(SelectedColorProperty); set=>SetValue(SelectedColorProperty,value); }
    public event RoutedPropertyChangedEventHandler<Color>? SelectedColorChanged;
    static ColorField()=>DefaultStyleKeyProperty.OverrideMetadata(typeof(ColorField),new FrameworkPropertyMetadata(typeof(ColorField)));
    public ColorField()=>Unloaded+=(_,_)=>CancelEdit();

    public override void OnApplyTemplate()
    {
        CancelEdit();
        if(_surface is not null){_surface.MouseLeftButtonUp-=Toggle;_surface.KeyDown-=OnKey;}
        if(_apply is not null)_apply.Click-=Apply;
        if(_cancel is not null)_cancel.Click-=Cancel;
        if(_picker is not null){_picker.PreviewKeyDown-=OnKey;_picker.SelectedColorChanged-=OnPendingColorChanged;_picker.PaletteColorChosen-=OnPaletteChosen;}
        base.OnApplyTemplate();
        _surface=GetTemplateChild("PART_SelectorBorder") as Border;
        _preview=GetTemplateChild("PART_ColorPreview") as Border;
        _rgb=GetTemplateChild("PART_RgbText") as TextBlock;
        _picker=GetTemplateChild("PART_ColorPicker") as VmColorPicker;
        _popup=GetTemplateChild("PART_ColorPickerPopup") as Popup;
        _apply=GetTemplateChild("PART_Apply") as Button;
        _cancel=GetTemplateChild("PART_Cancel") as Button;
        if(_surface is not null){_surface.MouseLeftButtonUp+=Toggle;_surface.KeyDown+=OnKey;}
        if(_apply is not null)_apply.Click+=Apply;
        if(_cancel is not null)_cancel.Click+=Cancel;
        if(_picker is not null){_picker.PreviewKeyDown+=OnKey;_picker.SelectedColorChanged+=OnPendingColorChanged;_picker.PaletteColorChosen+=OnPaletteChosen;}
        UpdateDisplay();
    }
    public void BeginEdit(){ApplyTemplate();if(_picker is null||_popup is null)return;_picker.SelectedColor=SelectedColor;_popup.IsOpen=true;}
    public void ApplyEdit(){if(_popup?.IsOpen!=true||_picker is null)return;SetCurrentValue(SelectedColorProperty,_picker.SelectedColor);_popup.IsOpen=false;}
    public void CancelEdit(){if(_popup is not null)_popup.IsOpen=false;}
    private void Toggle(object sender,MouseButtonEventArgs e){if(_popup?.IsOpen==true)CancelEdit();else BeginEdit();e.Handled=true;}
    private void Apply(object sender,RoutedEventArgs e)=>ApplyEdit();
    private void Cancel(object sender,RoutedEventArgs e)=>CancelEdit();
    private void OnKey(object sender,KeyEventArgs e)
    {if(e.Key==Key.Escape){CancelEdit();e.Handled=true;}else if(ReferenceEquals(sender,_surface)&&e.Key is Key.Enter or Key.Space){BeginEdit();e.Handled=true;}}
    private void OnPaletteChosen(object? sender,EventArgs e)=>ApplyEdit();
    private void OnPendingColorChanged(object sender,RoutedPropertyChangedEventArgs<Color> e)
    {
        if(_popup?.IsOpen==true)SetCurrentValue(SelectedColorProperty,e.NewValue);
        e.Handled=true;
    }
    private static void OnColorChanged(DependencyObject sender,DependencyPropertyChangedEventArgs e)
    {var field=(ColorField)sender;field.UpdateDisplay();field.SelectedColorChanged?.Invoke(field,new((Color)e.OldValue,(Color)e.NewValue));}
    private void UpdateDisplay()
    {if(_preview is not null)_preview.Background=new SolidColorBrush(SelectedColor);if(_rgb is not null)_rgb.Text=$"{SelectedColor.R}, {SelectedColor.G}, {SelectedColor.B}";}
}
