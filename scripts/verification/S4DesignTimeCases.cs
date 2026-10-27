using SunEyeVision.Plugin.Wpf.Presenters.Display;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Markup;
using System.Xml.Linq;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Fields;
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
            var nativeColor=new NativeColorPresenter {SelectedColor=Color.FromArgb(200,10,20,30)};
            Render(nativeColor,"native-color-independent.png",394,38);
            Check(nativeColor.NativeView.CurrentColor.Color==Color.FromArgb(200,10,20,30)
                &&nativeColor.NativeView.GetType().FullName=="SunEyeVision.Plugin.Wpf.Native.Input.HikPcUI.ListView.ColorPickerControlView"
                &&Application.Current is null,"原色板及RGB真实视图可无Application/宿主独立设计渲染");
            using var first=new BlobParameterEditor();using var second=new BlobParameterEditor();
            var presenter=(DeclaredSettingsView)first.Content;
            var fields=(DeclaredFieldsView)presenter.Content;
            // The user's designer may select another page. Materialize the input page before
            // inspecting its real visual template; preserve the saved XAML page index.
            var inputTabs=(TabControl)fields.Content;
            inputTabs.SelectedItem=inputTabs.Items.Cast<TabItem>().Single(tab=>
                tab.Content is ScrollViewer {Content:StackPanel panel}
                &&panel.Children.OfType<DeclaredInputFieldsView>().Any(view=>
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
            {tabs.SelectedIndex=index;Render(first,"parameters-"+index+".png",600,680);}
            Check(tabs.Items.Count==3&&fields.TryFindResource("TopTabItemStyle") is Style,
                "三页及正式迁入模板独立可解析并渲染");
            using var result=new BlobResultView();Render(result,"results.png",900,350);
            Check(result.FilteredBlobs.Count==2&&result.FilteredTotalArea==336&&Descendants<DataGrid>(result).Single().Columns.Count>0
                &&result.TryFindResource("O06.PanelBackgroundBrush") is Brush,
                "真实结果视图有独立示例行、XML列及正式资源");
            result.Update(null);
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
    private static int replacementInputCount(DeclaredFieldsView fields)
        =>InputFields(fields).Count();
    private static IEnumerable<DeclaredFieldEditor> InputFields(DeclaredFieldsView fields)
    {
        var tabs=(TabControl)fields.Content;
        return tabs.Items.Cast<TabItem>().Select(tab=>(ScrollViewer)tab.Content)
            .Select(scroll=>(StackPanel)scroll.Content).SelectMany(panel=>panel.Children.OfType<DeclaredInputFieldsView>())
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
        var bitmap=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);bitmap.Render(view);
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var output=File.Create(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,file));encoder.Save(output);
    }
}

