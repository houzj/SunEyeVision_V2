using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Controls.References.Variables;

internal static class S2DocumentEditingCases
{
    private static int _passed;
    [STAThread] private static int Main()
    {
        Thread.CurrentThread.CurrentUICulture=CultureInfo.GetCultureInfo("zh-CN");
        _=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};
        var items=Enumerable.Range(1,17).Select(index=>new VariableSelectionItem("变量"+index,index<=13?"检测参数":"运行参数",index,"原件布局测试",index.ToString(),
            new VariableReference("test-solution","variable-"+index,TypeRef.ScalarOf(ScalarKind.Int64)))).ToList();
        VariableSelectionDialog Open()
        {
            var dialog=new VariableSelectionDialog(items){Left=-10000,Top=0,ShowInTaskbar=false,WindowStartupLocation=WindowStartupLocation.Manual};
            dialog.Show();Pump();dialog.UpdateLayout();return dialog;
        }
        var first=Open();
        Case("原680×520窗口、正式主题及语言资源与四列表格真实组装",()=>
        {
            Require(first.Width==680&&first.Height==520&&first.IsVisible,"原窗口尺寸或真实加载失败");
            Require(first.Title=="选择变量"&&first.Style is not null,"正式语言或BaseWindow模板未取得");
            Require(first.ItemsTotalCount==13&&first.PageCount==10&&first.BindItems.Count==10,"默认分组或10条分页不符");
            Require(Visual(first).OfType<ListView>().Single().ItemContainerGenerator.ContainerFromIndex(0) is ListViewItem,"真实列表未生成控件");
            Capture(first,"variable-selection.png");
        });
        Case("原分页、名称包含搜索与空状态切换",()=>
        {
            var navigator=Visual(first).OfType<VariablePageNavigator>().Single();navigator.CurrentPageNumber=2;Pump();
            Require(first.CurrentPageNum==2&&first.BindItems.Count==3&&first.BindItems[0].Index==11,"分页控件未走原两向页码绑定");
            var search=(TextBox)first.FindName("SearchText");search.Text="13";Pump();
            Require(first.CurrentPageNum==1&&first.ItemsTotalCount==1&&first.BindItems.Single().Index==13,"名称包含搜索没有复位页码");
            search.Text="不存在";Pump();Require(first.BindItems.Count==0&&first.EmptyStatus=="Visible"&&first.PageNumShowStatus=="Collapsed","空态与页码显隐不符");
            search.Clear();Pump();Require(first.BindItems.Count==10&&first.ItemsTotalCount==13,"清空搜索未恢复当前组");
            ((TextBox)navigator.FindName("txtJumpNum")).Text="2";
            ((Button)navigator.FindName("btnOk")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump();
            Require(first.CurrentPageNum==2&&first.BindItems.Count==3,"实际跳转按钮未切换数据页");
            ((TextBox)navigator.FindName("txtJumpNum")).Text="99";
            ((Button)navigator.FindName("btnOk")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump();
            Require(first.CurrentPageNum==2,"越界跳转改变当前页");
        });
        Case("原树分组选择回写且组内按Index排序",()=>
        {
            var selector=Visual(first).OfType<VariableGroupSelector>().Single();selector.IsDropDownOpen=true;Pump();
            var popup=(Popup)selector.Template.FindName("PART_Popup",selector);
            var groupControl=Visual(popup.Child).OfType<VariableGroupTree>().Single();groupControl.ApplyTemplate();Pump();
            var tree=Visual(groupControl).OfType<TreeView>().Single();
            var root=(TreeViewItem)tree.ItemContainerGenerator.ContainerFromIndex(0);root.IsExpanded=true;Pump();
            var leaf=(TreeViewItem)root.ItemContainerGenerator.ContainerFromIndex(1);leaf.IsSelected=true;Pump();
            leaf.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,0,MouseButton.Left){RoutedEvent=Control.MouseDoubleClickEvent});Pump();
            Require(first.TriggerSelectedObject?.DisplayNameA=="运行参数"&&first.BindItems.Count==4&&first.BindItems[0].Index==14,"分组下拉没有切换实际列表");
            Require(!selector.IsDropDownOpen,"原分组树叶双击没有关闭下拉");
        });
        Case("实际确认返回正式引用而非展示值，关闭清空列表",()=>
        {
            var list=(ListView)first.FindName("globalVarList");list.SelectedIndex=0;
            Confirm(first);Pump();
            Require(first.IsConfirm&&first.SelectedVarItem?.Reference.VariableId=="variable-14"&&first.SelectedVarItem.Value=="14","确认没有保留正式变量身份");
            Require(!first.IsVisible&&first.BindItems.Count==0&&list.ItemsSource is null,"窗口关闭后列表未清理");
        });
        var unselected=Open();
        Case("未选确认也关闭且不产生选择",()=>{Confirm(unselected);Pump();Require(!unselected.IsConfirm&&unselected.SelectedVarItem is null&&!unselected.IsVisible,"未选确认行为不符");});
        var doubled=Open();
        Case("原列表双击确认",()=>
        {
            var list=(ListView)doubled.FindName("globalVarList");list.SelectedIndex=1;Pump();
            var row=(ListViewItem)list.ItemContainerGenerator.ContainerFromIndex(1);
            row.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,0,MouseButton.Left){RoutedEvent=Control.MouseDoubleClickEvent});Pump();
            Require(doubled.IsConfirm&&doubled.SelectedVarItem?.Reference.VariableId=="variable-2"&&!doubled.IsVisible,"原双击确认未返回所选身份");
        });
        var retired=Open();var late=ConfirmButton(retired);
        Case("退役关闭及迟到确认不能产生选择，外部候选集合不被清空",()=>
        {
            ((ListView)retired.FindName("globalVarList")).SelectedIndex=0;retired.Retire();Pump();late.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump();
            Require(!retired.IsConfirm&&retired.SelectedVarItem is null&&items.Count==17,"退役回调接纳或窗口清理写入外部候选");
        });
        var empty=new VariableSelectionDialog(Array.Empty<VariableSelectionItem>()){Left=-10000,ShowInTaskbar=false};empty.Show();Pump();
        Case("空候选独立打开、正式资源与清理",()=>{Require(empty.BindItems.Count==0&&empty.EmptyStatus=="Visible","空候选窗口不可用");empty.Close();Pump();});
        File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"variable-dialog-report.json"),JsonSerializer.Serialize(new{
            passed=_passed,fullB08Acceptance=false,scope="原件变量选择框独立WPF、分页搜索分组及确认关闭；未接入参数来源会话，不代实际VS/人工"}));
        Console.WriteLine($"变量选择原件闭包：{_passed}组通过；B08字段来源会话仍未完整接入。");return 0;
    }
    private static Button ConfirmButton(VariableSelectionDialog dialog)=>Visual(dialog).OfType<Button>().Single(button=>Equals(button.Content,dialog.TryFindResource("Confirm")));
    private static void Confirm(VariableSelectionDialog dialog)=>ConfirmButton(dialog).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static IEnumerable<DependencyObject> Visual(DependencyObject root)
    {yield return root;for(var i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)foreach(var child in Visual(VisualTreeHelper.GetChild(root,i)))yield return child;}
    private static void Pump(){var frame=new DispatcherFrame();Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(()=>frame.Continue=false));Dispatcher.PushFrame(frame);}
    private static void Capture(FrameworkElement view,string name)
    {
        var image=new RenderTargetBitmap((int)Math.Ceiling(view.ActualWidth),(int)Math.Ceiling(view.ActualHeight),96,96,PixelFormats.Pbgra32);image.Render(view);
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));using var file=File.Create(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,name));encoder.Save(file);
    }
    private static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
    private static void Case(string name,Action check){check();_passed++;Console.WriteLine("通过："+name);}
}
