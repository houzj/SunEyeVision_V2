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
    private readonly List<Rectangle> _canvasWireHandles=new();
    private CanvasConnectionProjection? _canvasSelectedWire,_canvasEditingWire;
    private System.Windows.Shapes.Path? _canvasSelectedWirePath;
    private RecoveredConnectionDrag? _canvasWireDrag;
    private Point[]? _canvasBeforePath,_canvasOriginalRendered;
    private Window? _canvasWireWindow;
    private object? _canvasWireOwner;
    private WorkflowTabViewModel? _canvasWireTab;
    private string? _canvasWireStamp, _canvasWireConfigurationStamp;
    private bool CanEditCanvas => IsEnabled && CurrentWorkflowTab?.IsRunning != true && _viewModel?.CanEditWorkflowCanvas != false;
    private bool CanEditCanvasWire => CanEditCanvas;
    private void InitializeCanvasWireEditing()
    {
        WorkflowCanvas.PreviewMouseMove+=CanvasWireMove;
        Loaded+=(_,_)=>{if(_canvasWireWindow!=null)_canvasWireWindow.Deactivated-=CanvasWireWindowDeactivated;_canvasWireWindow=Window.GetWindow(this);if(_canvasWireWindow!=null)_canvasWireWindow.Deactivated+=CanvasWireWindowDeactivated;};
        WorkflowCanvas.PreviewMouseDown+=(_,e)=>{if(_canvasWireDrag==null&&!(e.OriginalSource is Rectangle r&&_canvasWireHandles.Contains(r))){ClearCanvasWireSelection();}};
        WorkflowCanvas.PreviewMouseLeftButtonUp+=(_,e)=>{if(_canvasWireDrag!=null){FinishCanvasWireDrag(true);e.Handled=true;}};
        WorkflowCanvas.LostMouseCapture+=(_,_)=>{if(_canvasWireDrag!=null)FinishCanvasWireDrag(false);};
        PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Escape&&_canvasWireDrag!=null){FinishCanvasWireDrag(false);e.Handled=true;}};
        DataContextChanged+=(_,_)=>{FinishCanvasWireDrag(false);ClearCanvasWireSelection();};
        Unloaded+=(_,_)=>{FinishCanvasWireDrag(false);ClearCanvasWireSelection();if(_canvasWireWindow!=null)_canvasWireWindow.Deactivated-=CanvasWireWindowDeactivated;_canvasWireWindow=null;};
    }
    private void CanvasWireWindowDeactivated(object? sender,EventArgs e)=>FinishCanvasWireDrag(false);
    private void ClearCanvasWireSelection()
    {
        if(_canvasSelectedWire!=null)
        {
            _canvasSelectedWire.PropertyChanged-=CanvasWireSelectionChanged;
            _canvasSelectedWire.IsSelected=false;
        }
        if(_canvasSelectedWirePath!=null)_canvasSelectedWirePath.LostKeyboardFocus-=CanvasWireLostFocus;
        _canvasSelectedWirePath=null;_canvasSelectedWire=null;ClearCanvasWireHandles();
    }
    private void CanvasWireSelectionChanged(object? sender,System.ComponentModel.PropertyChangedEventArgs e)
    {
        if(e.PropertyName==nameof(CanvasConnectionProjection.IsSelected)&&_canvasSelectedWire?.IsSelected!=true)
        {FinishCanvasWireDrag(false);ClearCanvasWireSelection();}
    }
    private void ClearCanvasWireHandles()
    {
        foreach(var handle in _canvasWireHandles)WorkflowCanvas.Children.Remove(handle);
        _canvasWireHandles.Clear();
    }
    private void SelectCanvasWire(CanvasConnectionProjection connection,System.Windows.Shapes.Path hit)
    {
        hit.Focus();ClearCanvasWireSelection();connection.IsSelected=true;
        _canvasSelectedWirePath=hit;_canvasSelectedWire=connection;connection.PropertyChanged+=CanvasWireSelectionChanged;DrawCanvasWireHandles();
        var delete=new SunEyeVision.UI.Core.RelayCommand(()=>DeleteCanvasWire(connection),()=>CanEditCanvasWire&&CurrentWorkflowTab?.DocumentCanvas?.Connections.Contains(connection)==true);
        hit.InputBindings.Clear();hit.InputBindings.Add(new KeyBinding(delete,Key.Delete,ModifierKeys.None));
        var menu=new ContextMenu();menu.Items.Add(new MenuItem{Header="删除",Command=delete});hit.ContextMenu=menu;
        hit.LostKeyboardFocus-=CanvasWireLostFocus;hit.LostKeyboardFocus+=CanvasWireLostFocus;
    }
    private void DeleteCanvasWire(CanvasConnectionProjection connection)
    {
        var canvas=CurrentWorkflowTab?.DocumentCanvas;
        if(!CanEditCanvasWire||canvas?.Connections.Contains(connection)!=true)return;
        try { canvas.Delete(Array.Empty<CanvasNodeProjection>(),new[]{connection}); }
        catch(Exception error) { if(_viewModel!=null)_viewModel.StatusText="删除连线已取消："+error.Message; return; }
        FinishCanvasWireDrag(false);ClearCanvasWireSelection();
    }
    private void ConnectionHitArea_MouseRightButtonDown(object sender,MouseButtonEventArgs e)
    {
        if(sender is not System.Windows.Shapes.Path path||path.DataContext is not CanvasConnectionProjection connection||!CanEditCanvasWire)return;
        var tab=CurrentWorkflowTab;if(tab?.DocumentCanvas==null||!tab.DocumentCanvas.Connections.Contains(connection))return;
        foreach(var edge in tab.DocumentCanvas.Connections)edge.IsSelected=ReferenceEquals(edge,connection);
        SelectCanvasWire(connection,path);e.Handled=true;
    }
    private void CanvasWireLostFocus(object sender,KeyboardFocusChangedEventArgs e)
    {
        if(sender is System.Windows.Shapes.Path path)path.LostKeyboardFocus-=CanvasWireLostFocus;
        if(_canvasWireDrag!=null)FinishCanvasWireDrag(false);
        ClearCanvasWireSelection();
    }
    private void DrawCanvasWireHandles()
    {
        ClearCanvasWireHandles();
        var connection=_canvasSelectedWire;
        if(connection==null||!connection.IsSelected||!CanEditCanvasWire)return;
        var points=_canvasWireDrag?.PathPoints??connection.PathPoints.Select(point=>point.ToWpf()).ToArray();
        for(int i=0;i+1<points.Count;i++)
        {
            var a=points[i];var b=points[i+1];if((b-a).Length<=5)continue;
            var index=i;var handle=new Rectangle{Width=6,Height=6,Fill=new SolidColorBrush(Color.FromRgb(231,105,0)),
                Cursor=a.Y==b.Y?Cursors.SizeNS:Cursors.SizeWE};
            System.Windows.Controls.Canvas.SetLeft(handle,(a.X+b.X)/2-3);System.Windows.Controls.Canvas.SetTop(handle,(a.Y+b.Y)/2-3);
            Panel.SetZIndex(handle,10000);
            handle.MouseLeftButtonDown+=(_,e)=>{BeginCanvasWireDrag(connection,index);e.Handled=true;};
            _canvasWireHandles.Add(handle);WorkflowCanvas.Children.Add(handle);
        }
    }
    private void BeginCanvasWireDrag(CanvasConnectionProjection connection,int segment)
    {
        var tab=_viewModel?.WorkflowTabViewModel?.SelectedTab;
        if(!CanEditCanvasWire||!connection.IsSelected||tab?.DocumentCanvas==null||!tab.DocumentCanvas.Connections.Contains(connection))return;
        var source=tab.DocumentCanvas.Nodes.OfType<CanvasNodeProjection>().FirstOrDefault(n=>n.Id==connection.SourceNodeId);
        var target=tab.DocumentCanvas.Nodes.OfType<CanvasNodeProjection>().FirstOrDefault(n=>n.Id==connection.TargetNodeId);
        if(source==null||target==null||segment<0||segment+1>=connection.PathPoints.Count)return;
        _canvasWireTab=tab;_canvasWireStamp=CanvasGraphStamp(tab);_canvasWireConfigurationStamp=CanvasConfigurationStamp(tab);
        _canvasWireOwner=DataContext;_canvasEditingWire=connection;_canvasBeforePath=connection.ManualPathPoints?.Select(point=>point.ToWpf()).ToArray();_canvasOriginalRendered=connection.PathPoints.Select(point=>point.ToWpf()).ToArray();
        _canvasWireDrag=new RecoveredConnectionDrag(_canvasOriginalRendered,segment,source.NodeRect,target.NodeRect);
        WorkflowCanvas.CaptureMouse();
    }
    private void RefreshCanvasWire(CanvasConnectionProjection connection)
    {
        _connectionPathCache?.MarkDirty(connection);connection.InvalidatePath();
    }
    private void CanvasWireMove(object sender,MouseEventArgs e)
    {
        if(_canvasWireDrag==null||_canvasEditingWire==null)return;
        if(!CanEditCanvasWire||_canvasWireTab==null||!ReferenceEquals(CurrentWorkflowTab,_canvasWireTab)||CanvasGraphStamp(_canvasWireTab)!=_canvasWireStamp||!ReferenceEquals(_canvasWireOwner,DataContext)||_viewModel?.WorkflowTabViewModel?.SelectedTab?.DocumentCanvas?.Connections.Contains(_canvasEditingWire)!=true||e.LeftButton!=MouseButtonState.Pressed)
        {FinishCanvasWireDrag(false);return;}
        _canvasWireDrag.Move(e.GetPosition(WorkflowCanvas));
        _canvasEditingWire.PreviewPathPoints=_canvasWireDrag.PathPoints.Select(point=>point.ToCanvas()).ToArray();
        RefreshCanvasWire(_canvasEditingWire);DrawCanvasWireHandles();e.Handled=true;
    }
    private void FinishCanvasWireDrag(bool commit)
    {
        var connection=_canvasEditingWire;var drag=_canvasWireDrag;var owner=_canvasWireOwner;var before=_canvasBeforePath;var original=_canvasOriginalRendered;
        _canvasWireDrag=null;_canvasEditingWire=null;_canvasWireOwner=null;_canvasBeforePath=null;_canvasOriginalRendered=null;
        if(connection==null)return;
        var valid=_canvasWireTab!=null&&ReferenceEquals(CurrentWorkflowTab,_canvasWireTab)&&CanvasGraphStamp(_canvasWireTab)==_canvasWireStamp
            &&_canvasWireConfigurationStamp!=null&&_canvasWireConfigurationStamp==CanvasConfigurationStamp(_canvasWireTab)
            &&(connection.ManualPathPoints??Array.Empty<CanvasPoint>()).Select(point=>point.ToWpf()).SequenceEqual(before??Array.Empty<Point>());
        _canvasWireTab=null;_canvasWireStamp=null;_canvasWireConfigurationStamp=null;
        var after=drag?.PathPoints.ToArray();connection.PreviewPathPoints=null;
        if(WorkflowCanvas.IsMouseCaptured)WorkflowCanvas.ReleaseMouseCapture();
        if(commit&&valid&&CanEditCanvasWire&&ReferenceEquals(owner,DataContext)&&after!=null&&original!=null&&!after.SequenceEqual(original)&&_viewModel?.WorkflowTabViewModel?.SelectedTab?.DocumentCanvas?.Connections.Contains(connection)==true)
        {
            try { _viewModel?.WorkflowTabViewModel?.SelectedTab?.DocumentCanvas?.EditPath(connection,after.Select(point=>point.ToCanvas()).ToArray()); }
            catch(Exception error) { if(_viewModel!=null)_viewModel.StatusText="连线路径未保存："+error.Message; }
        }
        RefreshCanvasWire(connection);DrawCanvasWireHandles();
    }
}
