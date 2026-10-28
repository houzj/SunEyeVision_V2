using SunEyeVision.Plugin.Wpf.Adapters.References;
using SunEyeVision.Plugin.Wpf.Presenters.Numeric;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Collections.ObjectModel;
using NativeHost=SunEyeVision.Plugin.Wpf.Native.Composition.Apps.XmlUI.ParamListView;
using SunEyeVision.Plugin.Wpf.Presenters.References;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Adapters.Resources;
using SunEyeVision.Plugin.Wpf.Adapters.Fields;
using SunEyeVision.Plugin.Wpf.Adapters.Text;
using SunEyeVision.Plugin.Wpf.Presenters.Display;
namespace SunEyeVision.Plugin.Wpf.Fields;

/// <summary>Finite page/section/conditional composition. No workflow access or publication.</summary>
public sealed partial class DeclaredFieldsView : UserControl,IDisposable
{
 private readonly List<DeclaredFieldAccess> _fields=new();
 private readonly Dictionary<string,DeclaredFieldEditor> _editors=new(StringComparer.Ordinal);
 private readonly Dictionary<string,FrameworkElement> _rows=new(StringComparer.Ordinal);
 private readonly List<NativeRangePresenter> _ranges=new();
 private readonly List<DeclaredFieldState> _fieldStates=new();
 private readonly List<NativeGraphicStylePresenter> _displayCompositions=new();
 private bool _disposed;
 private readonly Dictionary<string,NativeParameterPage> _groups=new(StringComparer.Ordinal);
 private readonly List<(NativeParameterPage Page,NativeHost Host)> _pages=new();
 private readonly Dictionary<FrameworkElement,(NativeParameterPage Page,NativeParameterRow Row)> _positions=new();
 private readonly List<NativeAdvancedPresenter> _advanced=new();
 private readonly Dictionary<FrameworkElement,(NativeAdvancedPresenter? Advanced,FieldCondition? Condition)> _visibility=new();
 private long _visibilityVersion;
 private bool _updatingVisibility,_visibilityPending;
 private readonly List<NativeParameterRow> _parameterRows=new();
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
 public void AddToGroup(string group,UIElement content,int? index=null,FieldCondition? condition=null)
 {
  ObjectDisposedException.ThrowIf(_disposed,this);
  if(!_groups.TryGetValue(group,out var page))throw new ArgumentException("Unknown declared presentation group.");
  if(content is not FrameworkElement view)throw new ArgumentException("参数组合必须提供真实FrameworkElement。");
  ValidateCondition(condition);
  EnsureAttachable(view);var position=index??page.Rows.Count;
  if(position<0||position>page.Rows.Count)throw new ArgumentOutOfRangeException(nameof(index));
  var metadata=index.HasValue&&page.Rows.FirstOrDefault()?.GroupName is {DisplayType:"CategoryNoHead"} existing?existing:new NativeParameterGroup("",true);
  var row=BorrowRow(metadata,view);InstallRow(page,position,row,view);
  _visibility.Add(view,(null,condition));UpdateVisibility();
 }
 public void AddAfterField(string fieldId,FrameworkElement content,FieldCondition? condition=null)
 {
  ObjectDisposedException.ThrowIf(_disposed,this);EnsureAttachable(content);var anchor=_rows[fieldId];
  ValidateCondition(condition);
  if(!_positions.TryGetValue(anchor,out var position))throw new InvalidOperationException("字段不属于当前原参数行宿主。");
  var advanced=_visibility.TryGetValue(anchor,out var policy)?policy.Advanced:null;
  _visibility.Add(content,(advanced,condition));
  _rowGateAnchors.Add(content,fieldId);
  var row=BorrowRow(position.Row.GroupName,content);
  try
  {
   content.Visibility=(advanced?.IsExpanded??true)&&IsFieldActive(fieldId)&&(condition is null||Meets(condition))?Visibility.Visible:Visibility.Collapsed;
   ObjectDisposedException.ThrowIf(_disposed,this);
   InstallRow(position.Page,position.Page.Rows.IndexOf(position.Row)+1,row,content);
  }
  catch
  {
   if(!_positions.ContainsKey(content)){_visibility.Remove(content);_rowGateAnchors.Remove(content);_parameterRows.Remove(row);row.Dispose();}
   throw;
  }
  UpdateVisibility();
 }
 private void EnsureAttachable(FrameworkElement view)
 {
  if(_positions.ContainsKey(view)||_visibility.ContainsKey(view)||_rows.Values.Contains(view)||view.Parent is not null||VisualTreeHelper.GetParent(view) is not null)
   throw new ArgumentException("控件已经属于一个参数行或其他活动视图。");
 }
 private void InstallRow(NativeParameterPage page,int index,NativeParameterRow row,FrameworkElement view)
 {
  _positions.Add(view,(page,row));
  try{page.Rows.Insert(index,row);}
  catch
  {
   if(!page.Rows.Contains(row)){_positions.Remove(view);_parameterRows.Remove(row);row.Dispose();}
   throw;
  }
  ObjectDisposedException.ThrowIf(_disposed,this);
 }
 private NativeParameterRow BorrowRow(NativeParameterGroup group,FrameworkElement view,Func<NativeParameterItemState>? state=null)
 {
  var row=new NativeParameterRow(group,view,state,()=>{if(!_disposed&&Window.GetWindow(this) is {} owner)ReferenceExpansionCoordinator.ClearSelectionFor(owner);});
  _parameterRows.Add(row);return row;
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
  Func<string,ConstantSetting?>? constantBackup=null,Func<OutputReference,string>? formatReference=null,
  IReadOnlyDictionary<string,NativeIntegerRadioOptions>? integerRadios=null,
  Func<FieldGroupCandidate,string?>? validateComposition=null,Func<bool>? isCurrent=null,
  IReadOnlyDictionary<string,Func<IReadOnlyList<DeclaredReferenceCandidate>>>? imageSources=null,
  IReadOnlyDictionary<string,IReadOnlyList<FieldCondition>>? parentConditions=null,IReadOnlySet<string>? inlineConstants=null,
  IReadOnlyDictionary<string,TextExpressionOptions>? textExpressions=null,
  IReadOnlyList<NativeGraphicDisplayOptions>? graphicDisplays=null,
  IReadOnlyList<NativeTextDisplayOptions>? textDisplays=null)
 {
  ArgumentNullException.ThrowIfNull(writeDraft);
  _writeGroup=writeDraft;_validateComposition=validateComposition;_isCurrent=isCurrent;_groupReferences=references;
  Resources.MergedDictionaries.Add(EditorResources.Create());Background=new SolidColorBrush(Color.FromRgb(242,243,245));Foreground=new SolidColorBrush(Color.FromRgb(102,102,102));FontSize=14;FontWeight=FontWeights.Normal;
  try
  {
  var all=fields.OrderBy(field=>field.Presentation.Order).ToArray();
  var declarations=all.ToDictionary(field=>field.Port.Id,StringComparer.Ordinal);
  _groupPresentations=all.ToDictionary(field=>field.Port.Id,field=>field.Presentation,StringComparer.Ordinal);
  var paired=new HashSet<string>(StringComparer.Ordinal);
  var radios=integerRadios?.ToDictionary(x=>x.Key,x=>x.Value,StringComparer.Ordinal)??new(StringComparer.Ordinal);
  _integerRadios=radios;
  foreach(var option in radios)
  {
   if(!declarations.TryGetValue(option.Key,out var declared)||option.Value is null)throw new ArgumentException("整数单选选项引用了未知字段。");
   option.Value.ValidatePort(declared.Port);
   if(declared.Presentation.Layout?.PairWith is not null||declared.Presentation.Layout?.DialogGroup is not null)
    throw new ArgumentException("整数单选不能同时声明范围配对或显示弹层。");
  }
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
  if(radios.Keys.Any(paired.Contains))throw new ArgumentException("整数单选不能同时作为范围的任一端点。");
  _pairedFields.UnionWith(paired);
  var textDisplayOptions=textDisplays?.ToArray()??[];
  if(textDisplayOptions.Any(option=>option is null))throw new ArgumentException("文本显示组合不能为空。");
  var textCoordinates=textDisplayOptions.SelectMany(option=>new[]{option.PositionXFieldId,option.PositionYFieldId}).ToHashSet(StringComparer.Ordinal);
  var imageProviders=ConfigureComposition(declarations,imageSources,parentConditions,radios,paired,inlineConstants,textCoordinates);
  var textOptions=textExpressions?.ToDictionary(pair=>pair.Key,pair=>pair.Value,StringComparer.Ordinal)??new(StringComparer.Ordinal);
  foreach(var (id,option) in textOptions)
  {
   if(option is null||!declarations.TryGetValue(id,out var field))throw new ArgumentException("文本表达式配置引用未知字段。");
   option.Validate(field.Port);
   if(paired.Contains(id)||radios.ContainsKey(id)||imageProviders.ContainsKey(id)||_inlineConstants.Contains(id)
       ||field.Presentation.Layout?.DialogPrimary==true)throw new ArgumentException("文本表达式与其他字段组合冲突。");
   if(field.Value is ConstantSetting {Value:string text})
   {
    if(!CurrentGroup())throw new InvalidOperationException("文本初值所属范围已失效。");
    var error=option.ValidateText?.Invoke(text);
    if(!CurrentGroup())throw new InvalidOperationException("文本初值验证期间范围已改变。");
    if(!string.IsNullOrEmpty(error))throw new ArgumentException(error);
   }
  }
  _textExpressions=textOptions;
  var graphicOptions=ValidateGraphics(declarations,graphicDisplays,paired,radios,imageProviders,textOptions);
  var graphicIds=graphicOptions.Values.SelectMany(GraphicIds).ToHashSet(StringComparer.Ordinal);
  ValidateTextDisplays(declarations,textDisplayOptions,graphicIds,paired,radios,imageProviders,inlineConstants,textOptions);
  foreach(var option in textDisplayOptions)
  {
   var ids=TextIds(option);var own=ids.ToHashSet(StringComparer.Ordinal);
   var anchor=Array.FindIndex(all,field=>field.Port.Id==option.ContentFieldId);
   all=all.Take(anchor).Where(field=>!own.Contains(field.Port.Id)).Concat(ids.Select(id=>declarations[id]))
       .Concat(all.Skip(anchor).Where(field=>!own.Contains(field.Port.Id))).ToArray();
  }
  var states=new Dictionary<string,DeclaredFieldState>(StringComparer.Ordinal);
  foreach(var field in all)
  {
   if(field.Presentation.Layout?.VisibleWhen is {} activity)_fieldConditions.Add(field.Port.Id,activity);
   var state=new DeclaredFieldState(field.Port,field.Value,value=>writeDraft(new Dictionary<string,ParameterSetting>{{field.Port.Id,value}}),constantBackup?.Invoke(field.Port.Id),
       propose:proposal=>ProposeSingle(field.Port.Id,proposal));
   _fieldStates.Add(state);states.Add(field.Port.Id,state);
  }
  var access=new Dictionary<string,DeclaredFieldAccess>(StringComparer.Ordinal);
  foreach(var option in graphicOptions.Values)
  {
   var ids=GraphicIds(option);
   var graphic=new NativeGraphicStylePresenter(option,ids.ToDictionary(id=>id,id=>states[id],StringComparer.Ordinal),_groupPresentations,CurrentGroup);
   _displayCompositions.Add(graphic);
   foreach(var id in ids)
   {
    var state=states[id];var fieldId=id;
    access.Add(id,new(id,graphic,()=>state.Accepted,()=>state.HasInvalidInput,()=>state.Error,()=>graphic.Input(fieldId),
     value=>graphic.TrySetConstant(fieldId,value),()=>graphic.TryCommitPending(fieldId),handler=>state.Changed+=handler,handler=>state.Changed-=handler,()=>!state.IsDisposed));
    _rows.Add(id,graphic);
   }
  }
  foreach(var field in all)
  {
   if(graphicIds.Contains(field.Port.Id))continue;
   if(field.Presentation.Layout?.PairWith is {} id)
   {
    var other=declarations[id];var left=states[field.Port.Id];var right=states[id];
    var range=new NativeRangePresenter(left,right,field.Presentation,other.Presentation,writeDraft,references,formatReference,
        SubmitRange,BeginRange);_ranges.Add(range);
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
    var editor=new DeclaredFieldEditor(states[field.Port.Id],field.Presentation.DisplayName,references,formatReference,field.Presentation.ChoiceLabels,field.Presentation,
        integerRadioOptions:radios.GetValueOrDefault(field.Port.Id),inlineConstantReference:_inlineConstants.Contains(field.Port.Id));
    _editors.Add(field.Port.Id,editor);access.Add(field.Port.Id,new(editor));_rows.Add(field.Port.Id,editor);
    if(textOptions.TryGetValue(field.Port.Id,out var textOption))editor.UseTextExpressions(textOption);
    if(imageProviders.TryGetValue(field.Port.Id,out var provider))
    {
     editor.UseImageSourceSelector(()=>
     {
      if(!CurrentGroup())throw new InvalidOperationException("图像来源编辑范围已失效。");
      var candidates=provider();
      if(!CurrentGroup())throw new InvalidOperationException("图像来源编辑范围已重绑。");
      return candidates;
     });
     if(!CurrentGroup()){Dispose();return;}
    }
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
   var pageRows=new NativeParameterPage();var body=new NativeHost();body.SetRows(pageRows.Rows);_pages.Add((pageRows,body));_groups[page.Key]=pageRows;
   foreach(var group in page.GroupBy(field=>field.Presentation.Group))_groups.TryAdd(group.Key,pageRows);
   foreach(var section in page.GroupBy(field=>(field.Presentation.Layout?.Section??field.Presentation.Group,field.Presentation.Layout?.Advanced??false)))
   {
    var metadata=new NativeParameterGroup(section.Key.Item1,section.Key.Item2);
    NativeAdvancedPresenter? advanced=null;
    if(section.Key.Item2)
    {
     advanced=new NativeAdvancedPresenter();_advanced.Add(advanced);advanced.Changed+=UpdateVisibility;
     var buttonRow=BorrowRow(metadata,advanced);pageRows.Rows.Add(buttonRow);_positions.Add(advanced,(pageRows,buttonRow));
     _advancedFields.Add(advanced,section.Select(field=>field.Port.Id).ToArray());_visibility.Add(advanced,(null,null));
    }
    var rightEnds=section.Where(f=>f.Presentation.Layout?.PairWith is not null).Select(f=>f.Presentation.Layout!.PairWith!).ToHashSet();
    foreach(var field in section)
    {
     if(rightEnds.Contains(field.Port.Id))continue;
     if(field.Presentation.Layout?.DialogGroup is {} dialog&&!field.Presentation.Layout.DialogPrimary)continue;
     FrameworkElement view=_rows[field.Port.Id];
     _rows[field.Port.Id]=view;
     {
      var affected=field.Presentation.Layout?.DialogGroup is {} displayGroup?section.Where(item=>item.Presentation.Layout?.DialogGroup==displayGroup).Select(item=>item.Port.Id).ToArray():
       field.Presentation.Layout?.PairWith is {} other?new[]{field.Port.Id,other}:new[]{field.Port.Id};
      var row=BorrowRow(metadata,view,()=>affected.Any(id=>states[id].EditError.Length!=0)?NativeParameterItemState.WriteError:
       affected.Any(id=>states[id].SourceError.Length!=0)?NativeParameterItemState.ReadError:
       affected.Any(id=>states[id].HasInvalidInput)?NativeParameterItemState.WriteError:NativeParameterItemState.Normal);
      pageRows.Rows.Add(row);_positions.Add(view,(pageRows,row));
     }
     _visibility.Add(view,(advanced,field.Presentation.Layout?.VisibleWhen));
     _rowGateAnchors.Add(view,field.Port.Id);
    }
   }
   var tab=new TabItem{Header=page.Key,Content=body};
   tab.SetResourceReference(StyleProperty,"TopTabItemStyle");tabs.Items.Add(tab);
  }
  Content=tabs;UpdateVisibility();
  }
  catch(Exception failure){try{Dispose();}catch(Exception cleanup){throw new AggregateException("参数宿主构造与清理均失败。",failure,cleanup);}throw;}
 }
 // Logical declaration activity is independent of selected tabs, collapsed Advanced sections, or attachment state.
 private bool IsFieldActive(string fieldId)=>(!_fieldConditions.TryGetValue(fieldId,out var condition)||Meets(condition))
  &&(!_parentConditions.TryGetValue(fieldId,out var parents)||parents.All(Meets));
 private bool Meets(FieldCondition condition)
 {
  var source=_fields.SingleOrDefault(field=>field.FieldId==condition.FieldId);
  // UI never resolves a reference: its dependents remain explicitly visible and active for review.
  return source?.Setting is ReferenceSetting||source?.Setting is ConstantSetting literal&&(Equals(literal.Value,condition.Value)!=condition.Negate);
 }
 private void UpdateVisibility()
 {
  if(_disposed)return;_visibilityVersion++;_visibilityPending=true;if(_updatingVisibility)return;
  _updatingVisibility=true;var failures=new List<Exception>();
  try
  {
   while(_visibilityPending&&!_disposed)
   {
    _visibilityPending=false;var version=_visibilityVersion;
    bool Current()=>!_disposed&&version==_visibilityVersion;
    var targets=_visibility.ToArray().Select(item=>(item.Key,Visible:(item.Value.Advanced?.IsExpanded??true)
     &&(item.Value.Condition is not {} condition||Meets(condition))
     &&(!_rowGateAnchors.TryGetValue(item.Key,out var anchor)||IsFieldActive(anchor))
     &&(!_advancedFields.TryGetValue(item.Key,out var members)||members.Any(IsFieldActive)))).ToArray();
    try
    {
     ParameterCleanup.Run(targets.Select(item=>(Action)(()=>ParameterCleanup.Run(new Action[]{
      ()=>{if(Current())(item.Key as IParameterVisibility)?.PrepareForVisibility(item.Visible);},
      ()=>{if(Current())item.Key.Visibility=item.Visible?Visibility.Visible:Visibility.Collapsed;}
     }))).Concat(_parameterRows.ToArray().Select(row=>(Action)(()=>{if(Current())row.Refresh();})))
     .Append(()=>
     {
      var updates=new List<Action>();
      foreach(var (page,_) in _pages.ToArray())
      {
       var first=true;
       foreach(var group in page.Rows.GroupBy(row=>row.GroupName))
       {
        var visible=group.Any(row=>row.CustomVisibleA);
        var isFirst=visible&&first;
        updates.Add(()=>{if(Current())group.Key.CustomVisibleA=visible;});
        updates.Add(()=>{if(Current())group.Key.IsFirst=isFirst;});
        if(visible)first=false;
       }
      }
      ParameterCleanup.Run(updates);
     }));
    }
    catch(Exception failure){failures.Add(failure);}
   }
  }
  finally{_updatingVisibility=false;}
  ParameterCleanup.Run(failures.Select(failure=>(Action)(()=>System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw())));
 }
 public void Dispose()
 {
  if(_disposed)return;_disposed=true;_visibilityVersion++;_visibilityPending=false;
  var cleanup=new List<Action>();
  if(_tabTextScope is not null)cleanup.Add(_tabTextScope.Dispose);
  foreach(var advanced in _advanced.ToArray()){advanced.Changed-=UpdateVisibility;cleanup.Add(advanced.Dispose);}
  cleanup.AddRange(_parameterRows.ToArray().Select(row=>(Action)row.Dispose));
  foreach(var (page,host) in _pages.ToArray()){cleanup.Add(()=>host.SetRows(null));cleanup.Add(page.Dispose);}
  cleanup.AddRange(_displayCompositions.ToArray().Select(composition=>(Action)composition.Dispose));
  foreach(var field in _fields.ToArray())cleanup.Add(()=>field.Changed-=UpdateVisibility);
  cleanup.AddRange(_ranges.ToArray().Select(range=>(Action)range.Dispose));
  cleanup.AddRange(_editors.Values.ToArray().Select(editor=>(Action)editor.Dispose));
  cleanup.AddRange(_fieldStates.ToArray().Select(state=>(Action)state.Dispose));
  cleanup.Add(()=>
  {
   _parameterRows.Clear();_pages.Clear();_positions.Clear();_visibility.Clear();_advanced.Clear();_ranges.Clear();_editors.Clear();_rows.Clear();
   _fieldStates.Clear();_displayCompositions.Clear();_fields.Clear();_fieldConditions.Clear();_groups.Clear();
   _pairedFields.Clear();_validateComposition=null;_groupReferences=null;_isCurrent=null;_writeGroup=null!;
   _parentConditions.Clear();_rowGateAnchors.Clear();_advancedFields.Clear();_inlineConstants.Clear();
   _groupPresentations=new Dictionary<string,FieldPresentation>();
   _textExpressions=new Dictionary<string,TextExpressionOptions>();
   PageChanged=null;_tabs?.Items.Clear();Content=null;
  });
  ParameterCleanup.Run(cleanup);
 }
}
