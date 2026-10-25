using System.Windows;
using System.Windows.Controls;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Plugin.Wpf.Fields;

/// <summary>Composition of declared field editors. Callbacks address a private draft, not document publication.</summary>
public sealed class DeclaredFieldsView : UserControl
{
    private readonly List<DeclaredFieldEditor> _fields = new();
    private readonly Dictionary<string,StackPanel> _groups=new(StringComparer.Ordinal);
    public void AddToGroup(string group,UIElement content,int? index=null)
    {if(!_groups.TryGetValue(group,out var panel))throw new ArgumentException("Unknown declared presentation group.",nameof(group));if(index.HasValue)panel.Children.Insert(index.Value,content);else panel.Children.Add(content);}
    public IReadOnlyList<DeclaredFieldEditor> Fields => _fields.AsReadOnly();
    public bool TryValidate(out string error)
    { error = _fields.FirstOrDefault(field => field.HasInvalidText)?.Error ?? ""; return error.Length == 0; }
    public DeclaredFieldsView(IEnumerable<(Port Port, FieldPresentation Presentation, ParameterSetting Value)> fields,
        Action<string, ParameterSetting> writeDraft, Func<Port, ReferenceSetting?>? selectReference = null,
        Func<string,ConstantSetting?>? constantBackup = null,Func<OutputReference,string>? formatReference = null)
    {
        var tabs = new TabControl();
        foreach (var group in fields.OrderBy(field => field.Presentation.Order).GroupBy(field => field.Presentation.Group))
        {
            var body = new StackPanel { Margin = new Thickness(12) };
            _groups.Add(group.Key,body);
            foreach (var field in group)
            {
                var editor = new DeclaredFieldEditor(field.Port, field.Presentation.DisplayName, field.Value,
                    value => writeDraft(field.Port.Id, value), selectReference,constantBackup?.Invoke(field.Port.Id),formatReference,field.Presentation.ChoiceLabels);
                _fields.Add(editor); body.Children.Add(editor);
            }
            tabs.Items.Add(new TabItem { Header = group.Key, Content = new ScrollViewer { Content=body, VerticalScrollBarVisibility=ScrollBarVisibility.Auto } });
        }
        Content = tabs;
    }
}
