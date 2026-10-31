using System.Collections;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf;
using SunEyeVision.Plugin.Wpf.Fields;
using SunEyeVision.Plugin.Wpf.Adapters.References;

internal static class S2DocumentEditingCases
{
    private static readonly List<object> Rows=new();
    [STAThread] private static int Main()
    {
        try
        {
            if(Application.Current is null)new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
            foreach(var resource in new[]{"Shell/Resources/AppResources.xaml","Themes/Dark/DarkTheme.xaml"})
                Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("pack://application:,,,/SunEyeVision.UI;component/"+resource)});
            Scalar(.125);Range(.125,0);Range(.125,1);
            var file=System.IO.Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"numeric-contract-report.json");
            System.IO.File.WriteAllText(file,JsonSerializer.Serialize(Rows,new JsonSerializerOptions{WriteIndented=true}));
            if(Rows.Any(row=>!(bool)row.GetType().GetProperty("Passed")!.GetValue(row)!))return 1;
            return 0;
        }
        catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
    private static Port Port(string id)=>new(id,DataType.Number,EditModes:FieldEditModes.Constant|FieldEditModes.Reference);
    private static FieldPresentation Presentation(string id,string? pair=null)=>new(id,id,"数值",0,FieldEditorKind.Scalar)
        {Layout=new FieldLayout("参数","数值",PairWith:pair,Minimum:0,Maximum:1,DecimalPlaces:2,Increment:.01)};
    private static ReferenceFieldOptions Options(double value)=>new(_=>ReferenceCandidates.Empty,ReadCurrentValue:_=>new ConstantSetting(value));
    private static ParameterSetting Initial()=>new ReferenceSetting(new OutputReference("producer","out",DataType.Number));
    private static void Scalar(double source)
    {
        var writes=0;
        using var field=new DeclaredFieldEditor(Port("scalar"),"标量",Initial(),_=>writes++,Options(source),presentation:Presentation("scalar"));
        Prepare(field);Switch(field,0);
        Record("scalar",source,field.Setting,writes,field.HasInvalidText,field.Error,field);
    }
    private static void Range(double source,int endpoint)
    {
        var writes=0;var ports=new[]{Port("low"),Port("high")};
        using var fields=new DeclaredFieldsView(ports.Select((p,i)=>(p,Presentation(p.Id,i==0?"high":null),i==endpoint?Initial():(ParameterSetting)new ConstantSetting(i==0?0d:1d))),_=>writes++,Options(source));
        Prepare(fields);
        var ranges=(IEnumerable)typeof(DeclaredFieldsView).GetField("_ranges",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(fields)!;
        var presenter=(FrameworkElement)ranges.Cast<object>().Single();
        Prepare(presenter);Switch(presenter,endpoint);
        var field=fields.Fields.Single(f=>f.FieldId==ports[endpoint].Id);
        Record(endpoint==0?"range-left":"range-right",source,field.Setting,writes,field.HasInvalidText,field.Error,presenter);
    }
    private static void Prepare(FrameworkElement element)
    {element.Measure(new Size(650,500));element.Arrange(new Rect(0,0,650,500));element.UpdateLayout();foreach(var item in Desc(element).OfType<Control>().ToArray())item.ApplyTemplate();element.UpdateLayout();}
    private static void Switch(DependencyObject root,int endpoint)
    {
        var path=endpoint==0?"IsSubscribeLeft":"IsSubscribeRight";
        var button=Desc(root).OfType<ToggleButton>().Single(b=>BindingOperations.GetBinding(b,ToggleButton.IsCheckedProperty)?.Path?.Path==path);
        button.SetCurrentValue(ToggleButton.IsCheckedProperty,false);
        button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
    }
    private static IEnumerable<DependencyObject> Desc(DependencyObject root)
    {yield return root;for(var i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)foreach(var child in Desc(VisualTreeHelper.GetChild(root,i)))yield return child;}
    private static void Record(string kind,double source,ParameterSetting accepted,int writes,bool invalid,string error,FrameworkElement root)
    {
        var expected=Math.Round(source,2,MidpointRounding.ToEven);
        var passed=accepted is ConstantSetting {Value:double actual}&&actual==expected&&writes==1&&!invalid;
        Rows.Add(new{Kind=kind,Source=source,Expected=expected,Accepted=accepted.ToString(),Writes=writes,Invalid=invalid,Error=error,root.IsLoaded,root.IsVisible,HasPresentationSource=PresentationSource.FromVisual(root)!=null,Windows=Application.Current.Windows.Count,Passed=passed});
        Console.WriteLine((passed?"通过":"失败")+"：同源解绑 "+kind+"，写入="+writes+"，错误="+error);
    }
}

