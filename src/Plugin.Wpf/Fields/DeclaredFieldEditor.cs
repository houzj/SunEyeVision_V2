using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Data;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Plugin.Wpf.Fields;

/// <summary>Edits one declared field on a host-owned draft. Never publishes, executes or reads resources.</summary>
public sealed class DeclaredFieldEditor : UserControl
{
    private readonly Port _port;
    private readonly Action<ParameterSetting> _write;
    private readonly Func<Port, ReferenceSetting?>? _selectReference;
    private readonly Func<OutputReference,string>? _formatReference;
    private readonly IReadOnlyDictionary<string,string>? _choiceLabels;
    private readonly StackPanel _body = new();
    private readonly TextBlock _error = new() { Foreground = Brushes.OrangeRed, TextWrapping = TextWrapping.Wrap };
    private readonly List<DeclaredFieldEditor> _children = new();
    private ParameterSetting _setting;
    private ConstantSetting? _constant;
    private Button? _constantAction;
    public string FieldId => _port.Id;
    public ParameterSetting Setting => _setting;
    public FrameworkElement? InputElement { get; private set; }
    public bool HasInvalidText => _error.Text.Length != 0 || _children.Any(child => child.HasInvalidText);
    public string Error => _error.Text.Length != 0 ? _error.Text : _children.FirstOrDefault(child => child.HasInvalidText)?.Error ?? "";
    public event Action? Changed;

