using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SunEyeVision.HalAdapter;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Infrastructure.Managers.Plugin;
using SunEyeVision.Plugin.Infrastructure.Managers.Tool;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Plugin.SDK.Models.Visualization;
using SunEyeVision.Plugin.SDK.UI.Controls;
using SunEyeVision.Plugin.Wpf.Regions;
using SunEyeVision.Plugin.Wpf.Fields;
using SunEyeVision.UI.Services.Editing;
using SunEyeVision.UI.Services.Editing.Regions;
using SunEyeVision.Workflow;

internal static class S2DocumentEditingCases
{
    [STAThread] private static int Main()
    {
        try
        {
            if(Application.Current is null)
            {
                var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
                foreach(var resource in new[]{"Shell/Resources/AppResources.xaml","Themes/Dark/DarkTheme.xaml"})
                    app.Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("pack://application:,,,/SunEyeVision.UI;component/"+resource)});
            }
            var product=Environment.GetEnvironmentVariable("S2_PRODUCT_ROOT")!;
            var manager=new PluginManager();manager.LoadPlugins(Path.Combine(product,"plugins"));manager.AssertCompleteReleaseToolSet(ToolRegistry.GetAllToolMetadata().Select(tool=>tool.Id));
            var parameters=ToolRegistry.CreateParameters("BlobAnalysis");
            foreach(var key in new[]{"IsAreaFilterEnabled","IsCircularityFilterEnabled","IsRectangularityFilterEnabled","IsBlobCountFilterEnabled"})parameters.GetType().GetProperty(key)!.SetValue(parameters,false);
            var node=new WorkflowNodeBase("blob","Blob","Blob","BlobAnalysis"){Parameters=parameters};
            var engine=new WorkflowEngine(new CheckLogger());var flow=engine.CreateWorkflow("roi-flow","区域编辑");flow.Nodes.Add(node);
            PluginLoader.MarkAsLoaded();var folder=Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"roi-document");
            var solutions=new SolutionManager(folder);var document=solutions.CreateUntitledSolution();document.Workflows.Clear();document.Workflows.Add(flow);
            var root=(DeclaredToolConfigurationControl)(ToolConfigurationEditors.CreateControl(ToolRegistry.CreateToolInstance("BlobAnalysis")!)
                ??throw new Exception("正常Blob入口没有默认声明配置"));
            var contractReader=new OutputBindingChangeService();
            var query=new DataSourceQueryService{WorkflowId=flow.Id,
                BindingCandidateNodeIds=consumer=>flow.Nodes.Where(item=>item.Id!=consumer).Select(item=>item.Id).ToArray(),
                FormalOutputContractReader=id=>flow.Nodes.FirstOrDefault(item=>item.Id==id) is {} source?contractReader.ReadContract(source):null};
            void Bind(WorkflowNodeBase draft){root.SetCurrentNode(draft);root.SetParameters(draft.Parameters);root.SetDataProvider(query);}
            using var context=new ProductionEditorContext(solutions.Session!.Editing.Host,flow.Id,node,()=>true,Bind,documentEdits:solutions.Session.Editing);
            Bind(context.DraftNode);ToolEditorCommit.AttachDraftContext(root,context);
            typeof(ProductionEditorContext).GetProperty("PrepareView",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(context,(Func<string?>)(()=>ToolEditorCommit.TryPrepare(root,out var error)?null:error));
            typeof(ProductionEditorContext).GetProperty("HasPendingViewEdits",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(context,(Func<bool>)(()=>ToolEditorCommit.HasPendingEdits(root)));
            var pixels=Enumerable.Repeat((byte)200,64*64).ToArray();
            var bitmap=BitmapSource.Create(64,64,96,96,PixelFormats.Gray8,null,pixels,64);bitmap.Freeze();PreviewFrameProvenance.Attach(bitmap,Guid.NewGuid());
            var image=new ImageControl{SourceImage=bitmap,Width=420,Height=400};root.SetMainImageControl(image);
            var panel=new Grid();panel.ColumnDefinitions.Add(new(){Width=new GridLength(420)});panel.ColumnDefinitions.Add(new(){Width=new GridLength(430)});
            panel.Children.Add(image);Grid.SetColumn(root,1);panel.Children.Add(root);
            var window=new Window{Title="S4区域宿主检查",Content=panel,Width=880,Height=700,ShowActivated=false,ShowInTaskbar=false,Left=-10000};
            window.Show();Pump();
            void Capture(string name,FrameworkElement target)
            {
                target.BringIntoView();window.UpdateLayout();Pump();
                var frame=new RenderTargetBitmap((int)Math.Ceiling(root.ActualWidth),(int)Math.Ceiling(root.ActualHeight),96,96,PixelFormats.Pbgra32);
                var visual=new DrawingVisual();using(var drawing=visual.RenderOpen())drawing.DrawRectangle(new VisualBrush(root),null,new Rect(0,0,root.ActualWidth,root.ActualHeight));frame.Render(visual);
                var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(frame));using var stream=File.Create(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,name+".png"));png.Save(stream);
            }
            var scope=(RoiViewportEditScope)root.RegionViewport!;
            RoiParameters Draft()=>(RoiParameters)context.DraftNode.Parameters;
            RegionGeometryEditor Editor()=>Descendants(root).OfType<RegionGeometryEditor>().First();
            void Draw(string kind,params Point2[] points)
            {
                var count=Draft().InspectionRois.Rois.Count;Require(scope.BeginDraw("inspection",kind,out var error),error);
                if(kind=="point")scope.PointerDown(points[0]);
                else if(kind is "polygon" or "arc" or "annulus")
                {foreach(var p in points)scope.PointerDown(p);if(kind=="polygon")scope.FinishPolygon();}
                else{scope.PointerDown(points[0]);scope.PointerMove(points[1]);Require(Draft().InspectionRois.Rois.Count==count,"未完成手势提前写入几何");scope.PointerUp(points[1]);}
                Require(Draft().InspectionRois.Rois.Count==count+1,"形状绘制没有生成一个本地记录："+kind);
                var value=RoiShapeValueAdapter.Freeze(Draft().InspectionRois.Rois.Last().LocalGeometry!);
                Require(value.Get<string>("kind")==kind,"绘制类型与既有声明不一致");RoiGeometryEditing.Validate(value);
            }
            Draw("point",new Point2(8,8));Draw("line",new(4,30),new(18,30));Draw("circle",new(10,10),new(13,10));
            Draw("rectangle",new(20,20),new(28,26));var rectangle=Draft().InspectionRois.Rois.Last().Id;
            Draw("arc",new(40,20),new(44,20),new(40,24));Draw("annulus",new(40,40),new(45,40),new(42,40));
            Draw("polygon",new(12,40),new(18,40),new(18,46));
            Require(((RoiParameters)node.Parameters).InspectionRois.Rois.Count==0,"区域编辑直接污染正式参数");
            Console.WriteLine("通过：七种几何经宿主交互入口生成既有ROI记录，完成前不写入，正式参数保持隔离");

            RecordValue Rect()=>RoiShapeValueAdapter.Freeze(Draft().InspectionRois.Rois.Single(roi=>roi.Id==rectangle).LocalGeometry!);
            scope.Select("inspection",rectangle);scope.SetAction(RoiEditAction.Move);scope.PointerDown(new(24,23));scope.PointerMove(new(29,26));
            Require(RoiGeometryEditing.Center(Rect())==new Point2(24,23)&&!root.TryValidateEditor(out _),"拖动预览提前写入或未完成手势允许确认");
            scope.CancelGesture();Require(RoiGeometryEditing.Center(Rect())==new Point2(24,23),"取消拖动改变了草稿");
            scope.Select("inspection",rectangle);scope.SetAction(RoiEditAction.Move);scope.PointerDown(new(24,23));scope.PointerMove(new(29,26));scope.PointerUp(new(29,26));
            Require(RoiGeometryEditing.Center(Rect())==new Point2(29,26),"移动未写回同一几何");
            var editor=Editor();var x=Descendants(editor).OfType<TextBox>().Single(box=>Equals(box.Tag,"x"));
            Require(double.Parse(x.Text,System.Globalization.CultureInfo.InvariantCulture)==29,"拖动后数值未回填");
            x.Text="invalid";Require(editor.HasInvalidText&&!root.TryValidateEditor(out _)&&RoiGeometryEditing.Center(Rect()).X==29,"非法区域文本污染几何");
            var list=Descendants(editor).OfType<ListBox>().Single();list.SelectedIndex=0;Require(x.Text=="invalid"&&editor.SelectedId==rectangle,"切换选择丢失非法原文");
            x.Text="31";Require(!editor.HasInvalidText&&RoiGeometryEditing.Center(Rect()).X==31,"数值修正未回到同一几何");
            Console.WriteLine("通过：拖动预览、取消、完成和数字回填共用一个草稿；非法原文保留且阻止切换与确认");

            scope.Select("inspection",rectangle);scope.SetAction(RoiEditAction.Resize);scope.PointerDown(new(35,29));scope.PointerMove(new(36,30));scope.PointerUp(new(36,30));
            Require(Rect().Get<double>("width")==10&&Rect().Get<double>("height")==8,"尺寸手势没有更新矩形参数");
            scope.Select("inspection",rectangle);scope.SetAction(RoiEditAction.Rotate);scope.PointerDown(new(36,26));scope.PointerMove(new(31,31));scope.PointerUp(new(31,31));
            Require(Math.Abs(Rect().Get<double>("angle-degrees")-90)<1e-8,"旋转手势方向不正确");
            var matrix=new Matrix(0,1,-1,0,20,10);scope.SetPose(matrix,true);
            Require(scope.ImageToReference(new(17,12))==new Point2(2,3),"位置修正没有逆变换回参考坐标");
            scope.SetPose(matrix,false,"错帧");Require(!scope.BeginDraw("inspection","circle",out _),"错帧位置修正仍允许绘制");scope.SetPose(null,true);
            Console.WriteLine("通过：缩放与旋转更新同一参数，位置逆变换正确，错帧禁止绘制");

            var originalCount=Draft().InspectionRois.Rois.Count;Require(scope.BeginDraw("inspection","circle",out _),"不能启动新绘制");scope.PointerDown(new(10,10));
            PreviewFrameProvenance.Attach(bitmap,Guid.NewGuid());scope.PointerMove(new(12,10));scope.PointerUp(new(12,10));
            Require(!scope.HasPendingGesture&&Draft().InspectionRois.Rois.Count==originalCount,"同一Bitmap换帧后仍提交手势");
            var mode=((IConfigurationFieldProvider)context.DraftNode.Parameters).GetConfigurationFields().Single(field=>field.StoragePath=="InspectionMode");mode.Write("InheritedRegion");root.OnExternalResultArrived();
            Require(!scope.BeginDraw("inspection","circle",out _)&&Draft().InspectionRois.Rois.Count==originalCount,"继承模式仍写入或清除了本地底稿");mode.Write("LocalGeometry");root.OnExternalResultArrived();
            Console.WriteLine("通过：换帧取消未完成手势；继承模式保留本地底稿并拒绝写入");

            var declaredView=Descendants(root).OfType<DeclaredFieldsView>().Single();
            var modeEditor=declaredView.Fields.Single(field=>field.FieldId==mode.Port.Id);
            Require(modeEditor.InputElement is RegionSourceModeSelector,"真实宿主模式未接新Radio组合");
            var circleNode=new WorkflowNodeBase("circle-source","Circle","Circle","CircleDetect"){Parameters=ToolRegistry.CreateParameters("CircleDetect")};flow.Nodes.Add(circleNode);
            Require(modeEditor.TrySetConstant("InheritedCircle"),modeEditor.Error);window.UpdateLayout();Pump();
            var wholeEditor=Descendants(root).OfType<DeclaredFieldEditor>().Single(field=>field.FieldId=="inspection:InheritedCircle");
            void Pick(bool accept)
            {
                Exception? failure=null;
                System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle,new Action(()=>
                {
                    var dialog=Application.Current.Windows.Cast<Window>().Single(item=>item!=window);
                    try
                    {
                        if(!accept){dialog.Close();return;}
                        var picker=Descendants(dialog).OfType<ReferenceSelectionControl>().Single();
                        var choices=Descendants(picker).OfType<ListBox>().Single().Items.Cast<DeclaredReferenceCandidate>().ToArray();
                        picker.SelectedCandidate=choices.Single(choice=>choice.NodeId==circleNode.Id&&choice.Output.Type.Shape==ValueShape.Sequence&&choice.Output.Type.Element!.Geometry==GeometryKind.Circle);
                        picker.SelectionMode=1;picker.IndexText="1";
                        Require(picker.TryGetReference(out _),"实际圆集合声明未通过正式选择器验证："+string.Join("；",Descendants(picker).OfType<TextBlock>().Select(text=>text.Text)));
                        Descendants(dialog).OfType<Button>().Single(button=>Equals(button.Content,"确定")).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                    }
                    catch(Exception error){failure=error;if(dialog.IsVisible)dialog.Close();}
                }));
                Descendants(wholeEditor).OfType<Button>().Single(button=>Equals(button.ToolTip,"选择订阅来源")).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                if(failure is not null)throw failure;
            }
            var beforePick=Draft().InspectionRois.Rois.Count;Pick(false);Require(Draft().InspectionRois.Rois.Count==beforePick,"取消来源选择创建了ROI");
            Pick(true);var inherited=Draft().InspectionRois.Rois.Single(roi=>roi.IsSubscribed);
            var reference=SunEyeVision.Plugin.Runtime.BindingReferenceSerializer.Deserialize(inherited.Binding!.ReferenceExpression!);
            Require(reference.NodeId==circleNode.Id&&reference.Selection is BindingSelection.AtConstant{Index:1}&&inherited.InheritedShapeType==ShapeType.Circle,"圆来源或明确序号未写回同一合同");
            Capture("roi-inherit-circle-source",wholeEditor);
            Draft().InspectionRois.Rois.Remove(inherited);flow.Nodes.Remove(circleNode);
            Console.WriteLine("通过：真实整圆来源弹窗取消不写入，确认保留正式声明及明确元素序号");
            Require(modeEditor.TrySetConstant("InheritedRectangleParameters"),modeEditor.Error);Pump();
            var rectangleFields=Descendants(root).OfType<DeclaredFieldEditor>().Where(field=>field.FieldId.StartsWith("inspection:InheritedRectangleParameters:")).ToArray();
            Require(rectangleFields.Length==5&&rectangleFields.All(field=>field.IsVisible),"矩形五参数没有在实际活动面板呈现");
            Require(!root.TryValidateEditor(out var unset)&&unset.Contains("继承"),"未设置参数时确认未被拒绝");
            foreach(var field in rectangleFields)Require(field.TrySetConstant(field.FieldId.EndsWith(":Width")||field.FieldId.EndsWith(":Height")?6d:0d),field.Error);
            var composed=Draft().InspectionRois.Rois.Single(roi=>roi.IsComposed);var composedId=composed.Id;
            var configured="";Require(composed.ParameterBindings!.Count==5&&root.TryValidateEditor(out configured),configured);
            var width=rectangleFields.Single(field=>field.FieldId.EndsWith(":Width"));
            window.UpdateLayout();Pump();
            var widthText=width.InputElement as TextBox??(width.InputElement as SunEyeVision.Plugin.Wpf.Controls.NumericUpDown)?.TextEditor??throw new Exception("数值编辑模板尚未加载");
            widthText.Text="invalid";Require(!root.TryValidateEditor(out _),"活动参数非法文本未阻止确认");
            Require(modeEditor.TrySetConstant("LocalGeometry")&&root.TryValidateEditor(out _),"非活动非法草稿仍阻塞本地模式");
            Require(modeEditor.TrySetConstant("InheritedRectangleParameters")&&!root.TryValidateEditor(out _),"切回未保留参数非法原文");
            widthText.Text="7";Require(root.TryValidateEditor(out configured),configured);
            Capture("roi-inherit-rectangle-parameters",rectangleFields.Last());
            Require(composed.Id==composedId&&Convert.ToDouble(composed.ParameterBindings["Width"].ConstantValue)==7,"数值没有写回同一持久化ROI");
            var maskMode=((IConfigurationFieldProvider)context.DraftNode.Parameters).GetConfigurationFields().Single(field=>field.StoragePath=="MaskMode");
            var maskModeEditor=declaredView.Fields.Single(field=>field.FieldId==maskMode.Port.Id);Require(maskModeEditor.TrySetConstant("InheritedCircleParameters"),maskModeEditor.Error);
            var circleFields=Descendants(root).OfType<DeclaredFieldEditor>().Where(field=>field.FieldId.StartsWith("mask:InheritedCircleParameters:")).ToArray();
            Require(circleFields.Length==3&&circleFields.All(field=>field.IsVisible),"屏蔽圆三参数未呈现");
            foreach(var field in circleFields)Require(field.TrySetConstant(field.FieldId.EndsWith(":Radius")?2d:3d),field.Error);
            Require(Draft().MaskRois.Rois.Single().IsComposed&&root.TryValidateEditor(out configured),configured);
            Capture("roi-inherit-mask-circle-parameters",circleFields.Last());
            Require(((RoiParameters)node.Parameters).MaskRois.Rois.Count==0,"继承参数编辑污染正式节点");
            Require(modeEditor.TrySetConstant("LocalGeometry")&&maskModeEditor.TrySetConstant("LocalGeometry"),"不能恢复本地模式");
            Draft().InspectionRois.Rois.Remove(composed);Draft().MaskRois.Rois.Clear();
            Console.WriteLine("通过：实际矩形五参数与屏蔽圆三参数面板、活动校验、非法草稿切换保留、同ROI写回及正式参数隔离");

            Require(scope.BeginDraw("inspection","circle",out _),"不能启动Esc检查");scope.PointerDown(new(10,10));
            modeEditor.Focus();window.RaiseEvent(new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice,PresentationSource.FromVisual(window),0,System.Windows.Input.Key.Escape){RoutedEvent=System.Windows.Input.Keyboard.PreviewKeyDownEvent});
            Require(!scope.HasPendingGesture,"焦点位于参数栏时Esc未取消绘制");
            Require(scope.BeginDraw("inspection","circle",out _),"不能启动切页检查");scope.PointerDown(new(10,10));
            Descendants(declaredView).OfType<TabControl>().Single().SelectedIndex=1;Pump();Require(!scope.HasPendingGesture,"切页未取消绘制");
            Descendants(declaredView).OfType<TabControl>().Single().SelectedIndex=0;Pump();
            Console.WriteLine("通过：参数栏焦点Esc和页面切换取消当前区域手势");

            foreach(var roi in Draft().InspectionRois.Rois)roi.IsEnabled=roi.Id==rectangle;
            Require(context.TryCommitEditor(out var confirmError),confirmError);
            using var native=HalImage.CreateFromBytes(pixels,64,64,1);
            var executor=new WorkflowExecutionEngine(engine,manager,new CheckLogger());
            using(var run=executor.ExecuteWorkflowAsync(new WorkflowRunRequest(flow.Id,native)).GetAwaiter().GetResult())
            {
                Require(run.Success,string.Join(";",run.Errors));var result=run.NodeResults[node.Id].Result!;
                var area=Convert.ToDouble(result.GetType().GetProperty("FilteredTotalArea")!.GetValue(result));
                Require(Convert.ToInt32(result.GetType().GetProperty("FilteredBlobCount")!.GetValue(result))==1&&area>50&&area<120,"窗口ROI未由真实HALCON消费");
            }
            var file=Path.Combine(folder,"s4-roi.solution");var saving=new SunEyeVision.UI.Services.Solutions.SolutionApplicationService(solutions);
            Require(saving.Save(file,()=>solutions.Session!.Editing.AcceptAll()).FileWritten,"实际ROI保存失败："+solutions.LastError);
            var reopened=solutions.LoadSolutionOnly(file)??throw new Exception(solutions.LastError);
            var restored=(RoiParameters)reopened.Workflows.Single().Nodes.Single().Parameters;
            Require(restored.InspectionRois.Rois.Count==7&&restored.InspectionRois.Rois.Single(roi=>roi.Id==rectangle).IsEnabled
                &&RoiShapeValueAdapter.Freeze(restored.InspectionRois.Rois.Single(roi=>roi.Id==rectangle).LocalGeometry!).Get<double>("angle-degrees")==90,"ROI身份、参数或启用状态重开丢失");
            Console.WriteLine("通过：确认后真实Workflow/HALCON消费ROI，实际保存重开保留七形状、稳定ID、角度与启用状态");

            window.UpdateLayout();
            var rendered=new RenderTargetBitmap((int)Math.Ceiling(panel.ActualWidth),(int)Math.Ceiling(panel.ActualHeight),96,96,PixelFormats.Pbgra32);rendered.Render(panel);
            var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(rendered));
            using(var snapshot=File.Create(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"roi-editor.png")))encoder.Save(snapshot);

            var sentinel=new Border();image.InteractionOverlayCanvasControl.Children.Add(sentinel);var collector=new DisplayCollector();image.ResultRenderLayerControl.SetDisplayCollector(collector);
            var current=true;using var other=new RoiViewportEditScope(image,()=>current);other.SetScene(new[]{new RoiEditItem("mask",Guid.NewGuid(),"另一个编辑范围",RoiGeometryEditing.Create("circle",new(20,20),new(24,20)))});other.Activate();
            var before=image.InteractionOverlayCanvasControl.Children.Count;context.Discard();window.Close();Pump();
            Require(image.InteractionOverlayCanvasControl.Children.Contains(sentinel)&&image.InteractionOverlayCanvasControl.Children.Count==before-1&&other.IsActive,"关闭旧窗口清除了其他编辑范围");
            Require(ReferenceEquals(typeof(ResultRenderLayer).GetField("_collector",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(image.ResultRenderLayerControl),collector),"关闭ROI编辑清空共享正式结果");
            var writes=0;other.GeometryChanged+=_=>writes++;other.BeginDraw("mask","rectangle",out _);other.PointerDown(new(2,2));other.PointerMove(new(8,8));current=false;other.PointerUp(new(8,8));
            Require(writes==0&&!other.HasPendingGesture,"编辑范围失效后仍提交迟到手势");
            Console.WriteLine("通过：关闭只释放自身覆盖与订阅；保留其他编辑范围和正式结果，失效范围拒绝迟到提交");
            Console.WriteLine("S4区域编辑：9组通过；正常入口配置已使用新页面。实际鼠标手感及VM同态外观尚未验收。");return 0;
        }
        catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
    private static void Require(bool condition,string error){if(!condition)throw new Exception(error);}
    private static void Pump(){var frame=new System.Windows.Threading.DispatcherFrame();System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle,new Action(()=>frame.Continue=false));System.Windows.Threading.Dispatcher.PushFrame(frame);}
    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent){foreach(var item in LogicalTreeHelper.GetChildren(parent))if(item is DependencyObject child){yield return child;foreach(var next in Descendants(child))yield return next;}}
    private sealed class CheckLogger:ILogger{public void Log(LogLevel level,string message,string? source=null,Exception? exception=null,LogTarget target=LogTarget.All){}public ILogger ForSource(string source)=>this;}
}
