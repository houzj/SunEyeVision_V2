using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using SunEyeVision.UI.Services.PathCalculators;
using SunEyeVision.UI.Features.WorkflowUI.Commands;
using SunEyeVision.UI.Shared;
namespace SunEyeVision.UI.Features.WorkflowUI.Canvas;
public partial class WorkflowCanvasControl
{
    private readonly List<Rectangle> _vmWireHandles=new();
    private WorkflowConnection? _vmSelectedWire,_vmEditingWire;
    private RecoveredConnectionDrag? _vmWireDrag;
    private Point[]? _vmBeforePath,_vmOriginalRendered;
    private Window? _vmWireWindow;
    private object? _vmWireOwner;
    private bool CanEditVmWire=>IsEnabled&&_viewModel?.IsRunning!=true;
    private void InitializeVmWireEditing()
    {
        WorkflowCanvas.PreviewMouseMove+=VmWireMove;
        Loaded+=(_,_)=>{if(_vmWireWindow!=null)_vmWireWindow.Deactivated-=VmWireWindowDeactivated;_vmWireWindow=Window.GetWindow(this);if(_vmWireWindow!=null)_vmWireWindow.Deactivated+=VmWireWindowDeactivated;};
        WorkflowCanvas.PreviewMouseDown+=(_,e)=>{if(_vmWireDrag==null&&!(e.OriginalSource is Rectangle r&&_vmWireHandles.Contains(r))){ClearVmWireSelection();}};
        WorkflowCanvas.PreviewMouseLeftButtonUp+=(_,e)=>{if(_vmWireDrag!=null){FinishVmWireDrag(true);e.Handled=true;}};
        WorkflowCanvas.LostMouseCapture+=(_,_)=>{if(_vmWireDrag!=null)FinishVmWireDrag(false);};
        PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Escape&&_vmWireDrag!=null){FinishVmWireDrag(false);e.Handled=true;}};
        DataContextChanged+=(_,_)=>{FinishVmWireDrag(false);ClearVmWireSelection();};
        Unloaded+=(_,_)=>{FinishVmWireDrag(false);ClearVmWireSelection();if(_vmWireWindow!=null)_vmWireWindow.Deactivated-=VmWireWindowDeactivated;_vmWireWindow=null;};
    }
    private void VmWireWindowDeactivated(object? sender,EventArgs e)=>FinishVmWireDrag(false);
    private void ClearVmWireSelection()
    {
        if(_vmSelectedWire!=null)_vmSelectedWire.PropertyChanged-=VmWireSelectionChanged;
        _vmSelectedWire=null;ClearVmWireHandles();
    }
    private void VmWireSelectionChanged(object? sender,System.ComponentModel.PropertyChangedEventArgs e)
    {
        if(e.PropertyName==nameof(WorkflowConnection.IsSelected)&&_vmSelectedWire?.IsSelected!=true)
        {FinishVmWireDrag(false);ClearVmWireSelection();}
    }
    private void ClearVmWireHandles()
    {
        foreach(var handle in _vmWireHandles)WorkflowCanvas.Children.Remove(handle);
        _vmWireHandles.Clear();
    }
    private void SelectVmWire(WorkflowConnection connection,System.Windows.Shapes.Path hit)
    {
        hit.Focus();ClearVmWireSelection();_vmSelectedWire=connection;connection.PropertyChanged+=VmWireSelectionChanged;DrawVmWireHandles();
        hit.LostKeyboardFocus-=VmWireLostFocus;hit.LostKeyboardFocus+=VmWireLostFocus;
    }
    private void VmWireLostFocus(object sender,KeyboardFocusChangedEventArgs e)
    {
        if(sender is System.Windows.Shapes.Path path)path.LostKeyboardFocus-=VmWireLostFocus;
        if(_vmWireDrag!=null)FinishVmWireDrag(false);
        ClearVmWireSelection();
    }
    private void DrawVmWireHandles()
    {
        ClearVmWireHandles();
        var connection=_vmSelectedWire;
        if(connection==null||!connection.IsSelected||!CanEditVmWire)return;
        var points=_vmWireDrag?.PathPoints??connection.PathPoints.ToArray();
        for(int i=0;i+1<points.Count;i++)
        {
            var a=points[i];var b=points[i+1];if((b-a).Length<=5)continue;
            var index=i;var handle=new Rectangle{Width=6,Height=6,Fill=new SolidColorBrush(Color.FromRgb(231,105,0)),
                Cursor=a.Y==b.Y?Cursors.SizeNS:Cursors.SizeWE};
            System.Windows.Controls.Canvas.SetLeft(handle,(a.X+b.X)/2-3);System.Windows.Controls.Canvas.SetTop(handle,(a.Y+b.Y)/2-3);
            Panel.SetZIndex(handle,10000);
            handle.MouseLeftButtonDown+=(_,e)=>{BeginVmWireDrag(connection,index);e.Handled=true;};
            _vmWireHandles.Add(handle);WorkflowCanvas.Children.Add(handle);
        }
    }
    private void BeginVmWireDrag(WorkflowConnection connection,int segment)
    {
        var tab=_viewModel?.WorkflowTabViewModel?.SelectedTab;
        if(!CanEditVmWire||!connection.IsSelected||tab==null||!tab.WorkflowConnections.Contains(connection))return;
        var source=tab.WorkflowNodes.OfType<WorkflowNode>().FirstOrDefault(n=>n.Id==connection.SourceNodeId);
        var target=tab.WorkflowNodes.OfType<WorkflowNode>().FirstOrDefault(n=>n.Id==connection.TargetNodeId);
        if(source==null||target==null||segment<0||segment+1>=connection.PathPoints.Count)return;
        _vmWireOwner=DataContext;_vmEditingWire=connection;_vmBeforePath=connection.ManualPathPoints?.ToArray();_vmOriginalRendered=connection.PathPoints.ToArray();
        _vmWireDrag=new RecoveredConnectionDrag(connection.PathPoints.ToArray(),segment,source.NodeRect,target.NodeRect);
        WorkflowCanvas.CaptureMouse();
    }
    private void RefreshVmWire(WorkflowConnection connection)
    {
        _connectionPathCache?.MarkDirty(connection);connection.InvalidatePath();
    }
    private void VmWireMove(object sender,MouseEventArgs e)
    {
        if(_vmWireDrag==null||_vmEditingWire==null)return;
        if(!CanEditVmWire||!ReferenceEquals(_vmWireOwner,DataContext)||_viewModel?.WorkflowTabViewModel?.SelectedTab?.WorkflowConnections.Contains(_vmEditingWire)!=true||e.LeftButton!=MouseButtonState.Pressed)
        {FinishVmWireDrag(false);return;}
        _vmWireDrag.Move(e.GetPosition(WorkflowCanvas));
        _vmEditingWire.PreviewPathPoints=_vmWireDrag.PathPoints.ToArray();
        RefreshVmWire(_vmEditingWire);DrawVmWireHandles();e.Handled=true;
    }
    private void FinishVmWireDrag(bool commit)
    {
        var connection=_vmEditingWire;var drag=_vmWireDrag;var owner=_vmWireOwner;var before=_vmBeforePath;var original=_vmOriginalRendered;
        _vmWireDrag=null;_vmEditingWire=null;_vmWireOwner=null;_vmBeforePath=null;_vmOriginalRendered=null;
        if(connection==null)return;
        var after=drag?.PathPoints.ToArray();connection.PreviewPathPoints=null;
        if(WorkflowCanvas.IsMouseCaptured)WorkflowCanvas.ReleaseMouseCapture();
        if(commit&&CanEditVmWire&&ReferenceEquals(owner,DataContext)&&after!=null&&original!=null&&!after.SequenceEqual(original)&&_viewModel?.WorkflowTabViewModel?.SelectedTab?.WorkflowConnections.Contains(connection)==true)
        {
            var history=_viewModel?.CurrentCommandManager;
            if(history!=null)history.Execute(new EditConnectionPathCommand(connection,before,after,()=>{RefreshVmWire(connection);DrawVmWireHandles();}));
        }
        RefreshVmWire(connection);DrawVmWireHandles();
    }
}