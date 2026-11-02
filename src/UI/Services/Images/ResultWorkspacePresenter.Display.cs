using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;
using SunEyeVision.Plugin.Wpf.Presenters.Display;
using SunEyeVision.Plugin.Wpf.Presenters.Results;

namespace SunEyeVision.UI.Services.Images;

internal sealed partial class ResultWorkspacePresenter
{
    private sealed class DisplayScene : IDisposable
    {
        internal readonly IResultValueLease Values;
        internal readonly NativeResultViewport.DisplaySceneLayer Layer;
        internal bool Disposed;
        internal readonly long Attempt;
        internal DisplayScene(IResultValueLease values,NativeResultViewport.DisplaySceneLayer layer,long attempt){Values=values;Layer=layer;Attempt=attempt;}
        public void Dispose(){if(Disposed)return;Disposed=true;Run([Layer.Dispose,Values.Dispose]);}
    }
    private DispatcherOperation? _displayRefresh;
    private long _displayRequest,_displayAttempt;
    private void QueueResultDisplay()
    {
        if(_disposed)return;var request=++_displayRequest;
        _displayRefresh?.Abort();
        _displayRefresh=_shell.Dispatcher.BeginInvoke(DispatcherPriority.Render,new Action(()=>
        {if(_disposed||request!=_displayRequest)return;_displayRefresh=null;RefreshResultDisplay();}));
    }
    private void RefreshResultDisplay()
    {
        var request=_displayRequest;var epoch=_epoch;var attempt=++_displayAttempt;
        var owners=new[]{_input,_output}.Select(pane=>(Pane:pane,Scene:pane.Display,Generation:pane.Generation)).ToArray();
        try{RefreshResultDisplayCore();}
        catch(Exception error)
        {
            foreach(var owner in owners)
            {
                if(_disposed||request!=_displayRequest||epoch!=_epoch||attempt!=_displayAttempt)return;
                if(owner.Pane.Generation!=owner.Generation)continue;
                var current=owner.Pane.Display;
                if(!ReferenceEquals(current,owner.Scene)&&current?.Attempt!=attempt)continue;
                owner.Pane.Display=null;
                try{current?.Dispose();}catch(Exception cleanup){error=new AggregateException(error,cleanup);}
            }
            if(!_disposed&&request==_displayRequest&&epoch==_epoch&&attempt==_displayAttempt)
                try{_main.AddLog("结果图文准备失败："+error.Message);}catch{}
        }
    }
    private void RefreshResultDisplayCore()
    {
        var attempt=_displayAttempt;
        var publication=_main.CurrentResultPublication;var epoch=_epoch;var request=_displayRequest;
        var state=publication is null?null:_main.CaptureCurrentResultDisplay(publication);
        bool Current()=>!_disposed&&epoch==_epoch&&request==_displayRequest&&attempt==_displayAttempt&&ReferenceEquals(publication,_main.CurrentResultPublication)
            &&publication?.IsCurrent()==true&&state?.IsCurrent()==true&&!_disposed&&epoch==_epoch&&request==_displayRequest&&attempt==_displayAttempt;
        foreach(var pane in new[]{_input,_output})
        {
            if(_disposed||epoch!=_epoch||request!=_displayRequest||attempt!=_displayAttempt)return;
            var original=pane.Display;var paneGeneration=pane.Generation;
            bool Attempt()=>!_disposed&&epoch==_epoch&&request==_displayRequest&&attempt==_displayAttempt&&paneGeneration==pane.Generation&&ReferenceEquals(pane.Display,original);
            void ClearOriginal(){if(!Attempt())return;pane.Display=null;original?.Dispose();}
            if(state is null||publication is null||!Current()||pane.Image.CurrentFrame is not {} receipt)
            {ClearOriginal();continue;}
            IResultValueLease? values=null;DisplayScene? candidate=null;
            try
            {
                state.Descriptor.Validate(publication.Schema);values=publication.Borrow();
                if(values.Values is null||values.Stamp!=publication.Stamp||!Current()){values.Dispose();values=null;ClearOriginal();continue;}
                var rows=values.Values.Read(state.Descriptor.Rows.PortId);
                foreach(var member in state.Descriptor.Rows.MemberFieldPath)rows=rows.GetField(member);
                var geometry=new List<NativeGeometryOverlay>();var texts=new List<NativeTextOverlay>();var ids=new HashSet<long>();long textCharacters=0;
                DeclaredValueReader Member(DeclaredValueReader row,ResultFieldPath path)=>row.GetField(path.MemberFieldPath.Last());
                for(var i=0;i<rows.Count;i++)
                {
                    if(!Current())throw new InvalidOperationException("显示准备期间结果或样式已失效。");
                    var row=rows.GetElement(i);var id=Member(row,state.Descriptor.Identity).GetScalar<long>();
                    if(!ids.Add(id))throw new InvalidOperationException("结果显示对象身份重复。");
                    using var region=Member(row,state.Descriptor.GeometrySource).AcquireRegionReader();
                    if(region.SourceFrame!=receipt.Frame||region.Frame!=receipt.Coordinates)throw new InvalidOperationException("结果显示与当前图像不是同一物理帧或坐标域。");
                    var passed=Member(row,state.Descriptor.Passed).GetScalar<bool>();
                    var appearance=state.Appearance;
                    if(appearance.Graphic.Visible)
                    {
                        var corners=Member(row,state.Descriptor.Quadrilateral);if(corners.Count!=4)throw new InvalidOperationException("外接矩形缺少四个真实角点。");
                        var points=new List<Point2>();
                        for(var p=0;p<corners.Count;p++)
                        {if(corners.GetElement(p).GetGeometry() is not PointGeometry point||point.Frame!=receipt.Coordinates)throw new InvalidOperationException("矩形角点坐标域不匹配。");points.Add(point.Point);}
                        geometry.Add(new(region.SourceFrame,new PolygonGeometry(receipt.Coordinates,points),Color(passed?appearance.Graphic.Ok:appearance.Graphic.Ng),appearance.Graphic.Width,appearance.Graphic.Opacity));
                    }
                    if(appearance.Text.Visible)
                    {
                        var area=Member(row,state.Descriptor.TextValue).GetScalar<double>();
                        if(appearance.Text.Content.Length>1_000_000)throw new ArgumentException("text-budget: 场景文字超过资源预算。");
                        var content=string.Format(CultureInfo.CurrentCulture,appearance.Text.Content,area);textCharacters+=content.Length;
                        if(textCharacters>1_000_000)throw new ArgumentException("text-budget: 场景文字超过资源预算。");
                        texts.Add(new(content,new Point(appearance.Text.X,appearance.Text.Y),
                            checked((int)appearance.Text.FontSize),Color(passed?appearance.Text.Ok:appearance.Text.Ng),appearance.Text.Opacity));
                    }
                }
                if(!Current()||pane.Image.CurrentFrame!=receipt){values.Dispose();values=null;ClearOriginal();continue;}
                if(!Attempt()){values.Dispose();values=null;continue;}
                var layer=pane.Image.Viewport.CreateDisplaySceneLayer();candidate=new(values,layer,attempt);values=null;
                if(!Attempt()){candidate.Dispose();continue;}
                pane.Display=candidate;original?.Dispose();
                bool Owns()=>Current()&&epoch==_epoch&&request==_displayRequest&&attempt==_displayAttempt&&paneGeneration==pane.Generation&&ReferenceEquals(pane.Display,candidate)&&!candidate.Disposed&&pane.Image.CurrentFrame==receipt;
                if(!Owns()||!layer.Present(receipt,++_layerRevision,geometry,texts,Owns))
                {if(ReferenceEquals(pane.Display,candidate))pane.Display=null;candidate.Dispose();}
            }
            catch(Exception error)
            {
                // A runtime preparation failure clears this entire scene; never preserve half of a new publication.
                var cleanup=new List<Action>();if(values is not null)cleanup.Add(values.Dispose);
                if(candidate is not null){if(ReferenceEquals(pane.Display,candidate))pane.Display=null;cleanup.Add(candidate.Dispose);}
                else cleanup.Add(ClearOriginal);
                try{Run(cleanup);}catch(Exception failure){error=new AggregateException(error,failure);}
                if(!_disposed&&epoch==_epoch&&request==_displayRequest&&attempt==_displayAttempt)_main.AddLog("结果图文未显示："+error.Message);
            }
        }
    }
    private static Color Color(ResultDisplayColor color)=>System.Windows.Media.Color.FromArgb(color.Alpha,color.Red,color.Green,color.Blue);
}
