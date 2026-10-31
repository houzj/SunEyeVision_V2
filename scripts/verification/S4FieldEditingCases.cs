using SunEyeVision.Plugin.Wpf.Adapters.References;
using System.Windows.Media.Imaging;
using System.Windows.Data;
using System.Windows.Input;
using SunEyeVision.Plugin.Wpf.Presenters.Display;
using NativeBoolean=SunEyeVision.Plugin.Wpf.Native.Composition.Apps.XmlUI.ParamTabControl.BooleanControl;
using NativeTree=SunEyeVision.Plugin.Wpf.Controls.References.Primitives.SubTreeView;
using NativeText=SunEyeVision.Plugin.Wpf.Controls.References.Primitives.DeleteTextBox;
using AppsPopup=SunEyeVision.Plugin.Wpf.Controls.Popups.PopupButton;
using System.IO;
using System.Reflection;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using SunEyeVision.Plugin.Wpf;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Fields;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.Infrastructure.Managers.Tool;
using SunEyeVision.Plugin.Infrastructure.Managers.Plugin;
using SunEyeVision.Workflow;

internal static class S2DocumentEditingCases
{
    private static Window owner=null!;
    private static void Pump()=>Dispatcher.CurrentDispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);
    private static void Show(UIElement view){owner.Content=view;owner.UpdateLayout();Pump();}
    [STAThread] private static int Main()
    {
        try
        {
            if(Application.Current is null)new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
            foreach(var resource in new[]{"Shell/Resources/AppResources.xaml","Themes/Dark/DarkTheme.xaml"})Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("pack://application:,,,/SunEyeVision.UI;component/"+resource)});
            owner=new Window{Width=700,Height=720,Title="正式字段消费验证"};owner.Resources.MergedDictionaries.Add(EditorResources.Create());owner.Show();Pump();
            ParameterSetting current=new ConstantSetting(100L);var writes=0;
            var number=new DeclaredFieldEditor(new("threshold",DataType.Integer,Storage:NumericStorage.Int32),"阈值",current,
                next=>{current=next;writes++;});
            Show(number);var input=(TextBox)number.InputElement!;
            input.Text="2147483648";Require(number.HasInvalidText&&Equals(((ConstantSetting)current).Value,100L)&&writes==0,"溢出数字污染草稿");
            input.Text="1.2";Require(number.HasInvalidText&&input.Text=="1.2"&&writes==0,"非法原文未保留");
            input.Text="210";number.TryCommitPending();Require(!number.HasInvalidText&&Equals(((ConstantSetting)current).Value,210L)&&writes==1,"数字修正未恢复");
            var nativeInput=OriginalNumberInput(number);
            ((RepeatButton)nativeInput.FindName("UpButton")).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));Pump();
            Require(Equals(((ConstantSetting)current).Value,211L)&&writes==2,"无界原数值箭头未单次写入草稿；writes="+writes);
            Console.WriteLine("通过：声明Int32范围、整数格式、非法原文保留和修正恢复");

            var exact=1d;var floating=new DeclaredFieldEditor(new("ratio",DataType.Number),"比例",new ConstantSetting(exact),next=>exact=(double)((ConstantSetting)next).Value!);
            Show(floating);((TextBox)floating.InputElement!).Text="NaN";Require(floating.HasInvalidText&&exact==1d,"NaN通过声明校验");
            ((TextBox)floating.InputElement!).Text="0.5";floating.TryCommitPending();Require(!floating.HasInvalidText&&exact==.5d,"有限数值不能恢复");
            Console.WriteLine("通过：非有限数值拒绝，独立字段错误状态互不污染");

            var choiceValue="Binary";var choice=new DeclaredFieldEditor(new("type",DataType.Text,Choices:new TextChoiceConstraint(new[]{"Binary","BinaryInv"})),"极性",
                new ConstantSetting(choiceValue),next=>choiceValue=(string)((ConstantSetting)next).Value!);
            Show(choice);
            ((ComboBox)choice.InputElement!).SelectedValue="BinaryInv";Require(choiceValue=="BinaryInv","已声明选项未提交到草稿");
            Require(((ComboBox)choice.InputElement!).Items.Count==2,"选项不是来自正式闭合声明");
            Console.WriteLine("通过：下拉选项直接消费正式声明");
            VerifyNativeInputs();
            VerifyFieldState();
            VerifyAtomicFieldGroup();
            VerifyReferenceComposition();

            var color=PresentationDeclarations.Color(10,20,30);var colorWrites=0;
            var record=new DeclaredFieldEditor(new("color",PresentationDeclarations.Rgba),"颜色",new ConstantSetting(color),next=>{color=(RecordValue)((ConstantSetting)next).Value!;colorWrites++;});
            Show(record);var colorInput=(NativeColorPresenter)record.InputElement!;
            var nativeColorCommand=SunEyeVision.Plugin.Wpf.Native.Input.HikPcUI.ListView.ColorPickerControlView.SelectColorCommand;
            void Pick(NativeColorPresenter target,Color value)=>nativeColorCommand.Execute(new SolidColorBrush(value),target.NativeView);
            Pick(colorInput,Color.FromArgb(200,255,20,30));
            Pick(colorInput,Color.FromArgb(123,32,64,128));
            Require(!record.HasInvalidText&&color.Get<long>("r")==32&&color.Get<long>("a")==123&&colorWrites==2,"原调色命令连续写入RGBA失败");
            Require(BindingOperations.IsDataBound(colorInput.NativeView,SunEyeVision.Plugin.Wpf.Native.Input.HikPcUI.ListView.ColorPickerControlView.CurrentColorProperty),"原调色命令切断双向绑定");
            colorInput.SelectedColor=Color.FromArgb(200,255,20,30);
            Require(colorInput.NativeView.CurrentColor.Color==colorInput.SelectedColor,"外部颜色恢复没有同步原件");
            Pick(colorInput,Colors.Red);
            colorInput.Palette.IsDropDownOpen=true;Pump();
            var palettePopup=(Popup)colorInput.Palette.Template.FindName("PART_Popup",colorInput.Palette);
            var paletteTabs=NativeDescendants(palettePopup.Child).OfType<TabControl>().Single();
            Require(paletteTabs.Items.Count==2,"原默认/更多页不完整");
            void CapturePalette(string file)
            {
                var element=(FrameworkElement)palettePopup.Child;var bitmap=new RenderTargetBitmap((int)Math.Ceiling(element.ActualWidth),(int)Math.Ceiling(element.ActualHeight),96,96,PixelFormats.Pbgra32);
                bitmap.Render(element);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var stream=File.Create(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,file));encoder.Save(stream);
            }
            paletteTabs.SelectedIndex=1;Pump();CapturePalette("native-palette-more.png");
            var advanced=NativeDescendants(palettePopup.Child).OfType<SunEyeVision.Plugin.Wpf.Native.Input.HikPcUI.ListView.AdvancedColorPickerDialog>().Single();
            var brightness=(Slider)advanced.FindName("brightnessSlider");
            var beforeMoreWrites=colorWrites;
            var redBase=Color.FromRgb(255,0,0);
            var redHalf=Color.FromScRgb(1f,redBase.ScR*.5f,redBase.ScG*.5f,redBase.ScB*.5f);
            brightness.Value=.25;Pump();
            Require(colorWrites==beforeMoreWrites+1 && color.Get<long>("r")==redHalf.R && color.Get<long>("g")==redHalf.G && color.Get<long>("b")==redHalf.B && color.Get<long>("a")==255,
                "首次打开更多页调亮度未按原公式基于已接受红色写入");
            Require(BindingOperations.IsDataBound(advanced,SunEyeVision.Plugin.Wpf.Native.Input.HikPcUI.ListView.AdvancedColorPickerDialog.SelectedCurrentColorProperty)
                && BindingOperations.IsDataBound(colorInput.NativeView,SunEyeVision.Plugin.Wpf.Native.Input.HikPcUI.ListView.ColorPickerControlView.CurrentColorProperty),"更多页操作断开颜色绑定");
            brightness.Value=.75;Pump();
            var redLight=Color.FromScRgb(1f,redBase.ScR+(1f-redBase.ScR)*.5f,redBase.ScG+(1f-redBase.ScG)*.5f,redBase.ScB+(1f-redBase.ScB)*.5f);
            Require(colorWrites==beforeMoreWrites+2 && color.Get<long>("r")==redLight.R && color.Get<long>("g")==redLight.G && color.Get<long>("b")==redLight.B && color.Get<long>("a")==255,"更多页第二次调亮度未按原公式继续写入：实际="+color.Get<long>("r")+","+color.Get<long>("g")+","+color.Get<long>("b")+","+color.Get<long>("a")+" 预期="+redLight+" writes="+colorWrites+" before="+beforeMoreWrites);
            Pick(colorInput,Colors.Blue);Pump();beforeMoreWrites=colorWrites;brightness.Value=.25;Pump();
            Require(colorWrites==beforeMoreWrites+1 && color.Get<long>("r")==0 && color.Get<long>("g")==0 && color.Get<long>("b")>0,"外部选蓝后亮度仍沿用旧基色");
            var nativeColorProperty=SunEyeVision.Plugin.Wpf.Native.Input.HikPcUI.ListView.ColorPickerControlView.CurrentColorProperty;
            var descriptor=DependencyPropertyDescriptor.FromProperty(nativeColorProperty,colorInput.NativeView.GetType());
            var reenter=true;Color reenteredProposal=default;
            EventHandler reentrant=(_,_)=>
            {
                if(!reenter)return;reenter=false;reenteredProposal=colorInput.NativeView.CurrentColor.Color;
                colorInput.NativeView.SetCurrentValue(nativeColorProperty,new SolidColorBrush(Colors.Green));
                colorInput.NativeView.SetCurrentValue(nativeColorProperty,new SolidColorBrush(Color.FromArgb(reenteredProposal.A,reenteredProposal.R,reenteredProposal.G,reenteredProposal.B)));
            };
            descriptor.AddValueChanged(colorInput.NativeView,reentrant);
            try{brightness.Value=.75;Pump();}finally{descriptor.RemoveValueChanged(colorInput.NativeView,reentrant);}
            var baseBytes=(byte[])advanced.GetType().GetField("_pixels",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(advanced)!;
            Require(!reenter&&baseBytes[2]==reenteredProposal.R&&baseBytes[1]==reenteredProposal.G&&baseBytes[0]==reenteredProposal.B,
                "外部颜色重入后更多页基色不是最后接纳的颜色");
            var reentryBase=Color.FromRgb(reenteredProposal.R,reenteredProposal.G,reenteredProposal.B);
            var reentryHalf=Color.FromScRgb(1f,reentryBase.ScR*.5f,reentryBase.ScG*.5f,reentryBase.ScB*.5f);
            brightness.Value=.25;Pump();
            Require(color.Get<long>("r")==reentryHalf.R&&color.Get<long>("g")==reentryHalf.G&&color.Get<long>("b")==reentryHalf.B,"重入后的下次亮度仍使用旧颜色");
            var advancedColorProperty=SunEyeVision.Plugin.Wpf.Native.Input.HikPcUI.ListView.AdvancedColorPickerDialog.SelectedCurrentColorProperty;
            var sliderBorder=(Border)advanced.FindName("brightnessSliderBorder");var signal=true;
            RoutedPropertyChangedEventHandler<double> throwingSlider=(_,eventArgs)=>{if(signal&&eventArgs.NewValue==.5){signal=false;throw new InvalidOperationException("亮度观察者故障");}};
            brightness.ValueChanged+=throwingSlider;var failureObserved=false;
            try{advanced.SetCurrentValue(advancedColorProperty,new SolidColorBrush(Colors.Blue));}catch(InvalidOperationException sliderFailure)when(sliderFailure.Message=="亮度观察者故障"){failureObserved=true;}
            finally{brightness.ValueChanged-=throwingSlider;}
            var middle=((LinearGradientBrush)sliderBorder.Background).GradientStops[1].Color;
            Require(failureObserved&&middle.R==0&&middle.G==0&&middle.B==255&&BindingOperations.IsDataBound(advanced,advancedColorProperty),"观察者异常后已接纳颜色的渐变未同步或异常丢失");
            brightness.Value=.25;Pump();var nested=true;
            RoutedPropertyChangedEventHandler<double> nestedSlider=(_,eventArgs)=>{if(nested&&eventArgs.NewValue==.5){nested=false;advanced.SetCurrentValue(advancedColorProperty,new SolidColorBrush(Colors.Green));}};
            brightness.ValueChanged+=nestedSlider;
            try{advanced.SetCurrentValue(advancedColorProperty,new SolidColorBrush(Colors.Blue));}finally{brightness.ValueChanged-=nestedSlider;}
            middle=((LinearGradientBrush)sliderBorder.Background).GradientStops[1].Color;
            Require(!nested&&middle.R==Colors.Green.R&&middle.G==Colors.Green.G&&middle.B==Colors.Green.B&&advanced.CurrentColor.G==Colors.Green.G,"颜色通知重入后旧同步覆盖新颜色");
            BindingOperations.GetBindingExpression(advanced,advancedColorProperty)!.UpdateTarget();Pump();
            brightness.Value=.25;Pump();signal=true;brightness.ValueChanged+=throwingSlider;failureObserved=false;
            try{advanced.GetType().GetMethod("CommitChartPixel",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(advanced,new object[]{new byte[]{30,60,90,255},new Point(20,20)});}
            catch(TargetInvocationException chartFailure)when(chartFailure.InnerException is InvalidOperationException inner&&inner.Message=="亮度观察者故障"){failureObserved=true;}
            finally{brightness.ValueChanged-=throwingSlider;}
            middle=((LinearGradientBrush)sliderBorder.Background).GradientStops[1].Color;
            Require(failureObserved&&color.Get<long>("r")==90&&color.Get<long>("g")==60&&color.Get<long>("b")==30&&middle.R==90&&middle.G==60&&middle.B==30,"图表颜色已接纳但异常后缓存/渐变不一致或异常丢失");
            var markerShape=(System.Windows.Shapes.Ellipse)advanced.FindName("colorMarker");var markerTransform=(TranslateTransform)markerShape.RenderTransform;
            var markerDescriptor=DependencyPropertyDescriptor.FromProperty(TranslateTransform.XProperty,typeof(TranslateTransform));var markerArmed=true;
            EventHandler markerReentry=(_,_)=>{if(markerArmed){markerArmed=false;colorInput.NativeView.SetCurrentValue(nativeColorProperty,new SolidColorBrush(Colors.Green));}};
            markerDescriptor.AddValueChanged(markerTransform,markerReentry);
            try{advanced.GetType().GetMethod("CommitChartPixel",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(advanced,new object[]{new byte[]{80,70,60,255},new Point(38,25)});}
            finally{markerDescriptor.RemoveValueChanged(markerTransform,markerReentry);}
            middle=((LinearGradientBrush)sliderBorder.Background).GradientStops[1].Color;
            Require(!markerArmed&&color.Get<long>("r")==Colors.Green.R&&color.Get<long>("g")==Colors.Green.G&&color.Get<long>("b")==Colors.Green.B&&middle.G==Colors.Green.G&&advanced.CurrentColor.G==Colors.Green.G,"图表标记通知重入后旧颜色覆盖新接纳");
            markerArmed=true;EventHandler markerThrow=(_,_)=>{if(markerArmed){markerArmed=false;throw new InvalidOperationException("标记观察者故障");}};
            markerDescriptor.AddValueChanged(markerTransform,markerThrow);failureObserved=false;
            try{advanced.GetType().GetMethod("CommitChartPixel",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(advanced,new object[]{new byte[]{33,22,11,255},new Point(55,25)});}
            catch(TargetInvocationException markerFailure)when(markerFailure.InnerException is InvalidOperationException inner&&inner.Message=="标记观察者故障"){failureObserved=true;}
            finally{markerDescriptor.RemoveValueChanged(markerTransform,markerThrow);}
            middle=((LinearGradientBrush)sliderBorder.Background).GradientStops[1].Color;
            Require(failureObserved&&color.Get<long>("r")==11&&color.Get<long>("g")==22&&color.Get<long>("b")==33&&middle.R==11&&middle.G==22&&middle.B==33,"标记观察者异常后已接纳颜色未同步或异常丢失");
            Console.WriteLine("通过：更多页连续亮度、双向绑定、外部颜色同步、接纳及标记重入和异常清理；图表像素由测试注入");
            paletteTabs.SelectedIndex=0;Pump();CapturePalette("native-palette-default.png");
            Require(NativeDescendants(palettePopup.Child).OfType<SunEyeVision.Plugin.Wpf.Native.Input.HikPcUI.ListView.ColorPickerFirstItem>().Any(),"默认页未接原主题/标准色板");
            var originalHeader=NativeDescendants(palettePopup.Child).OfType<Label>().First();
            var headerText=NativeDescendants(originalHeader).OfType<TextBlock>().First();
            Require(headerText.Foreground is SolidColorBrush headerBrush&&headerBrush.Color==Color.FromRgb(0,0,139)&&headerText.FontWeight==FontWeights.Bold,"宿主全局文字样式覆盖原色板标题");
            Require(typeof(NativeColorPresenter).Assembly.GetType("SunEyeVision.Plugin.Wpf.Fields.ColorField")==null&&typeof(NativeColorPresenter).Assembly.GetType("SunEyeVision.Plugin.Wpf.Controls.VmColorPicker")==null,"自制色板仍作为产品后备路径");
            colorInput.ClosePalette();
            var rejectColor=true;var guardedAttempts=0;var guardedColor=PresentationDeclarations.Color(1,2,3);
            var guardedEditor=new DeclaredFieldEditor(new("guarded-color",PresentationDeclarations.Rgba),"拒绝颜色",new ConstantSetting(guardedColor),next=>
            {guardedAttempts++;if(rejectColor)throw new InvalidOperationException("颜色草稿已失效");guardedColor=(RecordValue)((ConstantSetting)next).Value!;});
            colorInput.Palette.IsDropDownOpen=true;Show(guardedEditor);Require(!colorInput.Palette.IsDropDownOpen,"卸载未关闭原色板");
            var guardedPicker=(NativeColorPresenter)guardedEditor.InputElement!;
            guardedPicker.Palette.IsDropDownOpen=true;Pump();
            var guardedPopup=(Popup)guardedPicker.Palette.Template.FindName("PART_Popup",guardedPicker.Palette);
            var guardedTabs=NativeDescendants(guardedPopup.Child).OfType<TabControl>().Single();guardedTabs.SelectedIndex=1;Pump();
            var guardedAdvanced=NativeDescendants(guardedPopup.Child).OfType<SunEyeVision.Plugin.Wpf.Native.Input.HikPcUI.ListView.AdvancedColorPickerDialog>().Single();
            var guardedBrightness=(Slider)guardedAdvanced.FindName("brightnessSlider");guardedBrightness.Value=.25;Pump();
            Require(guardedEditor.HasInvalidText&&guardedColor.Get<long>("r")==1&&guardedPicker.SelectedColor==Color.FromRgb(1,2,3)
                &&guardedPicker.NativeView.CurrentColor.Color==Color.FromRgb(1,2,3)&&guardedAdvanced.SelectedCurrentColor.Color==Color.FromRgb(1,2,3),"更多页写入拒绝未恢复完整颜色链");
            rejectColor=false;guardedBrightness.Value=.75;Pump();
            var originalGuarded=Color.FromRgb(1,2,3);
            var expectedGuarded=Color.FromScRgb(1f,originalGuarded.ScR+(1f-originalGuarded.ScR)*.5f,originalGuarded.ScG+(1f-originalGuarded.ScG)*.5f,originalGuarded.ScB+(1f-originalGuarded.ScB)*.5f);
            Require(!guardedEditor.HasInvalidText&&guardedColor.Get<long>("r")==expectedGuarded.R&&guardedColor.Get<long>("g")==expectedGuarded.G&&guardedColor.Get<long>("b")==expectedGuarded.B,"拒绝回滚后亮度没有基于已接纳颜色恢复写入");
            rejectColor=true;Pick(guardedPicker,Colors.Red);
            Require(guardedEditor.HasInvalidText&&guardedPicker.SelectedColor.R==expectedGuarded.R&&guardedPicker.SelectedColor.G==expectedGuarded.G&&guardedPicker.SelectedColor.B==expectedGuarded.B&&guardedPicker.NativeView.CurrentColor.Color==guardedPicker.SelectedColor,"默认色板拒绝没有恢复已接纳颜色");
            rejectColor=false;Pick(guardedPicker,Colors.Blue);Require(!guardedEditor.HasInvalidText&&guardedColor.Get<long>("b")==255,"拒绝回滚后原色板不能恢复写入");
            guardedPicker.ClosePalette();Pump();var closedAttempts=guardedAttempts;guardedBrightness.Value=.9;Pump();
            Require(guardedAttempts==closedAttempts&&guardedColor.Get<long>("r")==0&&guardedColor.Get<long>("g")==0&&guardedColor.Get<long>("b")==255,"已关闭的更多页改变草稿");
            guardedPicker.Palette.IsDropDownOpen=true;Pump();guardedPopup=(Popup)guardedPicker.Palette.Template.FindName("PART_Popup",guardedPicker.Palette);
            guardedTabs=NativeDescendants(guardedPopup.Child).OfType<TabControl>().Single();guardedTabs.SelectedIndex=1;Pump();
            guardedAdvanced=NativeDescendants(guardedPopup.Child).OfType<SunEyeVision.Plugin.Wpf.Native.Input.HikPcUI.ListView.AdvancedColorPickerDialog>().Single();
            guardedBrightness=(Slider)guardedAdvanced.FindName("brightnessSlider");
            Require(guardedBrightness.Value==.5&&guardedAdvanced.SelectedCurrentColor.Color.R==0&&guardedAdvanced.SelectedCurrentColor.Color.G==0&&guardedAdvanced.SelectedCurrentColor.Color.B==255,"重新打开未恢复已接纳颜色及亮度");
            guardedBrightness.Value=.25;Pump();Require(guardedColor.Get<long>("r")==0&&guardedColor.Get<long>("g")==0&&guardedColor.Get<long>("b")>0&&guardedColor.Get<long>("b")<255,"重新打开后沿旧颜色调亮度");
            guardedPicker.ClosePalette();Show(record);var retiredAttempts=guardedAttempts;var retiredR=guardedColor.Get<long>("r");var retiredG=guardedColor.Get<long>("g");var retiredB=guardedColor.Get<long>("b");var retiredA=guardedColor.Get<long>("a");guardedBrightness.Value=.25;Pump();
            Require(guardedAttempts==retiredAttempts&&guardedColor.Get<long>("r")==retiredR&&guardedColor.Get<long>("g")==retiredG&&guardedColor.Get<long>("b")==retiredB&&guardedColor.Get<long>("a")==retiredA,"关闭卸载后的旧更多页仍可写入");
            Console.WriteLine("通过：原VM色板默认/更多、重复原调色命令、外部恢复、RGBA、拒绝回滚及卸载关闭");
            var channelType=TypeRef.RecordOf("test.channel",new[]{new Port("r",DataType.Integer,Storage:NumericStorage.UInt8)});
            var channelValue=new RecordValue(channelType,new Dictionary<string,object?>{{"r",10L}});var channelWrites=0;
            var channel=new DeclaredFieldEditor(new("channel",channelType),"通道",new ConstantSetting(channelValue),next=>{channelValue=(RecordValue)((ConstantSetting)next).Value!;channelWrites++;});
            Show(channel);var red=Descendants(channel).OfType<DeclaredFieldEditor>().Single(field=>field.FieldId=="r");
            ((TextBox)red.InputElement!).Text="256";Require(channel.HasInvalidText&&channelValue.Get<long>("r")==10&&channelWrites==0,"记录成员溢出仍更新了记录");
            ((TextBox)red.InputElement!).Text="255";red.TryCommitPending();Require(!channel.HasInvalidText&&channelValue.Get<long>("r")==255&&channelWrites==1,"记录成员纠正未原子更新");
            Console.WriteLine("通过：真实颜色控件原子写RGBA；通用记录UInt8非法原文拦截及修正恢复");

            ParameterSetting bound=new ConstantSetting(7L);
            var reference=new OutputReference("producer","port",TypeRef.ScalarOf(ScalarKind.Int64));
            var binding=new DeclaredFieldEditor(new("optional",DataType.Integer,Required:false,EditModes:FieldEditModes.Constant|FieldEditModes.Reference),"可选数值",bound,
                next=>bound=next,new ReferenceFieldOptions(_=>new(new[]{new DeclaredReferenceCandidate("来源","producer",null,new Port("port",DataType.Integer),Array.Empty<string>())},Array.Empty<DeclaredReferenceCandidate>())));
            Show(binding);Click(binding,"切换为订阅");SelectReferenceLeaf(binding,c=>c.NodeId=="producer");Require(bound is ReferenceSetting source&&source.Reference.NodeId==reference.NodeId,"绑定意图未交给宿主");
            Click(binding,"切换为数值编辑");Require(bound is ConstantSetting restored&&Equals(restored.Value,7L),"常量备份没有恢复");
            Click(binding,"清除可选项");Require(bound is UnsetSetting,"清除可选项误变为空值或空集合");
            Console.WriteLine("通过：宿主选择绑定、恢复常量及明确Unset；控件不自行执行或发布");

            var rejected=3L;var reject=new DeclaredFieldEditor(new("host-reject",DataType.Integer),"宿主拒绝",new ConstantSetting(rejected),_=>throw new InvalidOperationException("编辑范围已失效"));
            Show(reject);((TextBox)reject.InputElement!).Text="4";reject.TryCommitPending();Require(reject.HasInvalidText&&reject.Error.Contains("编辑范围")&&Equals(((ConstantSetting)reject.Setting).Value,3L),"宿主拒绝后控件冒称写入成功");
            var names=typeof(DeclaredFieldEditor).Assembly.GetReferencedAssemblies().Select(assembly=>assembly.Name!).ToArray();
            Require(!names.Any(name=>name.Contains("Plugin.SDK")||name.Contains("Halcon",StringComparison.OrdinalIgnoreCase)||name=="SunEyeVision.UI"),"公共字段层倒置依赖");
            Console.WriteLine("通过：失效范围拒绝且保留原值；公共字段程序集不依赖SDK、HALCON或UI宿主");
            var imageType=TypeRef.ResourceOf(ResourceKind.Image);
            var sourceA=new DeclaredReferenceCandidate("A · 图像","A","workflow",new Port("output",imageType),Array.Empty<string>());
            var sourceB=new DeclaredReferenceCandidate("B · 图像","B","workflow",new Port("output",imageType),Array.Empty<string>());
            ParameterSetting imageAccepted=new ReferenceSetting(sourceA.Reference(BindingSelection.All));
            var imageWrites=0;var imageRefreshFails=false;var imageWriteRejected=false;
            using var imageEditor=new DeclaredFieldEditor(new("input",imageType,Required:false,EditModes:FieldEditModes.Reference),"输入源",imageAccepted,next=>
            {if(imageWriteRejected)throw new InvalidOperationException("写入被宿主拒绝");imageAccepted=next;imageWrites++;});
            imageEditor.UseImageSourceSelector(()=>
            {if(imageRefreshFails)throw new InvalidOperationException("候选来源读取失败");return new[]{sourceA,sourceB};});
            Show(imageEditor);var imageCombo=(ComboBox)imageEditor.InputElement!;
            void RefreshImageSources()
            {imageCombo.IsDropDownOpen=false;Pump();imageCombo.IsDropDownOpen=true;Pump();imageCombo.IsDropDownOpen=false;Pump();}
            ComboBoxItem ImageSourceItem(ComboBox combo,string nodeId)=>combo.Items.OfType<ComboBoxItem>().Single(item=>
                item.Tag is ReferenceSetting reference&&reference.Reference.NodeId==nodeId);
            var imagePriorItem=imageCombo.SelectedItem;imageRefreshFails=true;RefreshImageSources();
            Require(imageEditor.HasInvalidText&&ReferenceEquals(imageCombo.SelectedItem,imagePriorItem)&&imageWrites==0
                &&((ReferenceSetting)imageAccepted).Reference.NodeId=="A","候选读取失败破坏已接受来源或显示");
            imageCombo.SelectedItem=ImageSourceItem(imageCombo,"B");Pump();
            Require(imageEditor.HasInvalidText&&imageWrites==0&&ReferenceEquals(imageCombo.SelectedItem,imagePriorItem),"刷新失败后的旧候选仍能写入来源");
            imageRefreshFails=false;RefreshImageSources();
            Require(!imageEditor.HasInvalidText&&imageWrites==0&&((ReferenceSetting)imageAccepted).Reference.NodeId=="A","来源恢复后错误残留或擅自写入");
            Console.WriteLine("通过：来源刷新失败保留显示与身份，恢复只清除刷新错误");
            imageWriteRejected=true;imageCombo.SelectedItem=ImageSourceItem(imageCombo,"B");
            Require(imageEditor.HasInvalidText&&imageEditor.Error.Contains("写入被宿主拒绝")&&ReferenceEquals(imageCombo.SelectedItem,ImageSourceItem(imageCombo,"A"))&&imageWrites==0,"写入拒绝没有恢复原选中");
            imageRefreshFails=true;RefreshImageSources();imageRefreshFails=false;RefreshImageSources();
            Require(imageEditor.HasInvalidText&&imageEditor.Error.Contains("写入被宿主拒绝")&&imageWrites==0,"刷新恢复吞掉独立写入错误");
            imageWriteRejected=false;imageCombo.SelectedItem=ImageSourceItem(imageCombo,"B");
            Require(!imageEditor.HasInvalidText&&imageWrites==1&&((ReferenceSetting)imageAccepted).Reference.NodeId=="B","合法修正未恢复");
            Console.WriteLine("通过：刷新恢复不吞写入拒绝，合法修正后恢复");
            var staleImageCombo=imageCombo;staleImageCombo.IsDropDownOpen=true;Pump();
            imageEditor.UseImageSourceSelector(()=>new[]{sourceA,sourceB});owner.UpdateLayout();Pump();imageCombo=(ComboBox)imageEditor.InputElement!;
            Require(!ReferenceEquals(imageCombo,staleImageCombo)&&!staleImageCombo.IsDropDownOpen,"重绘没有替换旧下拉并关闭弹层");
            var beforeStale=imageWrites;staleImageCombo.SelectedItem=ImageSourceItem(staleImageCombo,"A");Pump();
            Require(imageWrites==beforeStale&&((ReferenceSetting)imageAccepted).Reference.NodeId=="B"&&!imageEditor.HasInvalidText,"旧下拉回调改变当前来源或错误");
            imageEditor.Dispose();imageCombo.SelectedItem=ImageSourceItem(imageCombo,"A");
            Require(imageWrites==beforeStale,"Dispose后的来源事件仍写入");
            Console.WriteLine("通过：重绘关闭旧弹层，旧选择及释放后回调不写当前字段");

            var takeoverWrites=0;
            ParameterSetting automatic=new ReferenceSetting(sourceA.Reference(BindingSelection.All),OriginId:"connection:edge");
            using var takeover=new DeclaredFieldEditor(new("takeover",imageType,EditModes:FieldEditModes.Reference),"同源确认",automatic,next=>{automatic=next;takeoverWrites++;});
            takeover.UseImageSourceSelector(()=>new[]{sourceA,sourceB});Show(takeover);
            var takeoverCombo=(ComboBox)takeover.InputElement!;takeoverCombo.IsDropDownOpen=true;Pump();
            Require(takeoverWrites==0&&((ReferenceSetting)automatic).OriginId=="connection:edge","打开图像下拉擅自接管自动来源");
            ImageSourceItem(takeoverCombo,"A").RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice,Environment.TickCount,System.Windows.Input.MouseButton.Left)
                {RoutedEvent=UIElement.PreviewMouseLeftButtonDownEvent});Pump();
            Require(takeoverWrites==1&&automatic is ReferenceSetting {OriginId:null,RebindingReason:null}&&!takeoverCombo.IsDropDownOpen,"显式点击同源没有接管或重复写入");
            takeoverCombo.IsDropDownOpen=true;Pump();
            ImageSourceItem(takeoverCombo,"A").RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice,Environment.TickCount,System.Windows.Input.MouseButton.Left)
                {RoutedEvent=UIElement.PreviewMouseLeftButtonDownEvent});Pump();
            Require(takeoverWrites==1,"已显式同源重复确认再次写入");
            Console.WriteLine("通过：图像下拉打开不接管，实际同项鼠标确认接管自动归属且去重");

            var product=Environment.GetEnvironmentVariable("S2_PRODUCT_ROOT")!;
            var manager=new PluginManager();manager.LoadPlugins();
            manager.AssertCompleteReleaseToolSet(ToolRegistry.GetAllToolMetadata().Select(tool=>tool.Id));
            var definition=(IToolSettingsFields)ToolRegistry.CreateRuntime("BlobAnalysis");var settings=new SunEyeVision.Tool.BlobAnalysis.Runtime.BlobSettings();
            var access=definition.SettingsSchema;Require(access.Inputs.Count==54&&access.Inputs.Select(p=>p.Id).Distinct().Count()==54,"XML设置字段不完整或重复");
            VerifySettingsCandidate(definition,settings);
            using var xmlStream=definition.GetType().Assembly.GetManifestResourceStream("BlobAnalysis.BlobParameters.xml")!;
            var xml=System.Xml.Linq.XDocument.Load(xmlStream);var rule=xml.Descendants("Judgment").Single(e=>(string?)e.Attribute("key")=="total-area");
            var key=(string)rule.Attribute("minimumFieldId")!;
            var initialFields=new FieldSettings(access.Inputs,definition.ReadSettings(settings).CopyValues().ToDictionary(pair=>pair.Key,pair=>(ParameterSetting)new ConstantSetting(pair.Value)));
            var updated=(SunEyeVision.Tool.BlobAnalysis.Runtime.BlobSettings)definition.MaterializeSettings(initialFields.Apply(new Dictionary<string,ParameterSetting>{{key,new ConstantSetting(12d)}}).Materialize(new Dictionary<string,object?>()));
            Require(updated.Judgments.Single(r=>r.Key=="total-area").Minimum==12&&updated.Judgments.Single(r=>r.Key=="max-area")==settings.Judgments.Single(r=>r.Key=="max-area"),"嵌套同名最小值写错判定");
            Console.WriteLine("通过：正式Runtime XML的54个唯一设置字段及嵌套判定精确读写，不创建旧Blob参数");
            var node=new WorkflowNodeBase("blob","Blob","Blob","BlobAnalysis"){RuntimeConfiguration=RuntimeToolConfiguration.Create("BlobAnalysis")};
            var flow=new SunEyeVision.Workflow.Workflow("flow","字段草稿");flow.Nodes.Add(node);var solution=new Solution();solution.Workflows.Add(flow);
            var edits=(DocumentEditSession)Activator.CreateInstance(typeof(DocumentEditSession),BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{solution},null)!;
            using var control=new SunEyeVision.UI.Services.Editing.RuntimeNodeConfigurationControl(node);
            using var context=new SunEyeVision.UI.Services.Editing.ProductionEditorContext(edits.Host,flow.Id,node,()=>true,control.BindDraft,documentEdits:edits);
            typeof(SunEyeVision.UI.Services.Editing.ProductionEditorContext).GetProperty("PrepareView",BindingFlags.Instance|BindingFlags.NonPublic)!
                .SetValue(context,(Func<string?>)(()=>control.TryCapture(out var error)?null:error));
            typeof(SunEyeVision.UI.Services.Editing.ProductionEditorContext).GetProperty("HasPendingViewEdits",BindingFlags.Instance|BindingFlags.NonPublic)!
                .SetValue(context,(Func<bool>)(()=>control.HasPendingEdits));
            control.BindDraft(context.DraftNode);SunEyeVision.Plugin.SDK.UI.Controls.ToolEditorCommit.AttachDraftContext(control,context);
            Show(control);var fields=Descendants(control).OfType<DeclaredFieldsView>().Single();
            var thresholdId=access.Presentation!.Fields.Single(f=>f.DisplayName==SunEyeVision.Tool.BlobAnalysis.Runtime.BlobParameterDefinition.Instance.Label("Threshold")).FieldId;
            ((TextBox)fields.Fields.Single(f=>f.FieldId==thresholdId).InputElement!).Text="201";
            int Threshold()=>((SunEyeVision.Tool.BlobAnalysis.Runtime.BlobSettings)node.RuntimeConfiguration!.CaptureSettings()).Threshold;
            Require(Threshold()==128,"公共控件提前写入正式配置");
            var valid=control.TryValidateEditor(out var validationError);
            if(!valid&&validationError=="当前阈值为纯界面试用，功能接入前不能执行或保存。")
            {
                Require(!context.TryCommitEditor(out _)&&Threshold()==128,"纯UI阈值绕过保护发布正式配置");
                context.Discard();Require(Threshold()==128,"纯UI取消改变正式配置");edits.Close();
                Console.WriteLine("通过：纯UI阈值明确拒绝正式确认，取消不改变现行配置；真实功能确认链待S2接通后验收。");
                Console.WriteLine("字段组件及现行UI阶段保护检查通过，不代表Blob功能确认、保存重开或人工验收完成。");
                return 0;
            }
            Require(valid,"Runtime草稿不能校验："+validationError);
            Require(context.TryCommitEditor(out var error),"Runtime草稿不能确认："+error);Require(Threshold()==201,"确认没有发布");
            Pump();((TextBox)Descendants(control).OfType<DeclaredFieldsView>().Single().Fields.Single(f=>f.FieldId==thresholdId).InputElement!).Text="215";
            context.Discard();Require(Threshold()==201,"取消改变已确认值");edits.Close();
            Console.WriteLine("通过：真实Runtime字段控件只写私有草稿，确认发布当前范围，取消保留已确认值");
            Console.WriteLine("S4字段编辑：31组通过；本轨道不代完整默认配置、ROI、保存重开或人工验收。");return 0;
        }
        catch(Exception error){Console.Error.WriteLine(error);return 1;}
        finally{owner?.Close();Application.Current?.Shutdown();}
    }
    private sealed record TreeNode(string Name,IReadOnlyList<TreeNode> EnumEntrysA)
    {public bool IsChecked {get;set;}}
    private static void VerifyReferenceComposition()
    {
        var sequence=TypeRef.SequenceOf(DataType.Integer);
        var source=new DeclaredReferenceCandidate("来源","producer",null,new Port("values",sequence),Array.Empty<string>());
        var alternate=new DeclaredReferenceCandidate("来源","other",null,new Port("values",sequence),Array.Empty<string>());
        ParameterSetting accepted=new ReferenceSetting(source.Reference(new BindingSelection.AtConstant(2)));
        var writes=0;
        using var field=new DeclaredFieldEditor(new("reference-enter",DataType.Integer,EditModes:FieldEditModes.Reference),"来源",accepted,
            value=>{accepted=value;writes++;},new ReferenceFieldOptions(_=>new(new[]{source,alternate},Array.Empty<DeclaredReferenceCandidate>()),EditableIndex:true));
        Show(field);
        var buttons=NativeDescendants(field).OfType<AppsPopup>().ToArray();
        Require(buttons.Length==2,"订阅未使用原两Popup组合");
        var sourceButton=buttons.Single(item=>item.Name=="rt");
        sourceButton.Focus();sourceButton.IsDropDownOpen=true;Pump();sourceButton.IsDropDownOpen=false;Pump();
        var text=NativeDescendants(field).OfType<NativeText>().Single();
        KeyEventArgs Enter()
        {
            var args=new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(owner)!,Environment.TickCount,Key.Return)
                {RoutedEvent=Keyboard.PreviewKeyDownEvent};
            text.RaiseEvent(args);Pump();return args;
        }
        text.Focus();Pump();text.Text="来源 [abc]";var invalidEnter=Enter();
        Require(invalidEnter.Handled&&text.IsKeyboardFocused&&field.HasInvalidText&&writes==0,"非法Enter未保留焦点或污染正式索引");
        foreach(var button in buttons)
        {
            text.Focus();Pump();Require(text.IsKeyboardFocused,"合法Enter缺少实际焦点前置");
            button.ApplyTemplate();button.IsDropDownOpen=true;Pump();
            Require(button.Template.FindName("PART_Popup",button) is Popup {IsOpen:true},"原Popup未实际打开");
            var index=writes+3;text.Text="来源 ["+index+"]";var before=writes;var enter=Enter();
            Require(enter.Handled&&!text.IsKeyboardFocused&&writes==before+1&&!field.HasInvalidText
                &&accepted is ReferenceSetting {Reference.Selection:BindingSelection.AtConstant selected}&&selected.Index==index,
                "合法Enter未单次提交、失焦或恢复错误");
            Require(buttons.All(popup=>!popup.IsDropDownOpen&&popup.Template.FindName("PART_Popup",popup) is not Popup {IsOpen:true}),
                "合法Enter没有关闭实际原Popup");
        }
        Console.WriteLine("通过：完整原订阅两Popup、非法Enter留焦、合法Enter单次提交失焦并关闭真实弹层");

        var session=((ContentControl)NativeDescendants(field).OfType<ContentControl>().First(item=>item.Content?.GetType().Name=="ReferenceFieldSession")).Content;
        var sessionType=session.GetType();
        object Property(string name)=>sessionType.GetProperty(name)!.GetValue(session)!;
        void Command(string name,object? argument)=>((ICommand)Property(name)).Execute(argument);
        var changedDuringAccept=false;
        void NewPending()
        {
            if(changedDuringAccept||accepted is not ReferenceSetting {Reference.Selection:BindingSelection.AtConstant {Index:20}})return;
            changedDuringAccept=true;text.Text="来源 [21]";
        }
        field.Changed+=NewPending;
        try
        {
            text.Focus();text.Text="来源 [20]";var before=writes;Enter();
            Require(changedDuringAccept&&writes==before+1&&text.Text=="来源 [21]",
                "接纳回调中新索引原文被旧提交清除");
            Require(field.TryCommitPending()&&writes==before+2
                &&accepted is ReferenceSetting {Reference.Selection:BindingSelection.AtConstant {Index:21}},
                "回调中的新索引未保留为待提交编辑");
        }
        finally{field.Changed-=NewPending;}
        text.Focus();sourceButton.IsDropDownOpen=true;Pump();var reopened=false;
        PropertyChangedEventHandler reopen=(_,args)=>
        {
            if(reopened||args.PropertyName!="IsDropDownOpen"||(bool)Property("IsDropDownOpen"))return;
            reopened=true;sourceButton.SetCurrentValue(ComboBox.IsDropDownOpenProperty,true);
        };
        ((INotifyPropertyChanged)session).PropertyChanged+=reopen;
        try
        {
            text.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(owner)!,Environment.TickCount,Key.Escape)
                {RoutedEvent=Keyboard.PreviewKeyDownEvent});Pump();
            Require(reopened&&sourceButton.IsDropDownOpen&&sourceButton.Template.FindName("PART_Popup",sourceButton) is Popup {IsOpen:true},
                "关闭通知重开后的新来源Popup被旧关闭操作覆盖");
        }
        finally{((INotifyPropertyChanged)session).PropertyChanged-=reopen;sourceButton.IsDropDownOpen=false;Pump();}
        sourceButton.Focus();sourceButton.IsDropDownOpen=true;Pump();
        var clear=((System.Collections.IEnumerable)Property("SourceNodes")).Cast<object>().Single(node=>(bool)node.GetType().GetProperty("Clear")!.GetValue(node)!);
        var beforeClear=writes;Command("ClickCommand",clear);Pump();
        Require(field.HasInvalidText&&text.Text==""&&writes==beforeClear,"必填清空未保持未完成状态");
        Require(buttons.All(button=>!button.IsDropDownOpen&&button.Template.FindName("PART_Popup",button) is not Popup {IsOpen:true}),
            "清除来源没有关闭实际Popup");
        text.Text="来源 [1]";Require(!field.TryCommitPending()&&writes==beforeClear,"清空后文本复活旧来源身份");
        SelectReferenceLeaf(field,candidate=>candidate.NodeId=="other");
        Require(accepted is ReferenceSetting {Reference.NodeId:"other"} cleared&&Equals(cleared.Reference.Selection,BindingSelection.All)
            &&writes==beforeClear+1&&!field.HasInvalidText,"清空后换集合复活旧索引或混淆同名身份");
        Console.WriteLine("通过：必填清空阻断旧身份文本，重新选择同名不同来源不继承旧索引");

        var indexButton=buttons.Single(button=>button.Name=="et");
        text.Focus();indexButton.IsDropDownOpen=true;Pump();
        Require(text.IsKeyboardFocused&&indexButton.Template.FindName("PART_Popup",indexButton) is Popup {IsOpen:true},
            "Delete缺少索引Popup打开及实际文本焦点前置");
        var beforeDelete=writes;
        text.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(owner)!,Environment.TickCount,Key.Delete)
            {RoutedEvent=Keyboard.KeyUpEvent});Pump();
        Require(text.Text==""&&field.HasInvalidText&&writes==beforeDelete
            &&buttons.All(button=>!button.IsDropDownOpen&&button.Template.FindName("PART_Popup",button) is not Popup {IsOpen:true}),
            "Delete没有清空并关闭实际索引Popup，或污染已接纳引用");
        Console.WriteLine("通过：实际索引弹层打开且文本持焦时Delete清空并关闭两层，未接受必填清除");

        var record=TypeRef.RecordOf("record",new[]{new Port("area",DataType.Number)});
        var projection=new DeclaredReferenceCandidate("面积","blob",null,new Port("blobs",TypeRef.SequenceOf(record)),new[]{"area"});
        Require(projection.SelectedType.Shape==ValueShape.Sequence&&projection.SelectedType.Element==DataType.Number,
            "记录成员投影丢失集合形状");
        Console.WriteLine("通过：集合记录成员投影保留正式集合类型");

        var refreshFails=false;var badProjection=false;var refreshWrites=0;
        using var refreshField=new DeclaredFieldEditor(new("refresh",DataType.Integer,EditModes:FieldEditModes.Reference),"刷新",
            new ReferenceSetting(source.Reference(new BindingSelection.AtConstant(2))),_=>refreshWrites++,new ReferenceFieldOptions(_=>
            {
                if(refreshFails)throw new InvalidOperationException("目录读取失败");
                return new ReferenceCandidates(badProjection?new[]{source,source with{MemberPath=new[]{"不存在成员"}}}:new[]{source},Array.Empty<DeclaredReferenceCandidate>());
            }));
        Show(refreshField);
        var refreshSession=NativeDescendants(refreshField).OfType<ContentControl>().Single(item=>item.Content?.GetType().Name=="ReferenceFieldSession").Content;
        var refreshType=refreshSession.GetType();
        var refresh=(ICommand)refreshType.GetProperty("DropDownCommand")!.GetValue(refreshSession)!;
        refresh.Execute(null);var treeSnapshot=refreshType.GetProperty("SourceNodes")!.GetValue(refreshSession);
        refreshFails=true;refresh.Execute(null);
        Require(refreshField.Error.Contains("目录读取失败")&&refreshWrites==0
            &&ReferenceEquals(treeSnapshot,refreshType.GetProperty("SourceNodes")!.GetValue(refreshSession)),"刷新失败半替换树或污染已接纳值");
        refreshFails=false;refresh.Execute(null);Require(!refreshField.HasInvalidText&&refreshWrites==0,"刷新恢复未清来源错误或擅自写入");
        treeSnapshot=refreshType.GetProperty("SourceNodes")!.GetValue(refreshSession);badProjection=true;refresh.Execute(null);
        Require(refreshField.HasInvalidText&&ReferenceEquals(treeSnapshot,refreshType.GetProperty("SourceNodes")!.GetValue(refreshSession)),"投影失败半替换来源树");
        badProjection=false;refresh.Execute(null);
        PropertyChangedEventHandler observer=(_,args)=>{if(args.PropertyName=="SourceNodes")throw new InvalidOperationException("刷新观察者失败");};
        ((INotifyPropertyChanged)refreshSession).PropertyChanged+=observer;
        try{refresh.Execute(null);throw new Exception("刷新观察者异常被吞掉");}
        catch(InvalidOperationException error)when(error.Message=="刷新观察者失败"){}
        finally{((INotifyPropertyChanged)refreshSession).PropertyChanged-=observer;}
        Require(!refreshField.HasInvalidText&&refreshWrites==0,"刷新观察者异常被误标为来源错误");
        Console.WriteLine("通过：原订阅树刷新及投影失败不半替换，恢复不写入，观察者异常不误判为读源失败");
    }
    private static void VerifySettingsCandidate(IToolSettingsFields definition,object settings)
    {
        var tool=(IToolDefinition)definition;
        FieldSettings Initial()=>new(definition.SettingsSchema.Inputs,definition.ReadSettings(settings).CopyValues().ToDictionary(pair=>pair.Key,pair=>(ParameterSetting)new ConstantSetting(pair.Value)));
        var draft=new SunEyeVision.Plugin.Runtime.ToolSettingsEditSession(tool,Initial());
        var schema=draft.Schema;
        using var declaration=definition.GetType().Assembly.GetManifestResourceStream("BlobAnalysis.BlobParameters.xml")!;
        var xml=System.Xml.Linq.XDocument.Load(declaration);
        string Id(string parameter)=>(string)xml.Descendants("Parameter").Single(item=>(string?)item.Attribute("id")==parameter).Attribute("fieldId")!;
        var low=Id("Threshold");var high=Id("UpperThreshold");var highPort=schema.Inputs.Single(port=>port.Id==high);
        var source=new DeclaredReferenceCandidate("来源","producer",null,highPort,Array.Empty<string>());
        var calls=0;var reject=true;var rebind=false;
        using var view=new SunEyeVision.Plugin.Wpf.Presenters.Parameters.DeclaredSettingsView();
        var options=new SunEyeVision.Plugin.Wpf.Presenters.Parameters.ToolSettingsViewOptions(AcceptCandidate:candidate=>
        {
            calls++;
            Require(candidate.Get<long>(low)==73
                &&candidate.Read(high) is ReferenceSetting {Reference.NodeId:"producer",Conversion:BindingConversion.Automatic}
                &&draft.CaptureFields().Get<long>(low)==128,
                "宿主没有取得完整常量和引用候选，或预检前污染草稿");
            if(reject)throw new InvalidOperationException("整组候选拒绝");
            if(rebind)view.Bind(new SunEyeVision.Plugin.Runtime.ToolSettingsEditSession(tool,Initial()));
        });
        view.Bind(draft,options);
        var method=typeof(SunEyeVision.Plugin.Wpf.Presenters.Parameters.DeclaredSettingsView).GetMethod("WriteGroup",BindingFlags.Instance|BindingFlags.NonPublic)!;
        var group=new Dictionary<string,ParameterSetting>{{low,new ConstantSetting(73L)},{high,new ReferenceSetting(source.Reference(BindingSelection.All))}};
        void Apply()=>method.Invoke(view,new object[]{group});
        try{Apply();throw new Exception("整组宿主拒绝被忽略");}
        catch(TargetInvocationException error)when(error.InnerException is InvalidOperationException {Message:"整组候选拒绝"}){}
        Require(calls==1&&view.TryCapture(out var captured,out _)
            &&view.AcceptedFieldOrder.Count==0
            &&captured.Get<long>(low)==128&&captured.Capture().Values.All(value=>value is ConstantSetting),
            "混合常量和引用拒绝后缓存已污染");
        reject=false;rebind=true;
        try{Apply();throw new Exception("候选回调重绑仍接纳旧提案");}
        catch(TargetInvocationException error)when(error.InnerException is InvalidOperationException){}
        Require(draft.CaptureFields().Get<long>(low)==128
            &&view.AcceptedFieldOrder.Count==0
            &&view.TryCapture(out captured,out _)&&captured.Get<long>(low)==128&&captured.Capture().Values.All(value=>value is ConstantSetting),
            "候选回调重绑污染旧草稿或新视图");
        view.Bind(draft,options with{AcceptCandidate=null});
        Apply();
        Require(view.AcceptedFieldOrder.SequenceEqual(new[]{low,high})&&view.AcceptedRevision==1,
            "完整快照提交丢失本次已接纳的字段提案顺序");
        Console.WriteLine("通过：混合设置与引用候选一次预检，宿主拒绝及同步重绑不污染草稿或引用缓存");
    }
    private static void VerifyAtomicFieldGroup()
    {
        using var left=new DeclaredFieldEditor(new("low",DataType.Number),"下限",new ConstantSetting(10d),_=>throw new Exception("不能逐字段写入"));
        using var right=new DeclaredFieldEditor(new("high",DataType.Number),"上限",new ConstantSetting(20d),_=>throw new Exception("不能逐字段写入"));
        var stateField=typeof(DeclaredFieldEditor).GetField("_state",BindingFlags.Instance|BindingFlags.NonPublic)!;
        var a=stateField.GetValue(left)!;var b=stateField.GetValue(right)!;var stateType=a.GetType();
        var tupleType=typeof(ValueTuple<,>).MakeGenericType(stateType,typeof(ParameterSetting));
        var method=stateType.GetMethod("TrySetGroup",BindingFlags.Static|BindingFlags.NonPublic)!;
        bool Apply(double low,double high,Action<IReadOnlyDictionary<string,ParameterSetting>> writer)
        {
            var values=Array.CreateInstance(tupleType,2);
            values.SetValue(Activator.CreateInstance(tupleType,a,new ConstantSetting(low)),0);
            values.SetValue(Activator.CreateInstance(tupleType,b,new ConstantSetting(high)),1);
            try{return (bool)method.Invoke(null,new object?[]{values,writer,null,null,false,null})!;}
            catch(TargetInvocationException error)when(error.InnerException is not null)
            {System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error.InnerException).Throw();throw;}
        }
        double Low()=> (double)((ConstantSetting)left.Setting).Value!;
        double High()=> (double)((ConstantSetting)right.Setting).Value!;
        var writes=0;
        Require(!Apply(30,double.PositiveInfinity,_=>writes++)&&writes==0&&Low()==10&&High()==20,"第二端非法仍写入第一端");
        left.Changed+=()=>Require(Low()==30&&High()==40,"通知看到半组新状态");
        right.Changed+=()=>Require(Low()==30&&High()==40,"右端通知看到半组新状态");
        Require(Apply(30,40,proposal=>
        {
            Require(Low()==10&&High()==20&&proposal.Count==2,"宿主写入前已改变状态或提案不完整");
            Require(!left.TrySetConstant(99d),"组内重入单字段写入没有拒绝");writes++;
        })&&writes==1&&Low()==30&&High()==40,"合法整组没有单次接纳");
        Require(!Apply(50,60,_=>throw new InvalidOperationException("拒绝整组"))&&Low()==30&&High()==40,"宿主拒绝污染一端");
        Console.WriteLine("通过：范围两字段先完整准备再一次宿主接纳，通知只见完整状态，非法或重入不半写");

        using var observedLeft=new DeclaredFieldEditor(new("a",DataType.Number),"A",new ConstantSetting(1d),_=>{});
        using var observedRight=new DeclaredFieldEditor(new("b",DataType.Number),"B",new ConstantSetting(2d),_=>{});
        var values=Array.CreateInstance(tupleType,2);
        values.SetValue(Activator.CreateInstance(tupleType,stateField.GetValue(observedLeft),new ConstantSetting(3d)),0);
        values.SetValue(Activator.CreateInstance(tupleType,stateField.GetValue(observedRight),new ConstantSetting(4d)),1);
        var rightNotified=false;var laterStateListener=false;
        observedRight.Changed+=()=>rightNotified=true;
        observedLeft.Changed+=()=>throw new InvalidOperationException("组观察者异常");
        stateType.GetEvent("Changed")!.AddEventHandler(stateField.GetValue(observedLeft),(Action)(()=>laterStateListener=true));
        try
        {
            method.Invoke(null,new object?[]{values,(Action<IReadOnlyDictionary<string,ParameterSetting>>)(_=>{}),null,null,false,null});
            throw new Exception("组观察者异常被吞掉");
        }
        catch(TargetInvocationException error)when(error.InnerException is InvalidOperationException {Message:"组观察者异常"}){}
        Require(Equals(((ConstantSetting)observedLeft.Setting).Value,3d)&&Equals(((ConstantSetting)observedRight.Setting).Value,4d)
            &&!observedLeft.HasInvalidText&&!observedRight.HasInvalidText&&rightNotified&&laterStateListener,
            "观察者异常回滚整组、截断后续监听或误记宿主拒绝");
        Console.WriteLine("通过：整组观察者异常保留已接纳两端，不误记宿主拒绝");
    }
    private static void VerifyFieldState()
    {
        var writes=0;var changes=0;
        using var scalar=new DeclaredFieldEditor(new("state-value",DataType.Integer,Storage:NumericStorage.Int32),"数值",new ConstantSetting(3L),_=>writes++);
        scalar.Changed+=()=>changes++;
        Require(scalar.TrySetConstant(3L)&&writes==0&&changes==0,"同值提案仍重复写入或通知");
        Require(!scalar.TrySetConstant((long)int.MaxValue+1)&&scalar.HasInvalidText&&writes==0,"越界提案未拒绝");
        Require(scalar.TrySetConstant(5L)&&!scalar.HasInvalidText&&writes==1,"有错误的状态不能用合法值修复");
        Console.WriteLine("通过：唯一字段状态同值无重复写入通知，非法后可显式修复");

        var initial=new[]{1d,2d};var arrayWrites=0;
        using var array=new DeclaredFieldEditor(new("state-array",DataType.NumberArray),"数组",new ConstantSetting(initial),setting=>
        {arrayWrites++;((double[])((ConstantSetting)setting).Value!)[0]=999;});
        initial[0]=77;
        Require(((double[])((ConstantSetting)array.Setting).Value!)[0]==1,"初始数组引用泄漏进状态");
        var exposed=(double[])((ConstantSetting)array.Setting).Value!;exposed[0]=88;
        Require(((double[])((ConstantSetting)array.Setting).Value!)[0]==1,"读取状态暴露可变数组");
        Require(array.TrySetConstant(new[]{3d,4d})&&((double[])((ConstantSetting)array.Setting).Value!)[0]==3,"宿主回调原地修改污染已接纳状态");
        Require(array.TrySetConstant(new[]{3d,4d})&&arrayWrites==1,"内容相同数组重复写入");
        Console.WriteLine("通过：初值、读取及宿主提案数组相互隔离，按规范内容去重");

        var referenceWrites=0;
        OutputReference Source()=>new("producer","values",TypeRef.SequenceOf(DataType.Integer),selection:new BindingSelection.AtConstant(2));
        using var reference=new DeclaredFieldEditor(new("state-reference",DataType.Integer,EditModes:FieldEditModes.Constant|FieldEditModes.Reference),"来源",
            new ReferenceSetting(Source()),_=>referenceWrites++,new ReferenceFieldOptions(_=>new(new[]{new DeclaredReferenceCandidate("来源","producer",null,new Port("values",TypeRef.SequenceOf(DataType.Integer)),Array.Empty<string>())},Array.Empty<DeclaredReferenceCandidate>())));
        Show(reference);SelectReferenceLeaf(reference,c=>c.NodeId=="producer");SelectReferenceLeaf(reference,c=>c.NodeId=="producer");
        Require(referenceWrites==0&&reference.Setting is ReferenceSetting,"相同新建索引引用没有语义去重");
        using var noBackup=new DeclaredFieldEditor(new("no-backup",DataType.Integer,EditModes:FieldEditModes.Constant|FieldEditModes.Reference),"无备份",
            new ReferenceSetting(Source()),_=>throw new Exception("不得猜默认写入"),ReferenceFieldOptions.Empty);
        Show(noBackup);Click(noBackup,"切换为数值编辑");
        Require(noBackup.Setting is ReferenceSetting&&noBackup.HasInvalidText&&noBackup.Error.Contains("常量"),"缺少备份时制造了默认常量");
        Console.WriteLine("通过：索引引用按完整身份去重，无常量备份时明确拒绝恢复");

        var repairedWrites=0;
        using var repair=new DeclaredFieldEditor(new("repair",DataType.Integer,EditModes:FieldEditModes.Constant|FieldEditModes.Reference),"修复来源",
            new ReferenceSetting(Source()),_=>repairedWrites++,ReferenceFieldOptions.Empty);
        var repairState=typeof(DeclaredFieldEditor).GetField("_state",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(repair)!;
        void SourceError(string message)=>repairState.GetType().GetMethod("SetSourceError")!.Invoke(repairState,new object[]{message});
        Show(repair);SourceError("旧来源不可读");Click(repair,"切换为数值编辑");
        var repairText=(TextBox)repair.InputElement!;
        Require(repairText.Text==""&&repair.Setting is ReferenceSetting&&repairedWrites==0,"无备份切常量产生伪默认值");
        Require(!repair.TryCommitPending()&&repairedWrites==0&&repair.Setting is ReferenceSetting,"空常量替换了原引用");
        repairText.Text="42";
        Require(repair.TryCommitPending()&&!repair.HasInvalidText&&repair.Setting is ConstantSetting {Value:42L}&&repairedWrites==1,"合法数值接纳后旧来源错误残留");
        var repairChanges=0;repair.Changed+=()=>repairChanges++;
        SourceError("旧来源诊断");var beforeRepair=repairChanges;
        Require(repair.TrySetConstant(42L)&&!repair.HasInvalidText&&repairedWrites==1&&repairChanges>beforeRepair,"同值常量没有清诊断通知或重复写入");
        SourceError("浏览失败但常量有效");
        Require(repair.TryCommitPending()&&!repair.HasInvalidText&&repairedWrites==1,"无pending合法常量无法经接纳清旧来源诊断");
        Console.WriteLine("通过：无备份引用不补默认值，空值不写入，合法常量单次接纳并原子清理旧来源诊断");

        var observed=0;
        using var observer=new DeclaredFieldEditor(new("observer",DataType.Integer),"观察者",new ConstantSetting(1L),_=>observed++);
        observer.Changed+=()=>throw new InvalidOperationException("观察者失败");
        try{observer.TrySetConstant(2L);throw new Exception("观察者异常被吞掉");}
        catch(InvalidOperationException error)when(error.Message=="观察者失败"){}
        Require(observed==1&&Equals(((ConstantSetting)observer.Setting).Value,2L)&&!observer.HasInvalidText,"观察者异常被误判为宿主拒绝");
        DeclaredFieldEditor? rebound=null;var oldChanges=0;
        rebound=new DeclaredFieldEditor(new("rebind-state",DataType.Integer),"重绑",new ConstantSetting(1L),_=>rebound!.Dispose());
        rebound.Changed+=()=>oldChanges++;
        Require(!rebound.TrySetConstant(2L)&&oldChanges==0,"宿主同步释放后旧会话继续通知");
        Console.WriteLine("通过：观察者异常不回滚已接纳值，宿主同步释放后旧状态不通知");

        using var missing=new DeclaredFieldEditor(new("required",DataType.Integer),"必填",new UnsetSetting(),_=>{});
        Require(missing.HasInvalidText&&missing.Error.Contains("必填"),"必填未设置状态被当作合法");
        Require(!Descendants(missing).OfType<TextBlock>().Any(text=>text.Text==missing.Error),"字段下方仍显示行内错误文字");
        Console.WriteLine("通过：必填未设置阻断确认且不显示行内提示，不猜默认值");
    }
    private static void VerifyNativeInputs()
    {
        var boolWrites=0;var booleanValue=false;var boolReject=true;
        using var boolean=new DeclaredFieldEditor(new("enabled",DataType.Boolean),"使能",new ConstantSetting(false),setting=>
        {if(boolReject)throw new InvalidOperationException("布尔写入拒绝");booleanValue=(bool)((ConstantSetting)setting).Value!;boolWrites++;});
        Show(boolean);var toggle=(NativeBoolean)boolean.InputElement!;
        Require(boolWrites==0&&toggle.OffContent is Image {Source:BitmapSource off}&&off.PixelWidth==48&&off.PixelHeight==32&&toggle.OnContent is Image {Source:BitmapSource on}&&on.PixelWidth==48&&on.PixelHeight==32,"原开关图片尺寸不符或初始化写入");
        toggle.IsChecked=true;Require(toggle.IsChecked==false&&!booleanValue&&boolWrites==0&&boolean.HasInvalidText,"开关拒绝未恢复");
        boolReject=false;toggle.IsChecked=true;Require(booleanValue&&boolWrites==1&&!boolean.HasInvalidText,"原开关修正未写入");
        toggle.IsChecked=true;Require(boolWrites==1,"重复同步写入布尔草稿");
        toggle.IsEnabled=false;toggle.IsChecked=false;Require(boolWrites==1,"禁用开关仍写入");
        Require(typeof(DeclaredFieldEditor).Assembly.GetType("SunEyeVision.Plugin.Wpf.Native.Common.SourceBooleanToggle")==null,"同源重复开关仍为产品后备");
        Console.WriteLine("通过：原Apps.XmlUI开关图片、初始化无写入、拒绝回滚、去重和禁用保护");

        var choiceWrites=0;var choiceReject=true;
        using var choice=new DeclaredFieldEditor(new("enum",DataType.Text,Choices:new TextChoiceConstraint(new[]{"a","b"})),"枚举",new ConstantSetting("a"),_=>
        {if(choiceReject)throw new InvalidOperationException("枚举写入拒绝");choiceWrites++;},choiceLabels:new Dictionary<string,string>{{"a","同名"},{"b","同名"}});
        Show(choice);var combo=(ComboBox)choice.InputElement!;
        Require(((SolidColorBrush)combo.FindResource("Col_Theme")).Color==Color.FromRgb(231,105,0),"旧宿主主题覆盖原组合框主题");
        combo.SelectedValue="b";Require(Equals(combo.SelectedValue,"a")&&choice.HasInvalidText&&choiceWrites==0,"拒绝枚举没有恢复正式键");
        choiceReject=false;combo.SelectedValue="b";Require(Equals(((ConstantSetting)choice.Setting).Value,"b")&&choiceWrites==1&&!choice.HasInvalidText,"同名显示标签影响正式枚举键");
        combo.SelectedValue="b";Require(choiceWrites==1,"枚举同值重复提交");
        var oldCombo=combo;Require(choice.TrySetConstant("a"),"枚举重绘提案未接纳");
        owner.UpdateLayout();Pump();
        combo=(ComboBox)choice.InputElement!;var beforeOld=choiceWrites;oldCombo.SelectedValue="a";
        Require(choiceWrites==beforeOld&&Equals(combo.SelectedValue,"a"),"旧枚举回调污染新状态");
        combo.SelectedValue="b";Require(choiceWrites==beforeOld+1,"新枚举实例不能继续选择");
        using var missingChoice=new DeclaredFieldEditor(new("enum-missing",DataType.Text,Choices:new TextChoiceConstraint(new[]{"a","b"})),"未知枚举",new ConstantSetting("removed"),_=>choiceWrites++);
        Show(missingChoice);
        Require(missingChoice.HasInvalidText&&((ComboBox)missingChoice.InputElement!).SelectedIndex==-1,"未知初值被静默替换");
        ((ComboBox)missingChoice.InputElement!).SelectedValue="b";
        Require(!missingChoice.HasInvalidText&&Equals(((ConstantSetting)missingChoice.Setting).Value,"b"),"未知枚举不能显式修复");
        Show(choice);
        owner.UpdateLayout();Pump();
        Require(NativeDescendants(combo).OfType<TextBlock>().Any(text=>text.Text=="同名"),"原枚举实际选中标签未渲染");
        Require(NativeDescendants(combo).OfType<TextBlock>().Where(text=>text.Text=="同名").All(text=>
            text.Foreground is SolidColorBrush foreground&&combo.Foreground is SolidColorBrush original&&foreground.Color==original.Color),"深色宿主隐式样式覆盖原枚举文字");
        combo.IsDropDownOpen=true;Pump();
        var listPopup=(Popup)combo.Template.FindName("Popup",combo);
        Require(NativeDescendants(listPopup.Child).OfType<TextBlock>().Any(text=>text.Text=="同名"),"原下拉选项标签未渲染");
        Require(NativeDescendants(listPopup.Child).OfType<TextBlock>().Where(text=>text.Text=="同名").All(text=>
            text.Foreground is SolidColorBrush foreground&&combo.Foreground is SolidColorBrush original&&foreground.Color==original.Color),"深色宿主隐式样式覆盖原枚举下拉文字");
        CaptureNativeInput((FrameworkElement)listPopup.Child,"native-enumeration-popup.png");combo.IsDropDownOpen=false;Pump();
        CaptureNativeInput(combo,"native-enumeration.png");
        Console.WriteLine("通过：原枚举主题、正式键与同名标签分离、拒绝恢复和同值去重");

        var nativeText=new NativeText{Text="abc",Width=180};var next=new TextBox{Width=180};
        var textPanel=new StackPanel();textPanel.Children.Add(nativeText);textPanel.Children.Add(next);Show(textPanel);
        Require(nativeText.IsReadOnly&&((TextBox)nativeText).IsReadOnly&&BindingOperations.IsDataBound(nativeText,TextBox.IsReadOnlyProperty),"原DeleteTextBox默认只读或真实基类绑定缺失");
        nativeText.IsReadOnly=false;Pump();Require(!((TextBox)nativeText).IsReadOnly,"原只读投影没有同步基类");
        nativeText.IsReadOnly=true;Pump();Require(((TextBox)nativeText).IsReadOnly,"原只读恢复未同步基类");
        nativeText.Focus();Pump();Require(nativeText.IsKeyboardFocused,"原文本Enter检查缺焦点前置");
        var enter=new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(owner)!,Environment.TickCount,Key.Return){RoutedEvent=Keyboard.PreviewKeyDownEvent};
        nativeText.RaiseEvent(enter);Pump();Require(enter.Handled&&next.IsKeyboardFocused,"原Apps.PcUI文本父类Enter未移焦");
        nativeText.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(owner)!,Environment.TickCount,Key.Delete){RoutedEvent=Keyboard.KeyUpEvent});
        Require(string.IsNullOrEmpty(nativeText.Text),"原Delete未清空整文本");
        Console.WriteLine("通过：原文本父类Enter移焦及Delete整文本清除");

        var leaf=new TreeNode("来源",Array.Empty<TreeNode>());var group=new TreeNode("节点",new[]{leaf});
        var tree=new NativeTree{ItemSource=new[]{group},SelectedItem=leaf,Height=200,Width=250};var executions=0;object? chosen=null;
        tree.OnTreeNodeDoubleClick+=(_,args)=>{executions++;chosen=args.Param;};Show(tree);Pump();
        var actual=(TreeView)tree.FindName("treeView");
        var root=(TreeViewItem)actual.ItemContainerGenerator.ContainerFromIndex(0);root.IsExpanded=true;root.UpdateLayout();Pump();
        var item=(TreeViewItem)root.ItemContainerGenerator.ContainerFromIndex(0);
        Require(item.IsSelected&&ReferenceEquals(tree.SelectedItem,leaf),"原树没有按对象恢复叶选中");
        // A generated/selected item is not proof that the native header is actually painted.
        var visualRows=new List<string>();
        void InspectVisual(DependencyObject node)
        {
            if(node is FrameworkElement element)
                visualRows.Add($"{element.GetType().Name}\t{element.Name}\t{element.ActualWidth}\t{element.ActualHeight}\t{element.IsVisible}\t{element.Opacity}\t{(element is TextBlock text?text.Text:element is ContentPresenter presenter?presenter.Content?.GetType().Name:"")}\toffset={VisualTreeHelper.GetOffset(element)}\tclip={System.Windows.Controls.Primitives.LayoutInformation.GetLayoutClip(element)}\ttransform={element.RenderTransform}");
            if(node is Visual)for(var index=0;index<VisualTreeHelper.GetChildrenCount(node);index++)InspectVisual(VisualTreeHelper.GetChild(node,index));
        }
        InspectVisual(tree);
        File.WriteAllLines(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"native-tree-visual.tsv"),visualRows);
        CaptureNativeInput(tree,"native-reference-tree-before-click.png");
        var frame=new RenderTargetBitmap((int)Math.Ceiling(owner.ActualWidth),(int)Math.Ceiling(owner.ActualHeight),96,96,PixelFormats.Pbgra32);
        frame.Render(owner);
        void SaveTreeFrame(BitmapSource source,string name)
        {
            var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(source));
            using var output=File.Create(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,name));encoder.Save(output);
        }
        SaveTreeFrame(frame,"native-reference-tree-window.png");
        var treeBounds=tree.TransformToAncestor(owner).TransformBounds(new Rect(tree.RenderSize));
        File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"native-tree-window-bounds.txt"),treeBounds.ToString());
        var treeFrame=new CroppedBitmap(frame,new Int32Rect((int)treeBounds.X,(int)treeBounds.Y,(int)treeBounds.Width,(int)treeBounds.Height));
        SaveTreeFrame(treeFrame,"native-reference-tree-window-crop.png");
        var treePixels=new byte[treeFrame.PixelWidth*treeFrame.PixelHeight*4];
        treeFrame.CopyPixels(treePixels,treeFrame.PixelWidth*4,0);
        Require(Enumerable.Range(0,treePixels.Length/4).Any(index=>treePixels[index*4+3]>128
            &&treePixels[index*4]<128&&treePixels[index*4+1]<128&&treePixels[index*4+2]<128),
            "实际窗口树区域未绘制文字或展开图标");
        void DoubleClick(TreeViewItem target)=>target.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,Environment.TickCount,MouseButton.Left){RoutedEvent=Control.MouseDoubleClickEvent});
        DoubleClick(root);Require(executions==0,"分组双击错误提交");DoubleClick(item);
        Require(executions==1&&ReferenceEquals(chosen,leaf),"叶双击没有原样回传投影对象");
        CaptureNativeInput(tree,"native-reference-tree.png");
        tree.SelectedItem=null;Require(!item.IsSelected,"空选中没有清原视觉高亮");
        Console.WriteLine("通过：原树实际窗口绘制、对象选中恢复、分组不提交与叶双击单次回传；不代完整订阅或人工验收");

        var popup=new AppsPopup();var content=new FrameworkElementFactory(typeof(TextBlock));content.SetValue(TextBlock.TextProperty,"原Popup内容");popup.SubContent=new DataTemplate{VisualTree=content};
        Show(popup);popup.ApplyTemplate();popup.IsDropDownOpen=true;Pump();
        Require(popup.IsPopupOpen&&popup.Template.FindName("PART_Popup",popup) is Popup {IsOpen:true},"Apps原弹层实际开层或状态同步失败");
        popup.IsDropDownOpen=false;Pump();Require(!popup.IsPopupOpen,"Apps弹层关闭未同步原状态");
        Require(NativeDescendants(popup).OfType<Image>().Any(image=>image.Source is BitmapSource bitmap&&bitmap.PixelWidth>0),"AppsPopup原编辑图标未渲染");
        Console.WriteLine("通过：原Apps.PcUI弹层资源、编辑图标与实际开关状态同步");

        using var pages=new DeclaredFieldsView(Enumerable.Range(0,3).Select(index=>
        {
            var id="header-test-"+index;var page=new[]{"基本参数","运行参数","结果显示"}[index];
            return (new Port(id,DataType.Boolean),
                new FieldPresentation(id,"使能",page,index,FieldEditorKind.Scalar){Layout=new FieldLayout(page,"参数")},
                (ParameterSetting)new ConstantSetting(false));
        }),_=>{});
        var tabs=(TabControl)pages.Content;
        var hostileText=new Style(typeof(TextBlock));
        hostileText.Setters.Add(new Setter(TextBlock.ForegroundProperty,Brushes.White));
        hostileText.Setters.Add(new Setter(TextBlock.FontWeightProperty,FontWeights.Light));
        hostileText.Setters.Add(new Setter(TextBlock.FontSizeProperty,29d));
        tabs.Resources[typeof(TextBlock)]=hostileText;
        Show(pages);Require(tabs.IsLoaded,"页签标题检查没有进入实际Loaded窗口");
        void CheckHeaders()
        {
            foreach(var tab in tabs.Items.Cast<TabItem>())
            {
                var header=(ContentControl)tab.Template.FindName("Content",tab);
                var text=NativeDescendants(header).OfType<TextBlock>().Single(item=>Equals(item.Text,tab.Header?.ToString()));
                var key=tab.IsSelected?"Col_ModuleConfig_Title_Selected_Foreground":"Col_ModuleConfig_Title_Foreground";
                var expected=(SolidColorBrush)tabs.FindResource(key);
                Require(text.Foreground is SolidColorBrush actual&&actual.Color==expected.Color,
                    "宿主隐式白字覆盖原页签标题："+tab.Header);
                Require(text.FontWeight==header.FontWeight&&text.FontSize==header.FontSize&&Equals(header.Content,tab.Header),
                    "原页签字体继承或Header绑定被改变："+tab.Header);
            }
        }
        for(var index=0;index<3;index++){tabs.SelectedIndex=index;Pump();CheckHeaders();}
        CaptureNativeInput(pages,"native-tab-headers.png");
        Show(new TextBlock{Text="临时卸载"});Show(pages);tabs.SelectedIndex=1;Pump();CheckHeaders();
        Console.WriteLine("通过：真实窗口原三页标题抵御宿主隐式样式，选中及临时卸载重载保持继承");
    }
    private static void CaptureNativeInput(FrameworkElement element,string name)
    {
        // VisualBrush bounds include the referenced root's offset. For inline controls,
        // capture the same actual window first and crop using its accumulated transform.
        if(element.IsDescendantOf(owner))
        {
            owner.UpdateLayout();
            var frame=new RenderTargetBitmap((int)Math.Ceiling(owner.ActualWidth),(int)Math.Ceiling(owner.ActualHeight),96,96,PixelFormats.Pbgra32);
            frame.Render(owner);
            var area=element.TransformToAncestor(owner).TransformBounds(new Rect(element.RenderSize));
            var left=Math.Max(0,(int)Math.Floor(area.Left));var top=Math.Max(0,(int)Math.Floor(area.Top));
            var right=Math.Min(frame.PixelWidth,(int)Math.Ceiling(area.Right));var bottom=Math.Min(frame.PixelHeight,(int)Math.Ceiling(area.Bottom));
            Require(right>left&&bottom>top,"实际窗口取图区域为空");
            var inlineEncoder=new PngBitmapEncoder();inlineEncoder.Frames.Add(BitmapFrame.Create(new CroppedBitmap(frame,new Int32Rect(left,top,right-left,bottom-top))));
            using var inlineStream=File.Create(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,name));inlineEncoder.Save(inlineStream);
            return;
        }
        element.UpdateLayout();var bitmap=new RenderTargetBitmap((int)Math.Ceiling(element.ActualWidth),(int)Math.Ceiling(element.ActualHeight),96,96,PixelFormats.Pbgra32);
        // Popup has a separate visual root; retain its own content capture.
        var bounds=new Rect(0,0,element.ActualWidth,element.ActualHeight);var drawing=new DrawingVisual();
        using(var context=drawing.RenderOpen())context.DrawRectangle(new VisualBrush(element){ViewboxUnits=BrushMappingMode.Absolute,Viewbox=bounds,ViewportUnits=BrushMappingMode.Absolute,Viewport=bounds},null,bounds);
        bitmap.Render(drawing);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream=File.Create(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,name));encoder.Save(stream);
    }
    private static void SelectReferenceLeaf(DeclaredFieldEditor field,Func<DeclaredReferenceCandidate,bool> predicate)
    {
        var popup=NativeDescendants(field).OfType<AppsPopup>().Single(item=>item.Name=="rt");
        popup.ApplyTemplate();popup.Focus();popup.IsDropDownOpen=true;Pump();
        var root=(Popup)popup.Template.FindName("PART_Popup",popup);
        var tree=NativeDescendants(root.Child).OfType<NativeTree>().Single();
        var actual=(TreeView)tree.FindName("treeView");
        TreeViewItem? Find(ItemsControl parent)
        {
            for(var index=0;index<parent.Items.Count;index++)
            {
                if(parent.ItemContainerGenerator.ContainerFromIndex(index) is not TreeViewItem item)continue;
                if(item.DataContext?.GetType().GetProperty("Candidate")?.GetValue(item.DataContext) is DeclaredReferenceCandidate candidate&&predicate(candidate))return item;
                item.IsExpanded=true;item.UpdateLayout();Pump();var nested=Find(item);if(nested is not null)return nested;
            }
            return null;
        }
        var selected=Find(actual)??throw new Exception("原来源树缺少目标候选");
        selected.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,Environment.TickCount,MouseButton.Left){RoutedEvent=Control.MouseDoubleClickEvent});Pump();
        Require(NativeDescendants(field).OfType<AppsPopup>().All(button=>!button.IsDropDownOpen
            &&button.Template.FindName("PART_Popup",button) is not Popup {IsOpen:true}),"来源接纳后原Popup实际未关闭");
    }
    private static SunEyeVision.Plugin.Wpf.Native.Numeric.HikPcUI.ListView.NumericControl OriginalNumberInput(DependencyObject field)
    {
        var original=NativeDescendants(field).OfType<SunEyeVision.Plugin.Wpf.Native.Numeric.HikPcUI.ListView.SliderArrangeBase>().Single();
        var box=(ComboBox)original.FindName("CBox");box.ApplyTemplate();
        return (SunEyeVision.Plugin.Wpf.Native.Numeric.HikPcUI.ListView.NumericControl)box.Template.FindName("T1",box);
    }
    private static void Click(DependencyObject control,string tooltip)
    {
        if(tooltip is "切换为订阅" or "切换为数值编辑")
        {
            var original=NativeDescendants(control).OfType<ToggleButton>().SingleOrDefault(button=>BindingOperations.GetBinding(button,ToggleButton.IsCheckedProperty)?.Path?.Path=="IsSubscribeLeft");
            if(original is not null){original.IsChecked=tooltip=="切换为订阅";original.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));Pump();return;}
        }
        Descendants(control).OfType<ButtonBase>().Single(button=>Equals(button.ToolTip,tooltip)).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));Pump();
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        foreach(var item in LogicalTreeHelper.GetChildren(parent))if(item is DependencyObject child)
        {yield return child;foreach(var descendant in Descendants(child))yield return descendant;}
    }
    private static IEnumerable<DependencyObject> NativeDescendants(DependencyObject root)
    {
        var seen=new HashSet<DependencyObject>();var pending=new Stack<DependencyObject>();pending.Push(root);
        while(pending.Count>0)
        {
            var node=pending.Pop();if(!seen.Add(node))continue;yield return node;
            foreach(var item in LogicalTreeHelper.GetChildren(node))if(item is DependencyObject child)pending.Push(child);
            if(node is Visual)for(var i=0;i<VisualTreeHelper.GetChildrenCount(node);i++)pending.Push(VisualTreeHelper.GetChild(node,i));
        }
    }
    private static void Require(bool condition,string error){if(!condition)throw new Exception(error);}
}
