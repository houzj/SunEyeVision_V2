using SunEyeVision.Plugin.Wpf.Adapters.Display;

namespace SunEyeVision.Plugin.Wpf.Presenters.Display;

public sealed partial class NativeResultViewport
{
    /// <summary>One atomic group of the existing geometry and text projections. No second renderer or image owner.</summary>
    public DisplaySceneLayer CreateDisplaySceneLayer(int order=10)
    {
        _view.Dispatcher.VerifyAccess();ObjectDisposedException.ThrowIf(_disposed,this);
        var layer=new DisplaySceneLayer(this,order);_shapeLayers.Add(layer);return layer;
    }
    public sealed class DisplaySceneLayer : ShapeLayer
    {
        internal DisplaySceneLayer(NativeResultViewport owner,int order):base(owner,order){}
        public bool Present(NativeResultFrameReceipt receipt,long revision,IReadOnlyList<NativeGeometryOverlay> geometry,
            IReadOnlyList<NativeTextOverlay> texts,Func<bool> isCurrent,IReadOnlyList<NativeRoiResultOverlay>? roi=null,IReadOnlyList<NativeOutlineOverlay>? outlines=null)
            =>Present(receipt,revision,isCurrent,(owner,current)=>
            {
                if(texts.Sum(text=>(long)(text.Content?.Length??0))>1_000_000)throw new ArgumentException("text-budget: 场景文字超过资源预算。");
                var shapes=NativeShapeProjection.Geometry(receipt,geometry,owner.ShapeBudget,owner._shapeLimits.MaximumPolygonVertices,current);
                if(!current())throw new NativeProjectionRetiredException();
                var words=NativeShapeProjection.Texts(texts,owner.ShapeBudget);
                if(!current())throw new NativeProjectionRetiredException();
                var regions=NativeRoiResultProjection.Prepare(receipt,roi??Array.Empty<NativeRoiResultOverlay>(),owner.ShapeBudget,owner._shapeLimits.MaximumPolygonVertices,current);
                var points=NativeShapeProjection.Outlines(receipt,outlines??Array.Empty<NativeOutlineOverlay>(),owner.ShapeBudget,owner._shapeLimits.MaximumPolygonVertices,current);
                var items=shapes.Items.Concat(regions.Items).Concat(words.Items).Concat(points.Items).ToArray();
                return new PreparedNativeShapes(items,checked(shapes.Count+regions.Count+words.Count+points.Count),checked(shapes.Vertices+regions.Vertices+points.Vertices),
                    NativeShapeProjection.Presentation(items,owner._view));
            });
    }
}
