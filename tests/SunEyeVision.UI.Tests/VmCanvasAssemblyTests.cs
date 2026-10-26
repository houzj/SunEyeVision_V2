using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SunEyeVision.UI.Features.WorkflowUI;
using SunEyeVision.UI.Features.WorkflowUI.Canvas;
using SunEyeVision.UI.Services.Interaction;
using SunEyeVision.UI.Shared;
using Xunit;
using NodeBase=SunEyeVision.Workflow.WorkflowNodeBase;
using Connection=SunEyeVision.Workflow.WorkflowConnection;
namespace SunEyeVision.UI.Tests;
public sealed class VmCanvasAssemblyTests
{
    private static IEnumerable<SunEyeVision.UI.Services.Rendering.VmExecutionPort> VisualPorts(DependencyObject parent)
    {
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++)
        {var child=VisualTreeHelper.GetChild(parent,i);if(child is SunEyeVision.UI.Services.Rendering.VmExecutionPort port)yield return port;foreach(var nested in VisualPorts(child))yield return nested;}
    }    [Fact]
    public void Actual_canvas_template_ports_and_arrow_share_the_model_coordinates()
    {
        Exception? failure=null;
        var thread=new Thread(()=>
        {
            try
            {
                var control=new WorkflowCanvasControl{Width=700,Height=450};
                control.Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("/SunEyeVision.UI;component/Resources/Styles/NodeStyleResources.xaml",UriKind.Relative)});
                control.Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("/SunEyeVision.UI;component/Themes/Dark/DarkTheme.xaml",UriKind.Relative)});
                var source=new WorkflowNode{Id="source",ToolType="ImageLoad",DispName="图像源1",GlobalIndex=2,Position=new Point(450,90),IsSelected=true};
                var target=new WorkflowNode{Id="target",ToolType="BlobAnalysis",DispName="Blob分析1",GlobalIndex=1,Position=new Point(160,290)};
                var connection=new Connection{Id="arbitrary-guid",SourceNodeId=source.Id,TargetNodeId=target.Id,SourcePort="BottomPort",TargetPort="TopPort"};
                var model=new WorkflowTabViewModel();model.SetNodes(new ObservableCollection<NodeBase>{source,target});model.SetConnections(new ObservableCollection<Connection>{connection});
                control.DataContext=model;control.Measure(new Size(700,450));control.Arrange(new Rect(0,0,700,450));control.UpdateLayout();
                control.Dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.DataBind);control.UpdateLayout();
                var canvas=(System.Windows.Controls.Canvas)control.FindName("WorkflowCanvas");
                foreach(var port in VisualPorts(control))port.Visibility=Visibility.Visible;
                control.UpdateLayout();
                var ports=new PortPositionService(canvas,NodeStyles.Standard);
                foreach(var node in new[]{source,target})
                foreach(var side in new[]{"TopPort","BottomPort","LeftPort","RightPort"})
                {
                    var actual=ports.QueryPortPosition(node.Id,side);Assert.NotNull(actual);
                    var expected=ports.GetDefaultPortPosition(node,side);Assert.True((actual!.Value-expected).Length<0.01,$"{node.Id}/{side}: {actual} vs {expected}");
                }
                Assert.Equal(source.BottomPortPosition,connection.PathPoints[0]);Assert.Equal(target.TopPortPosition,connection.PathPoints[^1]);
                var image=new RenderTargetBitmap(700,450,96,96,PixelFormats.Pbgra32);image.Render(control);
                var pixel=new byte[4];image.CopyPixels(new Int32Rect(450,77,1,1),pixel,4,0);
                Assert.Equal(new byte[]{25,167,255,255},pixel); // 顶部手柄伸入节点，不应被热区边界裁剪。
                var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));
                var directory=Environment.GetEnvironmentVariable("S2_CASE_ROOT")??Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","..","_validation","VmCanvas"));Directory.CreateDirectory(directory);
                using var output=File.Create(Path.Combine(directory,"vm-canvas-integrated.png"));encoder.Save(output);
                control.DataContext=null;
            }
            catch(Exception error){failure=error;}
        });thread.SetApartmentState(ApartmentState.STA);thread.IsBackground=true;thread.Start();Assert.True(thread.Join(TimeSpan.FromSeconds(45)),"真实画布组装验证超时");
        if(failure!=null)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }
}