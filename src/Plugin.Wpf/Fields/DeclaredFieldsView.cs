using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SunEyeVision.Plugin.Contracts;
namespace SunEyeVision.Plugin.Wpf.Fields;

/// <summary>Finite page/section/conditional composition. No workflow access or publication.</summary>
public sealed class DeclaredFieldsView : UserControl
{
 private readonly List<DeclaredFieldEditor> _fields=new();
 private readonly Dictionary<string,StackPanel> _groups=new(StringComparer.Ordinal);
 private readonly List<(FrameworkElement View,FieldCondition Condition)> _conditions=new();
 public IReadOnlyList<DeclaredFieldEditor> Fields=>_fields.AsReadOnly();
 public void AddToGroup(string group,UIElement content,int? index=null)
 {if(!_groups.TryGetValue(group,out var panel))throw new ArgumentException("Unknown declared presentation group.");if(index.HasValue)panel.Children.Insert(index.Value,content);else panel.Children.Add(content);}
 public bool TryValidate(out string error)
 {error=_fields.FirstOrDefault(field=>field.HasInvalidText)?.Error??"";return error.Length==0;}
 public DeclaredFieldsView(IEnumerable<(Port Port,FieldPresentation Presentation,ParameterSetting Value)> fields,
  Action<string,ParameterSetting> writeDraft,Func<Port,ReferenceSetting?>? selectReference=null,
  Func<string,ConstantSetting?>? constantBackup=null,Func<OutputReference,string>? formatReference=null)
 {
  Resources.MergedDictionaries.Add(EditorResources.Create());Background=new SolidColorBrush(Color.FromRgb(242,243,245));FontSize=14;
  var all=fields.OrderBy(field=>field.Presentation.Order).ToArray();var editors=new Dictionary<string,DeclaredFieldEditor>();
  foreach(var field in all)
  {
   var editor=new DeclaredFieldEditor(field.Port,field.Presentation.DisplayName,field.Value,value=>writeDraft(field.Port.Id,value),selectReference,constantBackup?.Invoke(field.Port.Id),formatReference,field.Presentation.ChoiceLabels,field.Presentation);
   _fields.Add(editor);editors.Add(field.Port.Id,editor);editor.Changed+=UpdateVisibility;
  }
  var tabs=new TabControl {BorderThickness=new Thickness(0),Background=Background};
  foreach(var page in all.GroupBy(field=>field.Presentation.Layout?.Page??field.Presentation.Group))
  {
   var body=new StackPanel{Margin=new Thickness(32,4,32,18)};_groups[page.Key]=body;
   foreach(var group in page.GroupBy(field=>field.Presentation.Group))_groups.TryAdd(group.Key,body);
   foreach(var section in page.GroupBy(field=>(field.Presentation.Layout?.Section??field.Presentation.Group,field.Presentation.Layout?.Advanced??false)))
   {
    var content=new StackPanel{Margin=new Thickness(0,12,0,4)};
    if(section.Key.Item2)body.Children.Add(new Expander{Header=section.Key.Item1,Content=content,IsExpanded=false,Margin=new Thickness(0,8,0,2)});
    else {body.Children.Add(new TextBlock{Text=section.Key.Item1,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,16,0,10)});body.Children.Add(content);}
    var paired=section.Where(f=>f.Presentation.Layout?.PairWith is not null).Select(f=>f.Presentation.Layout!.PairWith!).ToHashSet();
    foreach(var field in section)
    {
     if(paired.Contains(field.Port.Id))continue;
     FrameworkElement view=editors[field.Port.Id];
     if(field.Presentation.Layout?.PairWith is {} partner&&editors.TryGetValue(partner,out var maximum))
     {view=new RangeField(editors[field.Port.Id],maximum,field.Presentation.Layout);editors[field.Port.Id].SetLabel(field.Presentation.DisplayName.Replace("下限","范围"));}
     content.Children.Add(view);
     if(field.Presentation.Layout?.VisibleWhen is {} condition)_conditions.Add((view,condition));
    }
   }
   tabs.Items.Add(new TabItem{Header=page.Key,Content=new ScrollViewer{Content=body,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled}});
  }
  Content=tabs;UpdateVisibility();
 }
 private void UpdateVisibility()
 {
  foreach(var (view,condition) in _conditions)
  {
   var source=_fields.SingleOrDefault(f=>f.FieldId==condition.FieldId);
   // A reference cannot be resolved by UI; keep the dependent editor visible for explicit review.
   view.Visibility=source?.Setting is ReferenceSetting||source?.Setting is ConstantSetting literal&&Equals(literal.Value,condition.Value)?Visibility.Visible:Visibility.Collapsed;
  }
 }
}
