using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace SunEyeVision.Plugin.Wpf.Presenters.Numeric;

/// <summary>Finite numerical mapping for the original slider template; retains its actual thumb and repeat buttons.</summary>
public sealed class StableSliderTrack : Track
{
    private bool _arranged;
    private Size _size;
    private bool Flip=>IsDirectionReversed^(Orientation==Orientation.Vertical);
    private double Axis(Size size)=>Orientation==Orientation.Vertical?size.Height:size.Width;
    private bool SliderMode=>double.IsNaN(ViewportSize);
    private static double Position(double value,double minimum,double maximum)
    {
        if(maximum<=minimum)return 0;
        value=Math.Clamp(value,minimum,maximum);var span=maximum-minimum;
        return Math.Clamp(double.IsFinite(span)?(value-minimum)/span:(value/2-minimum/2)/(maximum/2-minimum/2),0,1);
    }
    private static double Interpolate(double minimum,double maximum,double position)
    {
        position=Math.Clamp(position,0,1);if(position==0)return minimum;if(position==1)return maximum;
        return minimum<0&&maximum>0?(1-position)*minimum+position*maximum:minimum+position*(maximum-minimum);
    }
    private (double Thumb,double Travel) Lengths(Size size)
    {
        var length=Axis(size);var desired=Thumb is null?0:Axis(Thumb.DesiredSize);
        var thumb=Math.Clamp(double.IsFinite(desired)?desired:0,0,length);
        return(thumb,length-thumb);
    }
    protected override Size ArrangeOverride(Size arrangeSize)
    {
        if(!SliderMode){_arranged=false;return base.ArrangeOverride(arrangeSize);}
        _arranged=false;
        var (thumb,travel)=Lengths(arrangeSize);var t=Position(Value,Minimum,Maximum);
        var decrease=travel*t;var increase=travel-decrease;
        void Place(UIElement? child,double start,double length)
        {child?.Arrange(Orientation==Orientation.Vertical?new Rect(0,start,arrangeSize.Width,length):new Rect(start,0,length,arrangeSize.Height));}
        if(Flip){Place(IncreaseRepeatButton,0,increase);Place(Thumb,increase,thumb);Place(DecreaseRepeatButton,increase+thumb,decrease);}
        else{Place(DecreaseRepeatButton,0,decrease);Place(Thumb,decrease,thumb);Place(IncreaseRepeatButton,decrease+thumb,increase);}
        _size=arrangeSize;_arranged=true;return arrangeSize;
    }
    private bool TryLengths(out double thumb,out double travel)
    {
        thumb=travel=0;
        if(!_arranged||Thumb is null||RenderSize!=_size||!double.IsFinite(Minimum)||!double.IsFinite(Maximum)||!double.IsFinite(Value))return false;
        (thumb,travel)=Lengths(_size);return travel>0&&Maximum>Minimum;
    }
    public override double ValueFromDistance(double horizontal,double vertical)
    {
        if(!SliderMode)return base.ValueFromDistance(horizontal,vertical);
        var distance=Orientation==Orientation.Vertical?vertical:horizontal;
        if(!double.IsFinite(distance)||!TryLengths(out _,out var travel))return 0;
        var t=Math.Clamp(Position(Value,Minimum,Maximum)+(Flip?-distance:distance)/travel,0,1);
        var target=Interpolate(Minimum,Maximum,t);var delta=target-Value;
        // A full signed extreme domain may require more than one finite delta; the original Slider rejects Infinity.
        if(!double.IsFinite(delta))delta=target>Value?double.MaxValue:-double.MaxValue;
        return double.IsFinite(Value+delta)?delta:0;
    }
    public override double ValueFromPoint(Point point)
    {
        if(!SliderMode)return base.ValueFromPoint(point);
        if(!TryLengths(out var thumb,out var travel))return Math.Clamp(Value,Minimum,Maximum);
        var coordinate=Orientation==Orientation.Vertical?point.Y:point.X;
        if(!double.IsFinite(coordinate))return Math.Clamp(Value,Minimum,Maximum);
        var t=Math.Clamp((coordinate-thumb/2)/travel,0,1);
        return Interpolate(Minimum,Maximum,Flip?1-t:t);
    }
}
