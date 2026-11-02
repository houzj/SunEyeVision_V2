using SunEyeVision.Plugin.Editing.Wpf;
using SunEyeVision.Plugin.Wpf.Controls.References.Variables;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using SunEyeVision.Tool.BlobAnalysis.Runtime;
using SunEyeVision.Plugin.SDK.UI.Windows;
using SunEyeVision.Plugin.SDK.UI.Controls;
using SunEyeVision.UI.Shell.Windows;
using SunEyeVision.UI.Services.Editing;
using SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters;
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
        var result=1;
        var thread=new Thread(()=>{IntPtr desktop=IntPtr.Zero;try{desktop=CreateDesktop("SunEyeVision-Fields-"+Guid.NewGuid().ToString("N"),IntPtr.Zero,IntPtr.Zero,0,0x000F01FF,IntPtr.Zero);if(desktop==IntPtr.Zero||!SetThreadDesktop(desktop))throw new System.ComponentModel.Win32Exception(System.Runtime.InteropServices.Marshal.GetLastWin32Error(),"独立隐藏桌面不可用；不回退普通桌面。");Console.WriteLine("字段回归独立隐藏桌面，不切换用户桌面。");result=RunFieldCases();}catch(Exception error){Console.Error.WriteLine(error);}finally{if(desktop!=IntPtr.Zero)CloseDesktop(desktop);}});
        thread.SetApartmentState(ApartmentState.STA);thread.Start();thread.Join();return result;
    }
    [System.Runtime.InteropServices.DllImport("user32.dll",CharSet=System.Runtime.InteropServices.CharSet.Unicode,SetLastError=true)]private static extern IntPtr CreateDesktop(string name,IntPtr device,IntPtr mode,int flags,uint access,IntPtr security);
    [System.Runtime.InteropServices.DllImport("user32.dll",SetLastError=true)]private static extern bool SetThreadDesktop(IntPtr desktop);
    [System.Runtime.InteropServices.DllImport("user32.dll",SetLastError=true)]private static extern bool CloseDesktop(IntPtr desktop);
    private static int RunFieldCases()
    {
        try
        {
            if(Environment.GetEnvironmentVariable("B08_REFERENCE_WINDOW_CULTURE") is {Length:>0} culture){Require(culture is "zh-CN" or "en-US","未知窗口验证语言");System.Globalization.CultureInfo.CurrentUICulture=System.Globalization.CultureInfo.GetCultureInfo(culture);}
            if(Environment.GetEnvironmentVariable("B08_REFERENCE_CASE_SCOPE") is "CrossDocument" or "WindowSession" or "BehaviorUi" or "LanguageLayout")return RunCrossDocumentReferenceCases();
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
            VerifyNumericReferenceCleanup();
            VerifyReferenceLeaseCleanup();

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
            var access=definition.SettingsSchema;Require(access.Inputs.Count==55&&access.Inputs.Select(p=>p.Id).Distinct().Count()==55,"XML设置字段不完整或重复");
            VerifySettingsCandidate(definition,settings);
            using var xmlStream=definition.GetType().Assembly.GetManifestResourceStream("BlobAnalysis.BlobParameters.xml")!;
            var xml=System.Xml.Linq.XDocument.Load(xmlStream);var rule=xml.Descendants("Judgment").Single(e=>(string?)e.Attribute("key")=="total-area");
            var key=(string)rule.Attribute("minimumFieldId")!;
            var initialFields=new FieldSettings(access.Inputs,definition.ReadSettings(settings).CopyValues().ToDictionary(pair=>pair.Key,pair=>(ParameterSetting)new ConstantSetting(pair.Value)));
            var editorSchema=SunEyeVision.Plugin.Contracts.Results.Presentation.ResultDisplayFields.EditorSchema((IToolDefinition)definition);
            var editorFields=new FieldSettings(editorSchema.Inputs,initialFields.Capture().Concat(((SunEyeVision.Plugin.Contracts.Results.Presentation.IToolResultDisplayProvider)definition).CreateDisplaySettings().CopyValues().Select(pair=>new KeyValuePair<string,ParameterSetting>(pair.Key,new ConstantSetting(pair.Value)))).ToDictionary(pair=>pair.Key,pair=>pair.Value));
            VerifyBlobPage(xml,editorFields);
            var updated=(SunEyeVision.Tool.BlobAnalysis.Runtime.BlobSettings)definition.MaterializeSettings(initialFields.Apply(new Dictionary<string,ParameterSetting>{{key,new ConstantSetting(12d)}}).Materialize(new Dictionary<string,object?>()));
            Require(updated.Judgments.Single(r=>r.Key=="total-area").Minimum==12&&updated.Judgments.Single(r=>r.Key=="max-area")==settings.Judgments.Single(r=>r.Key=="max-area"),"嵌套同名最小值写错判定");
            Console.WriteLine("通过：正式Runtime XML的55个唯一设置字段及嵌套判定精确读写，不创建旧Blob参数");
            var node=new WorkflowNodeBase("blob","Blob","Blob","BlobAnalysis"){RuntimeConfiguration=RuntimeToolConfiguration.Create("BlobAnalysis")};
            var flow=new SunEyeVision.Workflow.Workflow("flow","字段草稿");flow.Nodes.Add(node);var solution=new Solution();solution.Workflows.Add(flow);
            var edits=(DocumentEditSession)Activator.CreateInstance(typeof(DocumentEditSession),BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{solution},null)!;
            using var control=new SunEyeVision.UI.Services.Editing.RuntimeNodeConfigurationControl(node);
            using var context=new SunEyeVision.UI.Services.Editing.ProductionEditorContext(edits.Host,flow.Id,node,()=>true,control.BindDraft,documentEdits:edits);
            typeof(SunEyeVision.UI.Services.Editing.ProductionEditorContext).GetProperty("PrepareView",BindingFlags.Instance|BindingFlags.NonPublic)!
                .SetValue(context,(Func<string?>)(()=>control.TryCapture(out var error)?null:error));
            typeof(SunEyeVision.UI.Services.Editing.ProductionEditorContext).GetProperty("HasPendingViewEdits",BindingFlags.Instance|BindingFlags.NonPublic)!
                .SetValue(context,(Func<bool>)(()=>control.HasPendingEdits));
            control.BindDraft(context.DraftNode);SunEyeVision.Plugin.Editing.Wpf.ToolEditorCommit.AttachDraftContext(control,context);
            Show(control);var fields=Descendants(control).OfType<DeclaredFieldsView>().Single();fields.SelectedPageIndex=1;Pump();CaptureBlobPreview(control,"production-host");
            var thresholdId=access.Presentation!.Fields.Single(f=>f.DisplayName==SunEyeVision.Tool.BlobAnalysis.Runtime.BlobParameterDefinition.Instance.Label("Threshold")).FieldId;
            ((TextBox)fields.Fields.Single(f=>f.FieldId==thresholdId).InputElement!).Text="201";
            int Threshold()=>((SunEyeVision.Tool.BlobAnalysis.Runtime.BlobSettings)node.RuntimeConfiguration!.CaptureSettings()).Threshold;
            Require(Threshold()==100,"公共控件提前写入正式配置");
            var valid=control.TryValidateEditor(out var validationError);
            if(!valid&&validationError=="ROI绘制、继承模式及结果显示组合尚未接入正式状态，当前窗口不能执行或保存。")
            {
                Require(!context.TryCommitEditor(out _)&&Threshold()==100,"未接入ROI组合绕过保护发布正式配置");
                context.Discard();Require(Threshold()==100,"纯UI取消改变正式配置");edits.Close();
                Console.WriteLine("通过：真实阈值编辑仅写私有草稿，未接入ROI组合仍阻断整窗确认，取消不改变正式配置。");
                Console.WriteLine("字段组件及现行UI阶段保护检查通过，不代表Blob功能确认、保存重开或人工验收完成。");
            Console.WriteLine(DateTime.UtcNow.ToString("O")+" BODY_COMPLETED / USING_DISPOSE_BEGIN");
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
        finally{Console.WriteLine(DateTime.UtcNow.ToString("O")+" OWNER_CLOSE_BEGIN");owner?.Close();Console.WriteLine(DateTime.UtcNow.ToString("O")+" OWNER_CLOSE_END / APP_SHUTDOWN_BEGIN");Application.Current?.Shutdown();Console.WriteLine(DateTime.UtcNow.ToString("O")+" APP_SHUTDOWN_END");}
    }
    private static int RunCrossDocumentReferenceCases()
    {
        Console.WriteLine("B08跨文档专项入口；只运行新增真实宿主场景，不计完整FieldEditing。");
        var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
        SunEyeVision.Plugin.SDK.Vision.Hosting.VisionImageFactoryLocator.Current=new SunEyeVision.HalAdapter.HalImageFactory();
        foreach(var resource in new[]{"Shell/Resources/AppResources.xaml","Resources/Styles/NodeStyleResources.xaml","Resources/Styles/ConnectionStyleResources.xaml","Resources/Styles/CanvasStyleResources.xaml","Resources/Styles/CommonStyleResources.xaml","Resources/Styles/MenuStyleResources.xaml","Resources/Styles/ToolTipStyleResources.xaml","Themes/Dark/DarkTheme.xaml"})
            app.Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("pack://application:,,,/SunEyeVision.UI;component/"+resource)});
        var plugins=new PluginManager();plugins.LoadPlugins();plugins.AssertCompleteReleaseToolSet(ToolRegistry.GetAllToolMetadata().Select(t=>t.Id));PluginLoader.MarkAsLoaded();
        var directory=Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"cross-document-reference");Directory.CreateDirectory(directory);
        ServiceInitializer.InitializeServices();ServiceInitializer.InitializeSolutionManager(directory);
        var manager=ServiceInitializer.SolutionManager;var vm=new MainWindowViewModel();
        owner=new Window{Width=720,Height=440,Title="B08跨文档真实宿主",Content=new ContentControl(),ShowInTaskbar=false};app.MainWindow=owner;owner.Show();Pump();
        var selectedTransition=Environment.GetEnvironmentVariable("B08_REFERENCE_CASE_TRANSITION");
        Require(string.IsNullOrEmpty(selectedTransition)||selectedTransition is "A-to-B" or "reopen-A","未知跨文档场景筛选");
        var transitions=string.IsNullOrEmpty(selectedTransition)?new[]{"A-to-B","reopen-A"}:new[]{selectedTransition!};
        var selectedKinds=Environment.GetEnvironmentVariable("B08_REFERENCE_CASE_KINDS");
        var windowSessionOnly=Environment.GetEnvironmentVariable("B08_REFERENCE_CASE_SCOPE")=="WindowSession";
        var behaviorUiOnly=Environment.GetEnvironmentVariable("B08_REFERENCE_CASE_SCOPE")=="BehaviorUi";
        var languageLayoutOnly=Environment.GetEnvironmentVariable("B08_REFERENCE_CASE_SCOPE")=="LanguageLayout";
        var allKinds=languageLayoutOnly?new[]{"language-layout"}:behaviorUiOnly?new[]{"behavior-ui"}:windowSessionOnly?new[]{"window-session"}:new[]{"source-popup","index-popup","expanded","variable-source","variable-index"};
        var kinds=string.IsNullOrEmpty(selectedKinds)?allKinds:selectedKinds.Split(',',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries);
        Require(kinds.Length!=0&&kinds.Distinct(StringComparer.Ordinal).Count()==kinds.Length&&kinds.All(kind=>allKinds.Contains(kind)||kind=="image-combo"),"未知或重复跨文档控件筛选");
        var reports=new List<object>();var unexpected=new List<string>();var expectDelete=false;var imageChainVerified=false;
        void Trace(string text){var line=DateTime.UtcNow.ToString("O")+" "+text;Console.WriteLine(line);File.AppendAllText(Path.Combine(directory,"progress.log"),line+Environment.NewLine);}
        var dialogs=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(50)};
        dialogs.Tick+=(_,_)=>{foreach(var dialog in app.Windows.OfType<Window>().Where(w=>w.IsVisible&&w.GetType().Name.Contains("MessageDialog")).ToArray())
            {if(expectDelete&&dialog.Title=="确认删除"&&dialog.FindName("YesButton") is Button yes){yes.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));continue;}
             unexpected.Add(string.Join(" | ",CrossVisuals(dialog).OfType<TextBlock>().Select(t=>t.Text)));dialog.Close();}};dialogs.Start();
        string Field(string name)=>BlobParameterDefinition.Instance.SettingsFieldId(name);
        var low=Field("Threshold");var count=Field("BlobCountLimit");
        string Stamp(RuntimeToolConfiguration config)
        {
            var identity=(string)typeof(RuntimeToolConfiguration).GetProperty("ConfigurationIdentity",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(config)!;
            var values=config.SettingsSchema.Inputs.Where(p=>p.Type==DataType.Integer||p.Type==DataType.Number)
                .ToDictionary(p=>p.Id,p=>config.ReadCurrentParameter(p.Id)?.Value,StringComparer.Ordinal);
            return identity+System.Text.Json.JsonSerializer.Serialize(values);
        }
        (SunEyeVision.UI.Shared.WorkflowNode Target,string SourceId,string WidthPort) Populate()
        {
            if(vm.WorkflowTabViewModel.SelectedTab is null)vm.NewWorkflowCommand.Execute(null);
            var tab=vm.WorkflowTabViewModel.SelectedTab!;
            var target=tab.CreateNode(BlobTool.Id);target.Name="同名Blob";target.DebugWindowStyle=SunEyeVision.Plugin.SDK.Core.DebugWindowStyle.Custom;vm.AddNodeToWorkflow(target);
            var source=tab.CreateNode("ImageLoad");source.Name="同名来源";vm.AddNodeToWorkflow(source);
            var image=Path.Combine(directory,"input.png");if(!File.Exists(image))
            {
                var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(BitmapSource.Create(8,9,96,96,PixelFormats.Gray8,null,Enumerable.Repeat((byte)200,72).ToArray(),8)));
                using var stream=File.Create(image);png.Save(stream);
            }
            var document=manager.CurrentSolution!;var flow=document.Workflows.Single(f=>f.Nodes.Any(n=>ReferenceEquals(n,target)));
            var host=manager.Session!.Editing.Host;var imageDraft=host.BeginEdit(flow.Id,source.Id);
            var policy=ImageSourcePolicy.Read(imageDraft.Node)??throw new Exception("真实图像源草稿无正式策略");policy.FilePaths=new(){image};policy.ForRunPaths=new(){image};policy.CurrentIndex=0;policy.SNInitialValue=1;policy.ApplyTo(imageDraft.Node);host.Commit(imageDraft);
            var edge=new WorkflowConnection{Id=Guid.NewGuid().ToString("N"),SourceNodeId=source.Id,TargetNodeId=target.Id};
            var imagePort=target.RuntimeConfiguration!.Schema.WorkflowImageInputId!;
            Require(target.RuntimeConfiguration.Fields.Read(imagePort) is UnsetSetting&&target.ParamSettings.Count==0,"真实工厂图像字段不是空正式配置");
            if(!imageChainVerified)
            {
                var initial=Stamp(target.RuntimeConfiguration);var injected=new InvalidOperationException("自动来源回调失败标记");
                var rollback=new SunEyeVision.UI.Features.WorkflowUI.Commands.AddConnectionCommand(flow.Connections,edge,flow.Nodes,()=>
                {
                    typeof(MainWindowViewModel).GetMethod("AutoBindImageSourceOnConnectionAdded",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(vm,new object[]{edge});
                    Require(target.RuntimeConfiguration!.Fields.Read(imagePort) is ReferenceSetting {OriginId:not null},"失败回调前没有真实自动绑定");throw injected;
                });
                try{rollback.Execute(null);throw new Exception("回调异常未传播");}catch(InvalidOperationException error){Require(ReferenceEquals(error,injected),"回滚掩盖原回调异常");}
                Require(!flow.Connections.Contains(edge)&&Stamp(target.RuntimeConfiguration!)==initial&&target.ParamSettings.Count==0,"回调失败没有完整回滚图/正式字段");
            }
            Require(vm.AddConnectionToWorkflow(edge),"真实MainWindow控制连线命令拒绝有效前提");
            var automatic=target.RuntimeConfiguration!.Fields.Read(imagePort) as ReferenceSetting;
            Require(automatic is not null&&automatic.OriginId=="connection:"+edge.Id&&automatic.Reference.NodeId==source.Id&&automatic.Reference.ScopeId==flow.Id&&target.ParamSettings.Count==0,"自动图像来源未写入正式字段/连线归属");
            if(!imageChainVerified)
            {
                var accepted=Stamp(target.RuntimeConfiguration!);Require(vm.UndoCommand.CanExecute(null),"真实连线无撤销入口");vm.UndoCommand.Execute(null);Pump();
                Require(!flow.Connections.Any(e=>e.Id==edge.Id)&&target.RuntimeConfiguration!.Fields.Read(imagePort) is UnsetSetting,"真实连线撤销未恢复空来源");
                Require(vm.RedoCommand.CanExecute(null),"真实连线无重做入口");vm.RedoCommand.Execute(null);Pump();
                Require(flow.Connections.Any(e=>e.Id==edge.Id)&&Stamp(target.RuntimeConfiguration!)==accepted,"真实连线重做未恢复来源身份");
                foreach(var n in flow.Nodes)n.IsSelected=false;foreach(var e in flow.Connections)e.IsSelected=e.Id==edge.Id;
                expectDelete=true;try{vm.DeleteSelectedNodesCommand.Execute(null);Pump();}finally{expectDelete=false;}
                Require(!flow.Connections.Any(e=>e.Id==edge.Id)&&target.RuntimeConfiguration!.Fields.Read(imagePort) is ReferenceSetting {OriginId:null,RebindingReason:not null} broken&&SunEyeVision.Plugin.Runtime.BindingReferenceSerializer.Serialize(broken.Reference)==SunEyeVision.Plugin.Runtime.BindingReferenceSerializer.Serialize(automatic!.Reference),"断边未保留明确待修复引用");
                var brokenStamp=Stamp(target.RuntimeConfiguration!);Require(vm.AddConnectionToWorkflow(edge)&&Stamp(target.RuntimeConfiguration!)==brokenStamp,"重新连线静默修复/替换待修复来源");
                vm.UndoCommand.Execute(null);Pump();Require(!flow.Connections.Any(e=>e.Id==edge.Id)&&Stamp(target.RuntimeConfiguration!)==brokenStamp,"待修复状态重新连线撤销失败");
                vm.UndoCommand.Execute(null);Pump();Require(Stamp(target.RuntimeConfiguration!)==accepted,"断边撤销未恢复自动归属");
                vm.RedoCommand.Execute(null);Pump();Require(target.RuntimeConfiguration!.Fields.Read(imagePort) is ReferenceSetting {RebindingReason:not null},"断边重做未恢复待修复状态");
                vm.UndoCommand.Execute(null);Pump();Require(Stamp(target.RuntimeConfiguration!)==accepted,"断边二次撤销失败");
                var fragment=WorkflowFragmentCopy.Prepare(flow,new[]{source.Id,target.Id},flow.Id,_=>null,(node,_)=>new WorkflowNodeBase("copy-"+node.Id,node.Name,node.DispName,node.ToolType));
                var copied=fragment.Nodes.Single(n=>n.Id=="copy-"+target.Id);var copiedSetting=(ReferenceSetting)copied.RuntimeConfiguration!.Fields.Read(imagePort);
                Require(copiedSetting.Reference.NodeId=="copy-"+source.Id&&copiedSetting.OriginId=="connection:"+fragment.Connections.Single().Id&&copied.ParamSettings.Count==0,"复制没有映射正式来源/连线身份");
                host.SynchronizeStructure();var manualNode=tab.CreateNode(BlobTool.Id);manualNode.Name="手动来源保留";vm.AddNodeToWorkflow(manualNode);
                var manualDraft=host.BeginEdit(flow.Id,manualNode.Id);var manual=new ReferenceSetting(automatic!.Reference);
                manualDraft.Node.RuntimeConfiguration=manualDraft.Node.RuntimeConfiguration!.WithFieldSettings(new Dictionary<string,ParameterSetting>{[imagePort]=manual});host.Commit(manualDraft);
                var manualEdge=new WorkflowConnection{Id=Guid.NewGuid().ToString("N"),SourceNodeId=source.Id,TargetNodeId=manualNode.Id};
                Require(vm.AddConnectionToWorkflow(manualEdge)&&manualNode.RuntimeConfiguration!.Fields.Read(imagePort) is ReferenceSetting {OriginId:null,RebindingReason:null} manualKept&&SunEyeVision.Plugin.Runtime.BindingReferenceSerializer.Serialize(manualKept.Reference)==SunEyeVision.Plugin.Runtime.BindingReferenceSerializer.Serialize(manual.Reference)&&manualNode.ParamSettings.Count==0,"自动连线覆盖手动来源");
                foreach(var n in flow.Nodes)n.IsSelected=false;foreach(var e in flow.Connections)e.IsSelected=e.Id==manualEdge.Id;
                expectDelete=true;try{vm.DeleteSelectedNodesCommand.Execute(null);Pump();}finally{expectDelete=false;}
                Require(manualNode.RuntimeConfiguration!.Fields.Read(imagePort) is ReferenceSetting {OriginId:null,RebindingReason:null} manualAfterDelete&&SunEyeVision.Plugin.Runtime.BindingReferenceSerializer.Serialize(manualAfterDelete.Reference)==SunEyeVision.Plugin.Runtime.BindingReferenceSerializer.Serialize(manual.Reference),"断边清除了手动来源");
                imageChainVerified=true;Console.WriteLine("PASS 自动图像真实连线/撤销重做/断边/手动保留/复制/异常回滚");
            }
            host.SynchronizeStructure();var vars=host.BeginVariableEdit();
            var sequence=new GlobalVariable("同名数组",new long[]{100,110,120},"Integer[]");var index=new GlobalVariable("同名索引",1L,"Integer");
            vars.Configuration.GlobalVariables.Add(sequence);vars.Configuration.GlobalVariables.Add(index);host.CommitBatch(Array.Empty<NodeConfigurationEditDraft>(),vars);
            var width=source.RuntimeConfiguration!.Schema.Outputs.First(p=>p.Type==DataType.Integer);
            var candidate=new DeclaredReferenceCandidate("真实整数来源",source.Id,flow.Id,width,Array.Empty<string>());
            Require(GlobalProducerBindingRules.TryDescribeTarget(sequence.Type,out var variablePort,out _,out var reason),reason);
            var draft=host.BeginEdit(flow.Id,target.Id);
            draft.Node.RuntimeConfiguration=draft.Node.RuntimeConfiguration!.WithFieldSettings(new Dictionary<string,ParameterSetting>
            {
                [low]=new ReferenceSetting(candidate.Reference(BindingSelection.All)),
                [count]=new VariableSetting(new VariableReference(document.Id,sequence.Id,variablePort.Type,new BindingSelection.AtConstant(0),variablePort.Storage))
            });host.Commit(draft);return(target,source.Id,width.Id);
        }
        DefaultDebugWindow Open(SunEyeVision.UI.Shared.WorkflowNode node)
        {
            vm.OpenDebugWindowCommand.Execute(node);var wait=System.Diagnostics.Stopwatch.StartNew();
            DefaultDebugWindow? found=null;
            while(wait.Elapsed<TimeSpan.FromSeconds(8))
            {
                Pump();found=app.Windows.OfType<DefaultDebugWindow>().SingleOrDefault(w=>w.IsVisible);
                if(found?.FindName("ContentHost") is ContentControl{Content:RuntimeNodeConfigurationControl})break;
                Thread.Sleep(10);
            }
            Require(found is not null&&found.FindName("ContentHost") is ContentControl{Content:RuntimeNodeConfigurationControl},"真实MainWindow打开命令未生成配置宿主："+string.Join(" | ",unexpected));
            found!.UpdateLayout();Pump();return found;
        }
        RuntimeNodeConfigurationControl Control(DefaultDebugWindow window)=>(RuntimeNodeConfigurationControl)((ContentControl)window.FindName("ContentHost")).Content;
        DeclaredFieldsView Fields(DefaultDebugWindow window)=>CrossVisuals(Control(window)).OfType<DeclaredFieldsView>().Single();
        object Session(DeclaredFieldAccess field)
        {
            Window.GetWindow(field.View)?.UpdateLayout();Pump();Require(field.View.IsLoaded&&field.View.IsVisible,"数值字段尚未真实加载/可见，不能取模板会话");
            var numeric=typeof(DeclaredFieldEditor).GetField("_numeric",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(field.View)!;
            var coordinator=numeric.GetType().GetField("_coordinator",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(numeric)!;
            var box=(ComboBox?)coordinator.GetType().GetField("_box",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(coordinator)??throw new Exception("真实数值ComboBox模板尚未加载");
            Window.GetWindow(field.View)!.Activate();box.Focus();box.SetCurrentValue(ComboBox.IsDropDownOpenProperty,true);Pump();
            var session=coordinator.GetType().GetField("_reference",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(coordinator)!;
            Require(!(bool)Property(session,"IsSuspended")!,"真实来源会话仍暂停，不能据此做退役试验");return session;
        }
        object? Property(object value,string name)=>value.GetType().GetProperty(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)!.GetValue(value);
        void Command(object value,string name,object? argument=null)=>((ICommand)Property(value,name)!).Execute(argument);
        object[] Nodes(object value,string property)=>Flatten(((System.Collections.IEnumerable)Property(value,property)!).Cast<object>()).ToArray();
        IEnumerable<object> Flatten(IEnumerable<object> roots)
        {foreach(var root in roots){yield return root;foreach(var child in Flatten(((System.Collections.IEnumerable)Property(root,"EnumEntrysA")!).Cast<object>()))yield return child;}}
        object ModuleLeaf(object value,string property,string source,string? excluding=null)=>Nodes(value,property).Single(n=>Property(n,"Candidate") is DeclaredReferenceCandidate c&&c.NodeId==source&&(excluding is null||c.Output.Id!=excluding));
        AppsPopup Popup(DeclaredFieldAccess field,object session,string path)
            =>CrossVisuals(field.View).OfType<AppsPopup>().Single(p=>ReferenceEquals(p.DataContext,session)
                &&p.Name==(path=="IsDropDownOpen"?"rt":"et")&&p.GetBindingExpression(AppsPopup.IsPopupOpenProperty)?.ParentBinding.Path?.Path==path);
        void VerifyImageCombo(string transition,(SunEyeVision.UI.Shared.WorkflowNode Target,string SourceId,string WidthPort) fixture,string pathA)
        {
            string ImageState(RuntimeToolConfiguration configuration)=>System.Text.Json.JsonSerializer.Serialize(configuration.Fields.Read(configuration.Schema.WorkflowImageInputId!));
            (DeclaredFieldEditor Field,ComboBox Combo) ImageCombo(DefaultDebugWindow window)
            {
                var inputs=CrossVisuals(Control(window)).OfType<DeclaredInputFieldsView>().Single();
                var field=inputs.Fields.Single(f=>f.FieldId==BlobTool.ImageInput);var combo=(ComboBox)field.InputElement!;
                Require(combo.IsLoaded&&combo.IsVisible&&combo.IsEnabled,"图像Combo未真实加载可用");return(field,combo);
            }
            void OpenCombo(DefaultDebugWindow window,ComboBox combo)
            {
                window.Activate();combo.Focus();combo.IsDropDownOpen=true;Pump();
                Require(combo.IsDropDownOpen&&combo.Template.FindName("Popup",combo) is Popup{IsOpen:true},"图像原Popup未实际打开");
            }
            var document=manager.CurrentSolution!;var edit=manager.Session!.Editing;var formal=Stamp(fixture.Target.RuntimeConfiguration!);var sourceState=ImageState(fixture.Target.RuntimeConfiguration!);var savedA=File.ReadAllBytes(pathA);
            var first=Open(fixture.Target);var firstControl=Control(first);var firstContext=(ProductionEditorContext)ToolEditorCommit.GetDraftContext(firstControl)!;
            var old=ImageCombo(first);OpenCombo(first,old.Combo);var selected=(ComboBoxItem)old.Combo.SelectedItem!;
            Require(selected.Tag is ReferenceSetting source&&source.Reference.NodeId==fixture.SourceId,"图像真实已选来源身份错误");
            var alternate=old.Combo.Items.OfType<ComboBoxItem>().FirstOrDefault(item=>item.IsEnabled&&!ReferenceEquals(item,selected)&&item.Tag is ReferenceSetting)??throw new Exception("真实多图候选前提缺失");
            foreach(var hidden in new[]{false,true})
            {
                var before=Stamp(firstContext.DraftNode.RuntimeConfiguration!);if(hidden)old.Field.Visibility=Visibility.Collapsed;else old.Field.IsEnabled=false;Pump();
                Require(!old.Combo.IsDropDownOpen,"停用未关闭原图像Popup");old.Combo.SelectedItem=alternate;Pump();Require(before==Stamp(firstContext.DraftNode.RuntimeConfiguration!),"停用程序选择写入草稿");
                if(hidden)old.Field.Visibility=Visibility.Visible;else old.Field.IsEnabled=true;Pump();
                Require(old.Combo.SelectedItem is ComboBoxItem{Tag:ReferenceSetting restored}&&restored.Reference.NodeId==((ReferenceSetting)selected.Tag).Reference.NodeId&&restored.Reference.PortId==((ReferenceSetting)selected.Tag).Reference.PortId&&restored.Reference.ScopeId==((ReferenceSetting)selected.Tag).Reference.ScopeId&&restored.Reference.MemberFieldPath.SequenceEqual(((ReferenceSetting)selected.Tag).Reference.MemberFieldPath),"恢复未刷新却显示未接纳选择");
                OpenCombo(first,old.Combo);
            }
            var oldItems=old.Combo.Items.OfType<ComboBoxItem>().ToArray();
            manager.CloseSolution(saveChanges:false);Pump();var destination=transition=="reopen-A"?manager.OpenSolution(pathA):manager.CreateUntitledSolution();Pump();
            Require(destination is not null&&!first.IsVisible&&(bool)typeof(RuntimeNodeConfigurationControl).GetField("_disposed",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(firstControl)!&&!firstContext.IsValid&&!ReferenceEquals(edit,manager.Session!.Editing),"换方案未退役真实编辑器");
            Require(!old.Combo.IsDropDownOpen&&savedA.SequenceEqual(File.ReadAllBytes(pathA)),"旧Popup未关或取消改变文件");
            var target=transition=="reopen-A"?((SunEyeVision.UI.Shared.WorkflowNode)vm.WorkflowTabViewModel.SelectedTab!.WorkflowNodes.Single(n=>n.Id==fixture.Target.Id)):Populate().Target;
            Require(transition!="reopen-A"||destination.Id==document.Id&&target.Id==fixture.Target.Id,"同ID新文档会话前提失败");
            var pathB=transition=="reopen-A"?pathA:Path.Combine(directory,"image-combo-B.solution");Require(manager.SaveCurrentDocument(pathB).FileWritten,"目标保存失败");
            var current=Open(target);var control=Control(current);var context=(ProductionEditorContext)ToolEditorCommit.GetDraftContext(control)!;var next=ImageCombo(current);OpenCombo(current,next.Combo);
            var draft=Stamp(context.DraftNode.RuntimeConfiguration!);var currentFormal=Stamp(target.RuntimeConfiguration!);var imageDraft=ImageState(context.DraftNode.RuntimeConfiguration!);var imageFormal=ImageState(target.RuntimeConfiguration!);var history=manager.Session!.History.Position;var version=manager.Session.Editing.Host.Capture().Version;var registered=manager.Session.Editing.RegisteredScopeCount;var savedB=File.ReadAllBytes(pathB);
            old.Combo.SelectedItem=alternate;
            foreach(var item in oldItems)item.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(Mouse.PrimaryDevice,Environment.TickCount,MouseButton.Left){RoutedEvent=UIElement.PreviewMouseLeftButtonDownEvent});
            old.Combo.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(next.Combo),Environment.TickCount,Key.Enter){RoutedEvent=UIElement.PreviewKeyDownEvent});Pump();
            Require(context.IsValid&&current.IsVisible&&next.Combo.IsDropDownOpen&&next.Combo.Template.FindName("Popup",next.Combo) is Popup{IsOpen:true},"旧事件破坏新宿主或图像Popup");
            Require(draft==Stamp(context.DraftNode.RuntimeConfiguration!)&&currentFormal==Stamp(target.RuntimeConfiguration!)&&imageDraft==ImageState(context.DraftNode.RuntimeConfiguration!)&&imageFormal==ImageState(target.RuntimeConfiguration!)&&formal==Stamp(fixture.Target.RuntimeConfiguration!)&&sourceState==ImageState(fixture.Target.RuntimeConfiguration!)&&history==manager.Session.History.Position&&version==manager.Session.Editing.Host.Capture().Version&&registered==manager.Session.Editing.RegisteredScopeCount&&savedB.SequenceEqual(File.ReadAllBytes(pathB))&&savedA.SequenceEqual(File.ReadAllBytes(pathA)),"旧图像事件改写草稿/正式/历史/版本/文件");
            Require(unexpected.Count==0,"意外产品提示");current.Close();Pump();var reopened=Open(target);OpenCombo(reopened,ImageCombo(reopened).Combo);reopened.Close();manager.CloseSolution(saveChanges:false);Pump();
            Trace("PASS_IMAGE_COMBO "+transition+" real-host/physical-popup/disabled-hidden/old-events/reopen");
        }
        void VerifyLanguageLayout((SunEyeVision.UI.Shared.WorkflowNode Target,string SourceId,string WidthPort) fixture)
        {
            var english=SunEyeVision.Plugin.Wpf.Native.Common.Localization.AppLocalizationService.GetLang("Language")=="English";
            var width=english?680d:420d;var label=english?170d:89d;var window=Open(fixture.Target);var control=Control(window);var fields=Fields(window);var initial=Stamp(fixture.Target.RuntimeConfiguration!);
            Require(window.Width==width&&Math.Abs(window.ActualWidth-width)<.5&&window.Height==500&&window.MinWidth==360,"真实宿主未沿同一语言资源采用原默认宽高");
            var pages=((System.Collections.IEnumerable)typeof(DeclaredFieldsView).GetField("_pages",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(fields)!).Cast<object>().ToArray();Require(pages.Length==3,"原三页丢失");
            for(var i=0;i<pages.Length;i++){fields.SelectedPageIndex=i;window.UpdateLayout();Pump();var host=pages[i].GetType().GetField("Item2")!.GetValue(pages[i])!;Require((double)Property(host,"ChildLeftWidth")! ==label,"页面标签未沿原89/170默认："+i);}
            fields.SelectedPageIndex=1;window.UpdateLayout();Pump();Require(fields.Fields.Single(f=>f.FieldId==Field("IsAreaFilterEnabled")).TrySetConstant(true),"正式面积过滤开关无法启用");
            var lower=fields.Fields.Single(f=>f.FieldId==Field("FilterAreaMin"));var upper=fields.Fields.Single(f=>f.FieldId==Field("FilterAreaMax"));
            Require(lower.TrySetConstant(999999999L),"面积两端合法相等上限未接纳");window.UpdateLayout();Pump();var endpoints=new List<object>();
            foreach(var field in new[]{lower,upper})
            {
                var text=(TextBox)field.InputElement!;Require(text.IsLoaded&&text.IsVisible&&text.IsEnabled&&text.Text=="999999999","真实面积端点文本/状态不完整");
                DependencyObject numeric=text;while(numeric is not SunEyeVision.Plugin.Wpf.Native.Numeric.HikPcUI.ListView.NumericControl){numeric=VisualTreeHelper.GetParent(numeric)??throw new Exception("找不到原NumericControl");}
                var tip=((FrameworkElement)numeric).ToolTip;Require(tip is string value&&value==text.Text,"原T1/T2 ToolTip未绑定完整当前文本");
                window.Activate();text.Focus();Pump();Require(text.IsKeyboardFocused,"真实面积文本无法取得焦点");
                void Key(Key key){text.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(text)!,Environment.TickCount,key){RoutedEvent=Keyboard.KeyDownEvent});window.UpdateLayout();Pump();}
                Key(System.Windows.Input.Key.Home);var start=text.HorizontalOffset;var extent=text.ExtentWidth;var viewport=text.ViewportWidth;var clipped=extent>viewport+.5;
                Key(System.Windows.Input.Key.End);var end=text.HorizontalOffset;var last=text.GetRectFromCharacterIndex(text.Text.Length,true);
                Require(text.CaretIndex==text.Text.Length&&!last.IsEmpty&&last.Right<=text.ActualWidth+.5&&(!clipped||end>start),"原焦点Home/End未实际滚动到完整末位");
                if(english)Require(!clipped,"英文680/170下九位面积仍显示不足");
                endpoints.Add(new{field=field.FieldId,text=text.Text,tooltip=tip,actualWidth=text.ActualWidth,extent,viewport,clipped,start,end,lastRight=last.Right,caret=text.CaretIndex,font=text.FontFamily.Source,text.FontSize});
            }
            var dpi=VisualTreeHelper.GetDpi(window);var image=new RenderTargetBitmap((int)Math.Ceiling(window.ActualWidth*dpi.DpiScaleX),(int)Math.Ceiling(window.ActualHeight*dpi.DpiScaleY),dpi.PixelsPerInchX,dpi.PixelsPerInchY,PixelFormats.Pbgra32);image.Render(window);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(image));using(var file=File.Create(Path.Combine(directory,"original-layout-"+System.Globalization.CultureInfo.CurrentUICulture.Name+".png")))png.Save(file);
            File.WriteAllText(Path.Combine(directory,"layout-metrics-"+System.Globalization.CultureInfo.CurrentUICulture.Name+".json"),System.Text.Json.JsonSerializer.Serialize(new{language=System.Globalization.CultureInfo.CurrentUICulture.Name,width=window.ActualWidth,label,height=window.ActualHeight,dpiX=dpi.PixelsPerInchX,dpiY=dpi.PixelsPerInchY,endpoints,liveLanguageSwitchImplemented=false,originalChineseClippingAllowed=true},new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
            Require(Stamp(fixture.Target.RuntimeConfiguration!)==initial,"可读性验证提前改变正式参数");window.Close();Pump();
            var reopened=Open(fixture.Target);Require(reopened.Width==width&&reopened.Height==500,"取消重开未恢复原语言默认窗口");Require(Stamp(fixture.Target.RuntimeConfiguration!)==initial,"取消后合法面积测试值写入正式模型");reopened.Close();Pump();Trace("PASS_LANGUAGE_LAYOUT "+System.Globalization.CultureInfo.CurrentUICulture.Name);
        }
        void VerifyBehaviorControls()
        {
            var first=ParameterReferenceBehavior.FirstItemWithIndexReadZeroFallback;var sequence=TypeRef.SequenceOf(DataType.Integer,CountConstraint.Exactly(3));
            var source=new DeclaredReferenceCandidate("数组","ui-policy-source",null,new Port("values",sequence),Array.Empty<string>());
            var index=new DeclaredReferenceCandidate("数组索引","ui-policy-index",null,new Port("indices",sequence),Array.Empty<string>());
            var snapshot=new ReferenceCandidates(new[]{source},new[]{index},behavior:first);var reads=0;var supplied=first;var writes=0;
            var presentation=new FieldPresentation("policy","整数","参数",0,FieldEditorKind.Scalar){Layout=new("参数","参数",Minimum:0,Maximum:1000,DecimalPlaces:0)};
            var target=new Port("policy",DataType.Integer,Storage:NumericStorage.Int32,EditModes:FieldEditModes.Constant|FieldEditModes.Reference);
            using var field=new DeclaredFieldEditor(target,"整数",new ReferenceSetting(source.Reference(new BindingSelection.AtConstant(0))),_=>writes++,
                new ReferenceFieldOptions(_=>snapshot,EditableIndex:true,ReadBehavior:_=>{reads++;return supplied;}),new ConstantSetting(7L),presentation:presentation);
            Show(field);var access=(DeclaredFieldAccess)Activator.CreateInstance(typeof(DeclaredFieldAccess),BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{field},null)!;var session=Session(access);Command(session,"DropDownCommand");supplied=ParameterReferenceBehavior.StrictExactlyOne;
            var text=(string)Property(session,"CurValueA")!;session.GetType().GetProperty("CurValueA")!.SetValue(session,System.Text.RegularExpressions.Regex.Replace(text,"\\[[^\\]]*\\]$","[]"));Pump();
            Require(reads==1&&writes==1&&field.Setting is ReferenceSetting {Reference.Selection:BindingSelection.AllItems}&&!field.HasInvalidText,"显式First未冻结一次/未接纳All[]");
            var leaf=Nodes(session,"IndexNodes").Single(n=>Property(n,"Candidate") is DeclaredReferenceCandidate c&&c.NodeId==index.NodeId);Command(session,"IndexClickCommand",leaf);Pump();
            Require(writes==2&&field.Setting is ReferenceSetting {Reference.Selection:BindingSelection.AtField dynamic}&&dynamic.Reference.Type.Shape==ValueShape.Sequence,"模块整数数组索引未显式传消费行为");
            using var strict=new DeclaredFieldEditor(target,"严格整数",new ReferenceSetting(source.Reference(new BindingSelection.AtConstant(0))),_=>throw new Exception("严格字段不应写入"),
                new ReferenceFieldOptions(_=>snapshot,EditableIndex:true),new ConstantSetting(7L),presentation:presentation);Show(strict);var strictSession=Session((DeclaredFieldAccess)Activator.CreateInstance(typeof(DeclaredFieldAccess),BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{strict},null)!);Command(strictSession,"DropDownCommand");
            Require(!Nodes(strictSession,"IndexNodes").Any(n=>Property(n,"Candidate") is DeclaredReferenceCandidate),"默认Strict泄露整数数组索引");
            var strictText=(string)Property(strictSession,"CurValueA")!;strictSession.GetType().GetProperty("CurValueA")!.SetValue(strictSession,System.Text.RegularExpressions.Regex.Replace(strictText,"\\[[^\\]]*\\]$","[]"));Pump();
            Require(strict.HasInvalidText&&strict.Setting is ReferenceSetting {Reference.Selection:BindingSelection.AtConstant},"Strict多项All[]未保持旧接纳值");
            var invalid=new ReferenceSetting(source.Reference(new BindingSelection.AtField(index.Reference(BindingSelection.All),first)));
            var accepted=(bool)typeof(DeclaredFieldEditor).GetMethod("TryApplySetting",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(strict,new object[]{invalid})!;
            Require(!accepted&&strict.Setting is ReferenceSetting {Reference.Selection:BindingSelection.AtConstant},"程序提案绕过冻结Strict索引校验");
            Show(new TextBlock());Trace("PASS_BEHAVIOR_CONTROLS 冻结/All/模块数组索引/Strict/程序拒绝");
        }
        void VerifyBehaviorHost((SunEyeVision.UI.Shared.WorkflowNode Target,string SourceId,string WidthPort) fixture)
        {
            var window=Open(fixture.Target);var control=Control(window);var fields=Fields(window);fields.SelectedPageIndex=1;Pump();
            var states=((System.Collections.IEnumerable)typeof(DeclaredFieldsView).GetField("_fieldStates",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(fields)!).Cast<object>().ToArray();
            var expected=new[]{"Threshold","UpperThreshold","BlobCountLimit","FilterAreaMin","FilterAreaMax"}.Select(Field).ToHashSet(StringComparer.Ordinal);
            foreach(var state in states)
            {
                var port=(Port)Property(state,"Port")!;var actual=(ParameterReferenceBehavior)Property(state,"ReferenceBehavior")!;
                Require(actual==fixture.Target.RuntimeConfiguration!.GetParameterReferenceBehavior(port.Id)&&actual==(expected.Contains(port.Id)?ParameterReferenceBehavior.FirstItemWithIndexReadZeroFallback:ParameterReferenceBehavior.StrictExactlyOne),"实际UI字段行为未来自唯一正式getter："+port.Id);
            }
            Require(expected.All(id=>states.Any(s=>((Port)Property(s,"Port")!).Id==id)),"未覆盖完整五整数");
            var access=fields.Fields.Single(f=>f.FieldId==low);var session=Session(access);Command(session,"DropDownCommand");
            void SelectArray(bool isIndex)
            {
                Exception? failure=null;var entered=false;var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(30)};
                timer.Tick+=(_,_)=>{var dialog=Application.Current.Windows.OfType<VariableSelectionDialog>().SingleOrDefault(d=>d.IsVisible);if(dialog is null)return;timer.Stop();entered=true;
                    try{var request=(VariableSelectionRequest)session.GetType().GetField("_variableRequest",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(session)!;Require(request.IsValid&&request.IsIndex==isIndex,"真实变量模态用途失效");
                        var array=request.Items.Single(item=>item.Reference.Type.Shape==ValueShape.Sequence);((ListView)dialog.FindName("globalVarList")).SelectedItem=array;
                        CrossVisuals(dialog).OfType<Button>().Single(b=>Equals(b.Content,dialog.TryFindResource("Confirm"))).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));}
                    catch(Exception error){failure=error;dialog.Close();}};
                timer.Start();try{Command(session,isIndex?"IndexClickCommand":"ClickCommand",Nodes(session,isIndex?"IndexNodes":"SourceNodes").Single(n=>(bool)Property(n,"VariableManager")!));}finally{timer.Stop();}
                if(failure is not null)throw failure;Require(entered,"未进入真实变量模态");Pump();
            }
            SelectArray(false);Require(access.Setting is VariableSetting {Reference.Selection:BindingSelection.AllItems},"生产字段未接纳整数数组All");
            SelectArray(true);Require(access.Setting is VariableSetting {Reference.Selection:BindingSelection.AtVariable dynamic}&&dynamic.Reference.Type.Shape==ValueShape.Sequence,"生产变量数组索引未显式传行为");
            var context=(ProductionEditorContext)ToolEditorCommit.GetDraftContext(control)!;manager.Session!.Editing.Host.Validate((NodeConfigurationEditDraft)Property(context,"PreparedDraft")!);
            Require(fixture.Target.RuntimeConfiguration!.Fields.Read(low) is ReferenceSetting,"UI私有提案越过正式确认");
            window.Close();Pump();Trace("PASS_BEHAVIOR_HOST 五字段/唯一getter/真实变量All与数组索引/私有草稿");
        }
        void VerifyWindowConsumers()
        {
            var panel=new StackPanel();var current=true;var writes=0;var preference=new ReferenceExpansionPreference{PreferExpanded=true};
            using var root=new ReferenceWindowOwner(panel,()=>current,preference,true);
            var integer=new DeclaredReferenceCandidate("整数","ui-producer",null,new Port("n",DataType.Integer),Array.Empty<string>());
            var image=new DeclaredReferenceCandidate("图像","ui-producer",null,new Port("image",TypeRef.ResourceOf(ResourceKind.Image)),Array.Empty<string>());
            var references=new ReferenceFieldOptions(port=>new(new[]{port.Type.Shape==ValueShape.Resource?image:integer},new[]{integer}),WindowOwner:root);
            FieldPresentation Layout(string id,int order,string? pair=null)=>new(id,id,"参数",order,FieldEditorKind.Scalar){Layout=new("参数","参数",PairWith:pair,Minimum:0,Maximum:100,DecimalPlaces:0)};
            var declarations=new[]{
                (new Port("number",DataType.Integer,EditModes:FieldEditModes.Constant|FieldEditModes.Reference),Layout("number",0),(ParameterSetting)new ReferenceSetting(integer.Reference(BindingSelection.All))),
                (new Port("lower",DataType.Integer,EditModes:FieldEditModes.Constant|FieldEditModes.Reference),Layout("lower",1,"upper"),(ParameterSetting)new ReferenceSetting(integer.Reference(BindingSelection.All))),
                (new Port("upper",DataType.Integer,EditModes:FieldEditModes.Constant|FieldEditModes.Reference),Layout("upper",2),(ParameterSetting)new ReferenceSetting(integer.Reference(BindingSelection.All))),
                (new Port("image",TypeRef.ResourceOf(ResourceKind.Image),EditModes:FieldEditModes.Reference),Layout("image",3),(ParameterSetting)new ReferenceSetting(image.Reference(BindingSelection.All)))};
            using var fields=new DeclaredFieldsView(declarations,_=>writes++,references,constantBackup:id=>id=="image"?null:new(10L));panel.Children.Add(fields);Show(panel);Pump();
            var window=owner.OwnedWindows.OfType<SunEyeVision.Plugin.Wpf.Controls.Parameters.ParamSubscribeWindow>().Single(w=>w.IsVisible);
            Require(root.IsExpanded&&root.CurrentRequest is null&&ReferenceEquals(window.DataContext,root)&&window.Width==(System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName=="zh"?240:320),"独立同组装链默认true空窗/原资源语言宽度不符");
            var numeric=Session(fields.Fields.Single(f=>f.FieldId=="number"));Command(numeric,"DropDownCommand");Pump();var numberRequest=root.CurrentRequest!;
            var range=fields.Fields.Single(f=>f.FieldId=="lower").View;var rangeSessions=(Array)range.GetType().GetField("_references",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(range)!;
            var native=(FrameworkElement)range.GetType().GetProperty("NativeView")!.GetValue(range)!;var rangeBox=(ComboBox)native.FindName("CBox");rangeBox.SetCurrentValue(ComboBox.IsDropDownOpenProperty,true);Pump();
            var rangeSession=rangeSessions.GetValue(0)!;Command(rangeSession,"DropDownCommand");Pump();
            Require(root.CurrentRequest is {IsValid:true} ranged&&ranged.Target.Id=="lower"&&!numberRequest.IsValid&&window.IsVisible,"真实范围端点未借用同Root或未退役普通数值目标");
            var imageEditor=fields.Fields.Single(f=>f.FieldId=="image").View;var presenter=imageEditor.GetType().GetField("_referencePresenter",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(imageEditor)!;
            var pure=presenter.GetType().GetField("_session",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(presenter)!;Command(pure,"PopSubscribeWindowCommand");Pump();
            Require(root.CurrentRequest is {IsValid:true} pureRequest&&pureRequest.Target.Id=="image"&&owner.OwnedWindows.OfType<SunEyeVision.Plugin.Wpf.Controls.Parameters.ParamSubscribeWindow>().Single(w=>w.IsVisible)==window&&writes==0,"纯引用未与数值/范围共用同窗或打开即写入");
            var beforeHide=root.CurrentRequest!;fields.Visibility=Visibility.Collapsed;Pump();
            Require(window.IsVisible&&root.IsExpanded&&root.CurrentRequest is null&&!beforeHide.IsValid&&writes==0,"字段隐藏没有保持空Root/退役字段或意外写入");fields.Visibility=Visibility.Visible;Pump();
            panel.Visibility=Visibility.Collapsed;Pump();Require(!window.IsVisible&&!root.IsExpanded&&preference.PreferExpanded,"整个Root隐藏没有关窗或错误清默认");panel.Visibility=Visibility.Visible;Pump();
            Command(numeric,"PopSubscribeWindowCommand");Pump();var prior=root.CurrentRequest!;var invoked=false;
            prior.Invalidated+=()=>{if(invoked)return;invoked=true;Command(pure,"PopSubscribeWindowCommand");};Command(rangeSession,"PopSubscribeWindowCommand");Pump();
            Require(invoked&&!prior.IsValid&&root.CurrentRequest is {IsValid:true} newer&&newer.Target.Id=="image","旧目标清理重入被外层覆盖/关闭");
            var primary=new InvalidOperationException("Root投影失败标记");var cleanup=new InvalidOperationException("Root清理失败标记");var armed=true;ReferenceExpansionRequest? failed=null;
            root.PropertyChanged+=(_,args)=>{if(armed&&args.PropertyName=="CurrentRequest"&&root.CurrentRequest is {} request){armed=false;failed=request;request.Invalidated+=()=>throw cleanup;throw primary;}};
            Exception? failure=null;try{Command(numeric,"PopSubscribeWindowCommand");}catch(Exception error){failure=error;}
            IEnumerable<Exception> Errors(Exception error)=>error is AggregateException aggregate?aggregate.InnerExceptions.SelectMany(Errors):new[]{error};
            Require(failure is not null&&Errors(failure).Any(e=>ReferenceEquals(e,primary))&&Errors(failure).Any(e=>ReferenceEquals(e,cleanup))&&failed?.IsValid==false&&root.CurrentRequest is null&&!root.IsExpanded,"Root投影/清理异常未保留或未完整退役");
            Command(pure,"PopSubscribeWindowCommand");Pump();Require(root.CurrentRequest is {IsValid:true},"异常清理后同Root不能恢复");
            var live=root.CurrentRequest!;current=false;root.Dispose();live.RefreshCommand.Execute(null);root.PreferExpanded=false;
            Require(!live.IsValid&&!root.IsCurrent&&preference.PreferExpanded&&writes==0,"失效Root迟到回调改默认/值或未退役");Show(new TextBlock());Trace("PASS_WINDOW_CONSUMERS 数值/范围/纯引用/隐藏/异常/重入");
        }
        ReferenceWindowOwner Root(DefaultDebugWindow window)=>(ReferenceWindowOwner)typeof(RuntimeNodeConfigurationControl).GetField("_referenceWindowOwner",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(Control(window))!;
        SunEyeVision.Plugin.Wpf.Controls.Parameters.ParamSubscribeWindow Subscription(DefaultDebugWindow window)=>window.OwnedWindows.OfType<SunEyeVision.Plugin.Wpf.Controls.Parameters.ParamSubscribeWindow>().Single(w=>w.IsVisible);
        void VerifyWindowSession(string transition,(SunEyeVision.UI.Shared.WorkflowNode Target,string SourceId,string WidthPort) fixture,string pathA)
        {
            var document=manager.CurrentSolution!;var editing=manager.Session!.Editing;Trace("ROOT_OPEN_EDITOR "+transition);var first=Open(fixture.Target);Trace("ROOT_OPENED_EDITOR "+transition);var control=Control(first);
            var context=(ProductionEditorContext)ToolEditorCommit.GetDraftContext(control)!;var root=Root(first);var fields=Fields(first);fields.SelectedPageIndex=1;Pump();
            var bytes=File.ReadAllBytes(pathA);var stamp=Stamp(fixture.Target.RuntimeConfiguration!);var history=manager.Session.History.Position;var dirty=manager.HasUnsavedChanges;
            Require(!root.IsExpanded&&!root.PreferExpanded&&!first.OwnedWindows.OfType<SunEyeVision.Plugin.Wpf.Controls.Parameters.ParamSubscribeWindow>().Any(),"新文档临时默认被其他文档/构造期owner污染");
            var lowAccess=fields.Fields.Single(f=>f.FieldId==low);var lowSession=Session(lowAccess);Command(lowSession,"PopSubscribeWindowCommand");Pump();
            var window=Subscription(first);var request=root.CurrentRequest!;var oldLeaf=ModuleLeaf(request,"SourceNodes",fixture.SourceId,fixture.WidthPort);
            Require(ReferenceEquals(window.DataContext,root)&&root.IsExpanded&&!root.PreferExpanded&&request.IsValid,"显式展开错误改变默认或未绑定真实Root");
            var countAccess=fields.Fields.Single(f=>f.FieldId==count);var countSession=Session(countAccess);Command(countSession,"DropDownCommand");Pump();
            Require(ReferenceEquals(Subscription(first),window)&&!request.IsValid&&root.CurrentRequest is {IsValid:true} next&&next.Target.Id==count,"默认false时切换字段未按当前Root展开/未退役旧目标");
            var check=CrossVisuals(window).OfType<CheckBox>().Single(c=>c.GetBindingExpression(ToggleButton.IsCheckedProperty)?.ParentBinding.Path?.Path=="PreferExpanded");
            check.SetCurrentValue(ToggleButton.IsCheckedProperty,true);Pump();Require(root.PreferExpanded&&root.IsExpanded,"默认方式错误改变当前Root");
            var targetBeforeRebind=root.CurrentRequest!;Trace("ROOT_REBIND "+transition);control.BindDraft(context.DraftNode);Pump();
            Require(ReferenceEquals(Root(first),root)&&ReferenceEquals(Subscription(first),window)&&root.CurrentRequest is null&&!targetBeforeRebind.IsValid,"BindDraft重建Root/窗口或保留旧字段目标");
            Require(!root.ClickCommand.CanExecute(oldLeaf)&&!root.IndexClickCommand.CanExecute(oldLeaf)&&!root.RefreshCommand.CanExecute(null)&&!root.SourceNodes.Cast<object>().Any(),"空窗仍可写来源/刷新或借了假字段目标");
            root.ClickCommand.Execute(oldLeaf);request.ClickCommand.Execute(oldLeaf);targetBeforeRebind.RefreshCommand.Execute(null);
            Require(history==manager.Session.History.Position&&dirty==manager.HasUnsavedChanges&&stamp==Stamp(fixture.Target.RuntimeConfiguration!)&&bytes.SequenceEqual(File.ReadAllBytes(pathA)),"窗口模式/默认投影进入参数、Dirty、历史或写盘");
            window.Close();Pump();Require(!root.IsExpanded&&root.PreferExpanded&&root.CurrentRequest is null,"关窗未仅清当前开关/目标");
            fields=Fields(first);fields.SelectedPageIndex=1;Pump();lowAccess=fields.Fields.Single(f=>f.FieldId==low);lowSession=Session(lowAccess);Command(lowSession,"DropDownCommand");Pump();
            var inline=Popup(lowAccess,lowSession,"IsDropDownOpen");inline.ApplyTemplate();inline.Focus();Pump();Require(inline.IsKeyboardFocusWithin,"关窗后inline原按钮没有取得真实键盘焦点");inline.SetCurrentValue(ComboBox.IsDropDownOpenProperty,true);Pump();
            Trace("ROOT_INLINE "+transition+" focus="+inline.IsKeyboardFocusWithin+" dropdown="+inline.IsDropDownOpen+" popup="+(inline.Template.FindName("PART_Popup",inline) is Popup{IsOpen:true})+" expanded="+root.IsExpanded+" owned="+first.OwnedWindows.OfType<SunEyeVision.Plugin.Wpf.Controls.Parameters.ParamSubscribeWindow>().Count());
            Require(inline.IsDropDownOpen&&inline.Template.FindName("PART_Popup",inline) is Popup{IsOpen:true}&&!root.IsExpanded&&!first.OwnedWindows.OfType<SunEyeVision.Plugin.Wpf.Controls.Parameters.ParamSubscribeWindow>().Any(),"关窗后错误按默认true重新展开或inline未真实打开");
            first.Close();Pump();Require(!root.IsCurrent&&!context.IsActive,"关闭配置窗口未退役Root/context");
            Trace("ROOT_REOPEN_EDITOR "+transition);var reopened=Open(fixture.Target);var liveRoot=Root(reopened);var automaticWindow=Subscription(reopened);
            Require(!ReferenceEquals(root,liveRoot)&&liveRoot.IsExpanded&&liveRoot.PreferExpanded&&liveRoot.CurrentRequest is null&&ReferenceEquals(automaticWindow.DataContext,liveRoot),"同会话新Root未按默认true自动开空窗或构造临时owner抢先开窗");
            Require(reopened.OwnedWindows.OfType<SunEyeVision.Plugin.Wpf.Controls.Parameters.ParamSubscribeWindow>().Count()==1&&automaticWindow.Width==(System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName=="zh"?240:320),"重复空窗/中英原宽度错误");
            var bitmap=new RenderTargetBitmap((int)automaticWindow.ActualWidth,(int)automaticWindow.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(automaticWindow);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var png=File.Create(Path.Combine(directory,"automatic-empty-"+System.Globalization.CultureInfo.CurrentUICulture.Name+"-"+transition+".png")))encoder.Save(png);
            Require(liveRoot.PreferenceScopeDescription.Contains(System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName=="zh"?"临时":"Temporary",StringComparison.Ordinal),"未标注会话临时边界");
            root.PreferExpanded=false;root.Close();root.Dispose();request.RefreshCommand.Execute(null);
            Require(liveRoot.IsExpanded&&liveRoot.PreferExpanded&&automaticWindow.IsVisible&&stamp==Stamp(fixture.Target.RuntimeConfiguration!)&&history==manager.Session.History.Position&&dirty==manager.HasUnsavedChanges,"旧Root回调改写/关闭同会话新Root或影响正式模型");
            var liveFields=Fields(reopened);liveFields.SelectedPageIndex=1;Pump();var liveSession=Session(liveFields.Fields.Single(f=>f.FieldId==low));Command(liveSession,"PopSubscribeWindowCommand");Pump();var oldRequest=liveRoot.CurrentRequest!;
            Require(oldRequest.IsValid,"文档切换前无真实活跃目标");
            Trace("ROOT_SWITCH_DOCUMENT "+transition);Solution destination;
            if(transition=="reopen-A"){manager.CloseSolution(false);destination=manager.OpenSolution(pathA)!;}else destination=manager.CreateUntitledSolution();Pump();
            Require(!liveRoot.IsCurrent&&!oldRequest.IsValid&&!automaticWindow.IsVisible&&manager.Session!.Editing.SessionId!=editing.SessionId&&editing.RegisteredScopeCount==0,"文档切换未退役Root/目标或重用了编辑会话");
            var target=transition=="reopen-A"?(SunEyeVision.UI.Shared.WorkflowNode)vm.WorkflowTabViewModel.SelectedTab!.WorkflowNodes.Single(n=>n.Id==fixture.Target.Id):Populate().Target;
            Require(transition!="reopen-A"||destination.Id==document.Id&&target.Id==fixture.Target.Id,"同ID真实重开前提错误");
            Trace("ROOT_OPEN_DESTINATION "+transition);var current=Open(target);Trace("ROOT_OPENED_DESTINATION "+transition);var currentRoot=Root(current);Require(!currentRoot.PreferExpanded&&!currentRoot.IsExpanded&&!current.OwnedWindows.OfType<SunEyeVision.Plugin.Wpf.Controls.Parameters.ParamSubscribeWindow>().Any(),"会话临时默认跨文档/同ID新会话泄漏");
            var currentFields=Fields(current);currentFields.SelectedPageIndex=1;Pump();var currentSession=Session(currentFields.Fields.Single(f=>f.FieldId==low));Command(currentSession,"PopSubscribeWindowCommand");Pump();var currentRequest=currentRoot.CurrentRequest!;var currentWindow=Subscription(current);
            var newFormal=Stamp(target.RuntimeConfiguration!);var newHistory=manager.Session.History.Position;liveRoot.PreferExpanded=false;liveRoot.Close();liveRoot.Dispose();oldRequest.RefreshCommand.Execute(null);oldRequest.ClickCommand.Execute(oldLeaf);
            Require(currentRoot.IsCurrent&&currentRoot.IsExpanded&&currentRequest.IsValid&&currentWindow.IsVisible&&!currentRoot.PreferExpanded&&newFormal==Stamp(target.RuntimeConfiguration!)&&newHistory==manager.Session.History.Position&&bytes.SequenceEqual(File.ReadAllBytes(pathA)),"旧文档Root迟到回调影响当前文档/窗口/模型/磁盘");
            Trace("ROOT_HIDE "+transition);current.Hide();Pump();Trace("ROOT_HIDDEN "+transition);Require(!currentRoot.IsExpanded&&!currentWindow.IsVisible&&!currentRequest.IsValid,"整个Root隐藏未关窗或退役目标");current.Close();Pump();
            Require(!currentRoot.IsCurrent,"整个Root关闭后仍允许回调");Trace("PASS_WINDOW_SESSION "+transition);
        }
        try
        {
            if(behaviorUiOnly)VerifyBehaviorControls();
            if(windowSessionOnly)VerifyWindowConsumers();
            foreach(var transition in transitions)foreach(var kind in kinds)
            {
                Trace("BEGIN "+transition+" "+kind);manager.CreateUntitledSolution();Pump();Trace("POPULATE "+kind);var a=Populate();Trace("POPULATED "+kind);var oldDocument=manager.CurrentSolution!;var oldSession=manager.Session!;var oldEditing=oldSession.Editing;
                var pathA=Path.Combine(directory,transition+"-"+kind+"-A.solution");Require(manager.SaveCurrentDocument(pathA).FileWritten,"原方案保存前提失败");
                if(kind=="image-combo"){VerifyImageCombo(transition,a,pathA);reports.Add(new{transition,kind,passed=true});continue;}
                if(languageLayoutOnly){VerifyLanguageLayout(a);reports.Add(new{transition,kind,passed=true});continue;}
                if(behaviorUiOnly){VerifyBehaviorHost(a);reports.Add(new{transition,kind,passed=true});continue;}
                if(windowSessionOnly){VerifyWindowSession(transition,a,pathA);reports.Add(new{transition,kind,windowRoot=true,passed=true});continue;}
                var oldBytes=File.ReadAllBytes(pathA);var formalBefore=Stamp(a.Target.RuntimeConfiguration!);
                Trace("OPEN "+kind);var oldWindow=Open(a.Target);Trace("OPENED "+kind);var oldControl=Control(oldWindow);var oldContext=(ProductionEditorContext)ToolEditorCommit.GetDraftContext(oldControl)!;
                var oldFields=Fields(oldWindow);oldFields.SelectedPageIndex=1;Pump();
                var oldAccess=oldFields.Fields.Single(f=>f.FieldId==(kind is "index-popup" or "variable-index"?count:low));
                var session=Session(oldAccess);Command(session,"DropDownCommand");Pump();
                var oldText=(string)Property(session,"CurValueA")!;
                object? staleLeaf=null;AppsPopup? stalePopup=null;ReferenceExpansionRequest? staleExpanded=null;Window? staleExpandedWindow=null;
                VariableSelectionRequest? staleVariable=null;VariableSelectionItem? staleVariableItem=null;VariableSelectionDialog? staleDialog=null;
                Solution? destination=null;Exception? nestedFailure=null;var switched=false;
                void Switch()
                {
                    Console.WriteLine("SWITCH "+transition+" "+kind);
                    if(transition=="reopen-A")
                    {
                        manager.CloseSolution(saveChanges:false);destination=manager.OpenSolution(pathA);Require(destination is not null,"真实文件重开失败");
                    }
                    else destination=manager.CreateUntitledSolution();
                    switched=true;Pump();Console.WriteLine("SWITCHED "+transition+" "+kind);
                }
                if(kind=="source-popup")
                {
                    stalePopup=Popup(oldAccess,session,"IsDropDownOpen");stalePopup.Focus();stalePopup.SetCurrentValue(ComboBox.IsDropDownOpenProperty,true);Pump();
                    staleLeaf=ModuleLeaf(session,"SourceNodes",a.SourceId,a.WidthPort);
                    Require(stalePopup.IsDropDownOpen&&stalePopup.Template.FindName("PART_Popup",stalePopup) is Popup{IsOpen:true},"原主来源Popup未真实打开");Switch();
                }
                else if(kind is "index-popup" or "variable-index")
                {
                    oldAccess=oldFields.Fields.Single(f=>f.FieldId==count);session=Session(oldAccess);Command(session,"DropDownCommand");oldText=(string)Property(session,"CurValueA")!;
                    var text=CrossVisuals(oldAccess.View).OfType<NativeText>().Single(t=>ReferenceEquals(t.DataContext,session));
                    text.Focus();text.CaretIndex=oldText.LastIndexOf('[')+1;Pump();
                    Command(session,"FocusedCommand",new SunEyeVision.Plugin.Wpf.Interop.Windows.TextBoxFocusEventArgs{Text=oldText,IsFocus=true,SelectionStart=oldText.LastIndexOf('[')+1});Pump();
                    if(kind=="index-popup")
                    {
                        stalePopup=Popup(oldAccess,session,"IsTextDropDownOpen");staleLeaf=Nodes(session,"IndexNodes").First(n=>Property(n,"Candidate") is DeclaredReferenceCandidate c&&c.NodeId==a.SourceId);
                        Require(stalePopup.IsDropDownOpen&&stalePopup.Template.FindName("PART_Popup",stalePopup) is Popup{IsOpen:true},"原索引Popup未真实打开");Switch();
                    }
                }
                if(kind=="expanded")
                {
                    Command(session,"PopSubscribeWindowCommand");Pump();staleExpandedWindow=oldWindow.OwnedWindows.OfType<SunEyeVision.Plugin.Wpf.Controls.Parameters.ParamSubscribeWindow>().Single(w=>w.IsVisible);
                    staleExpanded=((ReferenceWindowOwner)staleExpandedWindow.DataContext).CurrentRequest!;staleLeaf=ModuleLeaf(staleExpanded,"SourceNodes",a.SourceId,a.WidthPort);
                    Require(staleExpanded.IsValid,"展开请求不是有效租约");Switch();
                }
                if(kind is "variable-source" or "variable-index")
                {
                    var isIndex=kind=="variable-index";var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(30)};
                    timer.Tick+=(_,_)=>
                    {
                        var dialog=app.Windows.OfType<VariableSelectionDialog>().SingleOrDefault(w=>w.IsVisible);if(dialog is null)return;timer.Stop();Trace("MODAL_VISIBLE "+kind);
                        try
                        {
                            staleDialog=dialog;staleVariable=(VariableSelectionRequest)session.GetType().GetField("_variableRequest",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(session)!;
                            Require(staleVariable.IsValid&&staleVariable.IsIndex==isIndex&&ReferenceEquals(dialog.Owner,oldWindow),"真实变量模态/用途/Owner前提不符");
                            staleVariableItem=staleVariable.Items.First(item=>item.Reference.Type==DataType.Integer);
                            ((ListView)dialog.FindName("globalVarList")).SelectedItem=staleVariableItem;Switch();
                        }
                        catch(Exception error){nestedFailure=error;dialog.Close();}
                    };
                    timer.Start();Trace("MODAL_COMMAND "+kind);try{Command(session,isIndex?"IndexClickCommand":"ClickCommand",Nodes(session,isIndex?"IndexNodes":"SourceNodes").Single(n=>(bool)Property(n,"VariableManager")!));}
                    finally{timer.Stop();}
                    if(nestedFailure is not null)throw new Exception("模态中的真实方案切换失败",nestedFailure);
                }
                Require(switched&&destination is not null&&!ReferenceEquals(manager.Session,oldSession)&&!ReferenceEquals(manager.Session!.Editing,oldEditing)
                    &&manager.Session.Editing.SessionId!=oldEditing.SessionId&&oldEditing.RegisteredScopeCount==0&&!oldContext.IsActive&&!oldWindow.IsVisible,"换方案未退役旧真实编辑器/DocumentSession");
                Require(stalePopup is null||!stalePopup.IsDropDownOpen&&stalePopup.Template.FindName("PART_Popup",stalePopup) is not Popup{IsOpen:true},"换方案遗留真实原Popup");
                Require(staleExpanded is null||!staleExpanded.IsValid&&staleExpandedWindow?.IsVisible!=true,"换方案遗留有效展开窗");
                Require(staleVariable is null||!staleVariable.IsValid&&staleDialog?.IsVisible!=true,"换方案遗留有效变量模态");
                Require(oldBytes.SequenceEqual(File.ReadAllBytes(pathA)),"方案切换/取消擅自写旧文件");
                var b=transition=="reopen-A"?(Target:(SunEyeVision.UI.Shared.WorkflowNode)vm.WorkflowTabViewModel.SelectedTab!.WorkflowNodes.Single(n=>n.Id==a.Target.Id),SourceId:a.SourceId,WidthPort:a.WidthPort):Populate();
                Require(transition!="reopen-A"||destination!.Id==oldDocument.Id&&b.Target.Id==a.Target.Id,"同ID新会话的真实重开前提不成立");
                var loadedImage=(ReferenceSetting)b.Target.RuntimeConfiguration!.Fields.Read(b.Target.RuntimeConfiguration.Schema.WorkflowImageInputId!);
                Require(loadedImage.Reference.NodeId==b.SourceId&&loadedImage.Reference.ScopeId==vm.WorkflowTabViewModel.SelectedTab!.CurrentWorkflowId&&loadedImage.OriginId is not null&&destination!.Workflows.SelectMany(w=>w.Connections).Any(e=>loadedImage.OriginId=="connection:"+e.Id&&e.SourceNodeId==b.SourceId&&e.TargetNodeId==b.Target.Id)&&b.Target.ParamSettings.Count==0,"保存重开/新方案没有保持正式图像来源及真实连线归属");
                var pathB=transition=="reopen-A"?pathA:Path.Combine(directory,kind+"-B.solution");Require(manager.SaveCurrentDocument(pathB).FileWritten,"目标方案保存前提失败");
                var nextWindow=Open(b.Target);var nextControl=Control(nextWindow);var nextContext=(ProductionEditorContext)ToolEditorCommit.GetDraftContext(nextControl)!;
                var nextFields=Fields(nextWindow);nextFields.SelectedPageIndex=1;Pump();
                var nextLow=nextFields.Fields.Single(f=>f.FieldId==low);var nextSession=Session(nextLow);Command(nextSession,"PopSubscribeWindowCommand");Pump();
                var nextExpandedWindow=nextWindow.OwnedWindows.OfType<SunEyeVision.Plugin.Wpf.Controls.Parameters.ParamSubscribeWindow>().Single(w=>w.IsVisible);
                var nextExpanded=((ReferenceWindowOwner)nextExpandedWindow.DataContext).CurrentRequest!;Require(nextExpanded.IsValid,"目标方案无有效展开前提");
                var nextDraft=Stamp(nextContext.DraftNode.RuntimeConfiguration!);var nextFormal=Stamp(b.Target.RuntimeConfiguration!);var history=manager.Session!.History.Position;
                var version=manager.Session.Editing.Host.Capture().Version;var registered=manager.Session.Editing.RegisteredScopeCount;Require(registered>0,"新方案真实编辑范围未登记");var saved=File.ReadAllBytes(pathB);var savedOld=File.ReadAllBytes(pathA);var pending=nextControl.HasPendingEdits;var invalid=nextFields.HasInvalidActiveFields;
                if(staleLeaf is not null)
                {
                    Command(session,kind=="index-popup"?"IndexClickCommand":"ClickCommand",staleLeaf);
                    staleExpanded?.ClickCommand.Execute(staleLeaf);staleExpanded?.IndexClickCommand.Execute(staleLeaf);
                }
                session.GetType().GetProperty("CurValueA")!.SetValue(session,oldText.Replace("[0]","[2]",StringComparison.Ordinal));
                Command(session,"DropDownCommand");session.GetType().GetMethod("TryCommitPending")!.Invoke(session,null);
                Require(!oldAccess.TrySetConstant(222L),"旧字段访问仍可接纳常量");
                staleExpanded?.RefreshCommand.Execute(null);staleExpanded?.Dispose();
                if(staleVariable is not null)
                {
                    Require(!staleVariable.TrySelect(staleVariableItem!),"迟到变量请求仍可接纳");
                    staleDialog!.GetType().GetMethod("ConfirmSelection",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(staleDialog,null);staleVariable.Dispose();
                }
                Pump();
                Require(nextContext.IsValid&&nextExpanded.IsValid&&nextExpandedWindow.IsVisible&&nextWindow.IsVisible,"旧关闭/回调破坏新会话窗口或租约");
                Require(nextDraft==Stamp(nextContext.DraftNode.RuntimeConfiguration!)&&nextFormal==Stamp(b.Target.RuntimeConfiguration!)
                    &&formalBefore==Stamp(a.Target.RuntimeConfiguration!)&&history==manager.Session.History.Position&&version==manager.Session.Editing.Host.Capture().Version
                    &&registered==manager.Session.Editing.RegisteredScopeCount&&pending==nextControl.HasPendingEdits&&invalid==nextFields.HasInvalidActiveFields&&saved.SequenceEqual(File.ReadAllBytes(pathB))&&savedOld.SequenceEqual(File.ReadAllBytes(pathA)),
                    "旧来源/索引/模态/文本迟到回调改变草稿、正式、版本、历史、状态或磁盘");
                Require(unexpected.Count==0,"出现意外产品提示："+string.Join(" | ",unexpected));
                reports.Add(new{transition,kind,oldDocumentId=oldDocument.Id,newDocumentId=destination!.Id,oldEditing=oldEditing.SessionId,newEditing=manager.Session.Editing.SessionId,
                    oldNodeId=a.Target.Id,newNodeId=b.Target.Id,realWindowOwnerRecreated=!ReferenceEquals(oldWindow,nextWindow),passed=true,fullFunctionalAcceptance=false});
                Console.WriteLine("通过：真实MainWindow "+transition+" "+kind+" 退役，迟到动作不改新草稿/正式/历史/磁盘且不关闭新展开窗");
                manager.CloseSolution(saveChanges:false);Pump();
            }
            Require(reports.Count==transitions.Length*kinds.Length,"新增跨方案场景未全部执行");return 0;
        }
        catch(Exception failure){Console.Error.WriteLine("B08跨文档首个失败："+failure);throw;}
        finally
        {
            dialogs.Stop();manager.CloseSolution(saveChanges:false);Pump();
            File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"b08-cross-document-report.json"),
                System.Text.Json.JsonSerializer.Serialize(new{scope=languageLayoutOnly?"B08OriginalLanguageLayout":behaviorUiOnly?"B08ReferenceBehaviorUi":windowSessionOnly?"B08WindowSession":"B08CrossDocumentReferences",imageChainVerified,selectedTransition,selectedKinds,expected=transitions.Length*kinds.Length,cases=reports,passed=reports.Count,fullFieldEditingRegression=false,fullFunctionalAcceptance=false,vsDesigner=false,nativeVmSemanticsProved=false},new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
        }
    }
    private static IEnumerable<DependencyObject> CrossVisuals(DependencyObject root)
    {
        var seen=new HashSet<DependencyObject>();var stack=new Stack<DependencyObject>();stack.Push(root);
        while(stack.Count>0)
        {
            var node=stack.Pop();if(!seen.Add(node))continue;yield return node;
            foreach(var value in LogicalTreeHelper.GetChildren(node))if(value is DependencyObject logical)stack.Push(logical);
            if(node is Popup{Child:{} popup})stack.Push(popup);
            if(node is Visual)for(var i=0;i<VisualTreeHelper.GetChildrenCount(node);i++)stack.Push(VisualTreeHelper.GetChild(node,i));
        }
    }
    private static void VerifyBlobPage(System.Xml.Linq.XDocument xml,FieldSettings initial)
    {
        string Id(string name)=>(string)xml.Descendants("Parameter").Single(e=>(string?)e.Attribute("id")==name).Attribute("fieldId")!;
        var expected=new Dictionary<string,object>{{"Threshold",100L},{"UpperThreshold",150L},{"BlobCountLimit",100L},
            {"FilterAreaMin",10L},{"FilterAreaMax",999999999L},{"FilterCircularityMin",.1d},{"FilterCircularityMax",1d},
            {"FilterRectangularityMin",.1d},{"FilterRectangularityMax",1d}};
        using(var view=ToolRegistry.CreateSettingsView("BlobAnalysis",initial,new(ReferenceOptions:ReferenceFieldOptions.Empty)))
        {
            Show(view.View);var fields=Descendants(view.View).OfType<DeclaredFieldsView>().Single();fields.SelectedPageIndex=1;Pump();
            Require(!Descendants(view.View).OfType<SunEyeVision.Plugin.Wpf.Presenters.Parameters.NativeThresholdUiDraftControls>().Any(),"真实页面仍保留S1阈值替身");
            foreach(var (name,value) in expected)
            {
                var field=fields.Fields.Single(f=>f.FieldId==Id(name));
                Require(field.Setting is ConstantSetting c&&Equals(c.Value,value),"九字段初值没有消费正式声明："+name);
            }
            Require(fields.Fields.Where(f=>expected.Keys.Select(Id).Contains(f.FieldId)).Select(f=>f.FieldId).SequenceEqual(expected.Keys.Select(Id)),"九字段声明顺序被页面替身改写");
            var placeholders=NativeDescendants(view.View).OfType<DeclaredFieldEditor>().Where(e=>e.FieldId.StartsWith("ui-only-run.")).ToArray();
            Require(placeholders.Length>0&&placeholders.All(e=>!e.IsEnabled&&e.ToolTip is string),"未接入试用行仍可误编辑或没有说明");
            var mode=fields.Fields.Single(f=>f.FieldId==Id("SegmentationMode"));var combo=(ComboBox)mode.InputElement!;
            combo.IsDropDownOpen=true;Pump();combo.UpdateLayout();
            Require(combo.Items.Count==6&&Enumerable.Range(0,6).Count(i=>((ComboBoxItem)combo.ItemContainerGenerator.ContainerFromIndex(i)).IsEnabled)==1,
                "Blob没有保留六项且仅Single可用的原下拉容器");
            CaptureBlobPreview(view.View,"single");combo.IsDropDownOpen=false;
            Require(!mode.TrySetConstant("None")&&!mode.TrySetConstant("DoubleThreshold")&&!mode.TrySetConstant("AutoThreshold"),"禁用正式项/展示项写入Blob草稿");
            Require(mode.TrySetConstant("SingleThreshold"),"可用同值不能修复拒绝状态");
            foreach(var name in new[]{"IsCircularityFilterEnabled","IsRectangularityFilterEnabled"})Require(fields.Fields.Single(f=>f.FieldId==Id(name)).TrySetConstant(true),"正式过滤开关不可用");
            var expanders=NativeDescendants(view.View).OfType<SunEyeVision.Plugin.Wpf.Presenters.Parameters.NativeAdvancedPresenter>().ToArray();
            Require(expanders.Length>0,"真实高级展开控件未进入原可视树");foreach(var advanced in expanders)advanced.IsExpanded=true;Pump();
            Require(expanders.All(e=>e.IsExpanded),"高级展开事件未生效");
            CaptureBlobPreview(view.View,"advanced-before-check");
            foreach(var (lower,upper) in new[]{("FilterAreaMin","FilterAreaMax"),("FilterCircularityMin","FilterCircularityMax"),("FilterRectangularityMin","FilterRectangularityMax")})
            {
                var left=fields.Fields.Single(f=>f.FieldId==Id(lower));var right=fields.Fields.Single(f=>f.FieldId==Id(upper));
                var range=left.View;var native=range.GetType().GetProperty("NativeView")?.GetValue(range);
                Console.WriteLine($"范围检查 {lower} 类型={range.GetType().Name} 同视图={ReferenceEquals(left.View,right.View)} 可见={range.IsVisible}/{range.Visibility} 左订阅={native?.GetType().GetProperty("SubscribeDataLeft")?.GetValue(native) is not null} 右订阅={native?.GetType().GetProperty("SubscribeDataRight")?.GetValue(native) is not null}");
                Require(ReferenceEquals(left.View,right.View)&&range.GetType().Name=="NativeRangePresenter"&&range.IsVisible
                    &&native?.GetType().GetProperty("SubscribeDataLeft")?.GetValue(native) is not null
                    &&native.GetType().GetProperty("SubscribeDataRight")?.GetValue(native) is not null,
                    "正式范围两端不是同一真实范围/订阅组合："+lower);
                var types=NativeDescendants(left.View).OfType<ToggleButton>().Where(button=>BindingOperations.GetBinding(button,ToggleButton.IsCheckedProperty)?.Path?.Path is "IsSubscribeLeft" or "IsSubscribeRight").ToArray();
                Require(types.Length==2&&types.All(button=>button.IsEnabled&&button.IsVisible),"正式范围两侧原订阅按钮不可用："+lower);
                range.BringIntoView();Pump();CaptureBlobPreview(view.View,"range-"+lower);
            }
            CaptureBlobPreview(view.View,"advanced-ranges");
            var next=new Dictionary<string,object>{{"Threshold",101L},{"UpperThreshold",151L},{"BlobCountLimit",120L},
                {"FilterAreaMin",20L},{"FilterAreaMax",100000000L},{"FilterCircularityMin",.2d},{"FilterCircularityMax",.9d},
                {"FilterRectangularityMin",.3d},{"FilterRectangularityMax",.8d}};
            foreach(var (name,value) in next)Require(fields.Fields.Single(f=>f.FieldId==Id(name)).TrySetConstant(value),"正式数值接纳失败："+name);
            var accepted=view.CaptureAcceptedFields();foreach(var (name,value) in next)Require(accepted.Read(Id(name)) is ConstantSetting c&&Equals(c.Value,value),"正式草稿未保存九字段："+name);
            Require(initial.Get<long>(Id("Threshold"))==100&&!view.TryCapture(out _,out var error)&&error.Contains("ROI"),"接九字段误撤ROI组合整窗保护或污染初值");
        }
        foreach(var name in new[]{"None","SingleThreshold","DoubleThreshold"})
        {
            using var view=ToolRegistry.CreateSettingsView("BlobAnalysis",initial.Apply(new Dictionary<string,ParameterSetting>{{Id("SegmentationMode"),new ConstantSetting(name)}}),new(ReferenceOptions:ReferenceFieldOptions.Empty));
            Show(view.View);var fields=Descendants(view.View).OfType<DeclaredFieldsView>().Single();fields.SelectedPageIndex=1;Pump();
            DeclaredFieldAccess Field(string id)=>fields.Fields.Single(f=>f.FieldId==Id(id));
            Require(Field("SegmentationMode").Setting is ConstantSetting {Value:string saved}&&saved==name,"历史模式被打开页面自动替换");
            Require(Field("Type").View.IsVisible==(name!="DoubleThreshold")&&Field("Threshold").View.IsVisible==(name!="None")
                &&Field("UpperThreshold").View.IsVisible==(name=="DoubleThreshold"),"None/Single/Double原厂显隐不一致："+name);
            CaptureBlobPreview(view.View,name);
            Require(!view.TryCapture(out _,out _),"不可用历史模式或ROI未接通窗错误确认");
        }
        var sample=SunEyeVision.Tool.BlobAnalysis.Wpf.Design.BlobDesignData.CreateParameterContext();
        using(var preview=new SunEyeVision.Plugin.Wpf.Presenters.Parameters.DeclaredSettingsView(sample.Draft,sample.Options))
        {
            Show(preview);var fields=(DeclaredFieldsView)preview.Content;fields.SelectedPageIndex=1;Pump();
            Require(fields.Fields.Count==83&&fields.Fields.Select(field=>field.FieldId).ToHashSet(StringComparer.Ordinal).SetEquals(sample.Draft.Schema.Inputs.Select(port=>port.Id))&&((ComboBox)fields.Fields.Single(f=>f.FieldId==Id("SegmentationMode")).InputElement!).Items.Count==6,"独立设计数据未走同一正式组装链");
            CaptureBlobPreview(preview,"design-sample");
        }
        Console.WriteLine("通过：真实Blob六项仅Single可用，九字段正式默认/整数范围/草稿写入、原三模式显隐、历史保留及ROI整窗保护");
    }
    private static void CaptureBlobPreview(FrameworkElement view,string name)
    {
        view.UpdateLayout();Pump();var bitmap=new RenderTargetBitmap((int)Math.Ceiling(view.ActualWidth),(int)Math.Ceiling(view.ActualHeight),96,96,PixelFormats.Pbgra32);
        bitmap.Render(view);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));
        var directory=Path.GetFullPath(Path.Combine(Environment.GetEnvironmentVariable("S2_PRODUCT_ROOT")!,"../../_validation/B08ChoiceDisplay/page-previews"));Directory.CreateDirectory(directory);
        using var stream=File.Create(Path.Combine(directory,name+".png"));png.Save(stream);
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

        VerifyReferenceCandidateRefresh();
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
    private static void VerifyNumericReferenceCleanup()
    {
        foreach(var scenario in new[]{"unload","hide","close","reopen","dispose"})
        {
            var writes=0;var candidate=new DeclaredReferenceCandidate("数组","cleanup-source",null,
                new Port("values",TypeRef.SequenceOf(DataType.Integer)),Array.Empty<string>());
            var alternate=candidate with{NodeId="cleanup-other"};
            using var field=new DeclaredFieldEditor(new("cleanup-"+scenario,DataType.Integer,Storage:NumericStorage.Int32,
                EditModes:FieldEditModes.Constant|FieldEditModes.Reference),"清理来源",
                new ReferenceSetting(candidate.Reference(new BindingSelection.AtConstant(0))),_=>writes++,
                new ReferenceFieldOptions(_=>new(new[]{candidate,alternate},Array.Empty<DeclaredReferenceCandidate>()),EditableIndex:true),
                new ConstantSetting(7L),presentation:new("cleanup-"+scenario,"清理来源","参数",0,FieldEditorKind.Scalar)
                {Layout=new("参数","参数",Minimum:0,Maximum:1000,DecimalPlaces:0)});
            Show(field);
            var numeric=typeof(DeclaredFieldEditor).GetField("_numeric",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(field)!;
            var coordinator=numeric.GetType().GetField("_coordinator",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(numeric)!;
            var type=coordinator.GetType();
            object? Member(string name)=>type.GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(coordinator);
            var session=Member("_reference")!;var sessionType=session.GetType();
            var box=(ComboBox)Member("_box")!;var commit=Member("_commit")!;
            bool Flag(object target,string name)=>(bool)target.GetType().GetProperty(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)!.GetValue(target)!;
            void Open(){box.SetCurrentValue(ComboBox.IsDropDownOpenProperty,true);sessionType.GetProperty("IsDropDownOpen")!.SetValue(session,true);}
            Require(ReferenceEquals(Member("_owner"),owner),"卸载清理缺少真实Owner监听前提");
            sessionType.GetMethod("Refresh")!.Invoke(session,null);
            var roots=((System.Collections.IEnumerable)sessionType.GetProperty("SourceNodes")!.GetValue(session)!).Cast<object>();
            var stale=roots.SelectMany(root=>((System.Collections.IEnumerable)root.GetType().GetProperty("EnumEntrysA")!.GetValue(root)!).Cast<object>())
                .Single(node=>node.GetType().GetProperty("Candidate")!.GetValue(node) is DeclaredReferenceCandidate selectedCandidate&&selectedCandidate.NodeId=="cleanup-other");
            Open();Require(box.IsDropDownOpen&&Flag(session,"IsDropDownOpen"),"清理缺少原Combo及订阅打开前提");
            var armed=true;var observed=false;
            EventHandler outerFault=(_,_)=>{if(armed&&!box.IsDropDownOpen){armed=false;
                if(scenario=="dispose"){((ICommand)sessionType.GetProperty("ClickCommand")!.GetValue(session)!).Execute(stale);return;}
                if(scenario=="reopen"){sessionType.GetProperty("IsDropDownOpen")!.SetValue(session,false);sessionType.GetProperty("IsDropDownOpen")!.SetValue(session,true);return;}
                throw new InvalidOperationException("outer-close-failed");}};
            var outerDescriptor=DependencyPropertyDescriptor.FromProperty(ComboBox.IsDropDownOpenProperty,box.GetType());
            PropertyChangedEventHandler referenceFault=(_,args)=>
            {if(armed&&args.PropertyName=="IsDropDownOpen"&&!Flag(session,"IsDropDownOpen")){armed=false;throw new InvalidOperationException("reference-close-failed");}};
            if(scenario is "close" or "reopen" or "dispose")outerDescriptor.AddValueChanged(box,outerFault);else ((INotifyPropertyChanged)session).PropertyChanged+=referenceFault;
            try
            {
                var name=scenario=="dispose"?"Dispose":scenario is "close" or "reopen"?"ClosePopups":scenario=="unload"?"OnUnloaded":"PrepareForVisibility";
                var args=scenario is "close" or "reopen" or "dispose"?Array.Empty<object>():scenario=="unload"?new object[]{numeric,new RoutedEventArgs(FrameworkElement.UnloadedEvent)}:new object[]{false};
                try{type.GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public)!.Invoke(coordinator,args);}
                catch(TargetInvocationException error)when(error.InnerException is not null)
                {observed=error.InnerException.ToString().Contains(scenario=="close"?"outer-close-failed":"reference-close-failed",StringComparison.Ordinal);}
                if(scenario=="dispose"){Require(!armed&&writes==0&&!Flag(session,"IsDropDownOpen")&&Flag(session,"IsSuspended"),"借用数值协调器Dispose关闭回调仍可写活跃字段状态");continue;}
                if(scenario=="reopen"){Require(!armed&&!box.IsDropDownOpen&&Flag(session,"IsDropDownOpen")&&writes==0,"旧数值关闭操作覆盖回调中新代订阅Popup");continue;}
                Require(observed,"关闭故障没有保留异常语义："+scenario);
                Require(!box.IsDropDownOpen&&!Flag(session,"IsDropDownOpen"),"关闭故障截断原Combo或引用Popup清理："+scenario);
                Require(!(bool)type.GetField("_closing",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(coordinator)!,"关闭异常留下重入标记");
                if(scenario=="unload")Require(Member("_owner") is null&&!((bool)type.GetField("_active",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(coordinator)!),"卸载异常留下Owner监听归属或活跃状态");
                if(scenario=="hide")Require((bool)type.GetField("_presentationHidden",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(coordinator)!
                    &&Flag(commit,"IsSuspended")&&Flag(session,"IsSuspended"),"隐藏关闭异常跳过数值暂停或引用退役");
                if(scenario is "hide" or "unload")((ICommand)sessionType.GetProperty("ClickCommand")!.GetValue(session)!).Execute(stale);
                Require(writes==0,"清理异常或退役后的旧选择写入正式字段");
            }
            finally{outerDescriptor.RemoveValueChanged(box,outerFault);((INotifyPropertyChanged)session).PropertyChanged-=referenceFault;}
        }
        Console.WriteLine("通过：原数值Combo关闭故障仍关订阅双层，卸载解除Owner，隐藏先暂停数值与引用，异常完整保留且卸载/隐藏/借用Dispose旧回调无写入、重开新代不被覆盖");
    }
    private static void VerifyReferenceCandidateRefresh()
    {
        var sequence=TypeRef.SequenceOf(DataType.Integer);
        var source=new DeclaredReferenceCandidate("数组","source",null,new Port("values",sequence),Array.Empty<string>()){NodeDisplayName="乙模块"};
        var other=new DeclaredReferenceCandidate("数组","replacement",null,new Port("values",sequence),Array.Empty<string>()){NodeDisplayName="甲模块"};
        var index=new DeclaredReferenceCandidate("索引","index",null,new Port("value",DataType.Integer),Array.Empty<string>()){NodeDisplayName="索引模块"};
        var replacement=source with{NodeId="same-caption-new-id"};
        var currentSources=new[]{source,other};var currentIndices=new[]{index};var writes=0;
        var accepted=(ParameterSetting)new ReferenceSetting(source.Reference(new BindingSelection.AtField(index.Reference(BindingSelection.All))));
        using var field=new DeclaredFieldEditor(new("candidate-refresh",DataType.Integer,EditModes:FieldEditModes.Reference),"候选刷新",accepted,
            value=>{accepted=value;writes++;},new ReferenceFieldOptions(_=>new(currentSources,currentIndices),EditableIndex:true));
        Show(field);
        var session=NativeDescendants(field).OfType<ContentControl>().Single(item=>item.Content?.GetType().Name=="ReferenceFieldSession").Content;
        var type=session.GetType();
        object? Property(string name)=>type.GetProperty(name)!.GetValue(session);
        void Refresh()=>((ICommand)Property("DropDownCommand")!).Execute(null);
        object[] Roots()=>((System.Collections.IEnumerable)Property("SourceNodes")!).Cast<object>().ToArray();
        string Name(object node)=>(string)node.GetType().GetProperty("Name")!.GetValue(node)!;
        Refresh();
        Require(Roots().Skip(1).Select(Name).SequenceEqual(new[]{"乙模块","甲模块"}.OrderBy(name=>name)),"模块来源没有按原显示名称规则排序");
        Require(Property("SelectedSourceNode") is not null&&Property("SelectedIndexNode") is not null&&!field.HasInvalidText,"初始复合来源未恢复选中");
        var priorSource=Property("SelectedSourceNode");var priorIndex=Property("SelectedIndexNode");
        currentIndices=Array.Empty<DeclaredReferenceCandidate>();Refresh();
        Require(field.HasInvalidText&&Property("SelectedSourceNode") is not null&&Property("SelectedIndexNode") is null&&writes==0,"索引消失未使复合来源失效或修改正式绑定");
        Require(!field.TryCommitPending()&&writes==0,"索引已失效仍可确认配置");
        ((ICommand)Property("IndexClickCommand")!).Execute(priorIndex);
        Require(writes==0,"刷新后旧索引回调仍写入");
        currentIndices=new[]{index};Refresh();
        Require(!field.HasInvalidText&&Property("SelectedIndexNode") is not null&&writes==0,"索引恢复未清候选错误或擅自写入");
        currentSources=new[]{replacement,other};Refresh();
        Require(field.HasInvalidText&&Property("SelectedSourceNode") is null&&accepted is ReferenceSetting {Reference.NodeId:"source"}&&writes==0,"同显示名称新身份静默替代旧来源");
        Require(!field.TryCommitPending()&&writes==0,"主来源已失效仍可确认配置");
        ((ICommand)Property("ClickCommand")!).Execute(priorSource);
        Require(writes==0,"刷新后旧来源回调仍写入");
        var indexRoot=((System.Collections.IEnumerable)Property("IndexNodes")!).Cast<object>().Single(node=>Name(node)=="索引模块");
        var indexLeaf=((System.Collections.IEnumerable)indexRoot.GetType().GetProperty("EnumEntrysA")!.GetValue(indexRoot)!).Cast<object>().Single();
        ((ICommand)Property("IndexClickCommand")!).Execute(indexLeaf);
        Require(writes==0&&field.HasInvalidText,"主源已失效时索引选择仍接纳旧来源");
        currentSources=new[]{source,other};Refresh();
        Require(!field.HasInvalidText&&Property("SelectedSourceNode") is not null&&writes==0,"原身份恢复改变正式设置或错误残留");
        Require(field.TryCommitPending()&&writes==0,"恢复后的未改引用被重复写入");
        field.Dispose();((ICommand)Property("ClickCommand")!).Execute(priorSource);
        Require(writes==0,"退役后保留命令写入");
        Console.WriteLine("通过：完整来源/索引刷新按显示名排序，失效保留身份，同名不重绑，旧回调与退役拒绝，恢复不写入");
    }
    // REFERENCE_LEASE_CASE_BEGIN
    private static void VerifyReferenceLeaseCleanup()
    {
        foreach(var scenario in new[]{"projection","retire","host-open","variable-open","variable-submit","variable-index-submit","normal"})
        {
            var asm=typeof(ReferenceFieldOptions).Assembly;
            var stateType=asm.GetType("SunEyeVision.Plugin.Wpf.Adapters.Fields.DeclaredFieldState")!;
            var sessionType=asm.GetType("SunEyeVision.Plugin.Wpf.Adapters.References.ReferenceFieldSession")!;
            var sequence=scenario=="variable-index-submit";
            var candidate=new DeclaredReferenceCandidate("来源","lease-source",null,new Port("value",sequence?TypeRef.SequenceOf(DataType.Integer):DataType.Integer),Array.Empty<string>());
            var variable=new SunEyeVision.Plugin.Wpf.Controls.References.Variables.VariableSelectionItem("整数","方案",0,"","7",
                new VariableReference(Guid.NewGuid().ToString(),Guid.NewGuid().ToString(),DataType.Integer,BindingSelection.All));
            var writes=0;var opened=0;ReferenceExpansionRequest? expanded=null;VariableSelectionRequest? selected=null;
            var target=new Port("lease-target",DataType.Integer,EditModes:FieldEditModes.Reference);
            var stateCtor=stateType.GetConstructors().Single();var stateArgs=stateCtor.GetParameters().Select(p=>p.HasDefaultValue?p.DefaultValue:null).ToArray();
            stateArgs[0]=target;stateArgs[1]=new ReferenceSetting(candidate.Reference(sequence?new BindingSelection.AtConstant(0):BindingSelection.All));stateArgs[2]=(Action<ParameterSetting>)(_=>writes++);
            var state=stateCtor.Invoke(stateArgs);
            var options=new ReferenceFieldOptions(_=>new(new[]{candidate},Array.Empty<DeclaredReferenceCandidate>(),new[]{variable}),EditableIndex:true,
                Expand:request=>{expanded=request;opened++;if(scenario=="host-open"){request.Invalidated+=()=>throw new InvalidOperationException("lease-retire-failed");throw new InvalidOperationException("host-open-failed");}},
                SelectVariable:request=>{selected=request;if(scenario=="variable-open"){request.Invalidated+=()=>throw new InvalidOperationException("variable-retire-failed");throw new InvalidOperationException("variable-open-failed");}});
            var ctor=sessionType.GetConstructors().Single();var args=ctor.GetParameters().Select(p=>p.HasDefaultValue?p.DefaultValue:null).ToArray();args[0]=state;args[1]=options;
            if(scenario is "variable-submit" or "variable-index-submit")args[3]=(Func<ParameterSetting,bool>)(_=>throw new InvalidOperationException("variable-submit-failed"));
            var session=ctor.Invoke(args);var expansionField=sessionType.GetField("_expansion",BindingFlags.Instance|BindingFlags.NonPublic)!;
            var variableField=sessionType.GetField("_variableRequest",BindingFlags.Instance|BindingFlags.NonPublic)!;
            var resources=new ResourceDictionary{Source=new Uri("/SunEyeVision.Plugin.Wpf;component/Themes/Native/Composition/References/ReferenceField.xaml",UriKind.Relative)};
            var root=(Grid)((DataTemplate)resources["ButtonSelecter_sub"]).LoadContent();root.DataContext=session;
            root.Measure(new Size(400,40));root.Arrange(new Rect(0,0,400,40));Pump();
            var border=root.Children.OfType<Border>().First();var descriptor=DependencyPropertyDescriptor.FromProperty(Border.BorderThicknessProperty,typeof(Border));
            var armed=scenario=="projection";var textNotifications=0;
            EventHandler fault=(_,_)=>{if(armed&&border.BorderThickness.Left==(scenario=="retire"?0:3)){armed=false;throw new InvalidOperationException("projection-failed");}};
            descriptor.AddValueChanged(border,fault);
            PropertyChangedEventHandler observe=(_,eventArgs)=>{if(eventArgs.PropertyName=="IsTextPopupVisible")textNotifications++;};((INotifyPropertyChanged)session).PropertyChanged+=observe;
            Exception? Capture(Action operation){try{operation();return null;}catch(TargetInvocationException error){return error.InnerException??error;}catch(Exception error){return error;}}
            void Open()=>((ICommand)sessionType.GetProperty("PopSubscribeWindowCommand")!.GetValue(session)!).Execute(null);
            try
            {
                Exception? failure;
                if(scenario=="normal")
                {
                    Open();var first=expanded!;Require(first.IsValid&&opened==1,"正常展开没有有效借用租约");first.Dispose();
                    Require(!first.IsValid,"正常关闭未退役");Open();var second=expanded!;first.Dispose();
                    first.ClickCommand.Execute(null);Require(second.IsValid&&opened==2&&writes==0,"旧展开回调影响新代或写值");second.Dispose();
                }
                else if(scenario.StartsWith("variable-",StringComparison.Ordinal))
                {
                    failure=Capture(()=>sessionType.GetMethod("Refresh")!.Invoke(session,null));Require(failure is null,"变量前提刷新失败");
                    failure=Capture(()=>sessionType.GetMethod("OpenVariableSelector",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(session,new object[]{sequence}));
                    if(scenario!="variable-open")
                    {
                        Require(failure is null&&selected is {IsValid:true},"变量选择没有有效借用租约");selected!.Invalidated+=()=>throw new InvalidOperationException("variable-retire-failed");
                        failure=Capture(()=>selected.TrySelect(variable));
                    }
                    Require(failure is not null&&failure.ToString().Contains(scenario=="variable-open"?"variable-open-failed":"variable-submit-failed")
                        &&failure.ToString().Contains("variable-retire-failed")&&selected is {IsValid:false}&&variableField.GetValue(session) is null&&writes==0,
                        "变量打开/提案异常被清理覆盖或请求仍有效："+scenario+" "+failure);
                    Require(!selected!.TrySelect(variable),"迟到变量请求仍能写入");
                }
                else
                {
                    if(scenario=="retire")
                    {
                        Open();expanded!.Invalidated+=()=>throw new InvalidOperationException("lease-retire-failed");armed=true;
                        failure=Capture(()=>sessionType.GetMethod("InvalidateExpansion")!.Invoke(session,null));
                    }
                    else failure=Capture(Open);
                    var primary=scenario=="host-open"?"host-open-failed":"projection-failed";
                    Require(failure is not null&&failure.ToString().Contains(primary)&&expansionField.GetValue(session) is null,
                        "展开投影/宿主故障遗留有效租约或丢原异常："+scenario+" "+failure);
                    if(scenario is "host-open" or "retire")Require(failure!.ToString().Contains("lease-retire-failed"),"退役异常未与首异常同时保留");
                    Require((bool)sessionType.GetProperty("IsTextPopupVisible")!.GetValue(session)!&&textNotifications>=2&&writes==0,"第二投影未复原或故障误写值");
                    Require(expanded is null||!expanded.IsValid,"故障展开请求仍有效");
                }
            }
            finally
            {
                descriptor.RemoveValueChanged(border,fault);((INotifyPropertyChanged)session).PropertyChanged-=observe;root.DataContext=null;
                ((IDisposable)session).Dispose();((IDisposable)state).Dispose();
            }
        }
        Console.WriteLine("通过：原订阅模板真实WPF投影故障完整退役、两个投影均恢复、首及清理异常均保留、变量主源/索引拒绝及迟到回调不写新代");
    }
    // REFERENCE_LEASE_CASE_END
    private static void VerifySettingsCandidate(IToolSettingsFields definition,object settings)
    {
        var tool=(IToolDefinition)definition;
        FieldSettings Initial()=>new(SunEyeVision.Plugin.Contracts.Results.Presentation.ResultDisplayFields.EditorSchema(tool).Inputs,definition.ReadSettings(settings).CopyValues().Concat(((SunEyeVision.Plugin.Contracts.Results.Presentation.IToolResultDisplayProvider)tool).CreateDisplaySettings().CopyValues()).ToDictionary(pair=>pair.Key,pair=>(ParameterSetting)new ConstantSetting(pair.Value)));
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
                &&draft.CaptureFields().Get<long>(low)==100,
                "宿主没有取得完整常量和引用候选，或预检前污染草稿");
            if(reject)throw new InvalidOperationException("整组候选拒绝");
            if(rebind)view.Bind(new SunEyeVision.Plugin.Runtime.ToolSettingsEditSession(tool,Initial()),view.Options);
        });
        string D(string group,string role)=>BlobResultDisplay.Field(group,role);
        options=options with {
            GraphicDisplays=new[]{BlobResultDisplay.Graphic,BlobResultDisplay.Inspection,BlobResultDisplay.Mask,BlobResultDisplay.Annulus}.Select(group=>new NativeGraphicDisplayOptions(group,D(group,"visible"),D(group,"ok"),D(group,"ng"),D(group,"opacity"),D(group,"width"))).ToArray(),
            VisibilityDisplays=new[]{new NativeVisibilityDisplayOptions(D(BlobResultDisplay.Text,"visible"))},
            TextDisplays=new[]{new NativeTextDisplayOptions(D(BlobResultDisplay.Text,"content"),D(BlobResultDisplay.Text,"ok"),D(BlobResultDisplay.Text,"ng"),D(BlobResultDisplay.Text,"size"),D(BlobResultDisplay.Text,"opacity"),D(BlobResultDisplay.Text,"x"),D(BlobResultDisplay.Text,"y"))}};
        view.Bind(draft,options);
        var method=typeof(SunEyeVision.Plugin.Wpf.Presenters.Parameters.DeclaredSettingsView).GetMethod("WriteGroup",BindingFlags.Instance|BindingFlags.NonPublic)!;
        var group=new Dictionary<string,ParameterSetting>{{low,new ConstantSetting(73L)},{high,new ReferenceSetting(source.Reference(BindingSelection.All))}};
        void Apply()=>method.Invoke(view,new object[]{group});
        try{Apply();throw new Exception("整组宿主拒绝被忽略");}
        catch(TargetInvocationException error)when(error.InnerException is InvalidOperationException {Message:"整组候选拒绝"}){}
        Require(calls==1&&view.TryCapture(out var captured,out _)
            &&view.AcceptedFieldOrder.Count==0
            &&captured.Get<long>(low)==100&&captured.Capture().Values.All(value=>value is ConstantSetting),
            "混合常量和引用拒绝后缓存已污染");
        reject=false;rebind=true;
        try{Apply();throw new Exception("候选回调重绑仍接纳旧提案");}
        catch(TargetInvocationException error)when(error.InnerException is InvalidOperationException){}
        Require(draft.CaptureFields().Get<long>(low)==100
            &&view.AcceptedFieldOrder.Count==0
            &&view.TryCapture(out captured,out _)&&captured.Get<long>(low)==100&&captured.Capture().Values.All(value=>value is ConstantSetting),
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
