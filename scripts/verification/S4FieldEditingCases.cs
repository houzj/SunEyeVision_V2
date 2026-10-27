using System.IO;
using System.Reflection;
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
            input.Text="210";Require(!number.HasInvalidText&&Equals(((ConstantSetting)current).Value,210L)&&writes==1,"数字修正未恢复");
            var nativeInput=Descendants(number).OfType<NativeNumberField>().Single().InputControl!;
            ((RepeatButton)nativeInput.FindName("UpButton")).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));Pump();
            Require(Equals(((ConstantSetting)current).Value,211L)&&writes==2,"无界原数值箭头未单次写入草稿；writes="+writes);
            Console.WriteLine("通过：声明Int32范围、整数格式、非法原文保留和修正恢复");

            var exact=1d;var floating=new DeclaredFieldEditor(new("ratio",DataType.Number),"比例",new ConstantSetting(exact),next=>exact=(double)((ConstantSetting)next).Value!);
            Show(floating);((TextBox)floating.InputElement!).Text="NaN";Require(floating.HasInvalidText&&exact==1d,"NaN通过声明校验");
            ((TextBox)floating.InputElement!).Text="0.5";Require(!floating.HasInvalidText&&exact==.5d,"有限数值不能恢复");
            Console.WriteLine("通过：非有限数值拒绝，独立字段错误状态互不污染");

            var choiceValue="Binary";var choice=new DeclaredFieldEditor(new("type",DataType.Text,Choices:new TextChoiceConstraint(new[]{"Binary","BinaryInv"})),"极性",
                new ConstantSetting(choiceValue),next=>choiceValue=(string)((ConstantSetting)next).Value!);
            ((ComboBox)choice.InputElement!).SelectedItem="BinaryInv";Require(choiceValue=="BinaryInv","已声明选项未提交到草稿");
            Require(((ComboBox)choice.InputElement!).Items.Count==2,"选项不是来自正式闭合声明");
            Console.WriteLine("通过：下拉选项直接消费正式声明");

            var color=PresentationDeclarations.Color(10,20,30);var colorWrites=0;
            var record=new DeclaredFieldEditor(new("color",PresentationDeclarations.Rgba),"颜色",new ConstantSetting(color),next=>{color=(RecordValue)((ConstantSetting)next).Value!;colorWrites++;});
            Show(record);var colorInput=(ColorField)record.InputElement!;
            colorInput.SelectedColor=Color.FromArgb(200,255,20,30);
            Require(!record.HasInvalidText&&color.Get<long>("r")==255&&color.Get<long>("a")==200&&colorWrites==1,"实际颜色控件未原子更新RGBA记录");
            var channelType=TypeRef.RecordOf("test.channel",new[]{new Port("r",DataType.Integer,Storage:NumericStorage.UInt8)});
            var channelValue=new RecordValue(channelType,new Dictionary<string,object?>{{"r",10L}});var channelWrites=0;
            var channel=new DeclaredFieldEditor(new("channel",channelType),"通道",new ConstantSetting(channelValue),next=>{channelValue=(RecordValue)((ConstantSetting)next).Value!;channelWrites++;});
            Show(channel);var red=Descendants(channel).OfType<DeclaredFieldEditor>().Single(field=>field.FieldId=="r");
            ((TextBox)red.InputElement!).Text="256";Require(channel.HasInvalidText&&channelValue.Get<long>("r")==10&&channelWrites==0,"记录成员溢出仍更新了记录");
            ((TextBox)red.InputElement!).Text="255";Require(!channel.HasInvalidText&&channelValue.Get<long>("r")==255&&channelWrites==1,"记录成员纠正未原子更新");
            Console.WriteLine("通过：真实颜色控件原子写RGBA；通用记录UInt8非法原文拦截及修正恢复");

            ParameterSetting bound=new ConstantSetting(7L);
            var reference=new OutputReference("producer","port",TypeRef.SequenceOf(DataType.Integer),selection:new BindingSelection.AtConstant(0));
            var binding=new DeclaredFieldEditor(new("optional",DataType.Integer,Required:false,EditModes:FieldEditModes.Constant|FieldEditModes.Reference),"可选数值",bound,
                next=>bound=next,_=>new ReferenceSetting(reference));
            Show(binding);Click(binding,"切换为订阅");Click(binding,"选择订阅来源");Require(bound is ReferenceSetting source&&source.Reference==reference,"绑定意图未交给宿主");
            Click(binding,"切换为数值编辑");Require(bound is ConstantSetting restored&&Equals(restored.Value,7L),"常量备份没有恢复");
            Click(binding,"清除可选项");Require(bound is UnsetSetting,"清除可选项误变为空值或空集合");
            Console.WriteLine("通过：宿主选择绑定、恢复常量及明确Unset；控件不自行执行或发布");

            var rejected=3L;var reject=new DeclaredFieldEditor(new("host-reject",DataType.Integer),"宿主拒绝",new ConstantSetting(rejected),_=>throw new InvalidOperationException("编辑范围已失效"));
            Show(reject);((TextBox)reject.InputElement!).Text="4";Require(reject.HasInvalidText&&reject.Error.Contains("编辑范围")&&Equals(((ConstantSetting)reject.Setting).Value,3L),"宿主拒绝后控件冒称写入成功");
            var names=typeof(DeclaredFieldEditor).Assembly.GetReferencedAssemblies().Select(assembly=>assembly.Name!).ToArray();
            Require(!names.Any(name=>name.Contains("Plugin.SDK")||name.Contains("Halcon",StringComparison.OrdinalIgnoreCase)||name=="SunEyeVision.UI"),"公共字段层倒置依赖");
            Console.WriteLine("通过：失效范围拒绝且保留原值；公共字段程序集不依赖SDK、HALCON或UI宿主");
            var product=Environment.GetEnvironmentVariable("S2_PRODUCT_ROOT")!;
            var manager=new PluginManager();manager.LoadPlugins(Path.Combine(product,"plugins"));
            manager.AssertCompleteReleaseToolSet(ToolRegistry.GetAllToolMetadata().Select(tool=>tool.Id));
            var definition=(IToolSettingsFields)ToolRegistry.CreateRuntime("BlobAnalysis");var settings=new SunEyeVision.Tool.BlobAnalysis.Runtime.BlobSettings();
            var access=definition.SettingsSchema;Require(access.Inputs.Count==54&&access.Inputs.Select(p=>p.Id).Distinct().Count()==54,"XML设置字段不完整或重复");
            using var xmlStream=definition.GetType().Assembly.GetManifestResourceStream("BlobAnalysis.BlobParameters.xml")!;
            var xml=System.Xml.Linq.XDocument.Load(xmlStream);var rule=xml.Descendants("Judgment").Single(e=>(string?)e.Attribute("key")=="total-area");
            var key=(string)rule.Attribute("minimumFieldId")!;
            var updated=(SunEyeVision.Tool.BlobAnalysis.Runtime.BlobSettings)definition.ApplySettings(settings,new Dictionary<string,object?>{{key,12d}});
            Require(updated.Judgments.Single(r=>r.Key=="total-area").Minimum==12&&updated.Judgments.Single(r=>r.Key=="max-area")==settings.Judgments.Single(r=>r.Key=="max-area"),"嵌套同名最小值写错判定");
            Console.WriteLine("通过：正式Runtime XML的54个唯一设置字段及嵌套判定精确读写，不创建旧Blob参数");
            var node=new WorkflowNodeBase("blob","Blob","Blob","BlobAnalysis"){RuntimeConfiguration=RuntimeToolConfiguration.Create("BlobAnalysis")};
            var flow=new SunEyeVision.Workflow.Workflow("flow","字段草稿");flow.Nodes.Add(node);var solution=new Solution();solution.Workflows.Add(flow);
            var edits=(DocumentEditSession)Activator.CreateInstance(typeof(DocumentEditSession),BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{solution},null)!;
            using var control=new SunEyeVision.UI.Services.Editing.RuntimeNodeConfigurationControl(node);
            using var context=new SunEyeVision.UI.Services.Editing.ProductionEditorContext(edits.Host,flow.Id,node,()=>true,control.BindDraft,documentEdits:edits);
            control.BindDraft(context.DraftNode);SunEyeVision.Plugin.SDK.UI.Controls.ToolEditorCommit.AttachDraftContext(control,context);
            Show(control);var fields=Descendants(control).OfType<DeclaredFieldsView>().Single();
            var thresholdId=access.Presentation!.Fields.Single(f=>f.DisplayName==SunEyeVision.Tool.BlobAnalysis.Runtime.BlobParameterDefinition.Instance.Label("Threshold")).FieldId;
            ((TextBox)fields.Fields.Single(f=>f.FieldId==thresholdId).InputElement!).Text="201";
            int Threshold()=>((SunEyeVision.Tool.BlobAnalysis.Runtime.BlobSettings)node.RuntimeConfiguration!.CaptureSettings()).Threshold;
            Require(Threshold()==128,"公共控件提前写入正式配置");
            Require(control.TryValidateEditor(out _)&&context.TryCommitEditor(out var error),"Runtime草稿不能确认");Require(Threshold()==201,"确认没有发布");
            Pump();((TextBox)Descendants(control).OfType<DeclaredFieldsView>().Single().Fields.Single(f=>f.FieldId==thresholdId).InputElement!).Text="215";
            context.Discard();Require(Threshold()==201,"取消改变已确认值");edits.Close();
            Console.WriteLine("通过：真实Runtime字段控件只写私有草稿，确认发布当前范围，取消保留已确认值");
            Console.WriteLine("S4字段编辑：8组通过；本轨道不代完整默认配置、ROI、保存重开或人工验收。");return 0;
        }
        catch(Exception error){Console.Error.WriteLine(error);return 1;}
        finally{owner?.Close();Application.Current?.Shutdown();}
    }
    private static void Click(DependencyObject control,string tooltip)
    {
        Descendants(control).OfType<ButtonBase>().Single(button=>Equals(button.ToolTip,tooltip)).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));Pump();
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        foreach(var item in LogicalTreeHelper.GetChildren(parent))if(item is DependencyObject child)
        {yield return child;foreach(var descendant in Descendants(child))yield return descendant;}
    }
    private static void Require(bool condition,string error){if(!condition)throw new Exception(error);}
}
