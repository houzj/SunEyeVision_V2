using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Data;
using System.Windows.Controls.Primitives;
using SunEyeVision.Plugin.Wpf.Controls;
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
    private readonly FieldPresentation? _presentation;
    private bool _referenceMode;
    private readonly Grid _row = new();
    private readonly StackPanel _actions = new() { Orientation=Orientation.Horizontal };
    private readonly TextBlock _label = new();
    private NumericUpDown? _numeric;
    private bool _updatingInput;
    public UIElement EditorBody => _row;
    public void SetLabel(string text)=>_label.Text=text;
    public void UsePairedSlider(){if(_numeric is not null)_numeric.ShowSlider=false;}
    public bool TrySetConstant(object value)
    {
        if(HasInvalidText||!Set(new ConstantSetting(value)))return false;
        _updatingInput=true;
        try
        {
            if(_numeric is not null)_numeric.Value=Convert.ToDouble(value,CultureInfo.InvariantCulture);
            else if(InputElement is TextBox text)text.Text=Convert.ToString(value,CultureInfo.InvariantCulture)??"";
            else Render();
        }
        finally{_updatingInput=false;}
        return true;
    }
    public void HideLabel() { _label.Visibility=Visibility.Collapsed;_row.ColumnDefinitions[0].Width=new GridLength(0); }
    public string FieldId => _port.Id;
    public ParameterSetting Setting => _setting;
    public FrameworkElement? InputElement { get; private set; }
    public bool HasInvalidText => (_referenceMode && _setting is not ReferenceSetting) || _error.Text.Length != 0 || _children.Any(child => child.HasInvalidText);
    public string Error => _referenceMode && _setting is not ReferenceSetting ? "请选择订阅来源，或切回数值模式。" : _error.Text.Length != 0 ? _error.Text : _children.FirstOrDefault(child => child.HasInvalidText)?.Error ?? "";
    public event Action? Changed;

    public DeclaredFieldEditor(Port port, string label, ParameterSetting initial, Action<ParameterSetting> write,
        Func<Port, ReferenceSetting?>? selectReference = null, ConstantSetting? constantBackup = null,
        Func<OutputReference,string>? formatReference = null,IReadOnlyDictionary<string,string>? choiceLabels=null,FieldPresentation? presentation=null)
    {
        _port = port; _setting = initial; _constant = initial as ConstantSetting ?? constantBackup; _write = write; _selectReference = selectReference;
        _formatReference=formatReference;_choiceLabels=choiceLabels;
        _presentation=presentation;_referenceMode=initial is ReferenceSetting;
        Resources.MergedDictionaries.Add(EditorResources.Create());
        Tag=port.Id;Margin=new Thickness(0,1,0,1);MinHeight=32;
        _row.ColumnDefinitions.Add(new(){Width=new GridLength(89)});_row.ColumnDefinitions.Add(new());_row.ColumnDefinitions.Add(new(){Width=GridLength.Auto});
        _label.Text=label;_label.VerticalAlignment=VerticalAlignment.Center;_label.TextTrimming=TextTrimming.CharacterEllipsis;_label.ToolTip=label;_label.FontSize=14;
        _row.Children.Add(_label);Grid.SetColumn(_body,1);_row.Children.Add(_body);Grid.SetColumn(_actions,2);_row.Children.Add(_actions);
        var panel=new StackPanel();panel.Children.Add(_row);_error.Margin=new Thickness(89,2,0,0);panel.Children.Add(_error);Content=panel;
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
        _body.Children.Clear();_children.Clear();_actions.Children.Clear();InputElement=null;_numeric=null;
        var canReference=_selectReference is not null&&(_port.EditModes&FieldEditModes.Reference)!=0&&(_presentation?.Layout?.AllowReference!=false||_setting is ReferenceSetting);
        if(canReference)
        {
            var mode=new ToggleButton {Content=_referenceMode?"✎":"⇄",IsChecked=_referenceMode,Width=26,Height=26,Margin=new Thickness(5,0,0,0),ToolTip=_referenceMode?"切换为数值编辑":"切换为订阅"};
            mode.Click+=(_,_)=>{if(_referenceMode){if(_port.Type.Shape!=ValueShape.Resource&&Set(_constant??new ConstantSetting(Default(_port.Type))))_referenceMode=false;}else _referenceMode=true;Render();Changed?.Invoke();};_actions.Children.Add(mode);
        }
        if(!_port.Required&&_setting is not UnsetSetting)
        {
            var clear=new Button {Content="×",Width=26,Height=26,Margin=new Thickness(5,0,0,0),ToolTip="清除可选项"};
            clear.Click+=(_,_)=>{if(Set(new UnsetSetting())){_referenceMode=false;Render();}};_actions.Children.Add(clear);
        }
        if(_referenceMode||_setting is ReferenceSetting)
        {
            var dock=new DockPanel();var pick=new Button{Content="🔗",Width=29,Height=30,ToolTip="选择订阅来源"};DockPanel.SetDock(pick,Dock.Right);dock.Children.Add(pick);
            var label=new TextBox {IsReadOnly=true,Height=30,VerticalContentAlignment=VerticalAlignment.Center,Text=_setting is ReferenceSetting reference?(_formatReference?.Invoke(reference.Reference)??reference.Reference.NodeId+" · "+reference.Reference.PortId):""};dock.Children.Add(label);
            pick.Click+=(_,_)=>{var selected=_selectReference?.Invoke(_port);if(selected is not null&&Set(selected)){_referenceMode=true;Render();}};
            _body.Children.Add(dock);return;
        }
        if(_setting is UnsetSetting)
        {
            _body.Children.Add(new TextBlock{Text="未设置",Opacity=.6});
            if(_port.Type.Shape!=ValueShape.Resource&&(_port.EditModes==FieldEditModes.None||(_port.EditModes&FieldEditModes.Constant)!=0))
            {
                var create=new Button{Content="设置",Height=26,ToolTip="设置此可选项"};
                create.Click+=(_,_)=>{if(Set(_constant??new ConstantSetting(Default(_port.Type))))Render();};_actions.Children.Add(create);
            }
            return;
        }
        var value = ((ConstantSetting)_setting).Value;
        if (_port.Type.Shape == ValueShape.Resource) { _body.Children.Add(new TextBlock { Text = "运行时资源，请选择来源" }); return; }
        if (_port.Type.Shape == ValueShape.Scalar)
        {
            if (_port.Type.Scalar == ScalarKind.Boolean)
            {
                var input = new ToggleSwitch { IsChecked = value as bool? == true, SwitchWidth=40,SwitchHeight=22,CornerRadius=2 }; input.CheckedChanged += (_, _) => Set(new ConstantSetting(input.IsChecked)); InputElement = input; _body.Children.Add(input);
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
                void Attach(TextBox input)
                {
                    input.Text=Convert.ToString(value,CultureInfo.InvariantCulture)??"";
                    InputElement=input;
                input.TextChanged += (_, _) =>
                {
                    if(_updatingInput)return;
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

                }
                var numeric=_port.Type.Scalar is ScalarKind.Int64 or ScalarKind.Float64;
                // Int64 must never travel through a double-valued visual; retain the exact text editor.
                if(numeric&&_port.Storage!=NumericStorage.Int64&&(_port.Type.Scalar!=ScalarKind.Int64||_port.Storage is not null))
                {
                    var layout=_presentation?.Layout;
                    var input=new NumericUpDown{Height=32,FontSize=14,
                        Minimum=layout?.Minimum??-double.MaxValue,Maximum=layout?.Maximum??double.MaxValue,
                        Value=Convert.ToDouble(value,CultureInfo.InvariantCulture),
                        DecimalPlaces=_port.Type.Scalar==ScalarKind.Int64?0:layout?.DecimalPlaces??2,
                        ShowSlider=layout?.Minimum is not null&&layout.Maximum is not null,
                        IntegerType=_port.Type.Scalar==ScalarKind.Int64?TypeCode.Int32:TypeCode.Empty};
                    TextBox? attached=null;
                    input.Loaded+=(_,_)=>{input.ApplyTemplate();if(input.TextEditor is {} box&&!ReferenceEquals(box,attached)){attached=box;Attach(box);}};
                    _numeric=input;InputElement=input;_body.Children.Add(input);
                }
                else {var input=new TextBox{Height=30,FontSize=14,VerticalContentAlignment=VerticalAlignment.Center};Attach(input);_body.Children.Add(input);}

            }
        }
        else if (_port.Type.Shape == ValueShape.Record && value is RecordValue record)
        {
            if (_port.Type.Equals(PresentationDeclarations.Rgba))
            {
                var input=new ColorField {Height=32,SelectedColor=Color.FromArgb(
                    checked((byte)record.Get<long>("a")),checked((byte)record.Get<long>("r")),
                    checked((byte)record.Get<long>("g")),checked((byte)record.Get<long>("b")))};
                input.SelectedColorChanged+=(_,e)=>Set(new ConstantSetting(PresentationDeclarations.Color(e.NewValue.R,e.NewValue.G,e.NewValue.B,e.NewValue.A)));
                InputElement=input;_body.Children.Add(input);return;
            }
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
