using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
namespace SunEyeVision.UI.Shell.Controls.Shell;
/// <summary>Root overview presentation and gestures; the host owns all model commits and execution.</summary>
public partial class RootWorkflowOverviewControl : UserControl
{
 const string DragFormat="SunEyeVision.RootOverview.v1";
 readonly Dictionary<string,RootWorkflowOverviewEntry> _entries=new(StringComparer.Ordinal);
 string _documentIdentity="";
 long _revision;
 double _scale=1;
 object? _gestureToken;
 DragPayload? _payload;
 RootWorkflowOverviewEntry[]? _deferredEntries;
 public event Action<string>? DragFailed;
 Border? _ghost;
 FrameworkElement? _armedCard;
 Point _offset;
 Point? _panStart;
 RootOverviewViewport _panOrigin;
 bool _settingViewport;
 sealed record DragPayload(RootWorkflowOverviewControl Owner,object Token,string Document,long Revision,string RootId,Point Offset,bool Copy);
 public event EventHandler<RootOverviewRequestEventArgs>? OpenRequested;
 public event EventHandler<RootOverviewRequestEventArgs>? ConfigureRequested;
 public event EventHandler<RootOverviewRequestEventArgs>? RunRequested;
 public event EventHandler<RootOverviewPositionEventArgs>? MoveCommitted;
 public event EventHandler<RootOverviewPositionEventArgs>? CopyRequested;
 public event EventHandler? ViewportChanged;
 public RootWorkflowOverviewControl()
 {
  InitializeComponent();
  Unloaded+=(_,_)=>CancelGesture();
  PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Escape){CancelGesture();e.Handled=true;}};
  Surface.DragOver+=DragOverSurface;Surface.Drop+=DropSurface;
  Surface.DragLeave+=(_,e)=>{var p=e.GetPosition(ViewportScroll);if(!InsideViewport(p))ClearGhost();};
  PreviewMouseWheel+=ZoomWheel;
  PreviewMouseDown+=PanDown;PreviewMouseMove+=PanMove;PreviewMouseUp+=PanUp;
  LostMouseCapture+=(_,_)=>_panStart=null;
  ViewportScroll.ScrollChanged+=(_,_)=>{if(!_settingViewport)ViewportChanged?.Invoke(this,EventArgs.Empty);};
 }
 public RootOverviewViewport Viewport
 {
  get=>new(_scale,ViewportScroll.HorizontalOffset,ViewportScroll.VerticalOffset);
  set
  {
   if(!double.IsFinite(value.Scale)||value.Scale<.1||value.Scale>4||!double.IsFinite(value.PanX)||!double.IsFinite(value.PanY)||value.PanX<0||value.PanY<0)
    throw new ArgumentOutOfRangeException(nameof(value));
   _settingViewport=true;
   try{_scale=value.Scale;Surface.LayoutTransform=new ScaleTransform(_scale,_scale);ViewportScroll.UpdateLayout();ViewportScroll.ScrollToHorizontalOffset(value.PanX);ViewportScroll.ScrollToVerticalOffset(value.PanY);}
   finally{_settingViewport=false;}
   ViewportChanged?.Invoke(this,EventArgs.Empty);
  }
 }
 /// <summary>Replace a complete immutable root projection. Coordinates are logical pixels; paired nulls use the source 3-column layout.</summary>
 public void SetEntries(IEnumerable<RootWorkflowOverviewEntry> entries,string documentIdentity,long revision)
 {
  Dispatcher.VerifyAccess();ArgumentNullException.ThrowIfNull(entries);ArgumentException.ThrowIfNullOrWhiteSpace(documentIdentity);
  var array=entries.ToArray();var ids=new HashSet<string>(StringComparer.Ordinal);
  foreach(var entry in array)
  {
   if(entry is null||string.IsNullOrWhiteSpace(entry.RootId)||!ids.Add(entry.RootId))throw new ArgumentException("根流程身份必须非空且唯一",nameof(entries));
   if(entry.X.HasValue!=entry.Y.HasValue||entry.X is double x&&(!double.IsFinite(x)||x<0||x>4860)||entry.Y is double y&&(!double.IsFinite(y)||y<0||y>4880))throw new ArgumentException("总览位置必须成对且在页面内",nameof(entries));
   if(entry.RunCount<0||entry.ElapsedMilliseconds is double ms&&(!double.IsFinite(ms)||ms<0))throw new ArgumentException("运行统计必须来自有效完成结果",nameof(entries));
  }
  bool changedDocument=!StringComparer.Ordinal.Equals(_documentIdentity,documentIdentity);
  if(!changedDocument&&revision==_revision&&_payload is {} active&&array.Any(entry=>entry.RootId==active.RootId&&entry.CanMove))
  { _deferredEntries=array;return; } // 保留OLE源及镜像，统计刷新延后至手势退出。
  _deferredEntries=null;
  // Same-revision presentation refresh (e.g. completed-run statistics) may occur during OLE drag.
  // Invalidate only when its source entry or source generation changed; never silently move a new document.
  if(changedDocument||revision!=_revision||_payload is not null&&!array.Any(e=>e.RootId==_payload.RootId&&e.CanMove))CancelGesture();
  _documentIdentity=documentIdentity;_revision=revision;
  _entries.Clear();Surface.Children.Clear();_ghost=null;
  for(int index=0;index<array.Length;index++)
  {
   var entry=array[index];_entries.Add(entry.RootId,entry);
   var card=CreateCard(entry);Canvas.SetLeft(card,entry.X??index%3*150);Canvas.SetTop(card,entry.Y??index/3*150);Surface.Children.Add(card);
  }
  EmptyHint.Visibility=array.Length==0?Visibility.Visible:Visibility.Collapsed;
  if(changedDocument)Viewport=new(1,0,0);
 }
 public void CancelGesture()
 {
  var deferred=_deferredEntries;_deferredEntries=null;
  _gestureToken=null;_payload=null;_armedCard=null;_panStart=null;ClearGhost();
  if(IsMouseCaptured)ReleaseMouseCapture();
  if(deferred!=null)SetEntries(deferred,_documentIdentity,_revision);
 }
 Grid CreateCard(RootWorkflowOverviewEntry entry)
 {
  var card=new Grid{Width=140,Height=120,Tag=entry.RootId};
  card.RowDefinitions.Add(new(){Height=new GridLength(30)});card.RowDefinitions.Add(new());
  var body=new Border{Background=Brush("#F2F3F5"),CornerRadius=new(0,0,4,4)};Grid.SetRow(body,1);card.Children.Add(body);
  var inner=new Grid();inner.RowDefinitions.Add(new(){Height=new GridLength(46)});inner.RowDefinitions.Add(new());body.Child=inner;
  var buttons=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};
  buttons.Children.Add(VectorButton("ic_play_____","单次运行",entry.CanRun&&!entry.IsRunning,()=>Request(RunRequested,entry.RootId,RootOverviewRunKind.Single)));
  buttons.Children.Add(VectorButton(entry.IsContinuousRunning?"ic_stop_":"ic______",entry.IsContinuousRunning?"停止运行":"连续运行",entry.CanRun||entry.IsContinuousRunning,()=>Request(RunRequested,entry.RootId,RootOverviewRunKind.ToggleContinuous)));
  buttons.Children.Add(PngButton("ic_tab_group_configuration","流程配置",entry.CanConfigure,()=>Request(ConfigureRequested,entry.RootId)));
  inner.Children.Add(buttons);
  string count=entry.RunCount?.ToString(System.Globalization.CultureInfo.CurrentCulture)??"--";
  string time=entry.ElapsedMilliseconds?.ToString("N2",System.Globalization.CultureInfo.CurrentCulture)??"--";
  var details=new TextBlock{Text=$"{count} 次\n{time}ms",FontSize=11,Foreground=Brush("#666666"),Margin=new(12,6,0,0)};Grid.SetRow(details,1);inner.Children.Add(details);
  var title=new Border{Background=Brush("#282A30"),CornerRadius=new(4,4,0,0),Cursor=entry.CanMove?Cursors.SizeAll:Cursors.Arrow};
  var titleRow=new StackPanel{Orientation=Orientation.Horizontal};
  titleRow.Children.Add(new Ellipse{Width=8,Height=8,Fill=Brush("#EC0000"),Margin=new(10,0,0,0),Visibility=entry.IsReady?Visibility.Collapsed:Visibility.Visible});
  titleRow.Children.Add(new TextBlock{Text=entry.Name,Width=90,FontSize=13,FontWeight=FontWeights.Bold,Foreground=Brush("#E4E4E4"),VerticalAlignment=VerticalAlignment.Center,Margin=new(5,0,0,0),TextTrimming=TextTrimming.CharacterEllipsis,ToolTip=entry.Name});
  titleRow.Children.Add(PngButton("ic_展开","展开流程",true,()=>Request(OpenRequested,entry.RootId),20));
  title.Child=titleRow;card.Children.Add(title);AttachDrag(title,card,entry.RootId);
  return card;
 }
 static SolidColorBrush Brush(string color)=>new((Color)ColorConverter.ConvertFromString(color));
 void Request(EventHandler<RootOverviewRequestEventArgs>? handler,string rootId,RootOverviewRunKind kind=RootOverviewRunKind.Single)
 {
  if(!_entries.ContainsKey(rootId))return;CancelGesture();handler?.Invoke(this,new(rootId,kind,_documentIdentity,_revision));
 }
 Button VectorButton(string stem,string tip,bool enabled,Action action)
  => ImageButton(tip,enabled,action,state=>(ImageSource)FindResource(stem+state+"DrawingImage"),36);
 Button PngButton(string stem,string tip,bool enabled,Action action,double width=28)
  => ImageButton(tip,enabled,action,state=>new BitmapImage(new Uri($"pack://application:,,,/SunEyeVision.UI;component/Resources/Workflow/{stem}_{(stem=="ic_展开"&&state is "hover" or "disable"?"normal":state)}.png",UriKind.Absolute)),width);
 static Button ImageButton(string tip,bool enabled,Action action,Func<string,ImageSource> image,double width)
 {
  var button=new Button{Width=width,Height=Math.Min(width,36),ToolTip=tip,IsEnabled=enabled,Background=Brushes.Transparent,BorderThickness=new(0),Padding=new(0),FocusVisualStyle=null};
  var factory=new FrameworkElementFactory(typeof(Image));factory.SetValue(Image.StretchProperty,Stretch.None);factory.SetValue(Image.SourceProperty,image("normal"));factory.Name="Icon";
  var template=new ControlTemplate(typeof(Button)){VisualTree=factory};
  foreach(var pair in new[]{(UIElement.IsMouseOverProperty,"hover"),(ButtonBase.IsPressedProperty,"click"),(UIElement.IsEnabledProperty,"disable")})
  {var trigger=new Trigger{Property=pair.Item1,Value=pair.Item2!="disable"};trigger.Setters.Add(new Setter(Image.SourceProperty,image(pair.Item2),"Icon"));template.Triggers.Add(trigger);}
  button.Template=template;button.Click+=(_,e)=>{e.Handled=true;action();};return button;
 }
 void AttachDrag(Border title,Grid card,string rootId)
 {
  title.PreviewMouseLeftButtonDown+=(_,e)=>
  {
   for(DependencyObject? current=e.OriginalSource as DependencyObject;current is not null&&current!=title;current=VisualTreeHelper.GetParent(current))if(current is ButtonBase)return;
   if(e.ClickCount==2){Request(OpenRequested,rootId);e.Handled=true;return;}
   if(!_entries.TryGetValue(rootId,out var entry)||!entry.CanMove)return;
   _armedCard=card;_offset=e.GetPosition(card);e.Handled=true;
  };
  title.PreviewMouseLeftButtonUp+=(_,_)=>_armedCard=null;
  title.PreviewMouseMove+=(_,e)=>
  {
   if(!ReferenceEquals(_armedCard,card))return;_armedCard=null;
   if(e.LeftButton!=MouseButtonState.Pressed||!_entries.TryGetValue(rootId,out var entry)||!entry.CanMove)return;
   var token=new object();_gestureToken=token;
   var payload=new DragPayload(this,token,_documentIdentity,_revision,rootId,_offset,(Keyboard.Modifiers&ModifierKeys.Control)!=0);_payload=payload;
   var data=new DataObject(DragFormat,payload,false);
   QueryContinueDragEventHandler query=(_,args)=>{if(!Valid(payload))args.Action=DragAction.Cancel;};
   title.QueryContinueDrag+=query;
   try{DragDrop.DoDragDrop(title,data,payload.Copy?DragDropEffects.Copy:DragDropEffects.Move);}
   catch(Exception error){DragFailed?.Invoke("总览拖动已取消："+error.Message);}
   finally{title.QueryContinueDrag-=query;CancelGesture();}
   e.Handled=true;
  };
 }
 bool Valid(DragPayload payload)=>IsLoaded&&IsVisible&&ReferenceEquals(payload.Owner,this)&&ReferenceEquals(payload.Token,_gestureToken)&&payload.Document==_documentIdentity&&payload.Revision==_revision&&_entries.TryGetValue(payload.RootId,out var entry)&&entry.CanMove;
 bool InsideViewport(Point p)=>p.X>=0&&p.Y>=0&&p.X<Math.Max(0,ViewportScroll.ViewportWidth)&&p.Y<Math.Max(0,ViewportScroll.ViewportHeight);
 void DragOverSurface(object sender,DragEventArgs e)=>HandleDrag(e,false);
 void DropSurface(object sender,DragEventArgs e)=>HandleDrag(e,true);
 void HandleDrag(DragEventArgs e,bool drop)
 {
  if(!e.Data.GetDataPresent(DragFormat,false))return;
  e.Handled=true;e.Effects=DragDropEffects.None;
  if(e.Data.GetData(DragFormat,false) is not DragPayload payload||!Valid(payload)||!InsideViewport(e.GetPosition(ViewportScroll))||!RootOverviewGeometry.AcceptDrop(payload.Copy,e.KeyStates)) {ClearGhost();return;}
  var effect=payload.Copy?DragDropEffects.Copy:DragDropEffects.Move;
  if((e.AllowedEffects&effect)==0){ClearGhost();return;}e.Effects=effect;
  var pointer=e.GetPosition(Surface);var point=RootOverviewGeometry.Position(pointer.X-payload.Offset.X,pointer.Y-payload.Offset.Y);
  if(drop)
  {
   var args=new RootOverviewPositionEventArgs(payload.RootId,point.X,point.Y,payload.Document,payload.Revision);
   // Retire source token and visual before invoking the host, which may synchronously replace all entries.
   CancelGesture();(payload.Copy?CopyRequested:MoveCommitted)?.Invoke(this,args);return;
  }
  if(_ghost is null)
  {
   var original=Surface.Children.OfType<Grid>().FirstOrDefault(c=>(string?)c.Tag==payload.RootId);
   _ghost=new Border{Width=140,Height=120,Background=original is null?Brushes.LightGray:new VisualBrush(original){Stretch=Stretch.Fill},Opacity=.7,IsHitTestVisible=false};
   Panel.SetZIndex(_ghost,int.MaxValue);Surface.Children.Add(_ghost);
  }
  Canvas.SetLeft(_ghost,point.X);Canvas.SetTop(_ghost,point.Y);
 }
 void ClearGhost(){if(_ghost is not null)Surface.Children.Remove(_ghost);_ghost=null;}
 void ZoomWheel(object sender,MouseWheelEventArgs e)
 {
  if((Keyboard.Modifiers&ModifierKeys.Control)==0)return;
  CancelGesture();var point=e.GetPosition(ViewportScroll);double next=Math.Clamp(_scale*(e.Delta>0?1.1:1/1.1),.1,4),factor=next/_scale;
  Viewport=new(next,Math.Max(0,(ViewportScroll.HorizontalOffset+point.X)*factor-point.X),Math.Max(0,(ViewportScroll.VerticalOffset+point.Y)*factor-point.Y));e.Handled=true;
 }
 void PanDown(object sender,MouseButtonEventArgs e)
 {
  if(e.ChangedButton!=MouseButton.Middle)return;CancelGesture();_panStart=e.GetPosition(this);_panOrigin=Viewport;CaptureMouse();e.Handled=true;
 }
 void PanMove(object sender,MouseEventArgs e)
 {
  if(_panStart is not Point start)return;
  if(e.MiddleButton!=MouseButtonState.Pressed){CancelGesture();return;}var point=e.GetPosition(this);
  Viewport=new(_scale,Math.Max(0,_panOrigin.PanX+start.X-point.X),Math.Max(0,_panOrigin.PanY+start.Y-point.Y));e.Handled=true;
 }
 void PanUp(object sender,MouseButtonEventArgs e){if(e.ChangedButton==MouseButton.Middle&&_panStart is not null){CancelGesture();e.Handled=true;}}
}
