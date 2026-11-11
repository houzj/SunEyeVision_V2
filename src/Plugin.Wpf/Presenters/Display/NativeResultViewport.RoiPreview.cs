using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Adapters.Display;
using SunEyeVision.Plugin.Wpf.Adapters.Regions;
using SunEyeVision.Plugin.Wpf.Regions;
using System.Windows.Media;

namespace SunEyeVision.Plugin.Wpf.Presenters.Display;

public sealed partial class NativeResultViewport
{
    internal ReadOnlyRoiLayer CreateReadOnlyRoiLayer(IReadOnlyImage image)
    {
        _view.Dispatcher.VerifyAccess();ObjectDisposedException.ThrowIf(_disposed,this);
        var layer=new ReadOnlyRoiLayer(this,image);_shapeLayers.Add(layer);return layer;
    }
    /// <summary>Independent original shape renderer. Never installs ROIList or an editable attachment.</summary>
    public sealed class ReadOnlyRoiLayer : ShapeLayer
    {
        private readonly IReadOnlyImage _image;
        private NativeRoiSession? _projection;
        private readonly Guid _scope=Guid.NewGuid();
        internal ReadOnlyRoiLayer(NativeResultViewport owner,IReadOnlyImage image):base(owner,45){_image=image;}
        public bool Present(NativeResultFrameReceipt receipt,long revision,IReadOnlyList<RoiEditItem> items,Func<bool> current)
        {
            var owner=Owner??throw new ObjectDisposedException(nameof(ReadOnlyRoiLayer));
            if(_image.Frame!=receipt.Frame||_image.EffectiveDomain.Frame!=receipt.Coordinates)
                throw new ArgumentException("继承预览必须使用同一实际帧和坐标域。");
            var rows=items.ToArray();
            if(rows.Count(item=>item.Group=="inspection")>64||rows.Count(item=>item.Group=="mask")>64||
                rows.Any(item=>item.Enabled||!item.Visible||!(item.Group switch
                {
                    "inspection"=>item.Geometry.Get<string>("kind") is "rectangle" or "annulus",
                    "mask"=>item.Geometry.Get<string>("kind")=="polygon",
                    _=>false
                })))
                throw new ArgumentException("继承预览只接纳每组最多64个检测形状或屏蔽多边形。");
            var detection=rows.Where(row=>row.Group=="inspection").ToArray();
            var snapshot=new RoiEditSnapshot(new(_scope,revision,receipt.Frame),detection,
                new[]{new RoiGroupState("inspection",detection.Length==0?RoiCoverage.FullDomain:RoiCoverage.Explicit,RoiCoverage.FullDomain)});
            NativeRoiSession? candidate=null;
            try
            {
                candidate=new NativeRoiSession(_image,()=>snapshot,(stamp,proposal)=>RoiEditAcceptance.Reject(snapshot,"继承预览只读。"),current,()=>new(false,false),"inspection",canDraw:_=>false,canSetCoverage:_=>false);
                candidate.Refresh();
                var frozen=candidate;
                var accepted=Present(receipt,revision,current,(_,proof)=>
                {
                    NativeResultRaster.CheckCurrent(proof);
                    var rectangles=rows.Where(row=>row.Geometry.Get<string>("kind")=="rectangle").Select(row=>
                    {
                        var shape=row.Geometry;var center=RoiGeometryEditing.Center(shape);
                        return new NativeGeometryOverlay(receipt.Frame,new OrientedRectangleGeometry(receipt.Coordinates,new(center.X,center.Y),shape.Get<double>("width"),shape.Get<double>("height"),(shape.Get<double>("angle-degrees")%360)*Math.PI/180),Color.FromRgb(15,106,243));
                    }).ToArray();
                    var polygons=rows.Where(row=>row.Group=="mask").Select(row=>new NativeGeometryOverlay(receipt.Frame,
                        new PolygonGeometry(receipt.Coordinates,RoiGeometryEditing.Vertices(row.Geometry)),Color.FromRgb(255,84,0)));
                    var prepared=NativeShapeProjection.Geometry(receipt,rectangles.Concat(polygons).ToArray(),owner.ShapeBudget,owner._shapeLimits.MaximumPolygonVertices,proof);
                    var rings=frozen.Items.OfType<NativeInheritedAnnulusProjection>().Cast<object>().ToArray();
                    NativeResultRaster.CheckCurrent(proof);return new(prepared.Items.Concat(rings).ToArray(),rows.Length,prepared.Vertices);
                });
                if(!accepted)return false;
                var previous=_projection;_projection=candidate;candidate=null;previous?.Dispose();return true;
            }
            finally{candidate?.Dispose();}
        }
        protected override void OnDisposed(){var projection=_projection;_projection=null;projection?.Dispose();}
    }
}
