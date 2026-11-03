using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using SunEyeVision.UI.Services.PathCalculators;
using SunEyeVision.UI.Shared;
namespace SunEyeVision.UI.Features.WorkflowUI.Canvas;
public partial class WorkflowCanvasControl
{
    private readonly List<Rectangle> _vmWireHandles=new();
    private CanvasConnectionProjection? _vmSelectedWire,_vmEditingWire;
    private System.Windows.Shapes.Path? _vmSelectedWirePath;
    private RecoveredConnectionDrag? _vmWireDrag;
    private Point[]? _vmBeforePath,_vmOriginalRendered;
    private Window? _vmWireWindow;
    private object? _vmWireOwner;
    private WorkflowTabViewModel? _vmWireTab;
    private string? _vmWireStamp, _vmWireConfigurationStamp;
    private bool CanEditVmCanvas => IsEnabled && CurrentWorkflowTab?.IsRunning != true && _viewModel?.CanEditWorkflowCanvas != false;
    private bool CanEditVmWire => CanEditVmCanvas;
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
        if(_vmSelectedWire!=null)
        {
            _vmSelectedWire.PropertyChanged-=VmWireSelectionChanged;
            _vmSelectedWire.IsSelected=false;
        }
        if(_vmSelectedWirePath!=null)_vmSelectedWirePath.LostKeyboardFocus-=VmWireLostFocus;
        _vmSelectedWirePath=null;_vmSelectedWire=null;ClearVmWireHandles();
    }
    private void VmWireSelectionChanged(object? sender,System.ComponentModel.PropertyChangedEventArgs e)
    {
        if(e.PropertyName==nameof(CanvasConnectionProjection.IsSelected)&&_vmSelectedWire?.IsSelected!=true)
        {FinishVmWireDrag(false);ClearVmWireSelection();}
    }
    private void ClearVmWireHandles()
    {
        foreach(var handle in _vmWireHandles)WorkflowCanvas.Children.Remove(handle);
        _vmWireHandles.Clear();
    }
    private void SelectVmWire(CanvasConnectionProjection connection,System.Windows.Shapes.Path hit)
    {
        hit.Focus();ClearVmWireSelection();connection.IsSelected=true;
        _vmSelectedWirePath=hit;_vmSelectedWire=connection;connection.PropertyChanged+=VmWireSelectionChanged;DrawVmWireHandles();
        var delete=new SunEyeVision.UI.Core.RelayCommand(()=>DeleteVmWire(connection),()=>CanEditVmWire&&CurrentWorkflowTab?.DocumentCanvas?.Connections.Contains(connection)==true);
        hit.InputBindings.Clear();hit.InputBindings.Add(new KeyBinding(delete,Key.Delete,ModifierKeys.None));
        var menu=new ContextMenu();menu.Items.Add(new MenuItem{Header="删除",Command=delete});hit.ContextMenu=menu;
        hit.LostKeyboardFocus-=VmWireLostFocus;hit.LostKeyboardFocus+=VmWireLostFocus;
    }
    private void DeleteVmWire(CanvasConnectionProjection connection)
    {
        var canvas=CurrentWorkflowTab?.DocumentCanvas;
        if(!CanEditVmWire||canvas?.Connections.Contains(connection)!=true)return;
        try { canvas.Delete(Array.Empty<CanvasNodeProjection>(),new[]{connection}); }
        catch(Exception error) { if(_viewModel!=null)_viewModel.StatusText="删除连线已取消："+error.Message; return; }
        FinishVmWireDrag(false);ClearVmWireSelection();
    }
    private void ConnectionHitArea_MouseRightButtonDown(object sender,MouseButtonEventArgs e)
    {
        if(sender is not System.Windows.Shapes.Path path||path.DataContext is not CanvasConnectionProjection connection||!CanEditVmWire)return;
        var tab=CurrentWorkflowTab;if(tab?.DocumentCanvas==null||!tab.DocumentCanvas.Connections.Contains(connection))return;
        foreach(var edge in tab.DocumentCanvas.Connections)edge.IsSelected=ReferenceEquals(edge,connection);
        SelectVmWire(connection,path);e.Handled=true;
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
        var points=_vmWireDrag?.PathPoints??connection.PathPoints.Select(point=>point.ToWpf()).ToArray();
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
    private void BeginVmWireDrag(CanvasConnectionProjection connection,int segment)
    {
        var tab=_viewModel?.WorkflowTabViewModel?.SelectedTab;
        if(!CanEditVmWire||!connection.IsSelected||tab?.DocumentCanvas==null||!tab.DocumentCanvas.Connections.Contains(connection))return;
        var source=tab.DocumentCanvas.Nodes.OfType<CanvasNodeProjection>().FirstOrDefault(n=>n.Id==connection.SourceNodeId);
        var target=tab.DocumentCanvas.Nodes.OfType<CanvasNodeProjection>().FirstOrDefault(n=>n.Id==connection.TargetNodeId);
        if(source==null||target==null||segment<0||segment+1>=connection.PathPoints.Count)return;
        _vmWireTab=tab;_vmWireStamp=VmGraphStamp(tab);_vmWireConfigurationStamp=VmConfigurationStamp(tab);
        _vmWireOwner=DataContext;_vmEditingWire=connection;_vmBeforePath=connection.ManualPathPoints?.Select(point=>point.ToWpf()).ToArray();_vmOriginalRendered=connection.PathPoints.Select(point=>point.ToWpf()).ToArray();
        _vmWireDrag=new RecoveredConnectionDrag(_vmOriginalRendered,segment,source.NodeRect,target.NodeRect);
        WorkflowCanvas.CaptureMouse();
    }
    private void RefreshVmWire(CanvasConnectionProjection connection)
    {
        _connectionPathCache?.MarkDirty(connection);connection.InvalidatePath();
    }
    private void VmWireMove(object sender,MouseEventArgs e)
    {
        if(_vmWireDrag==null||_vmEditingWire==null)return;
        if(!CanEditVmWire||_vmWireTab==null||!ReferenceEquals(CurrentWorkflowTab,_vmWireTab)||VmGraphStamp(_vmWireTab)!=_vmWireStamp||!ReferenceEquals(_vmWireOwner,DataContext)||_viewModel?.WorkflowTabViewModel?.SelectedTab?.DocumentCanvas?.Connections.Contains(_vmEditingWire)!=true||e.LeftButton!=MouseButtonState.Pressed)
        {FinishVmWireDrag(false);return;}
        _vmWireDrag.Move(e.GetPosition(WorkflowCanvas));
        _vmEditingWire.PreviewPathPoints=_vmWireDrag.PathPoints.Select(point=>point.ToCanvas()).ToArray();
        RefreshVmWire(_vmEditingWire);DrawVmWireHandles();e.Handled=true;
    }
    private void FinishVmWireDrag(bool commit)
    {
        var connection=_vmEditingWire;var drag=_vmWireDrag;var owner=_vmWireOwner;var before=_vmBeforePath;var original=_vmOriginalRendered;
        _vmWireDrag=null;_vmEditingWire=null;_vmWireOwner=null;_vmBeforePath=null;_vmOriginalRendered=null;
        if(connection==null)return;
        var valid=_vmWireTab!=null&&ReferenceEquals(CurrentWorkflowTab,_vmWireTab)&&VmGraphStamp(_vmWireTab)==_vmWireStamp
            &&_vmWireConfigurationStamp!=null&&_vmWireConfigurationStamp==VmConfigurationStamp(_vmWireTab)
            &&(connection.ManualPathPoints??Array.Empty<CanvasPoint>()).Select(point=>point.ToWpf()).SequenceEqual(before??Array.Empty<Point>());
        _vmWireTab=null;_vmWireStamp=null;_vmWireConfigurationStamp=null;
        var after=drag?.PathPoints.ToArray();connection.PreviewPathPoints=null;
        if(WorkflowCanvas.IsMouseCaptured)WorkflowCanvas.ReleaseMouseCapture();
        if(commit&&valid&&CanEditVmWire&&ReferenceEquals(owner,DataContext)&&after!=null&&original!=null&&!after.SequenceEqual(original)&&_viewModel?.WorkflowTabViewModel?.SelectedTab?.DocumentCanvas?.Connections.Contains(connection)==true)
        {
            try { _viewModel?.WorkflowTabViewModel?.SelectedTab?.DocumentCanvas?.EditPath(connection,after.Select(point=>point.ToCanvas()).ToArray()); }
            catch(Exception error) { if(_viewModel!=null)_viewModel.StatusText="连线路径未保存："+error.Message; }
        }
        RefreshVmWire(connection);DrawVmWireHandles();
    }
}
