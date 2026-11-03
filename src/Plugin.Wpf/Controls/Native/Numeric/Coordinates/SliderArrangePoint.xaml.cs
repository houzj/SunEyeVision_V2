using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
namespace SunEyeVision.Plugin.Wpf.Native.Numeric.Coordinates;
/// <summary>Original point-number/slider template. A borrowed slot stages coordinates; point hashes are never identities.</summary>
public partial class SliderArrangePoint : SliderArrangeBase
{
    public static readonly DependencyProperty IsXProperty=DependencyProperty.Register("IsX",typeof(bool),typeof(SliderArrangePoint),new PropertyMetadata(true,(d,_)=>((SliderArrangePoint)d).ProjectPoint()));
    public static readonly DependencyProperty ImageLenthProperty=DependencyProperty.Register("ImageLenth",typeof(int),typeof(SliderArrangePoint),new PropertyMetadata(0));
    public static readonly DependencyProperty OriPointsProperty=DependencyProperty.Register("OriPoints",typeof(IList<Point>),typeof(SliderArrangePoint),new PropertyMetadata(null));
    public static readonly DependencyProperty PointSourceProperty=DependencyProperty.Register("PointSource",typeof(Point),typeof(SliderArrangePoint),new PropertyMetadata(default(Point),(d,_)=>((SliderArrangePoint)d).ProjectPoint()));
    public static readonly DependencyProperty DecimalDigitsProperty=DependencyProperty.Register("DecimalDigits",typeof(int),typeof(SliderArrangePoint),new PropertyMetadata(2));
    public static readonly DependencyProperty NeedSliderProperty=DependencyProperty.Register("NeedSlider",typeof(bool),typeof(SliderArrangePoint),new PropertyMetadata(true));
    private bool _projecting;
    private Func<double,bool>? _stage;
    private Action? _beforeRelease;
    private long _leaseGeneration;
    private ScrollViewer? _scroll;
    private double _verticalOffset,_horizontalOffset;
    private NumericControl? _leaf;
    public bool IsX{get=>(bool)GetValue(IsXProperty);set=>SetCurrentValue(IsXProperty,value);}
    public int ImageLenth{get=>(int)GetValue(ImageLenthProperty);set=>SetCurrentValue(ImageLenthProperty,value);}
    public IList<Point>? OriPoints{get=>(IList<Point>?)GetValue(OriPointsProperty);set=>SetCurrentValue(OriPointsProperty,value);}
    public Point PointSource{get=>(Point)GetValue(PointSourceProperty);set=>SetCurrentValue(PointSourceProperty,value);}
    public int DecimalDigits{get=>(int)GetValue(DecimalDigitsProperty);set=>SetCurrentValue(DecimalDigitsProperty,value);}
    public bool NeedSlider{get=>(bool)GetValue(NeedSliderProperty);set=>SetCurrentValue(NeedSliderProperty,value);}
    public SliderArrangePoint()
    {
        InitializeComponent();
        DataContextChanged+=(_,_)=>ReleaseInput();
        Unloaded+=(_,_)=>ReleaseInput();
    }
    private void ProjectPoint()
    {
        _projecting=true;
        try{SetCurrentValue(StartValueProperty,IsX?PointSource.X:PointSource.Y);}
        finally{_projecting=false;}
    }
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs args)
    {
        base.OnPropertyChanged(args);
        if(args.Property==StartValueProperty&&!_projecting&&_stage is {} stage)
        {
            var generation=_leaseGeneration;
            if(!stage(StartValue)&&generation==_leaseGeneration)ProjectPoint();
        }
    }
    internal IDisposable BindStage(Func<double,bool> stage)=>BindSlot(stage,()=>{});
    internal IDisposable BindSlot(Func<double,bool> stage,Action beforeRelease)
    {
        ArgumentNullException.ThrowIfNull(stage);ArgumentNullException.ThrowIfNull(beforeRelease);
        var generation=++_leaseGeneration;_stage=stage;_beforeRelease=beforeRelease;
        return new StageLease(()=>{if(generation==_leaseGeneration){_stage=null;_beforeRelease=null;_leaseGeneration++;}});
    }
    private sealed class StageLease(Action release):IDisposable
    {private Action? _release=release;public void Dispose(){var release=_release;_release=null;release?.Invoke();}}
    internal NumericControl EnsurePointInput()
    {
        // The original InitUI temporarily changes three star columns; run once, not per LayoutUpdated attachment.
        EnsureCompositionLayout();
        ToBtn.ApplyTemplate();
        var leaf=ToBtn.Template.FindName("T1",ToBtn) as NumericControl??throw new InvalidOperationException("原Point模板缺少T1。");
        T1_Loaded(leaf,new RoutedEventArgs());return leaf;
    }
    internal void ClosePointPopup()=>ToBtn.SetCurrentValue(ToggleButton.IsCheckedProperty,false);
    private void ReleaseInput()
    {
        var generation=++_leaseGeneration;var before=_beforeRelease;var leaf=_leaf;var scroll=_scroll;
        _stage=null;_beforeRelease=null;_scroll=null;
        try{before?.Invoke();}
        finally
        {
            if(scroll is not null)scroll.ScrollChanged-=ScrollChanged;
            if(generation==_leaseGeneration)
            {ClosePointPopup();if(leaf is not null)BindingOperations.ClearBinding(leaf,NumericControl.TextProperty);}
        }
    }
    protected override void T1_Loaded(object sender,RoutedEventArgs args)
    {
        if(sender is not NumericControl leaf)return;
        if(_leaf is not null&&!ReferenceEquals(_leaf,leaf))
        {ReleaseInput();BindingOperations.ClearBinding(_leaf,NumericControl.TextProperty);}
        _leaf=leaf;base.T1_Loaded(sender,args);
        var format=GetDecimalStr(DecimalDigits);
        var old=BindingOperations.GetBinding(leaf,NumericControl.TextProperty);
        if(ReferenceEquals(old?.Source,this)&&old.Path?.Path==nameof(StartValue)&&old.StringFormat==format)return;
        leaf.SetBinding(NumericControl.TextProperty,new Binding(nameof(StartValue)){Source=this,Mode=BindingMode.TwoWay,StringFormat=format});
    }
    private static string GetDecimalStr(int digits)=>digits>=0&&digits<10?"0."+new string('0',digits):"0.000";
    private void PART_Popup_Opened(object sender,EventArgs args)
    {
        SilderWidth=(int)ToBtn.ActualWidth;ClipSilder();DetachScroll();
        for(DependencyObject? parent=ToBtn;parent is not null;parent=VisualTreeHelper.GetParent(parent))
            if(parent is ScrollViewer scroll)
            {_scroll=scroll;_verticalOffset=scroll.VerticalOffset;_horizontalOffset=scroll.HorizontalOffset;scroll.ScrollChanged+=ScrollChanged;break;}
    }
    private void DetachScroll(){if(_scroll is not null)_scroll.ScrollChanged-=ScrollChanged;_scroll=null;}
    private void ScrollChanged(object sender,ScrollChangedEventArgs args)
    {
        if(!ReferenceEquals(sender,_scroll)||_scroll is null)return;
        if(Math.Abs(_verticalOffset-_scroll.VerticalOffset)>3||Math.Abs(_horizontalOffset-_scroll.HorizontalOffset)>3)ClosePointPopup();
    }
    private void PART_Popup_Closed(object sender,EventArgs args)=>DetachScroll();
    private void PART_Popup_GotFocus(object sender,RoutedEventArgs args)=>ToBtn.SetCurrentValue(ToggleButton.IsCheckedProperty,true);
    private void PART_Popup_LostFocus(object sender,RoutedEventArgs args)=>ClosePointPopup();
    private void T1_GotFocus(object sender,RoutedEventArgs args)=>ToBtn.SetCurrentValue(ToggleButton.IsCheckedProperty,true);
    private void T2_GotFocus(object sender,RoutedEventArgs args)=>ToBtn.SetCurrentValue(ToggleButton.IsCheckedProperty,true);
    private new void T1_LostFocus(object sender,RoutedEventArgs args){base.T1_LostFocus(sender,args);ClosePointPopup();}
    private new void T2_LostFocus(object sender,RoutedEventArgs args){base.T2_LostFocus(sender,args);ClosePointPopup();}
    private void NumericControl_KeyDown(object sender,KeyEventArgs args)
    {
        // A composed editor handles Preview Enter first and closes only after its complete candidate is valid.
        if(args.Key==Key.Enter&&!args.Handled&&_stage is null){ClosePointPopup();Keyboard.ClearFocus();args.Handled=true;}
    }
    private new void SL_Bat1_ValueChanged(object sender,RoutedPropertyChangedEventArgs<double> args){}
}
