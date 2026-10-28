using System.ComponentModel;
using System.Windows.Media;

namespace SunEyeVision.Plugin.Wpf.Presenters.Display;

internal sealed record NativeGraphicTemplateData(string DisplayNameA,NativeGraphicBridge BridgeObjA)
{
    public string DescriptionA=>DisplayNameA;
    public bool PanVisibleA=>true;
}

/// <summary>Original-template staging properties. Accepted values live in the caller's field states.</summary>
internal sealed class NativeGraphicBridge : INotifyPropertyChanged
{
    private bool _visible;
    private SolidColorBrush _ok=Brushes.Transparent,_ng=Brushes.Transparent;
    private double _opacity,_thickness;
    public bool StrokeThicknessConfigEnable {get;}
    public bool IsDisplayA {get=>_visible;set{if(_visible==value)return;_visible=value;Notify(nameof(IsDisplayA));}}
    public SolidColorBrush okcolorA {get=>_ok;set{if(ReferenceEquals(_ok,value))return;_ok=value;Notify(nameof(okcolorA));}}
    public SolidColorBrush ngcolorA {get=>_ng;set{if(ReferenceEquals(_ng,value))return;_ng=value;Notify(nameof(ngcolorA));}}
    public double opacityA {get=>_opacity;set{if(_opacity==value)return;_opacity=value;Notify(nameof(opacityA));}}
    public double StrokeThicknessA {get=>_thickness;set{if(_thickness==value)return;_thickness=value;Notify(nameof(StrokeThicknessA));}}
    public event PropertyChangedEventHandler? PropertyChanged;
    internal NativeGraphicBridge(bool thickness)=>StrokeThicknessConfigEnable=thickness;
    private void Notify(string property)=>PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(property));
}
