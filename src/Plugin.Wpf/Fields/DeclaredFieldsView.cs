using SunEyeVision.Plugin.Wpf.Adapters.References;
using SunEyeVision.Plugin.Wpf.Presenters.Numeric;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Adapters.Resources;
using SunEyeVision.Plugin.Wpf.Adapters.Fields;
namespace SunEyeVision.Plugin.Wpf.Fields;

/// <summary>Finite page/section/conditional composition. No workflow access or publication.</summary>
public sealed class DeclaredFieldsView : UserControl,IDisposable
{
 private readonly List<DeclaredFieldAccess> _fields=new();
 private readonly Dictionary<string,DeclaredFieldEditor> _editors=new(StringComparer.Ordinal);
 private readonly Dictionary<string,FrameworkElement> _rows=new(StringComparer.Ordinal);
 private readonly List<NativeRangePresenter> _ranges=new();
 private readonly List<DeclaredFieldState> _fieldStates=new();
 private readonly List<DisplayObjectField> _displayCompositions=new();
 private bool _disposed;
 private readonly Dictionary<string,StackPanel> _groups=new(StringComparer.Ordinal);
 private readonly List<(FrameworkElement View,FieldCondition Condition)> _conditions=new();
 private readonly Dictionary<string,FieldCondition> _fieldConditions=new(StringComparer.Ordinal);
 private readonly TabControl? _tabs;
 private readonly NativeTabHeaderTextScope? _tabTextScope;
 public int SelectedPageIndex
 {
  get=>_tabs!.SelectedIndex;
  set
  {
   if(value<0||value>=_tabs!.Items.Count)throw new ArgumentOutOfRangeException(nameof(value));
   _tabs.SelectedIndex=value;
  }
 }
 public bool HasInvalidActiveFields=>_fields.Any(field=>IsFieldActive(field.FieldId)&&field.HasInvalidText);
 public IReadOnlyList<DeclaredFieldAccess> Fields=>_fields.AsReadOnly();
 public event Action? PageChanged;
 public void AddToGroup(string group,UIElement content,int? index=null)
 {if(!_groups.TryGetValue(group,out var panel))throw new ArgumentException("Unknown declared presentation group.");if(index.HasValue)panel.Children.Insert(index.Value,content);else panel.Children.Add(content);}
 public void AddAfterField(string fieldId,FrameworkElement content,FieldCondition? condition=null)
 {
  var editor=_rows[fieldId];
  if(editor.Parent is not Panel panel)throw new InvalidOperationException("The declared field is not an inline attachment point.");
  panel.Children.Insert(panel.Children.IndexOf(editor)+1,content);
  if(condition is not null)_conditions.Add((content,condition));UpdateVisibility();
 }
 public void UseRegionSourceSelector(string fieldId)=>_editors[fieldId].UseRegionSourceSelector();
 public bool TryValidate(out string error)
 {
  foreach(var field in _fields.Where(field=>IsFieldActive(field.FieldId)))
   if(!field.TryCommitPending()){error=field.Error;return false;}
  error=_fields.FirstOrDefault(field=>IsFieldActive(field.FieldId)&&field.HasInvalidText)?.Error??"";return error.Length==0;
 }
 public DeclaredFieldsView(IEnumerable<(Port Port,FieldPresentation Presentation,ParameterSetting Value)> fields,
  Action<IReadOnlyDictionary<string,ParameterSetting>> writeDraft,ReferenceFieldOptions? references=null,
  Func<string,ConstantSetting?>? constantBackup=null,Func<OutputReference,string>? formatReference=null)
 {
  Resources.MergedDictionaries.Add(EditorResources.Create());Background=new SolidColorBrush(Color.FromRgb(242,243,245));Foreground=new SolidColorBrush(Color.FromRgb(102,102,102));FontSize=14;FontWeight=FontWeights.Normal;
  try
  {
  var all=fields.OrderBy(field=>field.Presentation.Order).ToArray();
  var declarations=all.ToDictionary(field=>field.Port.Id,StringComparer.Ordinal);
  var paired=new HashSet<string>(StringComparer.Ordinal);
  // Validate the complete topology before constructing any original view or subscribing listeners.
  foreach(var field in all)
  {
   if(field.Presentation.FieldId!=field.Port.Id)throw new ArgumentException("字段与布局身份不一致。");
   if(field.Presentation.Layout?.PairWith is not {} id)continue;
   if(id==field.Port.Id||!declarations.TryGetValue(id,out var other))throw new ArgumentException("配对字段缺失或自引用。");
   var a=field.Presentation.Layout;var b=other.Presentation.Layout;
   if(b is null||b.PairWith is not null||!paired.Add(field.Port.Id)||!paired.Add(id))throw new ArgumentException("范围端点重复、形成链或循环。");
   if(a.Page!=b.Page||a.Section!=b.Section||a.Advanced!=b.Advanced||a.VisibleWhen!=b.VisibleWhen||field.Presentation.Group!=other.Presentation.Group)
    throw new ArgumentException("范围两端必须属于相同页面、分组、层级与活动条件。");
   if(a.DialogGroup is not null||b.DialogGroup is not null)throw new ArgumentException("范围与显示弹层组合冲突。");
   if(field.Port.Type.Shape!=ValueShape.Scalar||field.Port.Type.Scalar is not (ScalarKind.Int64 or ScalarKind.Float64)||field.Port.Type!=other.Port.Type)
    throw new ArgumentException("原双端范围要求相同数值语义类型。");
   if(a.DecimalPlaces!=b.DecimalPlaces||a.DecimalPlaces is <0 or >9||a.Increment!=b.Increment||!double.IsFinite(a.Increment)||a.Increment<=0)
    throw new ArgumentException("原双端范围无法表达冲突的精度或步长。");
   foreach(var layout in new[]{a,b})if(layout.Minimum is {} min&&!double.IsFinite(min)||layout.Maximum is {} max&&!double.IsFinite(max)||layout.Minimum>layout.Maximum)
    throw new ArgumentException("范围声明边界必须有限且有序。");
   if(a.Minimum>b.Maximum)throw new ArgumentException("配对范围没有可行值。");
  }
  var states=new Dictionary<string,DeclaredFieldState>(StringComparer.Ordinal);
  foreach(var field in all)
  {
   if(field.Presentation.Layout?.VisibleWhen is {} activity)_fieldConditions.Add(field.Port.Id,activity);
   var state=new DeclaredFieldState(field.Port,field.Value,value=>writeDraft(new Dictionary<string,ParameterSetting>{{field.Port.Id,value}}),constantBackup?.Invoke(field.Port.Id),validate:proposal=>references?.ValidateProposal?.Invoke(field.Port,proposal));
   _fieldStates.Add(state);states.Add(field.Port.Id,state);
  }
  var access=new Dictionary<string,DeclaredFieldAccess>(StringComparer.Ordinal);
  foreach(var field in all)
  {
   if(field.Presentation.Layout?.PairWith is {} id)
   {
    var other=declarations[id];var left=states[field.Port.Id];var right=states[id];
    var range=new NativeRangePresenter(left,right,field.Presentation,other.Presentation,writeDraft,references,formatReference);_ranges.Add(range);
    foreach(var (state,endpoint) in new[]{(left,0),(right,1)})
    {
     var i=endpoint;
     access.Add(state.Port.Id,new(state.Port.Id,range,()=>state.Accepted,()=>state.HasInvalidInput,()=>state.Error,()=>range.Input(i),
      value=>range.TrySetConstant(i,value),()=>range.TryCommitPending(),handler=>state.Changed+=handler,handler=>state.Changed-=handler,()=>!state.IsDisposed));
     _rows.Add(state.Port.Id,range);
    }
   }
   else if(!paired.Contains(field.Port.Id))
   {
    var editor=new DeclaredFieldEditor(states[field.Port.Id],field.Presentation.DisplayName,references,formatReference,field.Presentation.ChoiceLabels,field.Presentation);
    _editors.Add(field.Port.Id,editor);access.Add(field.Port.Id,new(editor));_rows.Add(field.Port.Id,editor);
   }
  }
  foreach(var field in all){var item=access[field.Port.Id];_fields.Add(item);item.Changed+=UpdateVisibility;}
  var tabs=_tabs=new TabControl {BorderThickness=new Thickness(0),Padding=new Thickness(0),Background=Background};
  tabs.Resources.MergedDictionaries.Add(OriginalThemeResources.Create());
  _tabTextScope=new NativeTabHeaderTextScope(tabs);
  tabs.SetResourceReference(StyleProperty,"TabControlStyle");
  tabs.SelectionChanged+=(_,args)=>{if(ReferenceEquals(args.OriginalSource,tabs))PageChanged?.Invoke();};
  foreach(var page in all.GroupBy(field=>field.Presentation.Layout?.Page??field.Presentation.Group))
  {
   // The scrollbar occupies its own 12 px column; retain 32 px left / 32 px total right inset.
   var body=new StackPanel{Margin=new Thickness(32,4,20,18)};_groups[page.Key]=body;
   foreach(var group in page.GroupBy(field=>field.Presentation.Group))_groups.TryAdd(group.Key,body);
   foreach(var section in page.GroupBy(field=>(field.Presentation.Layout?.Section??field.Presentation.Group,field.Presentation.Layout?.Advanced??false)))
   {
    var content=new StackPanel{Margin=new Thickness(0,12,0,4)};
    if(section.Key.Item2)body.Children.Add(new Expander{Header=section.Key.Item1,Content=content,IsExpanded=false,Margin=new Thickness(0,8,0,2)});
    else {body.Children.Add(new TextBlock{Text=section.Key.Item1,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,16,0,10)});body.Children.Add(content);}
    var rightEnds=section.Where(f=>f.Presentation.Layout?.PairWith is not null).Select(f=>f.Presentation.Layout!.PairWith!).ToHashSet();
    foreach(var field in section)
    {
     if(rightEnds.Contains(field.Port.Id))continue;
     if(field.Presentation.Layout?.DialogGroup is {} dialog&&!field.Presentation.Layout.DialogPrimary)continue;
     FrameworkElement view=_rows[field.Port.Id];
     if(field.Presentation.Layout is {DialogGroup:{} dialogGroup,DialogPrimary:true} display)
     {
      var style=section.Where(item=>item.Presentation.Layout?.DialogGroup==dialogGroup&&!item.Presentation.Layout.DialogPrimary)
       .Select(item=>(_editors[item.Port.Id],item.Presentation.DisplayName.Split(" · ")[^1])).ToArray();
      var composition=new DisplayObjectField(display.DialogTitle!,_editors[field.Port.Id],style);
      _displayCompositions.Add(composition);view=composition;
     }
     content.Children.Add(view);_rows[field.Port.Id]=view;
     if(field.Presentation.Layout?.VisibleWhen is {} condition)_conditions.Add((view,condition));
    }
   }
   var tab=new TabItem{Header=page.Key,Content=new ScrollViewer{Style=(Style)FindResource("O06.PageScrollViewerStyle"),Content=body,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled}};
   tab.SetResourceReference(StyleProperty,"TopTabItemStyle");tabs.Items.Add(tab);
  }
  Content=tabs;UpdateVisibility();
  }
  catch{Dispose();throw;}
 }
 // Logical declaration activity is independent of selected tabs, collapsed Advanced sections, or attachment state.
 private bool IsFieldActive(string fieldId)=>!_fieldConditions.TryGetValue(fieldId,out var condition)||Meets(condition);
 private bool Meets(FieldCondition condition)
 {
  var source=_fields.SingleOrDefault(field=>field.FieldId==condition.FieldId);
  // UI never resolves a reference: its dependents remain explicitly visible and active for review.
  return source?.Setting is ReferenceSetting||source?.Setting is ConstantSetting literal&&(Equals(literal.Value,condition.Value)!=condition.Negate);
 }
 private void UpdateVisibility()
 {
  if(_disposed)return;
  foreach(var (view,condition) in _conditions)view.Visibility=Meets(condition)?Visibility.Visible:Visibility.Collapsed;
 }
 public void Dispose()
 {
  if(_disposed)return;_disposed=true;
  _tabTextScope?.Dispose();
  foreach(var composition in _displayCompositions)composition.Dispose();
  foreach(var field in _fields)field.Changed-=UpdateVisibility;
  foreach(var range in _ranges)range.Dispose();
  foreach(var editor in _editors.Values)editor.Dispose();_ranges.Clear();_editors.Clear();_rows.Clear();
  foreach(var state in _fieldStates)state.Dispose();
  _fieldStates.Clear();
  _displayCompositions.Clear();_fields.Clear();_conditions.Clear();_fieldConditions.Clear();_groups.Clear();
  PageChanged=null;_tabs?.Items.Clear();Content=null;
 }
}
