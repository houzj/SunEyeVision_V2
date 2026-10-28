using SunEyeVision.Plugin.Wpf.Presenters.Display;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Markup;
using System.Xml.Linq;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Fields;
using SunEyeVision.Plugin.Wpf.Adapters.References;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using SunEyeVision.Tool.BlobAnalysis.Runtime;
using SunEyeVision.Tool.BlobAnalysis.Views;
using SunEyeVision.Tool.BlobAnalysis.Wpf.Views;
using SunEyeVision.Tool.BlobAnalysis.Wpf.Design;

internal static class S2DocumentEditingCases
{
    private static int _count;
    private static void Check(bool condition,string message)
    {if(!condition)throw new InvalidOperationException(message);Console.WriteLine("PASS "+message);_count++;}
    [STAThread] private static int Main()
    {
        try
        {
            // Emulate the designer's WPF flag before creating any view. No Application or host bootstrap.
            DesignerProperties.IsInDesignModeProperty.OverrideMetadata(typeof(FrameworkElement),new FrameworkPropertyMetadata(true));
            using(var data=new SunEyeVision.Plugin.Wpf.Design.NativeCrosshairDesignData())
            {
                var view=new SunEyeVision.Plugin.Wpf.Controls.Native.Composition.Display.CrosshairSettingsView{DataContext=data};
                Render(view,"native-crosshair-direct-config.png",380,380);
                Check(Application.Current is null&&data.CrossDatas.Count==2&&Descendants<SunEyeVision.Plugin.Wpf.Native.Numeric.HikPcUI.ListView.SliderArrange>(view).Count()==6,"辅助线配置视图本身无宿主独立创建完整原控件");
                Check(CrosshairNumbers(view).SequenceEqual(data.CrossDatas.SelectMany(model=>new[]{Math.Round(model.CenterX),Math.Round(model.CenterY),model.StrokeThickness})),"配置视图未Loaded也显示真实数值而非短横占位");
            }
            using(var entry=new SunEyeVision.Plugin.Wpf.Design.NativeCrosshairPreview())
            {
                Render(entry,"native-crosshair-xaml-entry.png",1040,430);
                var sample=Descendants<SunEyeVision.Plugin.Wpf.Design.NativeCrosshairSample>(entry).Single();
                Check(Application.Current is null&&sample.Crosshairs.CrossDatas.Count==2,"辅助线真实XAML无Application创建同一两模型");
                Check(Descendants<SunEyeVision.Plugin.Wpf.Native.Numeric.HikPcUI.ListView.SliderArrange>(entry).Count()==6
                    &&Descendants<SunEyeVision.Plugin.Wpf.Native.Input.HikPcUI.ListView.ColorPickerControlView>(entry).Count()==2,"辅助线设计时完整原六数值与两色板");
                Check(CrosshairNumbers(entry).SequenceEqual(sample.Crosshairs.CrossDatas.SelectMany(model=>new[]{Math.Round(model.CenterX),Math.Round(model.CenterY),model.StrokeThickness})),"辅助线设计原数值模板已初始化并显示两模型");
                sample.Crosshairs.CrossDatas[0].CenterPoint=new Point(100,90);Render(entry,"native-crosshair-design-edited.png",1040,430);
                Check(sample.Crosshairs.CrossDatas[0].CenterX==100&&sample.Crosshairs.CrossDatas[0].CenterY==90,"辅助线设计时消费真实模型与布局");
                Check(CrosshairNumbers(entry).Take(2).SequenceEqual(new[]{100d,90d}),"设计模型编辑回显真实数值叶");
            }
            using(var entry=new SunEyeVision.Plugin.Wpf.Design.NativeAssistPreview())
            {
                Render(entry,"native-assist-xaml-entry.png",600,650);
                var sample=Descendants<SunEyeVision.Plugin.Wpf.Design.NativeAssistSample>(entry).Single();
                Check(Application.Current is null&&sample.Fields.Fields.Count==15,"Assist真实XAML无主程序或算法独立创建十五角色");
                for(var mode=0;mode<3;mode++)
                {
                    sample.PreviewMode=mode;Render(entry,"native-assist-design-mode-"+mode+".png",600,650);
                    var count=sample.Fields.Fields.Skip(2).Count(field=>field.View.Visibility==Visibility.Visible);
                    Check(count==new[]{1,8,10}[mode],"Assist设计器切原方式显示完整来源："+mode);
                }
                sample.PreviewMode=-1;Render(entry,"native-assist-design-disabled.png",600,650);
                Check(sample.Fields.Fields.Skip(2).All(field=>field.View.Visibility==Visibility.Collapsed),"Assist设计器关闭隐藏来源且不启动宿主");
            }
            using(var entry=new SunEyeVision.Plugin.Wpf.Design.NativeToolEditorPreview())
            {
                Render(entry,"native-tool-editor-xaml-entry.png",600,700);
                var sample=Descendants<SunEyeVision.Plugin.Wpf.Design.NativeToolEditorSample>(entry).Single();
                Check(Application.Current is null&&Descendants<TabItem>(entry).Count()==3,"原固定底栏真实XAML入口无宿主显示单套三页");
                for(var page=0;page<3;page++)
                {
                    sample.Fields.SelectedPageIndex=page;Render(entry,"native-tool-editor-page-"+page+".png",600,700);
                    Check(sample.Fields.SelectedPageIndex==page&&Descendants<ItemsControl>(entry).Single(control=>control.Name=="BottomCommands").ActualHeight==32,"设计器切原页并固定32底栏："+page);
                }
                Check(sample.Commands.ContinuousCalls==0&&sample.Commands.OnceCalls==0&&sample.Commands.ConfirmCalls==0,"独立设计创建切页不执行命令");
            }
            using(var entry=new SunEyeVision.Plugin.Wpf.Design.NativeRoiInheritancePreview())
            {
                Render(entry,"native-roi-inheritance-xaml-entry.png",1020,800);
                var sample=Descendants<SunEyeVision.Plugin.Wpf.Design.NativeRoiInheritanceSample>(entry).Single();
                Check(Application.Current is null&&sample.Fields.Fields.Count==19&&sample.Session.Frame==sample.Frame,
                    "整体来源与ROI真实XAML入口无Application并共用字段及物理帧");
                Check(sample.Fields.TrySetFields(new Dictionary<string,ParameterSetting>{{"detect.mode",new ConstantSetting(1L)},{"detect.source",new ReferenceSetting(sample.Reference("detect"))}})
                    &&sample.Session.Stamp.Revision==sample.Revision&&sample.Session.Items.Single(x=>x.Group=="detect").Enabled==false,
                    "独立原继承样例沿同一组接纳链切换显示修订");
                Check(sample.Fields.TrySetFields(new Dictionary<string,ParameterSetting>{{"mask.mode",new ConstantSetting(1L)},{"mask.source",new ReferenceSetting(sample.PartialAnnulusReference)}}),"独立来源组合接受同帧部分圆环");
                Render(entry,"native-inherited-annulus-xaml-entry.png",1020,800);
                Check(Application.Current is null&&Descendants<SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls.Base.ImageView.Shape.AnnularSectorShape>(entry).Count()==1,
                    "原部分圆环显示链在独立真实XAML入口实际绘制");
                Check(Descendants<SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls.Base.ImageView.ROI.NormalizedRectROI>(entry).Count()==1
                    &&!Descendants<SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls.Base.ImageView.ROI.NormalizedCircleROI>(entry).Any(),
                    "独立设计同时保留原矩形显示且只读部分环没有圆环编辑手柄");
                var local=sample.LocalItems.ToArray();
                Check(sample.Fields.TrySetFields(new Dictionary<string,ParameterSetting>{{"area.mode",new ConstantSetting(1L)}}),"设计入口切换图像分支");
                Render(entry,"native-inheritance-image-mode-xaml-entry.png",1020,800);
                Check(sample.Session.Items.Count==0&&sample.LocalItems.SequenceEqual(local)&&sample.Scene.ParameterHost.Visibility==Visibility.Collapsed,
                    "图像分支隐藏几何且保留本地对象与物理帧");
                Check(sample.Fields.Fields.Single(field=>field.FieldId=="mask.image").InputElement is ComboBox,
                    "图像分支使用真实EnumerationG选择器");
                Check(sample.Fields.TrySetFields(new Dictionary<string,ParameterSetting>{{"area.mode",new ConstantSetting(0L)}})&&sample.Session.Items.Count==2,
                    "图形分支恢复同一来源及本地对象");
                var components=new Dictionary<string,ParameterSetting>{{"detect.inherit-way",new ConstantSetting(1L)}};
                foreach(var id in new[]{"detect.rect.x","detect.rect.y","detect.rect.width","detect.rect.height","detect.rect.angle"})components[id]=new ReferenceSetting(sample.ComponentReference(id));
                Check(sample.Fields.TrySetFields(components)&&sample.TryCapture(out _,out _),"独立设计五分量来源同根捕获");
                Render(entry,"native-four-way-components-xaml-entry.png",1020,1100);
                Check(Descendants<RadioButton>(sample.Fields.Fields.Single(field=>field.FieldId=="detect.inherit-way").View).Count()==5,
                    "独立设计四方式保原五槽模板");
                var width=sample.Fields.Fields.Single(field=>field.FieldId=="detect.rect.width");
                var text=Descendants<SunEyeVision.Plugin.Wpf.Native.Composition.Apps.XmlUI.ParamTab.DeleteTextBox>(width.View).Single();
                var popup=Descendants<SunEyeVision.Plugin.Wpf.Native.Input.Apps.PcUI.Controls.PopupButton>(width.View).Single(item=>item.Name=="rt");
                var other=components.Where(item=>item.Key!="detect.inherit-way"&&item.Key!="detect.rect.width").ToDictionary(item=>item.Key,item=>sample.Fields.Fields.Single(field=>field.FieldId==item.Key).Setting);
                text.Text="0.125";text.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
                Check(width.TryCommitPending()&&width.Setting is ConstantSetting{Value:0.125d}&&sample.TryCapture(out _,out _),"设计时同槽常量沿原文本及owner捕获链接纳");
                Render(entry,"native-four-way-constant-xaml-entry.png",1020,1100);
                Check(Application.Current is null&&!text.IsReadOnly&&ReferenceEquals(text,Descendants<SunEyeVision.Plugin.Wpf.Native.Composition.Apps.XmlUI.ParamTab.DeleteTextBox>(width.View).Single())
                    &&ReferenceEquals(popup,Descendants<SunEyeVision.Plugin.Wpf.Native.Input.Apps.PcUI.Controls.PopupButton>(width.View).Single(item=>item.Name=="rt"))
                    &&!Descendants<SunEyeVision.Plugin.Wpf.Native.Numeric.HikPcUI.ListView.SliderArrangeFloat>(width.View).Any()
                    &&other.All(item=>Equals(sample.Fields.Fields.Single(field=>field.FieldId==item.Key).Setting,item.Value)),"设计同槽切常量保原文本弹层实例及其余四分量来源");
            }
            using(var entry=new SunEyeVision.Plugin.Wpf.Design.NativeIntegerRadioPreview())
            {
                Render(entry,"native-integer-radio-xaml-entry.png",600,300);
                Check(Application.Current is null&&Descendants<RadioButton>(entry).Count()==9,
                    "原整数单选真实XAML入口独立实例化并显示全部槽位");
            }
            using(var entry=new SunEyeVision.Plugin.Wpf.Design.NativeDisplayPreview())
            {
                Render(entry,"native-display-text-xaml-entry.png",1100,640);
                var sample=(SunEyeVision.Plugin.Wpf.Design.NativeDisplaySample)entry.Content;
                var displayFields=sample.Fields;
                Check(Application.Current is null&&displayFields.Fields.Count==16
                    &&displayFields.Fields.Single(field=>field.FieldId=="text.content").InputElement is SunEyeVision.Plugin.Wpf.Native.Composition.Apps.XmlUI.ParamTab.DeleteTextBox,
                    "完整原文本和图形真实XAML入口脱离Application实例化且无预览替身");
                Check(displayFields.Fields.Single(field=>field.FieldId=="text.size").InputElement is TextBox {Text.Length:>0}
                    &&displayFields.Fields.Single(field=>field.FieldId=="text.x").InputElement is not null,
                    "原七行数值和同槽坐标有实际输入和文字");
                foreach(var id in new[]{"text.size","text.opacity"})
                {
                    var text=(TextBox)displayFields.Fields.Single(field=>field.FieldId==id).InputElement!;
                    var bounds=text.TransformToAncestor(entry).TransformBounds(new Rect(text.RenderSize));
                    Check(text.ActualWidth>100&&text.ActualHeight>=12&&text.Visibility==Visibility.Visible
                        &&bounds.X>=120&&bounds.Right<=600&&bounds.Y>150&&bounds.Bottom<270,
                        "原数值实际完整显示而非零宽文字："+id+" "+bounds);
                }
                sample.SelectedPageIndex=1;
                Render(entry,"native-display-graphics-xaml-entry.png",1100,640);
                Check(displayFields.SelectedPageIndex==1&&sample.SettingsView.SelectedPageIndex==1&&!Descendants<Expander>(entry).Any(),
                    "同一真实设计入口可切到图形页且不使用更多Expander");
                Check(sample.Writes==0,"设计器创建测量和切页不得写参数");
            }
            using(var radio=new SunEyeVision.Plugin.Wpf.Design.NativeIntegerRadioSample())
            {
                Render(radio,"native-integer-radio-design.png",600,300);
                Check(Application.Current is null&&Descendants<RadioButton>(radio).Count()==9,
                    "原两槽五槽及非法初值无Application沿真实字段模板独立显示");
                var invalid=radio.Fields.Fields.Single(x=>x.FieldId=="invalid");
                Check(invalid.HasInvalidText&&!invalid.TrySetConstant(99L)&&invalid.TrySetConstant(1L),
                    "独立设计原单选示例可显式修复非法初值且不回退首项");
            }
            using(var field=new DeclaredFieldEditor(new Port("host.enabled",DataType.Boolean),"启用",new ConstantSetting(true),_=>throw new Exception("设计宿主不得写参数")))
            using(var row=new SunEyeVision.Plugin.Wpf.Adapters.Fields.NativeParameterRow(new("独立原宿主"),field))
            {
                var list=new SunEyeVision.Plugin.Wpf.Native.Composition.Apps.XmlUI.ParamListView();list.SetRows(new[]{row});
                Render(list,"native-parameter-host-independent.png",500,220);
                Check(Application.Current is null&&Descendants<SunEyeVision.Plugin.Wpf.Native.Composition.Apps.XmlUI.ParamUserControl>(list).Count()==1
                    &&Descendants<SunEyeVision.Plugin.Wpf.Native.Composition.Apps.XmlUI.ParamTab.LeftRightControl>(list).Single().ChildLeftWidth==89,
                    "原参数宿主与原行无Application独立组装且不写草稿");
                list.SetRows(null);
            }
            using(var advanced=new NativeAdvancedPresenter())
            {
                Render(advanced,"native-advanced-independent.png",500,80);
                Check(Application.Current is null&&advanced.NativeView.Style is not null&&!advanced.IsExpanded,
                    "原高级模板无Application独立创建且初始收起");
                advanced.IsExpanded=true;Render(advanced,"native-advanced-expanded-independent.png",500,80);
                Check(advanced.NativeView.IsChecked==true&&((ContentPresenter)advanced.NativeView.Template.FindName("cp",advanced.NativeView)).Content.Equals(advanced.FindResource("收起")),
                    "独立高级模板沿同一状态链切换原收起文字");
            }
            var preview=new SunEyeVision.Plugin.Wpf.Design.NativeRoiScenePreview();
            Render(preview,"native-roi-design-entry.png",1020,560);
            var previewScene=(SunEyeVision.Plugin.Wpf.Presenters.Regions.NativeRoiScene)preview.Content;
            Check(previewScene.Session?.Items.Count==4&&SunEyeVision.Plugin.Wpf.Design.NativeRoiDesignData.GetSampleIndex(previewScene)==0,"真实设计XAML子视图独立建立同链预览");
            previewScene.Dispose();
            using(var scene=new SunEyeVision.Plugin.Wpf.Presenters.Regions.NativeRoiScene())
            {
                SunEyeVision.Plugin.Wpf.Design.NativeRoiDesignData.SetSampleIndex(scene,0);
                var session=scene.Session!;var frame=session.Frame;var identities=session.Items.Select(item=>item.Id).ToArray();
                var expected=new[]{5,4,0,6};
                for(var index=0;index<4;index++)
                {
                    SunEyeVision.Plugin.Wpf.Design.NativeRoiDesignData.SetSampleIndex(scene,index);
                    Render(scene,$"native-roi-scene-{index}.png",1020,560);
                    var expand=(System.Windows.Controls.Primitives.ToggleButton)scene.Editor.NativeView.Template.FindName("expand",scene.Editor.NativeView);
                    Check(ReferenceEquals(scene.Session,session)&&session.Frame==frame&&session.Items.Select(item=>item.Id).SequenceEqual(identities)
                        &&expand.IsChecked==true&&scene.Viewport.NativeView.ImageDataSource is not null&&scene.Viewport.NativeView.Source is not null,
                        $"独立原ROI场景{index}同会话/同帧/稳定对象及原展开按钮和真实图像");
                    var numbers=Descendants<SunEyeVision.Plugin.Wpf.Native.Numeric.Apps.XmlUI.ParamTabControl.SliderArrangeFloat>(scene.Editor).Count();
                    foreach(var number in Descendants<SunEyeVision.Plugin.Wpf.Native.Numeric.Apps.XmlUI.ParamTabControl.SliderArrangeFloat>(scene.Editor))
                    {
                        var roiCombo=(ComboBox)number.FindName("CBox");
                        var leaf=(SunEyeVision.Plugin.Wpf.Native.Numeric.Apps.XmlUI.ParamTabControl.NumericControl)roiCombo.Template.FindName("T1",roiCombo);
                        var text=(TextBox)leaf.FindName("ValueText");
                        Check(leaf.ActualWidth>0&&text.ActualWidth>0&&double.TryParse(text.Text,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out var shown)&&Math.Abs(shown-number.StartValue)<.011,$"形状{index}真实数字列宽/文字及绑定值独立可见");
                    }
                    var points=Descendants<SunEyeVision.Plugin.Wpf.Native.Numeric.Apps.XmlUI.ParamTabControl.SliderArrangePoint>(scene.Editor).ToArray();
                    Check(index==2?points.Length==8&&points.Count(point=>point.IsX)==4&&points.Count(point=>!point.IsX)==4&&points.All(point=>((System.Windows.Controls.Primitives.ToggleButton)point.FindName("ToBtn")).Template.FindName("T1",(System.Windows.Controls.Primitives.ToggleButton)point.FindName("ToBtn")) is SunEyeVision.Plugin.Wpf.Native.Numeric.Apps.XmlUI.ParamTabControl.NumericControl):numbers==expected[index],$"独立原ROI形状{index}完整参数数量浮点={numbers}/顶点={points.Length}");
                }
                SunEyeVision.Plugin.Wpf.Design.NativeRoiDesignData.SetSampleIndex(scene,2);
                Render(scene,"native-roi-scene-polygon-visible.png",1020,560);
                foreach(var point in Descendants<SunEyeVision.Plugin.Wpf.Native.Numeric.Apps.XmlUI.ParamTabControl.SliderArrangePoint>(scene.Editor))
                {
                    var toggle=(System.Windows.Controls.Primitives.ToggleButton)point.FindName("ToBtn");
                    var leaf=(SunEyeVision.Plugin.Wpf.Native.Numeric.Apps.XmlUI.ParamTabControl.NumericControl)toggle.Template.FindName("T1",toggle);
                    var text=(TextBox)leaf.FindName("ValueText");
                    Check(leaf.ActualWidth>0&&text.ActualWidth>0&&double.TryParse(text.Text,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out var shown)&&Math.Abs(shown-(point.IsX?point.PointSource.X:point.PointSource.Y))<.011,"原Point真实坐标和列宽可在无Loaded设计中显示");
                }
                SunEyeVision.Plugin.Wpf.Design.NativeRoiDesignData.SetExpandedParameters(scene,false);
                Render(scene,"native-roi-scene-collapsed.png",1020,560);
                Check(((System.Windows.Controls.Primitives.ToggleButton)scene.Editor.NativeView.Template.FindName("expand",scene.Editor.NativeView)).IsChecked==false,"独立设计属性操作原展开按钮收起");
                using var pixels=session.Image.AcquirePixels();using var runs=pixels.AcquireDomainReader();
                Check(pixels.GetRow(0).Length==648&&pixels.GetRow(0)[640]==233&&runs.RunCount==320,"独立示例图像保留padding与显式实际域");
                scene.Dispose();
                Check(pixels.GetRow(0)[640]==233&&runs.ReadRuns(319,1).Length==1,"设计owner释放后已借用像素及域reader独立有效");
                var rejected=false;try{session.Image.AcquirePixels();}catch(ObjectDisposedException){rejected=true;}
                Check(rejected,"设计owner释放后拒绝新像素租约");
                using var retainedDomain=pixels.EffectiveDomain.AcquireReader();pixels.Dispose();
                Check(retainedDomain.ReadRuns(0,1).Length==1,"像素lease释放后已取得域reader独立有效");
                rejected=false;try{_=pixels.EffectiveDomain;}catch(ObjectDisposedException){rejected=true;}
                Check(rejected,"已释放像素lease的EffectiveDomain属性立即拒绝");
            }
            var expanded=new SunEyeVision.Plugin.Wpf.Native.Composition.Apps.XmlUI.ParamSubscribeWindow();
            try
            {
                Render((FrameworkElement)expanded.Content,"native-expanded-independent.png",240,480);
                Check(Application.Current is null&&expanded.Style is not null
                    &&expanded.FindName("treeView") is not null&&expanded.FindName("indextreeView") is not null,
                    "原扩展订阅专窗无Application和UI宿主可取得正式资源及双树");
            }
            finally{expanded.Close();}
            var nativeColor=new NativeColorPresenter {SelectedColor=Color.FromArgb(200,10,20,30)};
            Render(nativeColor,"native-color-independent.png",394,38);
            Check(nativeColor.NativeView.CurrentColor.Color==Color.FromArgb(200,10,20,30)
                &&nativeColor.NativeView.GetType().FullName=="SunEyeVision.Plugin.Wpf.Native.Input.HikPcUI.ListView.ColorPickerControlView"
                &&Application.Current is null,"原色板及RGB真实视图可无Application/宿主独立设计渲染");
            var paletteLeaf=new SunEyeVision.Plugin.Wpf.Native.Input.HikPcUI.ListView.ColorPickerFirstItem();
            Render(paletteLeaf,"native-palette-leaf-independent.png",300,250);
            var paletteLabels=Descendants<Label>(paletteLeaf).Select(label=>label.Content as string).ToArray();
            Check(paletteLabels.Contains((string)paletteLeaf.FindResource("Lang_ThemeColors"))
                &&paletteLabels.Contains((string)paletteLeaf.FindResource("Lang_StandardColors"))
                &&((Grid)paletteLeaf.Content).Background is SolidColorBrush {Color:var paletteBackground}
                &&paletteBackground==Colors.White&&Application.Current is null,
                "直接创建原色板叶取得原标题语言资源与原父白背景，不依赖父视图或Application");
            var nativeInputs=new StackPanel();
            nativeInputs.Children.Add(new SunEyeVision.Plugin.Wpf.Native.Input.Apps.PcUI.Controls.PopupButton());
            nativeInputs.Children.Add(new SunEyeVision.Plugin.Wpf.Native.Composition.Apps.XmlUI.ParamTab.DeleteTextBox{Text="设计来源"});
            nativeInputs.Children.Add(new SunEyeVision.Plugin.Wpf.Native.Composition.Apps.XmlUI.ParamTab.SubTreeView{Height=100});
            nativeInputs.Children.Add(SunEyeVision.Plugin.Wpf.Presenters.Input.NativeInputFactory.Enumeration());
            Render(nativeInputs,"native-input-family-independent.png",400,230);
            Check(Application.Current is null&&nativeInputs.Children.Count==4,
                "原Apps弹层文本树及枚举可无Application独立装配资源");
            using var subscription=new DeclaredFieldEditor(new Port("reference",DataType.Integer,EditModes:FieldEditModes.Reference),
                "来源",new ReferenceSetting(new OutputReference("producer","value",DataType.Integer)),_=>throw new Exception("设计渲染不得写入"),
                ReferenceFieldOptions.Empty);
            Render(subscription,"native-reference-composition-independent.png",400,65);
            Check(Application.Current is null&&Descendants<SunEyeVision.Plugin.Wpf.Native.Input.Apps.PcUI.Controls.PopupButton>(subscription).Count()==2
                &&Descendants<SunEyeVision.Plugin.Wpf.Native.Composition.Apps.XmlUI.ParamTab.DeleteTextBox>(subscription).Any(text=>text.IsReadOnly),
                "完整原订阅两Popup及只读文本无Application/宿主独立渲染，不写草稿");
            using var first=new BlobParameterEditor();using var second=new BlobParameterEditor();
            var presenter=(DeclaredSettingsView)first.Content;
            var fields=(DeclaredFieldsView)presenter.Content;
            // The user's designer may select another page. Materialize the input page before
            // inspecting its real visual template; preserve the saved XAML page index.
            var inputTabs=(TabControl)fields.Content;
            inputTabs.SelectedItem=inputTabs.Items.Cast<TabItem>().Single(tab=>
                tab.Content is SunEyeVision.Plugin.Wpf.Native.Composition.Apps.XmlUI.ParamListView native
                &&native.ItemsSource.Cast<SunEyeVision.Plugin.Wpf.Adapters.Fields.NativeParameterRow>().Select(row=>row.View).OfType<DeclaredInputFieldsView>().Any(view=>
                    view.Fields.Any(field=>field.FieldId==BlobTool.ImageInput)));
            Render(first,"parameters-initial.png",600,680);
            Check(fields.Fields.Count==54&&replacementInputCount(fields)==4,
                "独立无参真实参数视图包含54设置及4输入，未启动宿主");
            var inputFields=InputFields(fields);
            var image=inputFields.Single(field=>field.FieldId==BlobTool.ImageInput);
            Check(image.InputElement is ComboBox combo&&combo.Resources.MergedDictionaries.Any(dictionary=>dictionary.Source?.OriginalString.EndsWith("SourceComboBoxes.xaml")==true)
                &&Descendants<Image>(image).Any(icon=>icon.Source is BitmapSource bitmap&&bitmap.PixelWidth>0),
                "真实设计视图输入源使用VM EnumerationG下拉及原图标");
            var defaults=new BlobSettings();
            Check(first.TryCapture(out var captured,out _,out _)&&((BlobSettings)captured).Threshold==defaults.Threshold,
                "设计草稿默认值来自真实XML");
            var schema=presenter.Draft!.Schema;
            var threshold=schema.Presentation!.Fields.Single(field=>field.DisplayName=="阈值").FieldId;
            var oldField=fields.Fields.Single(field=>field.FieldId==threshold);
            Check(oldField.TrySetConstant(101L)&&first.TryCapture(out captured,out _,out _)&&((BlobSettings)captured).Threshold==101,
                "设计器使用真实数值控件及草稿写入链");
            Check(second.TryCapture(out var independent,out _,out _)&&((BlobSettings)independent).Threshold==defaults.Threshold,
                "两个设计视图不共享可变草稿");
            var replacement=BlobDesignData.CreateParameterContext();presenter.Bind(replacement.Draft,replacement.Options);
            Check(!oldField.TrySetConstant(102L)&&first.TryCapture(out captured,out _,out _)&&((BlobSettings)captured).Threshold==defaults.Threshold,
                "重绑后旧控件事件不能修改当前草稿");
            // Options can be rebound to the same input content; it must be detached from the old tree first.
            presenter.Bind(replacement.Draft,replacement.Options);
            fields=(DeclaredFieldsView)presenter.Content;
            var tabs=(TabControl)fields.Content;
            presenter.SelectedPageIndex=1;var previewSelected=tabs.SelectedIndex==1;
            tabs.SelectedIndex=2;
            Check(previewSelected&&presenter.SelectedPageIndex==2,
                "设计器SelectedPageIndex选页及运行时Tab反向同步走同一视图");
            for(var index=0;index<tabs.Items.Count;index++)
            {
                tabs.SelectedIndex=index;Render(first,"parameters-"+index+".png",600,680);
                foreach(var tab in tabs.Items.Cast<TabItem>())
                {
                    var header=(ContentControl)tab.Template.FindName("Content",tab);
                    var key=tab.IsSelected?"Col_ModuleConfig_Title_Selected_Foreground":"Col_ModuleConfig_Title_Foreground";
                    Check(header.Foreground is SolidColorBrush actual
                        &&tabs.FindResource(key) is SolidColorBrush expected&&actual.Color==expected.Color
                        &&expected.Color==(tab.IsSelected?Color.FromRgb(31,14,1):Color.FromRgb(61,61,61)),
                        "页签"+index+"/"+tab.Header+"保持原主题正常或选中标题颜色");
                }
            }
            Check(tabs.Items.Count==3&&fields.TryFindResource("TopTabItemStyle") is Style,
                "三页及正式迁入模板独立可解析并渲染");
            using var result=new BlobResultView();Render(result,"results.png",900,350);
            Check(result.FilteredBlobs.Count==2&&result.FilteredTotalArea==336&&Descendants<DataGrid>(result).Single().Columns.Count>0
                &&result.TryFindResource("O06.PanelBackgroundBrush") is Brush,
                "真实结果视图有独立示例行、XML列及正式资源");
            result.Clear();
            Check(result.FilteredBlobs.Count==0&&result.FilteredBlobCount==0,"真实结果视图可切换为空结果");
            Check(Application.Current is null&&!AppDomain.CurrentDomain.GetAssemblies().Any(assembly=>
                assembly.GetName().Name is "SunEyeVision.UI" or "SunEyeVision.Workflow" or "halcondotnetxl" or "halcondotnet"),
                "独立显示未加载主程序、工作流或HALCON程序集");
            // Load the actual root markup without running its x:Class constructor, as a designer surface can do.
            var markup=ReadRootMarkup("BlobParameterEditor.xaml");
            var root=(UserControl)XamlReader.Parse(markup);
            var markupPresenter=(DeclaredSettingsView)root.Content;
            Check(markupPresenter.Draft is not null&&((DeclaredFieldsView)markupPresenter.Content).Fields.Count==54,
                "根XAML不执行外层构造函数也能由真实子视图取得设计草稿");
            markupPresenter.Dispose();
            var resultRoot=(UserControl)XamlReader.Parse(ReadRootMarkup("BlobResultView.xaml"));
            resultRoot.DataContext=new BlobResultDesignData();Render(resultRoot,"results-root-markup.png",900,350);
            Check(Descendants<DataGrid>(resultRoot).Single() is {Columns.Count:>0,Items.Count:2},
                "结果根XAML不执行外层构造函数也有正式XML列及设计绑定");
            first.Dispose();
            Check(presenter.Draft is null&&presenter.Content is null,"释放视图移除草稿和控件树");
            Console.WriteLine($"设计时独立组装检查：{_count}组通过；不代替Visual Studio实际设计器与用户视觉验收。");return 0;
        }
        catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
    private static string ReadRootMarkup(string name)
    {
        var repo=new DirectoryInfo(Environment.GetEnvironmentVariable("S2_PRODUCT_ROOT")!).Parent!.Parent!.Parent!;
        var document=XDocument.Load(Path.Combine(repo.FullName,"tools/SunEyeVision.BlobAnalysis/Wpf/Views",name));
        document.Root!.Attribute(XName.Get("Class","http://schemas.microsoft.com/winfx/2006/xaml"))!.Remove();
        foreach(var attribute in document.Descendants().Attributes().Where(attribute=>attribute.Name.LocalName=="SelectionChanged").ToArray())attribute.Remove();
        foreach(var attribute in document.Root.Attributes().Where(attribute=>attribute.IsNamespaceDeclaration&&attribute.Value.StartsWith("clr-namespace:")&&!attribute.Value.Contains(";assembly=")).ToArray())
        {
            // Loose XAML has no containing project assembly; qualify the namespace, including expanded names.
            var original=attribute.Value;var qualified=original+";assembly=SunEyeVision.Tool.BlobAnalysis.Wpf";
            foreach(var element in document.Descendants().Where(element=>element.Name.NamespaceName==original).ToArray())
                element.Name=XName.Get(element.Name.LocalName,qualified);
            foreach(var member in document.Descendants().Attributes().Where(member=>!member.IsNamespaceDeclaration&&member.Name.NamespaceName==original).ToArray())
            {member.Parent!.SetAttributeValue(XName.Get(member.Name.LocalName,qualified),member.Value);member.Remove();}
            attribute.Value=qualified;
        }
        return document.ToString();
    }
    private static IEnumerable<double> CrosshairNumbers(DependencyObject root)
    {
        foreach(var field in Descendants<SunEyeVision.Plugin.Wpf.Native.Numeric.HikPcUI.ListView.SliderArrange>(root))
        {
            var box=(ComboBox)field.FindName("CBox");
            var leaf=(SunEyeVision.Plugin.Wpf.Native.Numeric.HikPcUI.ListView.NumericControl)box.Template.FindName("T1",box);
            var text=(TextBox)leaf.FindName("ValueText");
            yield return double.Parse(text.Text,CultureInfo.InvariantCulture);
        }
    }
    private static int replacementInputCount(DeclaredFieldsView fields)
        =>InputFields(fields).Count();
    private static IEnumerable<DeclaredFieldEditor> InputFields(DeclaredFieldsView fields)
    {
        var tabs=(TabControl)fields.Content;
        return tabs.Items.Cast<TabItem>().Select(tab=>(SunEyeVision.Plugin.Wpf.Native.Composition.Apps.XmlUI.ParamListView)tab.Content)
            .SelectMany(native=>native.ItemsSource.Cast<SunEyeVision.Plugin.Wpf.Adapters.Fields.NativeParameterRow>()).Select(row=>row.View).OfType<DeclaredInputFieldsView>()
            .SelectMany(input=>input.Fields);
    }
    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T:DependencyObject
    {
        for(int index=0;index<VisualTreeHelper.GetChildrenCount(root);index++)
        {var child=VisualTreeHelper.GetChild(root,index);if(child is T value)yield return value;foreach(var descendant in Descendants<T>(child))yield return descendant;}
    }
    private static void Render(FrameworkElement view,string file,int width,int height)
    {
        view.Measure(new Size(width,height));view.Arrange(new Rect(0,0,width,height));view.UpdateLayout();
        System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ContextIdle);
        view.UpdateLayout();
        var bitmap=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);bitmap.Render(view);
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var output=File.Create(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,file));encoder.Save(output);
    }
}