    public DeclaredFieldEditor(Port port, string label, ParameterSetting initial, Action<ParameterSetting> write,
        Func<Port, ReferenceSetting?>? selectReference = null, ConstantSetting? constantBackup = null,
        Func<OutputReference,string>? formatReference = null,IReadOnlyDictionary<string,string>? choiceLabels=null)
    {
        _port = port; _setting = initial; _constant = initial as ConstantSetting ?? constantBackup; _write = write; _selectReference = selectReference;
        _formatReference=formatReference;_choiceLabels=choiceLabels;
        Tag = port.Id; Margin = new Thickness(0, 3, 0, 5);
        var panel = new StackPanel();
        var header = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };
        var actions = new StackPanel { Orientation = Orientation.Horizontal };
        DockPanel.SetDock(actions, Dock.Right); header.Children.Add(actions);
        Button Action(string text, string tooltip, Action action)
        {
            var button = new Button { Content = text, ToolTip = tooltip, Padding = new Thickness(6, 1, 6, 1), Margin = new Thickness(3, 0, 0, 0) };
            button.Click += (_, _) => action(); actions.Children.Add(button);
            return button;
        }
        if (selectReference is not null && (port.EditModes & FieldEditModes.Reference) != 0)
            Action("绑定", "从声明的输出中选择来源", () => { var value = _selectReference!(port); if (value is not null && Set(value)) Render(); });
        if (port.Type.Shape != ValueShape.Resource && (port.EditModes == FieldEditModes.None || (port.EditModes & FieldEditModes.Constant) != 0))
            _constantAction = Action("常量", "恢复此字段最近的常量草稿", () => { try { if (Set(_constant ?? new ConstantSetting(Default(port.Type)))) Render(); }
                catch(ArgumentException exception) { _error.Text=exception.Message;Changed?.Invoke(); } });
        if (!port.Required) Action("清除", "移除此可选设置；不等同于空集合或空区域", () => { if (Set(new UnsetSetting())) Render(); });
        header.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(header); panel.Children.Add(_body); panel.Children.Add(_error); Content = panel;
        Render();
    }

    private bool Set(ParameterSetting next)
    {
        try
        {
            if (next is UnsetSetting && _port.Required) throw new ArgumentException("必填项不能清除。");
            if (next is ConstantSetting constant)
            {
                if (_port.Type.Shape == ValueShape.Resource) throw new ArgumentException("图像和区域资源必须绑定本次执行来源。");
                if (_port.EditModes != FieldEditModes.None && (_port.EditModes & FieldEditModes.Constant) == 0) throw new ArgumentException("此字段不允许常量。");
                next = new ConstantSetting(ValueRules.Freeze(_port, constant.Value));
            }
            _write(next); _setting = next;
            if (next is ConstantSetting accepted) _constant = accepted;
            _error.Text = ""; Changed?.Invoke(); return true;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or FormatException or OverflowException)
        { _error.Text = exception.Message; Changed?.Invoke(); return false; }
    }

    private void Render()
    {
        _body.Children.Clear(); _children.Clear(); InputElement = null;
        if(_constantAction is not null)_constantAction.Visibility=_setting is ConstantSetting?Visibility.Collapsed:Visibility.Visible;
        if (_setting is ReferenceSetting reference)
        {
            _body.Children.Add(new TextBlock { Text = _formatReference?.Invoke(reference.Reference)??$"{reference.Reference.NodeId} · {reference.Reference.PortId}", TextWrapping = TextWrapping.Wrap }); return;
        }
        if (_setting is UnsetSetting) { _body.Children.Add(new TextBlock { Text = "未设置", Opacity = .6 }); return; }
        var value = ((ConstantSetting)_setting).Value;
        if (_port.Type.Shape == ValueShape.Resource) { _body.Children.Add(new TextBlock { Text = "运行时资源，请选择来源" }); return; }
        if (_port.Type.Shape == ValueShape.Scalar)
        {
            if (_port.Type.Scalar == ScalarKind.Boolean)
            {
                var input = new CheckBox { IsChecked = value as bool? }; input.Click += (_, _) => Set(new ConstantSetting(input.IsChecked == true)); InputElement = input; _body.Children.Add(input);
            }
            else if (_port.Choices is { } choices)
            {
                var input = new ComboBox { ItemsSource = choices.Keys, SelectedItem = value };
                if(_choiceLabels is {Count:>0})
                {
                    var label=new FrameworkElementFactory(typeof(TextBlock));
                    label.SetBinding(TextBlock.TextProperty,new Binding{Converter=new ChoiceLabelConverter(_choiceLabels)});
                    input.ItemTemplate=new DataTemplate{VisualTree=label};
                }
                input.SelectionChanged += (_, _) => { if (input.SelectedItem is string key) Set(new ConstantSetting(key)); }; InputElement = input; _body.Children.Add(input);
            }
            else
            {
                var input = new TextBox { Text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "", MinWidth = 90 };
                input.TextChanged += (_, _) =>
                {
                    try
                    {
                        object parsed = _port.Type.Scalar switch
                        {
                            ScalarKind.Int64 => long.Parse(input.Text, CultureInfo.InvariantCulture),
                            ScalarKind.Float64 => double.Parse(input.Text, CultureInfo.InvariantCulture),
                            _ => input.Text
                        };
                        Set(new ConstantSetting(parsed));
                    }
                    catch (Exception exception) when (exception is FormatException or OverflowException)
                    { _error.Text = "请输入有效的" + (_port.Type.Scalar == ScalarKind.Int64 ? "整数。" : "数字。"); Changed?.Invoke(); }
                };
                InputElement = input; _body.Children.Add(input);
            }
        }
        else if (_port.Type.Shape == ValueShape.Record && value is RecordValue record)
        {
            foreach (var field in _port.Type.Fields)
            {
                var child = new DeclaredFieldEditor(field, field.Id,
                    record.Contains(field.Id) ? new ConstantSetting(record.Get<object?>(field.Id)) : new UnsetSetting(), next =>
                    {
                        var current = (RecordValue)((ConstantSetting)_setting).Value!;
                        var values = current.Declaration.Fields.Where(p => current.Contains(p.Id)).ToDictionary(p => p.Id, p => current.Get<object?>(p.Id));
                        if (next is UnsetSetting) values.Remove(field.Id); else values[field.Id] = ((ConstantSetting)next).Value;
                        if (!Set(new ConstantSetting(new RecordValue(_port.Type, values)))) throw new ArgumentException(_error.Text);
                    });
                child.Margin = new Thickness(10, 2, 0, 2); _children.Add(child); _body.Children.Add(child);
            }
        }
        else
        {
            // Geometry and collection edits have dedicated controls. Do not pretend an object
            // ToString textbox is an editable typed value, or inspect a borrowed resource.
            _body.Children.Add(new TextBlock { Text = "此字段由专用区域或集合编辑器处理。", TextWrapping = TextWrapping.Wrap });
        }
    }

    private sealed class ChoiceLabelConverter(IReadOnlyDictionary<string,string> labels):IValueConverter
    {
        public object Convert(object value,Type target,object parameter,CultureInfo culture)=>value is string key?labels.GetValueOrDefault(key,key):value;
        public object ConvertBack(object value,Type target,object parameter,CultureInfo culture)=>throw new NotSupportedException();
    }

    private static object? Default(TypeRef type) => type.Shape switch
    {
        ValueShape.Scalar => type.Scalar switch { ScalarKind.Boolean => false, ScalarKind.Int64 => 0L, ScalarKind.Float64 => 0d, _ => "" },
        ValueShape.Record => new RecordValue(type, type.Fields.Where(field => field.Required).ToDictionary(field => field.Id, field => Default(field.Type))),
        ValueShape.Sequence => new SequenceValue(type, Array.Empty<object?>()),
        _ => throw new ArgumentException("请使用相应的区域或资源编辑入口。")
    };
}
