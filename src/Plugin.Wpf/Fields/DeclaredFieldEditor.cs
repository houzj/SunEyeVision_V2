using SunEyeVision.Plugin.Wpf.Adapters.References;
using SunEyeVision.Plugin.Wpf.Presenters.References;
using SunEyeVision.Plugin.Wpf.Presenters.Numeric;
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
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;

namespace SunEyeVision.Plugin.Wpf.Fields;

/// <summary>Edits one declared field on a host-owned draft. Never publishes, executes or reads resources.</summary>
public sealed partial class DeclaredFieldEditor : UserControl, IDisposable,IParameterVisibility
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
    private readonly SunEyeVision.Plugin.Wpf.Native.Composition.Apps.XmlUI.ParamTab.LeftRightControl _row = new();
    private readonly StackPanel _externalActions=new(){Orientation=Orientation.Horizontal};
    internal void AddInlineAction(UIElement action)=>_externalActions.Children.Add(action);
    internal void RemoveInlineAction(UIElement action)=>_externalActions.Children.Remove(action);
    private readonly StackPanel _actions = new() { Orientation=Orientation.Horizontal };
    private readonly TextBlock _label = new();
    private NativeNumberPresenter? _numeric;
    private readonly NativeIntegerRadioOptions? _integerRadioOptions;
    private NativeIntegerRadioPresenter? _integerRadio;
    private bool _updatingInput;
    private bool _disposed;
    private long _renderGeneration;
    private string _editError => _state.EditError;
    private bool _regionSourceSelector;
    private readonly bool _allowUnboundReference;
    public void UseRegionSourceSelector(){_regionSourceSelector=true;HideLabel();Render();}
    public UIElement EditorBody => _row;
    public void SetLabel(string text){_label.Text=text;_integerRadio?.SetLabel(text);}
    internal void SetLabelWidth(double width)
    {
        if(_integerRadioOptions is null)_row.ChildLeftWidth=width;
    }
    internal bool TryApplySetting(ParameterSetting setting){if(!Set(setting))return false;Render();return true;}
    public bool TrySetConstant(object value)
    {
        if(_integerRadio is not null)return _integerRadio.TrySetConstant(value);
        if(_numeric is not null)return _numeric.TrySetConstant(value);
        if(!Set(new ConstantSetting(value)))return false;
        _updatingInput=true;
        try
        {
            if(InputElement is TextBox text)text.Text=Convert.ToString(value,CultureInfo.InvariantCulture)??"";
            else Render();
        }
        finally{_updatingInput=false;}
        return true;
    }
    public void HideLabel() { _label.Visibility=Visibility.Collapsed;_row.ChildLeftWidth=0; }
    internal bool IsDisposed=>_disposed||_state.IsDisposed;
    public string FieldId => _port.Id;
    public ParameterSetting Setting => _setting;
    private FrameworkElement? _inputElement;
    public FrameworkElement? InputElement {get=>_numeric?.TextEditor??_inputElement;private set=>_inputElement=value;}
    private bool MissingReference=>_state.MissingReference;
    internal NativeParameterItemState NativeItemState=>_state.EditError.Length!=0?NativeParameterItemState.WriteError:_state.SourceError.Length!=0?NativeParameterItemState.ReadError:HasInvalidText?NativeParameterItemState.WriteError:NativeParameterItemState.Normal;
    public bool HasInvalidText => _state.HasInvalidInput || _children.Any(child => child.HasInvalidText);
    public string Error => _state.Error.Length!=0?_state.Error:_children.FirstOrDefault(child => child.HasInvalidText)?.Error ?? "";
    private void UpdateErrorDisplay()=>_error.Text=_state.Error;
    public event Action? Changed;

    public DeclaredFieldEditor(Port port, string label, ParameterSetting initial, Action<ParameterSetting> write,
        ReferenceFieldOptions? references = null, ConstantSetting? constantBackup = null,
        Func<OutputReference,string>? formatReference = null,IReadOnlyDictionary<string,string>? choiceLabels=null,FieldPresentation? presentation=null,bool allowUnboundReference=false,
        NativeIntegerRadioOptions? integerRadioOptions=null)
        : this(new DeclaredFieldState(port,initial,write,constantBackup,allowUnboundReference,proposal=>integerRadioOptions?.Validate(proposal)??references?.ValidateProposal?.Invoke(port,proposal)),label,
            references,formatReference,choiceLabels,presentation,allowUnboundReference,integerRadioOptions,ownsState:true)
    { }

    internal DeclaredFieldEditor(DeclaredFieldState state,string label,
        ReferenceFieldOptions? references=null,Func<OutputReference,string>? formatReference=null,
        IReadOnlyDictionary<string,string>? choiceLabels=null,FieldPresentation? presentation=null,bool allowUnboundReference=false,NativeIntegerRadioOptions? integerRadioOptions=null,bool ownsState=false)
    {
        var port=state.Port;
        _state=state;_port=state.Port;_referenceOptions=references;_ownsState=ownsState;
        _formatReference=formatReference;_choiceLabels=choiceLabels;_presentation=presentation;
        _integerRadioOptions=integerRadioOptions;
        _allowUnboundReference=allowUnboundReference;
        try
        {
        if(integerRadioOptions is not null)
        {
            integerRadioOptions.ValidatePort(port);
            if(integerRadioOptions.Validate(_state.Accepted) is {} error)_state.SetEditError(error);
        }
        _state.Changed+=OnStateChanged;
        _state.ExplicitlyAccepted+=OnExplicitlyAccepted;
        Resources.MergedDictionaries.Add(EditorResources.Create());
        _error.Style=EditorResources.ErrorStyle();
        Tag=port.Id;Margin=new Thickness(0);MinHeight=32;
        _label.Text=label;_label.VerticalAlignment=VerticalAlignment.Center;_label.TextTrimming=TextTrimming.CharacterEllipsis;_label.ToolTip=label;_label.FontSize=14;
        var right=new Grid();right.ColumnDefinitions.Add(new());right.ColumnDefinitions.Add(new(){Width=GridLength.Auto});right.ColumnDefinitions.Add(new(){Width=GridLength.Auto});
        right.Children.Add(_body);Grid.SetColumn(_actions,1);right.Children.Add(_actions);Grid.SetColumn(_externalActions,2);right.Children.Add(_externalActions);
        var body=new StackPanel();body.Children.Add(right);_error.Margin=new Thickness(0,2,0,0);body.Children.Add(_error);
        _row.LeftContent=_label;_row.RightContent=body;Content=_row;
        Render();
        UpdateErrorDisplay();
        }
        catch(Exception failure)
        {
            try{Dispose();}
            catch(Exception cleanup){throw new AggregateException("字段视图构造和清理均失败。",failure,cleanup);}
            throw;
        }
    }

    private bool Set(ParameterSetting next)
    {
        if(_disposed)return false;
        if(_integerRadioOptions?.Validate(next) is {} error){_state.SetEditError(error);return false;}
        return _state.TrySet(next);
    }
    private void OnStateChanged()
    {
        if(_disposed)return;
        UpdateErrorDisplay();Changed?.Invoke();
    }
    private void OnExplicitlyAccepted()
    {
        if(_disposed)return;
        Render();UpdateErrorDisplay();
    }

    private void Render()
    {
        if(_disposed)return;
        _renderGeneration++;
        var generation=_renderGeneration;
        bool CurrentRender()=>!_disposed&&generation==_renderGeneration&&IsEnabled&&_presentationVisible;
        DetachOwnedEditors();
        _body.Children.Clear();_actions.Children.Clear();InputElement=null;_numeric=null;
        if(_imageSources is not null){RenderImageSource();return;}
        if(_integerRadioOptions is not null)
        {
            HideLabel();_row.Margin=new Thickness(0);
            _integerRadio=new NativeIntegerRadioPresenter(_state,_label.Text,_integerRadioOptions);
            InputElement=_integerRadio;_body.Children.Add(_integerRadio);return;
        }
        var numeric=_port.Type.Shape==ValueShape.Scalar&&_port.Type.Scalar is ScalarKind.Int64 or ScalarKind.Float64;
        var composedNumeric=numeric&&NativeNumberPresenter.UsesOriginalModes(_port,_presentation);
        var canReference=_referenceOptions is not null&&(_port.EditModes&FieldEditModes.Reference)!=0&&(_presentation?.Layout?.AllowReference!=false||_setting is ReferenceSetting);
        if(canReference&&!composedNumeric&&(_port.EditModes&FieldEditModes.Constant)!=0)
        {
            var mode=new ToggleButton {Content=_referenceMode?"✎":"⇄",IsChecked=_referenceMode,Width=26,Height=26,Margin=new Thickness(5,0,0,0),ToolTip=_referenceMode?"切换为数值编辑":"切换为订阅"};
            mode.SetResourceReference(StyleProperty,"EditLinkButton");
            mode.Click+=(_,_)=>{if(!CurrentRender()||!mode.IsEnabled)return;if(_referenceMode){if(!_state.TryUseConstant())return;}else _state.EnterReferenceMode();Render();};_actions.Children.Add(mode);
        }
        if(!_port.Required&&_setting is not UnsetSetting)
        {
            var clear=new Button {Content="×",Width=26,Height=26,Margin=new Thickness(5,0,0,0),ToolTip="清除可选项"};
            clear.Click+=(_,_)=>{if(CurrentRender()&&clear.IsEnabled&&Set(new UnsetSetting()))Render();};_actions.Children.Add(clear);
        }
        if(composedNumeric&&(_setting is not UnsetSetting||_referenceMode))
        {
            _numeric=new NativeNumberPresenter(_state,_presentation,_referenceOptions,_formatReference);
            InputElement=_numeric;_body.Children.Add(_numeric);return;
        }
        if(_referenceMode||_setting is ReferenceSetting||_allowUnboundReference&&_port.EditModes==FieldEditModes.Reference)
        {
            _referencePresenter=new NativeReferencePresenter(_state,_referenceOptions??ReferenceFieldOptions.Empty,_formatReference);
            _referencePresenter.PrepareForVisibility(_presentationVisible);
            InputElement=_referencePresenter;_body.Children.Add(_referencePresenter);return;
        }
        if(_setting is UnsetSetting)
        {
            _body.Children.Add(new TextBlock{Text="未设置",Opacity=.6});
            if(_port.Type.Shape!=ValueShape.Resource&&(_port.EditModes==FieldEditModes.None||(_port.EditModes&FieldEditModes.Constant)!=0))
            {
                var create=new Button{Content="设置",Height=26,ToolTip="设置此可选项"};
                create.Click+=(_,_)=>{if(CurrentRender()&&create.IsEnabled&&_state.TryUseConstant())Render();};_actions.Children.Add(create);
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
                    eye.Click+=(_,_)=>{if(!CurrentRender()||!eye.IsEnabled)return;var visible=((ConstantSetting)_setting).Value as bool?!=true;if(Set(new ConstantSetting(visible)))eye.IsChecked=visible;};
                    InputElement=eye;_body.Children.Add(eye);return;
                }
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
                    var selector=new RegionSourceModeSelector((string)value!,key=>CurrentRender()&&Set(new ConstantSetting(key)));
                    InputElement=selector;_body.Children.Add(selector);return;
                }
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
                if(numeric)
                {
                    _numeric=new NativeNumberPresenter(_state,_presentation,_referenceOptions,_formatReference);
                    InputElement=_numeric;_body.Children.Add(_numeric);
                }
                else
                {
                    var input=new TextBox{Height=30,FontSize=14,VerticalContentAlignment=VerticalAlignment.Center,Text=Convert.ToString(value,CultureInfo.InvariantCulture)??""};
                    input.TextChanged+=(_,_)=>{if(CurrentRender()&&!_updatingInput&&input.IsEnabled)Set(new ConstantSetting(input.Text));};
                    InputElement=input;_body.Children.Add(input);
                }
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

    public bool TryCommitPending()
    {
        if(IsDisposed)return false;
        if(_numeric is not null&&!_numeric.TryCommitPending())return false;
        if(_referencePresenter is not null&&!_referencePresenter.TryCommitPending())return false;
        return _children.All(child=>child.TryCommitPending());
    }

    private bool _presentationVisible=true;
    void IParameterVisibility.PrepareForVisibility(bool visible)
    {
        if(_disposed)return;
        _presentationVisible=visible;
        var cleanup=new List<Action>{()=>_numeric?.PrepareForVisibility(visible)};
        cleanup.Add(()=>_referencePresenter?.PrepareForVisibility(visible));
        cleanup.Add(()=>_integerRadio?.PrepareForVisibility(visible));
        cleanup.AddRange(_children.ToArray().Select(child=>(Action)(()=>((IParameterVisibility)child).PrepareForVisibility(visible))));
        if(!visible&&InputElement is ComboBox combo)cleanup.Add(()=>combo.SetCurrentValue(ComboBox.IsDropDownOpenProperty,false));
        ParameterCleanup.Run(cleanup);
    }

    private void DetachOwnedEditors()
    {
        var reference=_referencePresenter;var numeric=_numeric;var radio=_integerRadio;var input=InputElement;
        var children=_children.ToArray();_children.Clear();
        _referencePresenter=null;_numeric=null;_integerRadio=null;
        var cleanup=new List<Action>{DetachImageSource};
        if(reference is not null)cleanup.Add(reference.Dispose);
        if(numeric is not null)cleanup.Add(numeric.Dispose);
        if(radio is not null)cleanup.Add(radio.Dispose);
        if(input is ComboBox combo)cleanup.Add(()=>combo.SetCurrentValue(ComboBox.IsDropDownOpenProperty,false));
        if(input is NativeColorPresenter color)cleanup.Add(()=>{color.SelectedColorChanged-=OnNativeColorChanged;color.Dispose();});
        cleanup.AddRange(children.Select(child=>(Action)child.Dispose));
        ParameterCleanup.Run(cleanup);
    }

    public void Dispose()
    {
        if(_disposed)return;
        _disposed=true;_renderGeneration++;_imageSources=null;
        _state.Changed-=OnStateChanged;_state.ExplicitlyAccepted-=OnExplicitlyAccepted;Changed=null;
        var cleanup=new List<Action>();
        if(_ownsState)cleanup.Add(_state.Dispose);
        cleanup.Add(DetachOwnedEditors);
        cleanup.Add(()=>_body.Children.Clear());cleanup.Add(()=>_actions.Children.Clear());cleanup.Add(()=>_externalActions.Children.Clear());
        cleanup.Add(()=>{InputElement=null;_numeric=null;Content=null;});
        ParameterCleanup.Run(cleanup);
    }

    private sealed record ChoiceItem(string Key,string Label);

}
