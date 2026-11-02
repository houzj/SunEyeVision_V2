using System.Windows;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Regions;
using SunEyeVision.Plugin.Wpf.Native.Display.Contracts;

namespace SunEyeVision.Plugin.Wpf.Adapters.Regions;

/// <summary>Borrowed, read-only annulus through the original pixel-space IAnnularSector display contract.</summary>
public sealed class NativeInheritedAnnulusProjection : NativeRoiProjection,IAnnularSector
{
    internal NativeInheritedAnnulusProjection(NativeRoiSession scope,RoiEditItem item):base(scope,item){}
    internal override IReadOnlyList<NativeRoiField> Fields=>Array.Empty<NativeRoiField>();
    protected override IEnumerable<string> PropertyNames=>new[]{nameof(OuterRadius),nameof(InnerRadius),nameof(StartAngle),nameof(AngleRange)};
    protected override IEnumerable<string> GeometryKeys=>new[]{"outer-radius","inner-radius","start-radians","end-radians"};
    protected override double AcceptedValue(string key)=>key switch
    {"x"=>RoiGeometryEditing.Center(Accepted.Geometry).X,"y"=>RoiGeometryEditing.Center(Accepted.Geometry).Y,_=>Accepted.Geometry.Get<double>(key)};
    Point IAnnularSector.CenterPoint=>new(AcceptedValue("x"),AcceptedValue("y"));
    public double OuterRadius=>AcceptedValue("outer-radius");
    public double InnerRadius=>AcceptedValue("inner-radius");
    private double Sweep=>AcceptedValue("end-radians")-AcceptedValue("start-radians");
    // Reverse display traversal for a negative sweep, preserving the host's exact accepted record.
    public double StartAngle=>(Sweep<0?AcceptedValue("end-radians"):AcceptedValue("start-radians"))*180/Math.PI;
    public double AngleRange=>Math.Abs(Math.Abs(Sweep)-2*Math.PI)<=1e-9?360:Math.Abs(Sweep)*180/Math.PI;
    public double Opacity=>1;
    public string Stroke=>"#0F6AF3"; // Existing original ROIBaseColor; not a second geometry/default model.
    public string Fill=>"#00FFFFFF";
    public double StrokeThickness=>1;
    public string ToolTip=>"";
    internal override RecordValue Prepare()=>throw new InvalidOperationException("继承环形只读，不接本地四字段圆环编辑器。");
}
