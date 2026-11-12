using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;
using SunEyeVision.Plugin.Wpf.Presenters.Display;
using SunEyeVision.Plugin.Wpf.Presenters.Results;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.PresentationControls.RenderInterface;

namespace SunEyeVision.UI.Services.Images;

internal sealed partial class ResultWorkspacePresenter
{
    private static string? PointTip(string? label, RecordValue shape)
    {
        if (label is null || shape.Get<string>("kind") != "point")
            return null;
        var point = shape.Get<PointGeometry>("center").Point;
        return $"{label}:({(float)point.X},{(float)point.Y}) ";
    }

    private static IEnumerable<DeclaredValueReader> OutlinePaths(DeclaredValueReader paths)
    {
        for (var index = 0; index < paths.Count; index++)
            yield return paths.GetElement(index).GetField("points");
    }

    private sealed class DisplayScene : IDisposable
    {
        internal readonly IResultValueLease? Values;
        internal readonly SunEyeVision.Plugin.Runtime.ResultDisplayTextLease? Text;
        internal readonly NativeResultViewport.DisplaySceneLayer Layer;
        internal bool Disposed;
        internal readonly long Attempt;
        internal DisplayScene(IResultValueLease? values,NativeResultViewport.DisplaySceneLayer layer,long attempt,SunEyeVision.Plugin.Runtime.ResultDisplayTextLease? text=null){Values=values;Layer=layer;Attempt=attempt;Text=text;}
        public void Dispose(){if(Disposed)return;Disposed=true;Run([Layer.Dispose,()=>Values?.Dispose(),()=>Text?.Dispose()]);}
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
            var frozen = state.Geometry;
            // 原件 ModuleResultView.xaml:142 的 ShapeList 始终绑定模块结果，
            // imageListCombobox_SelectionChanged 只在 SelectedValue==null 时清空（:2152-2153），
            // 换显示图像从不重建形状。因此坐标域不一致时只跳过与坐标域绑定的 ROI/内接/轮廓，
            // 保留矩形与文字（它们来自结果行与文本租约，不需要域一致）。
            var sameDomain = frozen is null || frozen.Coordinates == receipt.Coordinates;
            if (!sameDomain)
                try { _main.AddLog("当前显示图像的坐标域与结果几何不一致：按原件语义仅切换背景图像，保留模块结果图形与文字。"); } catch { }
            var appearance = state.Appearance;
            var innerPath = appearance.InnerRectangle is { Visible: true } ? state.Descriptor.InnerRectangle : null;
            var outlinePath = appearance.Outline is { Visible: true } ? state.Descriptor.Outline : null;
            var showRows = appearance.Graphic.Visible || innerPath is not null || outlinePath is not null;
            var roiGroups = sameDomain
                ? frozen?.Groups
                    .Where(group => group.Coverage == ResultGeometryCoverage.Explicit && group.Shapes.Count != 0)
                    .Select(group => (Group:group, Style:RoiStyle(appearance,group.Role)))
                    .Where(item => item.Style?.Visible == true).ToArray() ?? []
                : [];
            if (!showRows && !appearance.Text.Visible && roiGroups.Length == 0)
            {ClearOriginal();continue;}
            IResultValueLease? values=null;DisplayScene? candidate=null;SunEyeVision.Plugin.Runtime.ResultDisplayTextLease? textLease=null;
            try
            {
                DeclaredValueReader? rows = null;
                if (showRows || appearance.Text.Visible)
                {
                    state.Descriptor.Validate(publication.Schema);values=publication.Borrow();
                    if(values.Values is null||values.Stamp!=publication.Stamp||!Current()){values.Dispose();values=null;ClearOriginal();continue;}
                    rows=values.Values.Read(state.Descriptor.Rows.PortId);
                    foreach(var member in state.Descriptor.Rows.MemberFieldPath)rows=rows.GetField(member);
                }
                if(appearance.Text.Visible)
                {
                    textLease=state.BorrowText();
                    var capturedText=textLease;
                    capturedText.SetInvalidatedCallback(()=>
                    {
                        if(_shell.Dispatcher.HasShutdownStarted||_shell.Dispatcher.HasShutdownFinished)return;
                        try{_shell.Dispatcher.BeginInvoke(DispatcherPriority.Render,new Action(()=>
                        {if(!_disposed&&pane.Generation==paneGeneration&&ReferenceEquals(pane.Display?.Text,capturedText))QueueResultDisplay();}));}
                        catch(TaskCanceledException){}
                        catch(InvalidOperationException) when(_shell.Dispatcher.HasShutdownStarted||_shell.Dispatcher.HasShutdownFinished){}
                    });
                }
                var roi=new List<NativeRoiResultOverlay>();
                if(frozen is not null)
                {
                    foreach(var (group,visibleStyle) in roiGroups)
                    {
                        var style=visibleStyle!;
                        var fill=style.Fill is {} color && !group.OutlineOnly ? Color(color) : Colors.Transparent;
                        var passed=style.FixedJudgment??frozen.Passed;
                        foreach(var shape in group.Shapes)roi.Add(new(frozen.SourceFrame,shape,Color(passed?style.Ok:style.Ng),fill,style.Width,style.Opacity,
                            IncludeMeasurements:style.ShowMeasurements,ToolTip:PointTip(style.PointLabel,shape)));
                    }
                }
                var outlines=new List<NativeOutlineOverlay>();long outlinePoints=0;
                var rectangles=new List<NativeMeasuredRectangle>();
                var geometry=new List<NativeGeometryOverlay>();var texts=new List<NativeTextOverlay>();var ids=new HashSet<long>();long textCharacters=0;
                var selection = _sceneSelection;
                var selectedId = selection?.Publication == publication.Stamp
                    && selection.Targets.Any(target => target.ImageFrame == receipt.Frame && target.Coordinates == receipt.Coordinates)
                    ? selection.ElementIdentity as long? : null;
                DeclaredValueReader Member(DeclaredValueReader row,ResultFieldPath path)=>row.GetField(path.MemberFieldPath.Last());
                var rowCount=showRows?rows!.Count:0;
                for(var i=0;i<rowCount;i++)
                {
                    if(!Current())throw new InvalidOperationException("显示准备期间结果或样式已失效。");
                    var row=rows!.GetElement(i);var id=Member(row,state.Descriptor.Identity).GetScalar<long>();
                    if(!ids.Add(id))throw new InvalidOperationException("结果显示对象身份重复。");
                    using var region=Member(row,state.Descriptor.GeometrySource).AcquireRegionReader();
                    if(sameDomain && region.Frame!=receipt.Coordinates)throw new InvalidOperationException("结果显示与当前图像坐标域不匹配。");
                    // 跨域浏览时形状仍按像素坐标绘制到当前背景上（原件同义），源帧取当前显示帧。
                    var shapeFrame=sameDomain?region.SourceFrame:receipt.Frame;
                    var passed=Member(row,state.Descriptor.Passed).GetScalar<bool>();
                    var selectedColor = id == selectedId ? state.Descriptor.SelectedRectangleColor : null;
                    if(appearance.Graphic.Visible)
                    {
                        var bounds=Member(row,state.Descriptor.Rectangle);
                        var center=bounds.GetField("center");
                        rectangles.Add(new(shapeFrame,
                            new(center.GetField("x").GetScalar<double>(),center.GetField("y").GetScalar<double>()),
                            bounds.GetField("width").GetScalar<double>(),bounds.GetField("height").GetScalar<double>(),
                            bounds.GetField("angle-degrees").GetScalar<double>(),
                            Color(selectedColor ?? (passed?appearance.Graphic.Ok:appearance.Graphic.Ng)),
                            appearance.Graphic.Width,appearance.Graphic.Opacity));
                    }
                    if (sameDomain && innerPath is not null && appearance.InnerRectangle is {} style)
                    {
                        var inner = Member(row, innerPath);
                        if (!inner.IsMissing && !inner.IsNull)
                        {
                            if (inner.GetGeometry() is not OrientedRectangleGeometry rectangle || rectangle.Frame != receipt.Coordinates)
                                throw new InvalidOperationException("内接矩形与图像坐标域不匹配。");
                            geometry.Add(new(region.SourceFrame, rectangle, Color(selectedColor ?? (passed ? style.Ok : style.Ng)), style.Width, style.Opacity,
                                IncludeMeasurements:true));
                        }
                    }
                    if(sameDomain && outlinePath is not null && appearance.Outline is {} outlineStyle)
                    {
                        var outline = Member(row, outlinePath);
                        var sources = outline.IsMissing || outline.IsNull ? Enumerable.Empty<DeclaredValueReader>()
                            : outline.Declaration.Shape == ValueShape.Sequence ? new[] { outline }
                            : OutlinePaths(outline.GetField("paths"));
                        var points=new List<Point2>();
                        foreach(var sourcePoints in sources)
                        {
                            outlinePoints=checked(outlinePoints+sourcePoints.Count);
                            if(outlinePoints>1_000_000)throw new ArgumentException("outline-budget: 场景轮廓点超过预算。");
                            for(var pointIndex=0;pointIndex<sourcePoints.Count;pointIndex++)
                            {
                                // The held result lease protects these immutable points. Check retirement in bounded chunks.
                                if((pointIndex & 255)==0&&!Current())
                                    throw new InvalidOperationException("轮廓准备期间结果已失效。");
                                if(sourcePoints.GetElement(pointIndex).GetGeometry() is not PointGeometry point||point.Frame!=receipt.Coordinates)
                                    throw new InvalidOperationException("轮廓点与图像坐标域不匹配。");
                                points.Add(point.Point);
                            }
                        }
                        if(points.Count!=0)
                            outlines.Add(new(region.SourceFrame,receipt.Coordinates,points.ToArray(),i,passed,Color(outlineStyle.Ok),Color(outlineStyle.Ng)));
                    }
                }
                if(textLease is not null)
                {
                    var textStyle=appearance.Text;
                    var statusCount=rows!.Count;
                    if(textStyle.Content.Length>1_000_000)throw new ArgumentException("text-budget: 场景文字超过资源预算。");
                    var textCount=Math.Min(textLease.GetContentCount(),GlobalRenderVariable.ShapeRenderingMaxNumber);
                    for(var i=0;i<textCount;i++)
                    {
                        if(!Current())throw new InvalidOperationException("文字准备期间结果或样式已失效。");
                        var evaluated=textLease.Evaluate(i);var content=evaluated.Content;
                        if(content.Length==0)continue;
                        textCharacters+=content.Length;
                        if(textCharacters>1_000_000)throw new ArgumentException("text-budget: 场景文字超过资源预算。");
                        var passed=statusCount!=0&&Member(rows.GetElement(i<statusCount?i:0),state.Descriptor.Passed).GetScalar<bool>();
                        texts.Add(new(content,new Point(evaluated.X,evaluated.Y),
                            checked((int)textStyle.FontSize),Color(passed?textStyle.Ok:textStyle.Ng),textStyle.Opacity));
                    }
                }
                if(!Current()||textLease?.IsCurrent==false||pane.Image.CurrentFrame!=receipt){values?.Dispose();values=null;ClearOriginal();continue;}
                if(!Attempt()){values?.Dispose();values=null;continue;}
                var layer=pane.Image.Viewport.CreateDisplaySceneLayer();candidate=new(values,layer,attempt,textLease);values=null;textLease=null;
                if(!Attempt()){candidate.Dispose();continue;}
                pane.Display=candidate;original?.Dispose();
                bool Owns()=>Current()&&candidate.Text?.IsCurrent!=false&&epoch==_epoch&&request==_displayRequest&&attempt==_displayAttempt&&paneGeneration==pane.Generation&&ReferenceEquals(pane.Display,candidate)&&!candidate.Disposed&&pane.Image.CurrentFrame==receipt;
                if(!Owns()||!layer.Present(receipt,++_layerRevision,geometry,texts,Owns,roi,outlines,rectangles))
                {if(ReferenceEquals(pane.Display,candidate))pane.Display=null;candidate.Dispose();}
            }
            catch(Exception error)
            {
                // A runtime preparation failure clears this entire scene; never preserve half of a new publication.
                var cleanup=new List<Action>();if(values is not null)cleanup.Add(values.Dispose);
                if(textLease is not null){cleanup.Add(textLease.Dispose);textLease=null;}
                if(candidate is not null){if(ReferenceEquals(pane.Display,candidate))pane.Display=null;cleanup.Add(candidate.Dispose);}
                else cleanup.Add(ClearOriginal);
                try{Run(cleanup);}catch(Exception failure){error=new AggregateException(error,failure);}
                if(!_disposed&&epoch==_epoch&&request==_displayRequest&&attempt==_displayAttempt)_main.AddLog("结果图文未显示："+error.Message);
            }
            finally{textLease?.Dispose();}
        }
    }
    private static ResultGraphicAppearance? RoiStyle(ResultDisplayAppearance appearance, ResultGeometryRole role) => role switch
    {
        ResultGeometryRole.Inspection => appearance.Inspection,
        ResultGeometryRole.Mask => appearance.Mask,
        ResultGeometryRole.Annulus => appearance.Annulus,
        ResultGeometryRole.ReferencePoint => appearance.ReferencePoint,
        ResultGeometryRole.RunPoint => appearance.RunPoint,
        _ => null
    };
    private static Color Color(ResultDisplayColor color)=>System.Windows.Media.Color.FromArgb(color.Alpha,color.Red,color.Green,color.Blue);
}
