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
    private sealed class ContentToggleProbe : INotifyPropertyChanged
    {
        private bool _value;
        public bool Value {get=>_value;set{if(_value==value)return;_value=value;PropertyChanged?.Invoke(this,new(nameof(Value)));}}
        public event PropertyChangedEventHandler? PropertyChanged;
    }
    private static void VerifyContentToggleComponent()
    {
        const string markup="""
        <input:ContentToggleSwitch xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
          xmlns:input="clr-namespace:SunEyeVision.Plugin.Wpf.Controls.Inputs;assembly=SunEyeVision.Plugin.Wpf">
          <input:ContentToggleSwitch.OffContent><Image Stretch="None" Source="/SunEyeVision.Plugin.Wpf;component/Themes/Assets/Inputs/Switch/off-dark.png"/></input:ContentToggleSwitch.OffContent>
          <input:ContentToggleSwitch.OnContent><Image Stretch="None" Source="/SunEyeVision.Plugin.Wpf;component/Themes/Assets/Inputs/Switch/on-dark.png"/></input:ContentToggleSwitch.OnContent>
        </input:ContentToggleSwitch>
        """;
        var first=(SunEyeVision.Plugin.Wpf.Controls.Inputs.ContentToggleSwitch)XamlReader.Parse(markup);
        var second=(SunEyeVision.Plugin.Wpf.Controls.Inputs.ContentToggleSwitch)XamlReader.Parse(markup);
        Check(first.GetType().BaseType==typeof(UserControl)&&!first.IsChecked
            &&SunEyeVision.Plugin.Wpf.Controls.Inputs.ContentToggleSwitch.IsCheckedProperty.PropertyType==typeof(bool),
            "内容开关保持PCUI的UserControl家族、布尔DP和默认false，不冒充参数ToggleButton");
        Check(first.GetType().Assembly.GetType("SunEyeVision.Plugin.Wpf.Native.Input.HikPcUI.ListView.BooleanControl",false) is null,
            "内容开关中立迁移不保留旧类型别名或双轨原件");
        Check(first.OffContent is Image {Stretch:Stretch.None,Source:BitmapSource {PixelWidth:>0}}
            &&first.OnContent is Image {Stretch:Stretch.None,Source:BitmapSource {PixelWidth:>0}},
            "内容开关从正式中立pack路径加载原dark两图且保持Stretch=None");
        Check(!ReferenceEquals(first.OffContent,second.OffContent)&&!ReferenceEquals(first.OnContent,second.OnContent),
            "内容开关双实例不共享可视Image内容");
        var panel=new StackPanel();panel.Children.Add(first);panel.Children.Add(second);
        second.IsChecked=true;Render(panel,"content-toggle-dark.png",180,100);second.IsChecked=false;
        var model=new ContentToggleProbe();
        first.SetBinding(SunEyeVision.Plugin.Wpf.Controls.Inputs.ContentToggleSwitch.IsCheckedProperty,
            new System.Windows.Data.Binding(nameof(ContentToggleProbe.Value)){Source=model,Mode=System.Windows.Data.BindingMode.TwoWay});
        var window=new Window{Content=panel,Width=220,Height=160,ShowInTaskbar=false,ShowActivated=false,Left=-32000,Top=-32000};
        void Pump(){window.UpdateLayout();System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ApplicationIdle);}
        try
        {
            window.Show();Pump();
            var inner=(System.Windows.Controls.Primitives.ToggleButton)first.FindName("TO");inner.ApplyTemplate();Pump();
            var shape=(ContentControl)inner.Template.FindName("shape",inner);
            Check(first.IsLoaded&&inner.IsLoaded&&ReferenceEquals(shape.Content,first.OffContent),
                "内容开关加载真实TO模板且未选时展示OffContent");
            var events=new List<bool>();object? lastSender=null;
            SunEyeVision.Plugin.Wpf.Controls.Inputs.ContentToggleSwitch.IsCheckedChange listener=(sender,_)=>
            {lastSender=sender;events.Add(((System.Windows.Controls.Primitives.ToggleButton)sender).IsChecked==true);};
            first.OnCheckedChange+=listener;
            model.Value=true;Pump();
            Check(first.IsChecked&&inner.IsChecked==true&&ReferenceEquals(shape.Content,first.OnContent)
                &&events.SequenceEqual(new[]{true})&&ReferenceEquals(lastSender,inner),
                "内容开关程序回填转发原Checked事件及内部发送者，切换OnContent");
            model.Value=true;Pump();Check(events.Count==1,"内容开关同值回填不重复通知");
            var click=typeof(System.Windows.Controls.Primitives.ToggleButton).GetMethod("OnClick",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!;
            click.Invoke(inner,null);Pump();
            Check(!first.IsChecked&&!model.Value&&events.SequenceEqual(new[]{true,false})
                &&System.Windows.Data.BindingOperations.IsDataBound(first,SunEyeVision.Plugin.Wpf.Controls.Inputs.ContentToggleSwitch.IsCheckedProperty),
                "内容开关原TO点击沿TwoWay绑定回写且保留外层Binding");
            Check(!second.IsChecked&&ReferenceEquals(shape.Content,first.OffContent),"内容开关另一实例状态不受影响且回到OffContent");
            first.IsEnabled=false;Pump();var blocked=false;
            var peer=new System.Windows.Automation.Peers.ToggleButtonAutomationPeer(inner);
            try{((System.Windows.Automation.Provider.IToggleProvider)peer.GetPattern(System.Windows.Automation.Peers.PatternInterface.Toggle)).Toggle();}
            catch(System.Windows.Automation.ElementNotEnabledException){blocked=true;}
            Check(blocked&&!model.Value&&events.Count==2,"内容开关禁用时原Automation切换被拒绝，无业务回写");
            first.IsEnabled=true;panel.Children.Remove(first);Pump();panel.Children.Insert(0,first);Pump();
            Check(first.IsLoaded&&!first.IsChecked&&events.Count==2,"内容开关卸载重载不凭空切换或重复通知");
            click.Invoke(inner,null);Pump();Check(model.Value&&events.SequenceEqual(new[]{true,false,true}),"内容开关重载后继续单次切换");
            first.OnCheckedChange-=listener;System.Windows.Data.BindingOperations.ClearBinding(first,SunEyeVision.Plugin.Wpf.Controls.Inputs.ContentToggleSwitch.IsCheckedProperty);
            var retiredCount=events.Count;click.Invoke(inner,null);Pump();
            Check(events.Count==retiredCount,"内容开关消费者退役解绑后原件事件不再回调旧宿主");
        }
        finally{window.Content=null;panel.Children.Clear();window.Close();}
    }
    private static void VerifyBooleanComponent()
    {
        var writes=new List<bool>();var reject=false;
        using var editor=new DeclaredFieldEditor(new Port("switch.unit",DataType.Boolean),"开关",
            new ConstantSetting(false),setting=>
            {
                if(reject)throw new ArgumentException("拒绝开关提案");
                writes.Add((bool)((ConstantSetting)setting).Value);
            });
        var first=(SunEyeVision.Plugin.Wpf.Native.Composition.Apps.XmlUI.ParamTabControl.BooleanControl)editor.InputElement!;
        var eventField=first.GetType().GetField("OnCheckedChange",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!;
        Check(first.IsChecked==false&&!first.IsThreeState&&writes.Count==0,"开关原件初始化恢复不提交且不启用三态");
        Check(first.OffContent is Image {Source:BitmapSource off}&&first.OnContent is Image {Source:BitmapSource on}
            &&off.IsFrozen&&on.IsFrozen,"开关两个原厂图片在独立视图中均已解析并冻结");
        Check(editor.TrySetConstant(true)&&writes.SequenceEqual(new[]{true})
            &&((ConstantSetting)editor.Setting).Value is true,"开关正式字段接纳链写入布尔值");
        var current=(SunEyeVision.Plugin.Wpf.Native.Composition.Apps.XmlUI.ParamTabControl.BooleanControl)editor.InputElement!;
        Check(current.IsChecked==true&&eventField.GetValue(first) is null,"开关回填新视图且退役原件解绑事件");
        first.SetCurrentValue(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty,true);
        Check(writes.Count==1,"退役开关迟到状态变化不提交");
        reject=true;
        Check(!editor.TrySetConstant(false)&&((ConstantSetting)editor.Setting).Value is true
            &&current.IsChecked==true&&writes.Count==1,"开关宿主拒绝保留已接纳值及视图");
        reject=false;
        Check(editor.TrySetConstant(false)&&writes.SequenceEqual(new[]{true,false})
            &&((SunEyeVision.Plugin.Wpf.Native.Composition.Apps.XmlUI.ParamTabControl.BooleanControl)editor.InputElement!).IsChecked==false,
            "开关拒绝后可重新接纳并清除错误回填");
        var last=editor.InputElement!;editor.Dispose();editor.Dispose();
        Check(editor.InputElement is null&&editor.Content is null&&eventField.GetValue(last) is null
            &&!editor.TrySetConstant(true),"开关重复释放解绑原件并拒绝关闭后写入");
        var clickWrites=new List<bool>();var rejectClick=false;
        using var clickable=new DeclaredFieldEditor(new Port("switch.click",DataType.Boolean),"开关",
            new ConstantSetting(false),setting=>
            {
                if(rejectClick)throw new ArgumentException("拒绝点击");
                clickWrites.Add((bool)((ConstantSetting)setting).Value);
            });
        var window=new Window{Content=clickable,Width=180,Height=100,ShowInTaskbar=false,
            ShowActivated=false,Left=-32000,Top=-32000};
        try
        {
            window.Show();window.UpdateLayout();
            var control=(System.Windows.Controls.Primitives.ToggleButton)clickable.InputElement!;
            var click=typeof(System.Windows.Controls.Primitives.ToggleButton).GetMethod("OnClick",
                System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!;
            Check(control.IsLoaded&&control.IsVisible,"开关事件检查使用已加载的真实WPF原件");
            click.Invoke(control,null);
            Check(control.IsChecked==true&&clickWrites.SequenceEqual(new[]{true}),"开关原件点击事件只接纳一次");
            rejectClick=true;click.Invoke(control,null);
            Check(control.IsChecked==true&&clickWrites.Count==1&&((ConstantSetting)clickable.Setting).Value is true,
                "开关点击被宿主拒绝时立即恢复已接纳状态");
            rejectClick=false;click.Invoke(control,null);
            Check(control.IsChecked==false&&clickWrites.SequenceEqual(new[]{true,false}),"开关拒绝后再次点击可正常接纳");
            clickable.IsEnabled=false;click.Invoke(control,null);
            Check(clickWrites.Count==2,"禁用开关的迟到点击不提交");
            clickable.IsEnabled=true;clickable.Visibility=Visibility.Collapsed;click.Invoke(control,null);
            Check(clickWrites.Count==2,"隐藏开关的迟到点击不提交");
            clickable.Dispose();click.Invoke(control,null);
            Check(clickWrites.Count==2&&eventField.GetValue(control) is null,"已加载开关关闭后点击不再持有适配事件");
        }
        finally{window.Content=null;window.Close();}
    }
    private static void VerifyEnumerationComponent()
    {
        var writes=new List<string>();var reject=false;
        using var editor=new DeclaredFieldEditor(new Port("enum.unit",DataType.Text,
            Choices:new TextChoiceConstraint(new[]{"first","second","third"})),"枚举",new ConstantSetting("first"),
            value=>{if(reject)throw new ArgumentException("宿主拒绝枚举");writes.Add((string)((ConstantSetting)value).Value!);},
            choiceLabels:new Dictionary<string,string>{{"first","同名"},{"second","同名"},{"third","第三项"}});
        var combo=(ComboBox)editor.InputElement!;
        var scopeProperty=(DependencyProperty)typeof(SunEyeVision.Plugin.Wpf.Presenters.Input.NativeInputFactory)
            .GetField("EnumerationTextScopeProperty",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static)!.GetValue(null)!;
        var scope=combo.GetValue(scopeProperty)!;
        var window=new Window{Content=editor,Width=280,Height=110,ShowInTaskbar=false,ShowActivated=false,Left=-32000,Top=-32000};
        try
        {
            window.Show();window.UpdateLayout();combo.ApplyTemplate();
            Check(writes.Count==0&&Equals(combo.SelectedValue,"first")&&combo.Items.Count==3&&!combo.IsEditable,
                "普通枚举初始化恢复正式键及全部候选而不提交");
            combo.IsDropDownOpen=true;
            Check(combo.Template.FindName("Popup",combo) is System.Windows.Controls.Primitives.Popup {IsOpen:true},
                "普通枚举使用可打开的原厂真实弹层");
            combo.SelectedValue="second";
            Check(writes.SequenceEqual(new[]{"second"})&&((ConstantSetting)editor.Setting).Value is "second",
                "普通枚举同名显示标签仍按正式候选键接纳");
            combo.SelectedValue="second";Check(writes.Count==1,"普通枚举同值同步不重复提交");
            reject=true;combo.SelectedValue="third";
            Check(Equals(combo.SelectedValue,"second")&&writes.Count==1&&editor.HasInvalidText,
                "普通枚举宿主拒绝恢复原键并保留诊断");
            reject=false;combo.SelectedValue="third";
            Check(writes.SequenceEqual(new[]{"second","third"})&&!editor.HasInvalidText,
                "普通枚举拒绝后重选可接纳并清除诊断");
            Check(!editor.TrySetConstant("removed")&&((ConstantSetting)editor.Setting).Value is "third",
                "普通枚举拒绝已删除或非法候选，不回退首项");
            Check(editor.TrySetConstant("first")&&!combo.IsDropDownOpen&&combo.GetValue(scopeProperty) is null
                &&(bool)scope.GetType().GetField("_disposed",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.GetValue(scope)!,
                "普通枚举重建关闭旧弹层并释放事件样式作用域");
            var count=writes.Count;combo.SelectedValue="second";Check(writes.Count==count,"普通枚举旧控件迟到选择不写新会话");
            var current=(ComboBox)editor.InputElement!;
            var currentScope=current.GetValue(scopeProperty)!;
            current.IsDropDownOpen=true;editor.Dispose();editor.Dispose();
            Check(!current.IsDropDownOpen&&current.GetValue(scopeProperty) is null
                &&currentScope.GetType().GetField("_pending",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.GetValue(currentScope) is null
                &&!editor.TrySetConstant("second"),"普通枚举重复释放关闭弹层、取消延迟任务并拒绝写入");
        }
        finally{window.Content=null;window.Close();}
        using var invalid=new DeclaredFieldEditor(new Port("enum.stale",DataType.Text,
            Choices:new TextChoiceConstraint(new[]{"valid"})),"旧选项",new ConstantSetting("removed"),_=>{});
        Check(invalid.HasInvalidText&&((ComboBox)invalid.InputElement!).SelectedIndex==-1
            &&invalid.TrySetConstant("valid")&&!invalid.HasInvalidText,"普通枚举保留非法初值供显式修复，不擅自选择首项");
    }
    private static void VerifyIntegerRadioComponent()
    {
        var choices=new NativeIntegerRadioOptions(new[]{new NativeIntegerRadioChoice(10,"同名"),new NativeIntegerRadioChoice(30,"同名")});
        var port=new Port("radio.unit",DataType.Integer,EditModes:FieldEditModes.Constant);
        var schema=new ToolSchema(new[]{port},Array.Empty<Port>());
        var draft=new FieldSettingsDraft(schema,new FieldSettings(schema.Inputs,
            new Dictionary<string,ParameterSetting>{{port.Id,new ConstantSetting(10L)}}));
        var reject=false;var writes=0;
        using var editor=new DeclaredFieldEditor(port,"两项单选",draft.CaptureFields().Read(port.Id),setting=>
            draft.ApplyFields(new Dictionary<string,ParameterSetting>{{port.Id,setting}},_=>
            {if(reject)throw new ArgumentException("拒绝单选");writes++;}),integerRadioOptions:choices);
        var presenter=editor.InputElement!;
        var command=(System.Windows.Input.ICommand)presenter.GetType().GetProperty("ValueChangedCommand")!.GetValue(presenter)!;
        var window=new Window{Content=editor,Width=440,Height=140,ShowActivated=false,ShowInTaskbar=false,Left=-32000,Top=-32000};
        var click=typeof(RadioButton).GetMethod("OnClick",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!;
        try
        {
            window.Show();window.UpdateLayout();
            var buttons=Descendants<RadioButton>(presenter).ToArray();
            Check(buttons.Length==2&&buttons[0].IsChecked==true&&buttons[1].IsChecked==false&&writes==0,
                "整数单选原两槽模板回填不提交且选中值互斥");
            Check(command.CanExecute(30)&&!command.CanExecute(30L)&&!command.CanExecute(null)&&!command.CanExecute(99),
                "整数单选原模板命令只接收已声明Int32候选值");
            click.Invoke(buttons[1],null);
            Check(draft.CaptureFields().Get<long>(port.Id)==30&&writes==1&&buttons[1].IsChecked==true&&buttons[0].IsChecked==false,
                "整数单选Checked链按正式整数而非同名标签写入声明草稿");
            click.Invoke(buttons[1],null);Check(writes==1,"整数单选重复点击不重复接纳");
            reject=true;click.Invoke(buttons[0],null);
            Check(draft.CaptureFields().Get<long>(port.Id)==30&&writes==1&&buttons[1].IsChecked==true&&buttons[0].IsChecked==false,
                "整数单选宿主拒绝恢复原互斥投影");
            reject=false;click.Invoke(buttons[0],null);
            Check(draft.CaptureFields().Get<long>(port.Id)==10&&writes==2&&!editor.HasInvalidText,
                "整数单选拒绝后可再次接纳并清除诊断");
            editor.IsEnabled=false;command.Execute(30);Check(writes==2&&!command.CanExecute(30),"整数单选禁用时拒绝迟到命令");
            editor.IsEnabled=true;editor.Visibility=Visibility.Collapsed;command.Execute(30);
            Check(writes==2&&!command.CanExecute(30),"整数单选隐藏时拒绝迟到命令");
            editor.Visibility=Visibility.Visible;
            Check(!editor.TrySetConstant(99L)&&!editor.TrySetConstant((long)int.MaxValue+1)&&draft.CaptureFields().Get<long>(port.Id)==10,
                "整数单选非法值与Int32溢出不污染声明草稿");
            Check(editor.TrySetConstant(30L)&&draft.CaptureFields().Get<long>(port.Id)==30,"整数单选显式常量提案沿同一状态接纳");
            editor.Dispose();editor.Dispose();command.Execute(10);
            Check(writes==3&&!command.CanExecute(10)&&presenter is Decorator {Child:null}&&editor.InputElement is null,
                "整数单选重复关闭释放模板与命令并拒绝迟到写入");
        }
        finally{window.Content=null;window.Close();}
        using var five=new DeclaredFieldEditor(new Port("radio.five",DataType.Integer,EditModes:FieldEditModes.Constant),"四个候选",
            new ConstantSetting(-99L),_=>{},integerRadioOptions:new NativeIntegerRadioOptions(
                new[]{new NativeIntegerRadioChoice(0,"零"),new NativeIntegerRadioChoice(1,"一"),new NativeIntegerRadioChoice(2,"二"),new NativeIntegerRadioChoice(3,"三")},true,-99));
        Render(five,"native-radio-five-slot-unit.png",600,150);
        var all=Descendants<RadioButton>(five).ToArray();
        Check(all.Length==5&&all.Count(button=>button.Visibility==Visibility.Visible)==4
            &&all.Where(button=>button.Visibility==Visibility.Visible).All(button=>button.IsChecked!=true)&&!five.HasInvalidText,
            "整数单选五槽保留空槽隐藏及显式未选值，不回退首项");
        Check(five.TrySetConstant(3L)&&all[3].IsChecked==true,"整数单选五槽显式选择保留同一原模板实例");
        using var invalid=new DeclaredFieldEditor(new Port("radio.invalid",DataType.Integer,EditModes:FieldEditModes.Constant),"非法原值",
            new ConstantSetting(99L),_=>{},integerRadioOptions:choices);
        Check(invalid.HasInvalidText&&invalid.TrySetConstant(30L)&&!invalid.HasInvalidText,"整数单选非法初值可显式修复");
    }
    private static void VerifyIntegerInputComponent()
    {
        var writes=new List<long>();var reject=false;
        using var field=new DeclaredFieldEditor(new Port("integer.unit",DataType.Integer,
            EditModes:FieldEditModes.Constant,Storage:NumericStorage.Int32),"整数",new ConstantSetting(10L),
            value=>{if(reject)throw new ArgumentException("拒绝整数");writes.Add((long)((ConstantSetting)value).Value!);},
            presentation:new FieldPresentation("integer.unit","整数","基础",0,FieldEditorKind.Scalar)
                {Layout=new FieldLayout("基础","数值",Minimum:0,Maximum:100,DecimalPlaces:0,AllowReference:false,Increment:5)});
        var window=new Window{Content=field,Width=400,Height=150,ShowActivated=false,ShowInTaskbar=false,Left=-32000,Top=-32000};
        void Pump()=>window.Dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        try
        {
            window.Show();window.UpdateLayout();Pump();
            var text=(TextBox)field.InputElement!;
            var native=Descendants<SunEyeVision.Plugin.Wpf.Native.Numeric.HikPcUI.ListView.SliderArrange>(field).Single();
            var box=(ComboBox)native.FindName("CBox");
            var leaf=(SunEyeVision.Plugin.Wpf.Native.Numeric.HikPcUI.ListView.NumericControl)box.Template.FindName("T1",box);
            var up=(System.Windows.Controls.Primitives.RepeatButton)leaf.FindName("UpButton");
            var down=(System.Windows.Controls.Primitives.RepeatButton)leaf.FindName("DownButton");
            Check(text.Text=="10"&&writes.Count==0&&native.Minimum==0&&native.Maximum==100&&native.SliderTickFrequency==5,
                "整数编辑真实原件按声明回填边界步长，不初始化写入");
            text.Text="1.5";Check(field.HasInvalidText&&text.Text=="1.5"&&!field.TryCommitPending()&&writes.Count==0,
                "整数编辑保留非法小数原文且拒绝提交");
            text.Text="101";Check(!field.TryCommitPending()&&writes.Count==0,"整数编辑按声明上限拒绝越界");
            text.Text="20";Check(field.TryCommitPending()&&writes.SequenceEqual(new[]{20L})&&!field.HasInvalidText,
                "整数编辑修正后沿原绑定与正式接纳链单次提交");
            up.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));Pump();
            Check(writes.SequenceEqual(new[]{20L,25L})&&text.Text=="25","整数编辑原向上箭头按声明步长单次接纳");
            down.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));Pump();
            Check(writes.SequenceEqual(new[]{20L,25L,20L}),"整数编辑原向下箭头沿同一接纳链");
            reject=true;text.Text="35";Check(!field.TryCommitPending()&&((ConstantSetting)field.Setting).Value is 20L
                &&text.Text=="35"&&field.HasPendingEdits,"整数编辑宿主拒绝保留原接纳值和待修复原文");
            reject=false;text.Text="40";Check(field.TryCommitPending()&&writes.Last()==40L&&!field.HasInvalidText,
                "整数编辑拒绝后重新提交恢复正式值");
            Check(field.TrySetConstant(100L),"整数编辑允许声明上界");var accepted=writes.Count;
            up.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));Pump();
            Check(writes.Count==accepted&&((ConstantSetting)field.Setting).Value is 100L,"整数编辑上界箭头不越界或重复写入");
            box.IsDropDownOpen=true;Pump();
            Check(box.Template.FindName("PART_Popup",box) is System.Windows.Controls.Primitives.Popup {IsOpen:true},
                "整数编辑打开真实原滑条弹层");
            box.IsDropDownOpen=false;Pump();Check(writes.Count==accepted,"整数编辑仅打开关闭弹层不重复接纳");
            field.IsEnabled=false;
            down.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));Pump();
            Check(writes.Count==accepted,"整数编辑禁用后的迟到箭头不写入草稿");
            field.IsEnabled=true;Pump();
            Check(text.Text=="100"&&writes.Count==accepted,"整数编辑重新启用恢复已接纳值而不提交禁用事件");
            text.Text="55";field.IsEnabled=false;
            down.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));Pump();
            field.IsEnabled=true;Pump();
            Check(text.Text=="55"&&field.HasPendingEdits&&writes.Count==accepted
                &&field.TryCommitPending()&&writes.Last()==55L,"整数编辑禁用恢复保留原待提交文字并沿显式接纳提交");
            accepted=writes.Count;
            field.Dispose();field.Dispose();text.Text="50";
            up.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));Pump();
            Check(writes.Count==accepted&&field.InputElement is null&&!field.TrySetConstant(60L),
                "整数编辑关闭后原叶子迟到事件不写入且重复释放安全");
        }
        finally{window.Content=null;window.Close();}
    }
    private static void VerifyFloatInputComponent()
    {
        var writes=new List<double>();var reject=false;
        using var field=new DeclaredFieldEditor(new Port("float.unit",DataType.Number,
            EditModes:FieldEditModes.Constant,Storage:NumericStorage.Float64),"浮点",new ConstantSetting(0.25d),
            value=>{if(reject)throw new ArgumentException("拒绝浮点");writes.Add((double)((ConstantSetting)value).Value!);},
            presentation:new FieldPresentation("float.unit","浮点","基础",0,FieldEditorKind.Scalar)
                {Layout=new FieldLayout("基础","数值",Minimum:0,Maximum:1,DecimalPlaces:2,AllowReference:false,Increment:0.05)});
        var window=new Window{Content=field,Width=400,Height=150,ShowActivated=false,ShowInTaskbar=false,Left=-32000,Top=-32000};
        void Pump()=>window.Dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        try
        {
            window.Show();window.UpdateLayout();Pump();
            var text=(TextBox)field.InputElement!;
            var native=Descendants<SunEyeVision.Plugin.Wpf.Native.Numeric.HikPcUI.ListView.SliderArrangeFloat>(field).Single();
            var box=(ComboBox)native.FindName("CBox");
            var leaf=(SunEyeVision.Plugin.Wpf.Native.Numeric.HikPcUI.ListView.NumericControl)box.Template.FindName("T1",box);
            var up=(System.Windows.Controls.Primitives.RepeatButton)leaf.FindName("UpButton");
            var down=(System.Windows.Controls.Primitives.RepeatButton)leaf.FindName("DownButton");
            Check(text.Text=="0.25"&&writes.Count==0&&native.DecimalDigits==2&&native.SliderTickFrequency==0.05,
                "浮点编辑原件回填精度范围步长且不初始化写入");
            foreach(var invalid in new[]{"NaN","Infinity","1.1","-0.1","wrong"})
            {text.Text=invalid;Check(!field.TryCommitPending()&&writes.Count==0&&text.Text==invalid,"浮点编辑拒绝非有限越界或非法原文："+invalid);}
            text.Text="0.376";Check(field.TryCommitPending()&&writes.SequenceEqual(new[]{0.38d}),"浮点编辑按声明精度接纳正式double值");
            up.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));Pump();
            Check(writes.Last()==0.43d,"浮点编辑原箭头按小数步长接纳");
            down.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));Pump();
            Check(writes.Last()==0.38d,"浮点编辑反向箭头恢复同精度值");
            reject=true;text.Text="0.55";var accepted=writes.Count;
            Check(!field.TryCommitPending()&&text.Text=="0.55"&&writes.Count==accepted&&field.HasPendingEdits,
                "浮点编辑宿主拒绝保留待修正原文及已接纳值");
            reject=false;Check(field.TryCommitPending()&&writes.Last()==0.55d,"浮点编辑拒绝后可重新接纳");
            accepted=writes.Count;box.IsDropDownOpen=true;Pump();
            Check(box.Template.FindName("PART_Popup",box) is System.Windows.Controls.Primitives.Popup{IsOpen:true},"浮点编辑真实原滑条弹层可打开");
            box.IsDropDownOpen=false;Pump();Check(writes.Count==accepted,"浮点编辑关闭未修改弹层不重复提交");
            field.IsEnabled=false;up.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));Pump();
            field.IsEnabled=true;Pump();Check(writes.Count==accepted&&text.Text=="0.55","浮点编辑禁用迟到输入不写草稿且恢复接纳值");
            field.Dispose();field.Dispose();text.Text="0.65";up.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));Pump();
            Check(writes.Count==accepted&&field.InputElement is null,"浮点编辑关闭解绑原件并拒绝迟到输入");
        }
        finally{window.Content=null;window.Close();}
    }
    private static void VerifyTextInputComponent()
    {
        var writes=new List<string>();var reject=false;
        using var field=new DeclaredFieldEditor(new Port("text.unit",DataType.Text,EditModes:FieldEditModes.Constant),
            "内容",new ConstantSetting("Blob面积:{面积}"),value=>
            {if(reject)throw new ArgumentException("拒绝文本");writes.Add((string)((ConstantSetting)value).Value!);});
        var window=new Window{Content=field,Width=400,Height=150,ShowActivated=false,ShowInTaskbar=false,Left=-32000,Top=-32000};
        void Pump()=>window.Dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        try
        {
            window.Show();window.UpdateLayout();Pump();
            var text=(TextBox)field.InputElement!;
            Check(text is SunEyeVision.Plugin.Wpf.Controls.References.Primitives.DeleteTextBox
                &&text.Text=="Blob面积:{面积}"&&writes.Count==0&&!text.IsReadOnly,"文本编辑真实原件回填且不初始化写入");
            text.Text="新内容";Pump();
            Check(writes.Count==0&&field.HasPendingEdits,"文本原文编辑不提前发布正式值");
            Check(field.TryCommitPending()&&writes.SequenceEqual(new[]{"新内容"}),"文本编辑沿正式接纳链提交原文");
            Check(field.TryCommitPending()&&writes.Count==1,"文本编辑同值提交去重");
            reject=true;text.Text="拒绝内容";Pump();
            Check(!field.TryCommitPending()&&text.Text=="拒绝内容"&&field.HasPendingEdits&&writes.Count==1,
                "文本宿主拒绝保留原文与已接纳值");
            reject=false;Check(field.TryCommitPending()&&writes.Last()=="拒绝内容","文本拒绝后可再次接纳");
            text.Text="";Pump();Check(field.TryCommitPending()&&writes.Last()=="","文本允许显式空字符串且不混同未设置");
            text.Text="保留原文";Pump();var accepted=writes.Count;
            field.IsEnabled=false;text.Text="禁用迟到";Pump();field.IsEnabled=true;Pump();
            Check(writes.Count==accepted&&text.Text=="保留原文"&&field.HasPendingEdits,"文本禁用恢复保留原待提交内容");
            Check(field.TryCommitPending()&&writes.Last()=="保留原文","文本恢复后可提交保留内容");
            accepted=writes.Count;field.Visibility=Visibility.Collapsed;text.Text="隐藏迟到";Pump();
            field.Visibility=Visibility.Visible;Pump();
            Check(writes.Count==accepted&&text.Text=="保留原文","文本隐藏迟到输入不改变正式值或恢复投影");
            field.Dispose();field.Dispose();text.Text="关闭迟到";Pump();
            Check(writes.Count==accepted&&field.InputElement is null&&!field.TryCommitPending(),"文本关闭解绑原件并拒绝迟到提交");
        }
        finally{window.Content=null;window.Close();}
    }
    private static void VerifyColorInputComponent()
    {
        var writes=new List<RecordValue>();var reject=false;
        using var field=new DeclaredFieldEditor(new Port("color.unit",PresentationDeclarations.Rgba),"颜色",
            new ConstantSetting(PresentationDeclarations.Color(10,20,30,200)),value=>
            {if(reject)throw new ArgumentException("拒绝颜色");writes.Add((RecordValue)((ConstantSetting)value).Value!);});
        var window=new Window{Content=field,Width=400,Height=150,ShowActivated=false,ShowInTaskbar=false,Left=-32000,Top=-32000};
        void Pump()=>window.Dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        try
        {
            window.Show();window.UpdateLayout();Pump();
            var input=(SunEyeVision.Plugin.Wpf.Presenters.Display.NativeColorPresenter)field.InputElement!;
            var command=SunEyeVision.Plugin.Wpf.Native.Input.HikPcUI.ListView.ColorPickerControlView.SelectColorCommand;
            void Pick(Color color){command.Execute(new SolidColorBrush(color),input.NativeView);Pump();}
            Check(writes.Count==0&&input.SelectedColor==Color.FromArgb(200,10,20,30),"颜色原件回填RGBA而不初始化写入");
            Pick(Colors.Red);Pick(Colors.Blue);
            Check(writes.Count==2&&writes.Last().Get<long>("b")==255,"颜色原调色命令连续写正式RGBA草稿");
            Check(System.Windows.Data.BindingOperations.IsDataBound(input.NativeView,
                SunEyeVision.Plugin.Wpf.Native.Input.HikPcUI.ListView.ColorPickerControlView.CurrentColorProperty),"颜色原命令保留双向绑定");
            reject=true;Pick(Colors.Green);
            Check(writes.Count==2&&input.SelectedColor==Colors.Blue&&input.NativeView.CurrentColor.Color==Colors.Blue,"颜色拒绝回填同一原件已接纳值");
            reject=false;Pick(Colors.Red);Check(writes.Count==3,"颜色拒绝后继续选择可接纳");
            input.Palette.IsDropDownOpen=true;Pump();
            var popup=(System.Windows.Controls.Primitives.Popup)input.Palette.Template.FindName("PART_Popup",input.Palette);
            var tabs=Descendants<TabControl>(popup.Child).Single();
            Check(popup.IsOpen&&tabs.Items.Count==2,"颜色真实弹层包含默认与更多两页");
            tabs.SelectedIndex=1;Pump();
            Check(Descendants<SunEyeVision.Plugin.Wpf.Native.Input.HikPcUI.ListView.AdvancedColorPickerDialog>(popup.Child).Any(),"颜色更多页显示原高级调色器");
            field.IsEnabled=false;Pump();Check(!input.Palette.IsDropDownOpen,"颜色禁用关闭真实弹层");
            var accepted=writes.Count;Pick(Colors.Green);
            Check(writes.Count==accepted&&input.SelectedColor==Colors.Red,"颜色禁用迟到原命令不写入且恢复正式投影");
            field.IsEnabled=true;Pick(Colors.Blue);Check(writes.Count==accepted+1,"颜色重新启用可继续接纳");
            input.Palette.IsDropDownOpen=true;Pump();field.Visibility=Visibility.Collapsed;Pump();
            Check(!input.Palette.IsDropDownOpen,"颜色隐藏关闭原弹层");
            accepted=writes.Count;Pick(Colors.Green);field.Visibility=Visibility.Visible;Pump();
            Check(writes.Count==accepted&&input.SelectedColor==Colors.Blue,"颜色隐藏迟到输入不污染正式投影");
            field.Dispose();field.Dispose();Pick(Colors.Red);
            Check(writes.Count==accepted&&input.Child is null&&field.InputElement is null,"颜色关闭解绑原件并拒绝迟到命令");
        }
        finally{window.Content=null;window.Close();}
    }
    private static void VerifyParameterHostUnit()
    {
        var definition=(IToolDefinition)new BlobTool();
        var declaration=(IToolSettingsFields)definition;
        SunEyeVision.Plugin.Runtime.ToolSettingsEditSession NewDraft()=>new(definition,
            new FieldSettings(declaration.SettingsSchema.Inputs,declaration.ReadSettings(new BlobSettings()).CopyValues()
                .ToDictionary(item=>item.Key,item=>(ParameterSetting)new ConstantSetting(item.Value),StringComparer.Ordinal)));
        const string fieldId="field:43bda03261df55b88f963bd18f2ba4d9";
        var draft=NewDraft();using var view=new DeclaredSettingsView(draft);
        Check(view.TryCapture(out var initial,out _)&&initial.Get<long>(fieldId)==new BlobSettings().Threshold,
            "参数页宿主单元从正式声明和草稿恢复值");
        var fields=(DeclaredFieldsView)view.Content;var tabs=(TabControl)fields.Content;
        var revision=draft.Revision;
        for(var i=0;i<tabs.Items.Count;i++)view.SelectedPageIndex=i;
        foreach(var tab in tabs.Items.Cast<TabItem>())
            if(tab.Content is SunEyeVision.Plugin.Wpf.Native.Composition.Apps.XmlUI.ParamListView host)
                foreach(var expand in host.Items.Cast<SunEyeVision.Plugin.Wpf.Adapters.Fields.NativeParameterRow>()
                    .Select(row=>row.View).OfType<NativeAdvancedPresenter>()){expand.IsExpanded=true;expand.IsExpanded=false;}
        Check(draft.Revision==revision,"参数页宿主整套导航与高级展开不写草稿");
        Check(view.TrySetFields(new Dictionary<string,ParameterSetting>{{fieldId,new ConstantSetting(101L)}})
            &&view.TryCapture(out var accepted,out _)&&accepted.Get<long>(fieldId)==101,
            "参数页宿主完整接纳链捕获同一正式草稿值");
        view.Bind(draft,new(AcceptCandidate:_=>throw new ArgumentException("宿主拒绝")));
        try{view.TrySetFields(new Dictionary<string,ParameterSetting>{{fieldId,new ConstantSetting(103L)}});}
        catch(ArgumentException error)when(error.Message=="宿主拒绝"){}
        Check(draft.CaptureFields().Get<long>(fieldId)==101,"参数页宿主拒绝候选不污染既有草稿");
        var nestedRejected=false;
        view.Bind(draft,new(ValidateUiState:()=>{nestedRejected=!view.TryCapture(out _,out _);return null;}));
        Check(view.TryCapture(out _,out _)&&nestedRejected,"参数页宿主捕获回调重入被同一锁拒绝");
        var retired=((DeclaredFieldsView)view.Content).Fields.Single(field=>field.FieldId==fieldId);
        var next=NewDraft();
        view.Bind(draft,new(ValidateUiState:()=>{view.Bind(next);return null;}));
        Check(!view.TryCapture(out _,out _)&&next.CaptureFields().Get<long>(fieldId)==new BlobSettings().Threshold,
            "参数页宿主捕获中重绑不发布任一错误快照");
        Check(!retired.TrySetConstant(104L),"参数页宿主退役控件拒绝迟到写入");
        view.Bind(next,new(ValidateUiState:()=>{view.Dispose();return null;}));
        Check(!view.TryCapture(out _,out _),"参数页宿主捕获中关闭拒绝发布");
        view.Dispose();
        Check(view.Content is null&&view.Draft is null&&view.Options is null,"参数页宿主重复关闭释放视图草稿与选项引用");
    }
    private static void VerifyBooleanHostChain()
    {
        // Exercise existing production configuration/history/serialization contracts, not the
        // staged Blob page, whose UI-only threshold intentionally still blocks confirmation.
        Console.WriteLine("TRACE BooleanHostChain: begin PluginManager.LoadPlugins");
        var plugins=new SunEyeVision.Plugin.Infrastructure.Managers.Plugin.PluginManager();plugins.LoadPlugins();
        Console.WriteLine("TRACE BooleanHostChain: PluginManager.LoadPlugins returned");
        var node=new SunEyeVision.Workflow.WorkflowNodeBase("boolean-blob","Blob","Blob",BlobTool.Id)
            {RuntimeConfiguration=SunEyeVision.Workflow.RuntimeToolConfiguration.Create(BlobTool.Id)};
        var flow=new SunEyeVision.Workflow.Workflow("boolean-flow","开关生产合同");flow.Nodes.Add(node);
        var solution=new SunEyeVision.Workflow.Solution();solution.Workflows.Add(flow);
        var host=new SunEyeVision.Workflow.NodeConfigurationEditHost(solution);
        var candidate=host.BeginEdit(flow.Id,node.Id);
        var configuration=candidate.Node.RuntimeConfiguration!;
        var definition=new BlobTool();
        var fieldId=BlobParameterDefinition.Instance.SettingsSchema.Presentation!.Fields
            .Single(field=>field.DisplayName==BlobParameterDefinition.Instance.Label(nameof(BlobSettings.IsAreaFilterEnabled))).FieldId;
        var draft=new SunEyeVision.Plugin.Runtime.ToolSettingsEditSession(definition,
            configuration.Fields.Select(definition.SettingsSchema.Inputs));
        using var view=new DeclaredSettingsView(draft);
        var boolean=((DeclaredFieldsView)view.Content).Fields.Single(field=>field.FieldId==fieldId);
        bool Formal()=>((BlobSettings)node.RuntimeConfiguration!.CaptureSettings()).IsAreaFilterEnabled;
        Check(Formal()&&boolean.TrySetConstant(false)&&Formal(),"开关控件修改正式私有草稿不提前发布节点配置");
        Check(view.TryCapture(out var accepted,out _)&&accepted.Get<bool>(fieldId)==false,
            "开关从真实字段控件沿宿主捕获链取得同一布尔值");
        candidate.Node.RuntimeConfiguration=configuration.WithSettingsFields(accepted);host.Commit(candidate);
        Check(!Formal(),"开关配置通过正式节点发布网关确认");
        host.Undo();Check(Formal(),"开关正式撤销恢复原布尔值");
        host.Redo();Check(!Formal(),"开关正式重做恢复已确认布尔值");
        Check(!((BlobSettings)node.Clone().RuntimeConfiguration!.CaptureSettings()).IsAreaFilterEnabled,
            "开关节点复制保留同一字段配置");
        var path=Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"boolean-runtime-configuration.json");
        File.WriteAllText(path,System.Text.Json.JsonSerializer.Serialize(node.RuntimeConfiguration));
        var reopened=System.Text.Json.JsonSerializer.Deserialize<SunEyeVision.Workflow.RuntimeToolConfiguration>(File.ReadAllText(path))!;
        using var restoredView=new DeclaredSettingsView(new SunEyeVision.Plugin.Runtime.ToolSettingsEditSession(definition,
            reopened.Fields.Select(definition.SettingsSchema.Inputs)));
        var restored=((DeclaredFieldsView)restoredView.Content).Fields.Single(field=>field.FieldId==fieldId);
        Check(((System.Windows.Controls.Primitives.ToggleButton)restored.InputElement!).IsChecked==false,
            "开关正式配置序列化重开回填原件");
        Check(restored.TrySetConstant(true)&&!Formal(),"开关重开后未确认草稿不改变现行配置");
        restoredView.Dispose();Check(!Formal(),"开关取消编辑保留已确认配置");
        const string enumId="field:88b45ab61b965761b901670fd587903f";
        var enumEdit=host.BeginEdit(flow.Id,node.Id);var enumConfiguration=enumEdit.Node.RuntimeConfiguration!;
        using var enumView=new DeclaredSettingsView(new SunEyeVision.Plugin.Runtime.ToolSettingsEditSession(definition,
            enumConfiguration.Fields.Select(definition.SettingsSchema.Inputs)));
        var enumEditor=((DeclaredFieldsView)enumView.Content).Fields.Single(field=>field.FieldId==enumId);
        Check(enumEditor.TrySetConstant("BinaryInv")
            &&node.RuntimeConfiguration!.Fields.Read(enumId) is ConstantSetting {Value:"Binary"},
            "普通枚举正式字段草稿不提前发布正式配置");
        Check(enumView.TryCapture(out var enumValues,out _)&&enumValues.Get<string>(enumId)=="BinaryInv",
            "普通枚举宿主捕获保存正式键而非本地化标签");
        enumEdit.Node.RuntimeConfiguration=enumConfiguration.WithSettingsFields(enumValues);host.Commit(enumEdit);
        Check(node.RuntimeConfiguration!.Fields.Read(enumId) is ConstantSetting {Value:"BinaryInv"},"普通枚举沿正式节点网关确认");
        host.Undo();Check(node.RuntimeConfiguration!.Fields.Read(enumId) is ConstantSetting {Value:"Binary"},"普通枚举撤销恢复原正式键");
        host.Redo();Check(node.RuntimeConfiguration!.Fields.Read(enumId) is ConstantSetting {Value:"BinaryInv"},"普通枚举重做恢复新正式键");
        var enumPath=Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"enumeration-runtime-configuration.json");
        File.WriteAllText(enumPath,System.Text.Json.JsonSerializer.Serialize(node.RuntimeConfiguration));
        var enumRestored=System.Text.Json.JsonSerializer.Deserialize<SunEyeVision.Workflow.RuntimeToolConfiguration>(File.ReadAllText(enumPath))!;
        using var enumRestoredView=new DeclaredSettingsView(new SunEyeVision.Plugin.Runtime.ToolSettingsEditSession(definition,
            enumRestored.Fields.Select(definition.SettingsSchema.Inputs)));
        Check(((ComboBox)((DeclaredFieldsView)enumRestoredView.Content).Fields.Single(field=>field.FieldId==enumId).InputElement!).SelectedValue is "BinaryInv",
            "普通枚举正式配置序列化重开回填同一候选键");
    }
    [STAThread] private static int Main()
    {
        try
        {
            // Emulate the designer's WPF flag before creating any view. No Application or host bootstrap.
            DesignerProperties.IsInDesignModeProperty.OverrideMetadata(typeof(FrameworkElement),new FrameworkPropertyMetadata(true));
            VerifyContentToggleComponent();
            VerifyBooleanComponent();
            VerifyEnumerationComponent();
            VerifyIntegerRadioComponent();
            VerifyIntegerInputComponent();
            VerifyFloatInputComponent();
            VerifyTextInputComponent();
            VerifyColorInputComponent();
            VerifyParameterHostUnit();
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
                    var reset=Descendants<Button>(entry).Single(button=>button.Name=="ResetButton");
                    Check(reset.Visibility==(page==1?Visibility.Visible:Visibility.Collapsed)&&!reset.IsEnabled,
                        "重置入口只在运行参数页出现，宿主尚未接入时不可执行："+page);
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
                var text=Descendants<SunEyeVision.Plugin.Wpf.Controls.References.Primitives.DeleteTextBox>(width.View).Single();
                var popup=Descendants<SunEyeVision.Plugin.Wpf.Controls.Popups.PopupButton>(width.View).Single(item=>item.Name=="rt");
                var other=components.Where(item=>item.Key!="detect.inherit-way"&&item.Key!="detect.rect.width").ToDictionary(item=>item.Key,item=>sample.Fields.Fields.Single(field=>field.FieldId==item.Key).Setting);
                text.Text="0.125";text.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
                Check(width.TryCommitPending()&&width.Setting is ConstantSetting{Value:0.125d}&&sample.TryCapture(out _,out _),"设计时同槽常量沿原文本及owner捕获链接纳");
                Render(entry,"native-four-way-constant-xaml-entry.png",1020,1100);
                Check(Application.Current is null&&!text.IsReadOnly&&ReferenceEquals(text,Descendants<SunEyeVision.Plugin.Wpf.Controls.References.Primitives.DeleteTextBox>(width.View).Single())
                    &&ReferenceEquals(popup,Descendants<SunEyeVision.Plugin.Wpf.Controls.Popups.PopupButton>(width.View).Single(item=>item.Name=="rt"))
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
                Check(Application.Current is null&&displayFields.Fields.Count==20
                    &&displayFields.Fields.Single(field=>field.FieldId=="text.content").InputElement is SunEyeVision.Plugin.Wpf.Controls.References.Primitives.DeleteTextBox,
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
                var styleButtons=Descendants<SunEyeVision.Plugin.Wpf.Controls.Popups.PopupButton>(entry)
                    .Where(item=>Equals(item.Tag,"NativeGraphicStylePopup")).ToArray();
                var eyes=Descendants<System.Windows.Controls.Primitives.ToggleButton>(entry)
                    .Where(item=>Equals(item.Tag,"NativeGraphicVisibility")).ToArray();
                Check(eyes.Length==3&&styleButtons.Count(item=>item.Visibility==Visibility.Visible)==2
                    &&styleButtons.Count(item=>item.Visibility==Visibility.Collapsed)==1,
                    "设计图形页同时创建完整、无粗细和仅眼睛的原组合");
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
            var expanded=new SunEyeVision.Plugin.Wpf.Controls.Parameters.ParamSubscribeWindow();
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
            nativeInputs.Children.Add(new SunEyeVision.Plugin.Wpf.Controls.Popups.PopupButton());
            nativeInputs.Children.Add(new SunEyeVision.Plugin.Wpf.Controls.References.Primitives.DeleteTextBox{Text="设计来源"});
            nativeInputs.Children.Add(new SunEyeVision.Plugin.Wpf.Controls.References.Primitives.SubTreeView{Height=100});
            nativeInputs.Children.Add(SunEyeVision.Plugin.Wpf.Presenters.Input.NativeInputFactory.Enumeration());
            Render(nativeInputs,"native-input-family-independent.png",400,230);
            Check(Application.Current is null&&nativeInputs.Children.Count==4,
                "原Apps弹层文本树及枚举可无Application独立装配资源");
            using var subscription=new DeclaredFieldEditor(new Port("reference",DataType.Integer,EditModes:FieldEditModes.Reference),
                "来源",new ReferenceSetting(new OutputReference("producer","value",DataType.Integer)),_=>throw new Exception("设计渲染不得写入"),
                ReferenceFieldOptions.Empty);
            Render(subscription,"native-reference-composition-independent.png",400,65);
            Check(Application.Current is null&&Descendants<SunEyeVision.Plugin.Wpf.Controls.Popups.PopupButton>(subscription).Count()==2
                &&Descendants<SunEyeVision.Plugin.Wpf.Controls.References.Primitives.DeleteTextBox>(subscription).Any(text=>text.IsReadOnly),
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
            Check(fields.Fields.Count==55&&replacementInputCount(fields)==4,
                "独立无参真实参数视图包含55设置及4输入，未启动宿主");
            var inputFields=InputFields(fields);
            var image=inputFields.Single(field=>field.FieldId==BlobTool.ImageInput);
            Check(image.InputElement is ComboBox combo&&combo.Resources.MergedDictionaries.Any(dictionary=>dictionary.Source?.OriginalString.EndsWith("SourceComboBoxes.xaml")==true)
                &&Descendants<Image>(image).Any(icon=>icon.Source is BitmapSource bitmap&&bitmap.PixelWidth>0),
                "真实设计视图输入源使用VM EnumerationG下拉及原图标");
            var defaults=new BlobSettings();
            const string threshold="field:43bda03261df55b88f963bd18f2ba4d9";
            var captured=first.CaptureAcceptedFields();
            Check(captured.Get<long>(threshold)==defaults.Threshold,
                "设计草稿默认值来自真实XML");
            var schema=presenter.Draft!.Schema;

            var oldField=fields.Fields.Single(field=>field.FieldId==threshold);
            Check(!first.TryCapture(out _,out var previewError)&&previewError.Contains("未接入",StringComparison.Ordinal),
                "未接入的ROI和结果组合明确阻止整窗提交");
            var independent=second.CaptureAcceptedFields();
            Check(independent.Get<long>(threshold)==defaults.Threshold,
                "两个设计视图不共享可变草稿");
            var replacement=BlobDesignData.CreateParameterContext();presenter.Bind(replacement.Draft,replacement.Options);
            Check(!oldField.TrySetConstant(102L)&&first.CaptureAcceptedFields().Get<long>(threshold)==defaults.Threshold,
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
                if(index==0)
                {
                    var position=fields.Fields.Single(field=>field.FieldId=="field:c95f3a26edf55097863a7ec47d17cce5");
                    var pose=replacement.Options.AdditionalAfterContent!;
                    Check(pose.Visibility==Visibility.Collapsed,"位置修正关闭时来源输入隐藏");
                    Check(position.TrySetConstant(true)&&pose.Visibility==Visibility.Visible,"位置修正开启时在开关后显示来源输入");
                    var displayRows=(SunEyeVision.Plugin.Wpf.Presenters.Display.NativeDisplayUiDraftRows)replacement.Options.AdditionalPageContents!["结果显示"];
                    Check(new[]{4,5}.All(item=>((SunEyeVision.Plugin.Wpf.Adapters.Fields.NativeParameterRow)displayRows.Items[item]).View!.Visibility==Visibility.Visible),
                        "基本页位置修正开启同步显示结果页两项点配置");
                    var assist=(SunEyeVision.Plugin.Wpf.Presenters.Regions.NativeAssistUiDraftControls)pose;
                    var assistRows=assist.Children.OfType<SunEyeVision.Plugin.Wpf.Fields.DeclaredFieldEditor>().ToArray();
                    Check(assistRows.Length==13&&assistRows.Count(row=>row.Visibility==Visibility.Visible)==1,
                        "位置修正默认按信息展示唯一来源");
                    Check(assist.TrySetMode(1)&&assist.IsUnimplementedMode&&assistRows.Count(row=>row.Visibility==Visibility.Visible)==8,
                        "位置修正按点显示两点和六项尺度角度");
                    Check(assist.TrySetMode(2)&&assist.IsUnimplementedMode&&assistRows.Count(row=>row.Visibility==Visibility.Visible)==10,
                        "位置修正按坐标显示四个坐标和六项尺度角度");
                    Check(assistRows.Skip(1).All(row=>row.IsEnabled)&&assist.TrySetMode(0)&&!assist.IsUnimplementedMode,
                        "位置来源支持原订阅界面试用且可切回正式修正信息");
                    Check(position.TrySetConstant(false)&&pose.Visibility==Visibility.Collapsed,"关闭位置修正后来源输入重新隐藏");
                    Check(new[]{4,5}.All(item=>((SunEyeVision.Plugin.Wpf.Adapters.Fields.NativeParameterRow)displayRows.Items[item]).View!.Visibility==Visibility.Collapsed),
                        "基本页位置修正关闭同步隐藏结果页两项点配置");
                    var roi=Descendants<SunEyeVision.Plugin.Wpf.Presenters.Regions.NativeRoiUiDraftControls>(first).Single();
                    var outputMask=(SunEyeVision.Plugin.Wpf.Fields.DeclaredFieldEditor)roi.Children[0];
                    Check(outputMask.FieldId=="ui-only-output-mask"&&outputMask.TrySetConstant(true)&&outputMask.TrySetConstant(false),
                        "输出掩膜使用原开关并能切换临时UI状态");
                    Check(roi.IsEnabled&&Descendants<SunEyeVision.Plugin.Wpf.Native.Composition.Apps.XmlUI.ParamTabControl.RoiSelectControl>(roi).Count()==2,
                        "真实Blob基本页显示可操作的检测与屏蔽原形状控件，当前状态仅用于界面试用");
                    bool SetRadio(int index,long value)=>(bool)roi.Children[index].GetType().GetMethod("TrySetConstant")!.Invoke(roi.Children[index],new object[]{value})!;
                    Check(position.TrySetConstant(true),"开启位置修正以检查区域类型跨页显隐");
                    Check(SetRadio(1,1)&&roi.Children[2].Visibility==Visibility.Visible&&roi.Children[4].Visibility==Visibility.Collapsed,
                        "ROI图像类型显示图像来源并隐藏图形选择");
                    Check(position.View.Visibility==Visibility.Collapsed&&pose.Visibility==Visibility.Collapsed
                        &&new[]{4,5}.All(item=>((SunEyeVision.Plugin.Wpf.Adapters.Fields.NativeParameterRow)displayRows.Items[item]).View!.Visibility==Visibility.Collapsed),
                        "图像类型同时隐藏位置修正和结果页点配置");
                    Check(SetRadio(1,0)&&position.View.Visibility==Visibility.Visible&&pose.Visibility==Visibility.Visible,
                        "恢复图形类型恢复原位置修正界面状态");
                    Check(position.TrySetConstant(false),"关闭位置修正恢复基础检查状态");
                    Check(SetRadio(1,0)&&SetRadio(3,1)&&roi.Children[4].Visibility==Visibility.Collapsed&&roi.Children[5].Visibility==Visibility.Visible,
                        "ROI继承显示继承方式并隐藏绘制形状");
                    Render(first,"parameters-roi-rectangle-source.png",600,680);
                    Check(roi.Children[6].Visibility==Visibility.Visible&&SetRadio(5,1)&&roi.Children[6].Visibility==Visibility.Collapsed
                        &&roi.Children[8].Visibility==Visibility.Visible&&SetRadio(5,2)&&roi.Children[7].Visibility==Visibility.Visible
                        &&SetRadio(5,3)&&roi.Children[9].Visibility==Visibility.Visible,
                        "四种继承方式分别显示矩形来源、矩形参数、圆形来源及圆形参数");
                    Render(first,"parameters-roi-circle-parameters.png",600,680);
                    Check(SetRadio(5,1),"切换到矩形参数以检查对应布局");
                    Render(first,"parameters-roi-rectangle-parameters.png",600,680);
                    Check(SetRadio(5,2),"切换到圆形区域以检查对应布局");
                    Render(first,"parameters-roi-circle-source.png",600,680);
                    Check(SetRadio(3,0)&&roi.Children[4].Visibility==Visibility.Visible,"切回绘制恢复原形状控件");
                    var shape=(FrameworkElement)roi.Children[4];var context=shape.DataContext;
                    var command=(System.Windows.Input.ICommand)context.GetType().GetProperty("ValueChangedCommand")!.GetValue(context)!;
                    command.Execute(SunEyeVision.Plugin.Wpf.Native.Dependencies.Apps.XmlParser.ParamTab.RoiType.Box);
                    Check((bool)context.GetType().GetProperty("BoxA")!.GetValue(context)!&&!(bool)context.GetType().GetProperty("FullScreenA")!.GetValue(context)!,
                        "原ROI形状命令更新界面选择且保持互斥");
                    var maskContext=Descendants<SunEyeVision.Plugin.Wpf.Native.Composition.Apps.XmlUI.ParamTabControl.RoiSelectControl>(roi).Last().DataContext;
                    var maskCommand=(System.Windows.Input.ICommand)maskContext.GetType().GetProperty("ValueChangedCommand")!.GetValue(maskContext)!;
                    Check((bool)maskContext.GetType().GetProperty("FullScreenEnableA")!.GetValue(maskContext)!
                        &&!(bool)maskContext.GetType().GetProperty("FullScreenA")!.GetValue(maskContext)!,
                        "屏蔽区提供全屏入口但默认不选择全屏");
                    maskCommand.Execute(SunEyeVision.Plugin.Wpf.Native.Dependencies.Apps.XmlParser.ParamTab.RoiType.FullScreen);
                    Check((bool)maskContext.GetType().GetProperty("FullScreenA")!.GetValue(maskContext)!,"屏蔽区全屏入口可选择");
                }
                if(index==1)
                {
                    var page=(SunEyeVision.Plugin.Wpf.Native.Composition.Apps.XmlUI.ParamListView)((TabItem)tabs.Items[index]).Content;
                    var advanced=page.Items.Cast<SunEyeVision.Plugin.Wpf.Adapters.Fields.NativeParameterRow>()
                        .Select(row=>row.View).OfType<SunEyeVision.Plugin.Wpf.Presenters.Parameters.NativeAdvancedPresenter>().Single();
                    var circle=fields.Fields.Single(field=>field.FieldId=="field:7d59f57f71c65911b88bae5f90d994d9").View;
                    var binaryOutput=fields.Fields.Single(field=>field.FieldId=="field:73f2c01e7b385e0ab2d35f739a88e718");
                    var binaryRow=(SunEyeVision.Plugin.Wpf.Native.Composition.Apps.XmlUI.ParamTab.LeftRightControl)
                        ((SunEyeVision.Plugin.Wpf.Fields.DeclaredFieldEditor)binaryOutput.View).EditorBody;
                    var expectedLabelWidth=SunEyeVision.Plugin.Wpf.Native.Common.Localization.AppLocalizationService.GetLang("Language")=="English"?170d:89d;
                    Check(page.ChildLeftWidth==expectedLabelWidth&&binaryRow.ChildLeftWidth==expectedLabelWidth,"运行参数页标签使用当前语言原控件列");
                    var unavailable=replacement.Options.AdditionalAnchoredContents!.Select(item=>item.Content)
                        .OfType<SunEyeVision.Plugin.Wpf.Fields.DeclaredFieldEditor>()
                        .Where(editor=>editor.FieldId.StartsWith("ui-only-run.",StringComparison.Ordinal)).ToArray();
                    Check(unavailable.Length>0&&unavailable.All(editor=>!editor.IsEnabled&&editor.ToolTip is string),
                        "尚未接入的运行参数显示为不可误用的占位控件");
                    Check(!first.TryCapture(out _,out var uiDraftError)&&uiDraftError.Contains("未接入",StringComparison.Ordinal),
                        "真实设计视图保持未接入组合的整窗提交保护");
                    var areaSwitch=fields.Fields.Single(field=>field.FieldId=="field:e9845ab7a46e5ee9b42310bef5a11c19");
                    var booleanPorts=((IToolSettingsFields)new SunEyeVision.Tool.BlobAnalysis.Runtime.BlobTool()).SettingsSchema.Inputs
                        .Where(port=>port.Type==DataType.Boolean).ToArray();
                    Check(booleanPorts.Length>0&&booleanPorts.All(port=>port.EditModes==FieldEditModes.Constant),
                        "Blob普通Boolean声明仅开放原模板常量开关，不从继承SubData推定订阅能力");
                    var choicePorts=((IToolSettingsFields)new BlobTool()).SettingsSchema.Inputs.Where(port=>port.Choices is not null).ToArray();
                    Check(choicePorts.Length==5&&choicePorts.All(port=>port.EditModes==FieldEditModes.Constant),
                        "Blob五个普通枚举仅开放原下拉常量选择，不额外开放参数订阅");
                    var choice=choicePorts[0];var acceptedChoices=first.CaptureAcceptedFields();
                    var choiceRejected=false;
                    try{acceptedChoices.Apply(new Dictionary<string,ParameterSetting>{{choice.Id,
                        new ReferenceSetting(new OutputReference("choice-source","choice",choice.Type))}});}
                    catch(ArgumentException){choiceRejected=true;}
                    Check(choiceRejected&&acceptedChoices.Read(choice.Id) is ConstantSetting,
                        "Blob普通枚举引用在唯一字段提案入口拒绝，既有常量保持不变");
                    var booleanIds=booleanPorts.Select(port=>port.Id).ToHashSet(StringComparer.Ordinal);
                    var booleanEditors=fields.Fields.Where(field=>booleanIds.Contains(field.FieldId)).ToArray();
                    Check(booleanEditors.Any(field=>field.FieldId=="field:e9845ab7a46e5ee9b42310bef5a11c19")
                        &&booleanEditors.All(field=>!Descendants<System.Windows.Controls.Primitives.ToggleButton>(field.View).Any(button=>button.Content is string action&&action is "⇄" or "✎")),
                        "真实Blob普通布尔行没有额外编辑订阅切换按钮");
                    Check(areaSwitch.TrySetConstant(false)&&areaSwitch.TrySetConstant(true),"正式面积开关可在独立设计草稿中切换");
                    var mode=fields.Fields.Single(field=>field.FieldId=="field:8ca20000000040008000000000000001");
                    var modeCombo=(ComboBox)mode.InputElement!;
                    Check(modeCombo.Items.Count==6,"设计视图通过正式下拉显示六种阈值模式");
                    Check(!mode.TrySetConstant("None")&&!mode.TrySetConstant("DoubleThreshold")&&mode.TrySetConstant("SingleThreshold"),
                        "未实现模式不可写入设计草稿，单阈值可用");
                    Check(!Descendants<SunEyeVision.Plugin.Wpf.Presenters.Parameters.NativeThresholdUiDraftControls>(first).Any(),
                        "设计视图与运行时共用正式阈值控件而非旧替身");
                    Render(first,"parameters-six-thresholds.png",600,680);
                    var beforeExpansion=replacement.Draft.Revision;
                    Check(!advanced.IsExpanded&&circle.Visibility==Visibility.Collapsed,"运行参数高级项默认收起");
                    advanced.IsExpanded=true;
                    Check(circle.Visibility==Visibility.Visible,"运行参数高级展开显示正式圆度筛选");
                    Render(first,"parameters-run-advanced.png",600,900);
                    advanced.IsExpanded=false;
                    Check(circle.Visibility==Visibility.Collapsed,"运行参数高级收起恢复隐藏");
                    Check(replacement.Draft.Revision==beforeExpansion,"高级展开和收起不改变参数草稿");
                }
                if(index==2)
                {
                    var judgments=Descendants<SunEyeVision.Plugin.Wpf.Presenters.Parameters.NativeJudgmentUiDraftControls>(first).Single();
                    Check(judgments.Children.Count==17&&judgments.Children.Cast<FrameworkElement>().Count(view=>view.Visibility==Visibility.Visible)==5,
                        "原结果判定包含四项基础项、展开及十二项高级判定");
                    var judgmentExpand=judgments.Children.OfType<SunEyeVision.Plugin.Wpf.Presenters.Parameters.NativeAdvancedPresenter>().Single();
                    judgmentExpand.IsExpanded=true;
                    Check(judgments.Children.Cast<FrameworkElement>().All(view=>view.Visibility==Visibility.Visible),
                        "结果判定高级展开显示全部原厂判定组合");
                    Render(first,"parameters-judgments-expanded.png",600,900);
                    judgmentExpand.IsExpanded=false;
                    Check(fields.Fields.Where(field=>field.FieldId=="field:4712e8a0ae495b84a014e0a0880efa0f"
                        ||field.FieldId=="field:c1d661cc9b735a37a41776b91a655948").All(field=>field.View.Visibility==Visibility.Collapsed),
                        "旧结果判定不与原厂界面双轨显示");
                    var display=Descendants<SunEyeVision.Plugin.Wpf.Presenters.Display.NativeDisplayUiDraftRows>(first).Single();
                    Check(display.IsEnabled&&display.Items.Count==16,
                        "真实Blob结果页包含九项原图形声明与七项文字设置，位置修正图形初始隐藏");
                    var textRows=display.Items.Cast<SunEyeVision.Plugin.Wpf.Adapters.Fields.NativeParameterRow>()
                        .Where(row=>row.GroupName.NameA=="文本显示").ToArray();
                    Check(textRows.Length==7&&textRows.All(row=>row.View is SunEyeVision.Plugin.Wpf.Fields.DeclaredFieldEditor editor
                        ?((FrameworkElement)editor.EditorBody).Height==32:row.View?.Height==32),"文本显示七行采用原厂32设备无关像素行高");
                    var coordinates=textRows.Select(row=>row.View).OfType<SunEyeVision.Plugin.Wpf.Fields.DeclaredFieldEditor>()
                        .Where(editor=>editor.FieldId is "unavailable.text.x" or "unavailable.text.y").ToArray();
                    Check(coordinates.Length==2&&coordinates.All(editor=>editor.Setting is SunEyeVision.Plugin.Contracts.ConstantSetting{Value:0d}
                        &&editor.InputElement?.GetType().Name=="NativeReferencePresenter"),
                        "文本位置显示临时零值与同槽来源入口");
                    using(var textGroup=new SunEyeVision.Plugin.Wpf.Presenters.Display.NativeDisplayUiDraftRows("图形显示",
                        Array.Empty<SunEyeVision.Plugin.Wpf.Presenters.Display.NativeDisplayUiDraftEntry>(),includeTextSettings:true))
                    {
                        Render(textGroup,"blob-text-settings.png",600,420);
                        var content=textGroup.Items.Cast<SunEyeVision.Plugin.Wpf.Adapters.Fields.NativeParameterRow>()
                            .Select(row=>row.View).OfType<SunEyeVision.Plugin.Wpf.Fields.DeclaredFieldEditor>()
                            .Single(editor=>editor.FieldId=="unavailable.text.content");
                        Check(content.InputElement is TextBox textBox&&textBox.ActualHeight>=28,"内容输入框按原行高实际伸展");
                    }
                }
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
            Check(Descendants<DataGrid>(result).Single().Columns.Count==new BlobTool().ResultProjection.Columns.Count
                &&Descendants<DataGrid>(result).Single().TryFindResource("CurResultDataGridStyle") is Style
                &&Descendants<DataGrid>(result).Single().Items.Count==2,
                "真实结果视图使用声明列、公共原结果表资源和同一发布链的两行设计数据");
            result.Clear();
            Check(Descendants<DataGrid>(result).Single().Items.Count==0,"真实结果视图可切换为空结果");
            Check(Application.Current is null&&!AppDomain.CurrentDomain.GetAssemblies().Any(assembly=>
                assembly.GetName().Name is "SunEyeVision.UI" or "SunEyeVision.Workflow" or "halcondotnetxl" or "halcondotnet"),
                "独立显示未加载主程序、工作流或HALCON程序集");
            // Load the actual root markup without running its x:Class constructor, as a designer surface can do.
            var markup=ReadRootMarkup("BlobParameterEditor.xaml");
            var root=(UserControl)XamlReader.Parse(markup);
            var markupPresenter=(DeclaredSettingsView)root.Content;
            Check(markupPresenter.Draft is not null&&((DeclaredFieldsView)markupPresenter.Content).Fields.Count==55,
                "根XAML不执行外层构造函数也能由真实子视图取得设计草稿");
            markupPresenter.Dispose();
            var resultRoot=(UserControl)XamlReader.Parse(ReadRootMarkup("BlobResultView.xaml"));
            Render(resultRoot,"results-root-markup.png",900,350);
            Check(Descendants<DataGrid>(resultRoot).Single() is {Columns.Count:>0,Items.Count:2},
                "结果根XAML不执行外层构造函数也能用正式声明表显示设计数据");
            ((IDisposable)((ContentControl)resultRoot.Content).Content).Dispose();
            first.Dispose();
            Check(presenter.Draft is null&&presenter.Content is null,"释放视图移除草稿和控件树");
            // Only after proving the designer's dependency boundary, load the real workflow
            // contracts for the separate Boolean configuration/history roundtrip checks.
            VerifyBooleanHostChain();
            Console.WriteLine($"独立设计组装及开关生产合同检查：{_count}组通过；生产合同检查在独立预览断言之后执行，不代替Visual Studio实际设计器与用户视觉验收。");return 0;
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

