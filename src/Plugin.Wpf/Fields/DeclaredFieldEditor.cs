using SunEyeVision.Plugin.Wpf.Adapters.References;
using SunEyeVision.Plugin.Wpf.Presenters.References;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Data;
using System.Windows.Controls.Primitives;
using SunEyeVision.Plugin.Wpf.Controls;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Presenters.Display;
using SunEyeVision.Plugin.Wpf.Presenters.Input;
using SunEyeVision.Plugin.Wpf.Adapters.Fields;

namespace SunEyeVision.Plugin.Wpf.Fields;

/// <summary>Edits one declared field on a host-owned draft. Never publishes, executes or reads resources.</summary>
public sealed partial class DeclaredFieldEditor : UserControl, IDisposable
{
    private readonly Port _port;
    private readonly DeclaredFieldState _state;
    private readonly bool _ownsState;
    private readonly ReferenceFieldOptions? _referenceOptions;
    private NativeReferencePresenter? _referencePresenter;
    private readonly Func<OutputReference,string>? _formatReference;
    private readonly IReadOnlyDictionary<string,string>? _choiceLabels;
    private readonly StackPanel _body = new();
    private readonly TextBlock _error = new() { Foreground = Brushes.OrangeRed, TextWrapping = TextWrapping.Wrap };
    private readonly List<DeclaredFieldEditor> _children = new();
    private ParameterSetting _setting => _state.Accepted;
    private ConstantSetting? _constant => _state.ConstantBackup;
    private readonly FieldPresentation? _presentation;
    private bool _referenceMode => _state.IsReferenceMode;
    private readonly Grid _row = new();
    private readonly StackPanel _actions = new() { Orientation=Orientation.Horizontal };
    private readonly TextBlock _label = new();
    private NativeNumberField? _numeric;
    private bool _updatingInput;
    private bool _disposed;
    private long _renderGeneration;
    private string _editError => _state.EditError;
    private bool _pairedSlider;
    private bool _regionSourceSelector;
    private readonly bool _allowUnboundReference;
    public void UseRegionSourceSelector(){_regionSourceSelector=true;HideLabel();Render();}
    public UIElement EditorBody => _row;
    public void SetLabel(string text)=>_label.Text=text;
    internal void SetLabelWidth(double width)
    {
        _row.ColumnDefinitions[0].Width=new GridLength(width);
        _error.Margin=new Thickness(width,2,0,0);
    }
    internal bool TryApplySetting(ParameterSetting setting){if(!Set(setting))return false;Render();return true;}
    public void UsePairedSlider(){_pairedSlider=true;_numeric?.UsePairedRange();}
    public bool TrySetConstant(object value)
    {
        if(!Set(new ConstantSetting(value)))return false;
        _updatingInput=true;
        try
        {
            if(_numeric is not null)_numeric.SetLiteral(value);
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
    private string NumericError => _numeric is not null&&!_numeric.TryValidatePendingEdit(out var error)?error:"";
    private bool MissingReference=>_state.MissingReference;
    public bool HasInvalidText => _state.HasInvalidInput || NumericError.Length!=0 || _children.Any(child => child.HasInvalidText);
    public string Error => _state.Error.Length!=0?_state.Error:NumericError is {Length:>0} numericError?numericError:_children.FirstOrDefault(child => child.HasInvalidText)?.Error ?? "";
    private void UpdateErrorDisplay()=>_error.Text=_state.Error;
    public event Action? Changed;

    public DeclaredFieldEditor(Port port, string label, ParameterSetting initial, Action<ParameterSetting> write,
        ReferenceFieldOptions? references = null, ConstantSetting? constantBackup = null,
        Func<OutputReference,string>? formatReference = null,IReadOnlyDictionary<string,string>? choiceLabels=null,FieldPresentation? presentation=null,bool allowUnboundReference=false)
        : this(new DeclaredFieldState(port,initial,write,constantBackup,allowUnboundReference,proposal=>references?.ValidateProposal?.Invoke(port,proposal)),label,
            references,formatReference,choiceLabels,presentation,allowUnboundReference)
    { _ownsState=true; }

    internal DeclaredFieldEditor(DeclaredFieldState state,string label,
        ReferenceFieldOptions? references=null,Func<OutputReference,string>? formatReference=null,
        IReadOnlyDictionary<string,string>? choiceLabels=null,FieldPresentation? presentation=null,bool allowUnboundReference=false)
    {
        var port=state.Port;
        _state=state;_port=state.Port;_referenceOptions=references;
        _formatReference=formatReference;_choiceLabels=choiceLabels;_presentation=presentation;
        _allowUnboundReference=allowUnboundReference;
        _state.Changed+=OnStateChanged;
        Resources.MergedDictionaries.Add(EditorResources.Create());
        _error.Style=EditorResources.ErrorStyle();
        Tag=port.Id;Margin=new Thickness(0,1,0,1);MinHeight=32;
        _row.ColumnDefinitions.Add(new(){Width=new GridLength(89)});_row.ColumnDefinitions.Add(new());_row.ColumnDefinitions.Add(new(){Width=GridLength.Auto});
        _label.Text=label;_label.VerticalAlignment=VerticalAlignment.Center;_label.TextTrimming=TextTrimming.CharacterEllipsis;_label.ToolTip=label;_label.FontSize=14;
        _row.Children.Add(_label);Grid.SetColumn(_body,1);_row.Children.Add(_body);Grid.SetColumn(_actions,2);_row.Children.Add(_actions);
        var panel=new StackPanel();panel.Children.Add(_row);_error.Margin=new Thickness(89,2,0,0);panel.Children.Add(_error);Content=panel;
        Render();
        UpdateErrorDisplay();
    }

    private bool Set(ParameterSetting next)=>!_disposed&&_state.TrySet(next);
    private void OnStateChanged()
    {
        if(_disposed)return;
        UpdateErrorDisplay();Changed?.Invoke();
    }

    private void Render()
    {
        if(_disposed)return;
        _renderGeneration++;
        DetachOwnedEditors();
        _body.Children.Clear();_actions.Children.Clear();InputElement=null;_numeric=null;
        if(_imageSources is not null){RenderImageSource();return;}
        var canReference=_referenceOptions is not null&&(_port.EditModes&FieldEditModes.Reference)!=0&&(_presentation?.Layout?.AllowReference!=false||_setting is ReferenceSetting);
        if(canReference&&(_port.EditModes&FieldEditModes.Constant)!=0)
        {
            var mode=new ToggleButton {Content=_referenceMode?"✎":"⇄",IsChecked=_referenceMode,Width=26,Height=26,Margin=new Thickness(5,0,0,0),ToolTip=_referenceMode?"切换为数值编辑":"切换为订阅"};
            mode.SetResourceReference(StyleProperty,"EditLinkButton");
            mode.Click+=(_,_)=>{if(_referenceMode){if(!_state.TryUseConstant())return;}else _state.EnterReferenceMode();Render();};_actions.Children.Add(mode);
        }
        if(!_port.Required&&_setting is not UnsetSetting)
        {
            var clear=new Button {Content="×",Width=26,Height=26,Margin=new Thickness(5,0,0,0),ToolTip="清除可选项"};
            clear.Click+=(_,_)=>{if(Set(new UnsetSetting()))Render();};_actions.Children.Add(clear);
        }
        if(_referenceMode||_setting is ReferenceSetting||_allowUnboundReference&&_port.EditModes==FieldEditModes.Reference)
        {
            _referencePresenter=new NativeReferencePresenter(_state,_referenceOptions??ReferenceFieldOptions.Empty,_formatReference);
            InputElement=_referencePresenter;_body.Children.Add(_referencePresenter);return;
        }
        if(_setting is UnsetSetting)
        {
            _body.Children.Add(new TextBlock{Text="未设置",Opacity=.6});
            if(_port.Type.Shape!=ValueShape.Resource&&(_port.EditModes==FieldEditModes.None||(_port.EditModes&FieldEditModes.Constant)!=0))
            {
                var create=new Button{Content="设置",Height=26,ToolTip="设置此可选项"};
                create.Click+=(_,_)=>{if(_state.TryUseConstant())Render();};_actions.Children.Add(create);
            }
            return;
        }
        var value = ((ConstantSetting)_setting).Value;
        if (_port.Type.Shape == ValueShape.Resource) { _body.Children.Add(new TextBlock { Text = "运行时资源，请选择来源" }); return; }
        if (_port.Type.Shape == ValueShape.Scalar)
        {
            if (_port.Type.Scalar == ScalarKind.Boolean)
            {
                if(_presentation?.Layout?.DialogPrimary==true)
                {
                    var eye=new ToggleButton{IsChecked=value as bool?==true,ToolTip="显示 / 隐藏",Width=36,Height=30};
                    eye.SetResourceReference(StyleProperty,"eyeToggleButton");
                    eye.Click+=(_,_)=>{var visible=((ConstantSetting)_setting).Value as bool?!=true;if(Set(new ConstantSetting(visible)))eye.IsChecked=visible;};
                    InputElement=eye;_body.Children.Add(eye);return;
                }
                var generation=_renderGeneration;
                var input = new SunEyeVision.Plugin.Wpf.Native.Composition.Apps.XmlUI.ParamTabControl.BooleanControl
                    { IsChecked = value as bool? == true,IsThreeState=false,
                      OffContent=NativeInputFactory.BooleanImage(false),OnContent=NativeInputFactory.BooleanImage(true),
                      HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Center };
                input.OnCheckedChange += (_, _) =>
                {
                    if(_disposed||generation!=_renderGeneration||_updatingInput||!input.IsEnabled)return;
                    if(input.IsChecked is bool proposed&&_setting is ConstantSetting {Value:bool accepted}&&proposed==accepted&&_editError.Length==0)return;
                    if(Set(new ConstantSetting(input.IsChecked)))return;
                    _updatingInput=true;
                    try{input.SetCurrentValue(ToggleButton.IsCheckedProperty,((ConstantSetting)_setting).Value as bool?==true);}
                    finally{_updatingInput=false;}
                };
                InputElement = input; _body.Children.Add(input);
            }
            else if (_port.Choices is { } choices)
            {
                if(_regionSourceSelector)
                {
                    var selector=new RegionSourceModeSelector((string)value!,key=>Set(new ConstantSetting(key)));
                    InputElement=selector;_body.Children.Add(selector);return;
                }
                var generation=_renderGeneration;
                var input=NativeInputFactory.Enumeration();
                input.DisplayMemberPath=nameof(ChoiceItem.Label);input.SelectedValuePath=nameof(ChoiceItem.Key);
                input.ItemsSource=choices.Keys.Select(key=>new ChoiceItem(key,_choiceLabels?.GetValueOrDefault(key,key)??key)).ToArray();
                input.SelectedValue=value;
                if(value is not string current||!choices.Keys.Contains(current))
                {_state.SetEditError("当前枚举值不在声明选项中，请重新选择。");}
                var restoring=false;
                input.SelectionChanged += (_, _) =>
                {
                    if(_disposed||generation!=_renderGeneration||restoring||!input.IsEnabled||input.SelectedValue is not string key)return;
                    if(_setting is ConstantSetting accepted&&Equals(accepted.Value,key)&&_editError.Length==0)return;
                    if(Set(new ConstantSetting(key)))return;
                    restoring=true;
                    try{input.SetCurrentValue(ComboBox.SelectedValueProperty,((ConstantSetting)_setting).Value);}
                    finally{restoring=false;}
                };
                InputElement=input;_body.Children.Add(input);
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
                        if(_numeric is not null&&!_numeric.TryValidatePendingEdit(out var numericError))
                        { _state.SetEditError(numericError);return; }
                        object parsed = _port.Type.Scalar switch
                        {
                            ScalarKind.Int64 => long.Parse(input.Text, CultureInfo.InvariantCulture),
                            ScalarKind.Float64 => double.Parse(input.Text, CultureInfo.InvariantCulture),
                            _ => input.Text
                        };
                        Set(new ConstantSetting(parsed));
                    }
                    catch (Exception exception) when (exception is FormatException or OverflowException)
                    { _state.SetEditError("请输入有效的" + (_port.Type.Scalar == ScalarKind.Int64 ? "整数。" : "数字。")); }
                };
                input.KeyDown+=(_,args)=>
                {
                    if(args.Key!=System.Windows.Input.Key.Enter)return;
                    args.Handled=true;
                    if(HasInvalidText)return;
                    System.Windows.Input.FocusManager.SetFocusedElement(System.Windows.Input.FocusManager.GetFocusScope(input),null);
                    System.Windows.Input.Keyboard.ClearFocus();
                };

                }
                var numeric=_port.Type.Scalar is ScalarKind.Int64 or ScalarKind.Float64;
                // Int64 must never travel through a double-valued visual; retain the exact text editor.
                if(numeric&&_port.Storage!=NumericStorage.Int64&&(_port.Type.Scalar!=ScalarKind.Int64||_port.Storage is not null))
                {
                    var layout=_presentation?.Layout;
                    var input=new NativeNumberField(_port,layout,value!,literal=>Set(new ConstantSetting(literal)));
                    if(_pairedSlider)input.UsePairedRange();
                    input.InputReady+=()=>InputElement=input.TextEditor;
                    input.ValidationChanged+=()=>Changed?.Invoke();
                    _numeric=input;InputElement=input;_body.Children.Add(input);
                }
                else {var input=new TextBox{Height=30,FontSize=14,VerticalContentAlignment=VerticalAlignment.Center};Attach(input);_body.Children.Add(input);}

            }
        }
        else if (_port.Type.Shape == ValueShape.Record && value is RecordValue record)
        {
            if (_port.Type.Equals(PresentationDeclarations.Rgba))
            {
                var input=new NativeColorPresenter {Height=32,SelectedColor=Color.FromArgb(
                    checked((byte)record.Get<long>("a")),checked((byte)record.Get<long>("r")),
                    checked((byte)record.Get<long>("g")),checked((byte)record.Get<long>("b")))};
                input.SelectedColorChanged+=OnNativeColorChanged;
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

    private void OnNativeColorChanged(object sender,RoutedPropertyChangedEventArgs<Color> e)
    {
        if(_disposed||_updatingInput||sender is not NativeColorPresenter input||!ReferenceEquals(input,InputElement))return;
        if(Set(new ConstantSetting(PresentationDeclarations.Color(e.NewValue.R,e.NewValue.G,e.NewValue.B,e.NewValue.A))))return;
        var accepted=(RecordValue)((ConstantSetting)_setting).Value!;
        _updatingInput=true;
        try{input.SetCurrentValue(NativeColorPresenter.SelectedColorProperty,Color.FromArgb(
            checked((byte)accepted.Get<long>("a")),checked((byte)accepted.Get<long>("r")),
            checked((byte)accepted.Get<long>("g")),checked((byte)accepted.Get<long>("b"))));}
        finally{_updatingInput=false;}
    }

    public bool TryCommitPending()=>_referencePresenter?.TryCommitPending()??true;

    private void DetachOwnedEditors()
    {
        _referencePresenter?.Dispose();_referencePresenter=null;
        DetachImageSource();
        if(InputElement is ComboBox combo)combo.SetCurrentValue(ComboBox.IsDropDownOpenProperty,false);
        if(InputElement is NativeColorPresenter color)
        {color.SelectedColorChanged-=OnNativeColorChanged;color.Dispose();}
        foreach(var child in _children)child.Dispose();
        _children.Clear();
    }

    public void Dispose()
    {
        if(_disposed)return;
        _disposed=true;_renderGeneration++;
        _state.Changed-=OnStateChanged;if(_ownsState)_state.Dispose();
        DetachOwnedEditors();Changed=null;
        _body.Children.Clear();_actions.Children.Clear();InputElement=null;_numeric=null;Content=null;
    }

    private sealed record ChoiceItem(string Key,string Label);

}
