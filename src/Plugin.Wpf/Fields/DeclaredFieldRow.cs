using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Adapters.Fields;
using SunEyeVision.Plugin.Wpf.Adapters.References;
using SunEyeVision.Plugin.Wpf.Adapters.Text;
using SunEyeVision.Plugin.Wpf.Presenters.Display;
using SunEyeVision.Plugin.Wpf.Presenters.Numeric;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;

namespace SunEyeVision.Plugin.Wpf.Fields;

/// <summary>
/// 编辑一条声明字段的行宿主：逻辑（状态订阅、校验、提交、引用）保留在 C#，
/// 视觉全部由原参数行 DataTemplate 提供（<see cref="ParameterRowTemplateSelector"/> 按键实例化）。
/// 模板未物化前本类只创建行视图模型，不构造任何逐字段控件树；会话类逻辑由
/// <see cref="ParameterFieldHost"/> 在模板控件 Loaded 后挂接。
/// </summary>
public sealed partial class DeclaredFieldRow : ContentControl, IDisposable, IParameterVisibility
{
    private readonly Port _port;
    private readonly DeclaredFieldState _state;
    private readonly bool _ownsState;
    private readonly ReferenceFieldOptions? _referenceOptions;
    private readonly Func<OutputReference,string>? _formatReference;
    private readonly IReadOnlyDictionary<string,string>? _choiceLabels;
    private readonly DeclaredChoiceDisplayOptions? _choiceDisplay;
    private string? ChoiceError => _choiceDisplay?.ValidateProposal(_state.Accepted);
    private ParameterSetting _setting => _state.Accepted;
    private readonly FieldPresentation? _presentation;
    private bool _referenceMode => _state.IsReferenceMode;
    private TextExpressionOptions? _textExpressions;
    private readonly NativeIntegerRadioOptions? _integerRadioOptions;
    private bool _updatingInput;
    private bool _inlineConstantReference;
    private readonly bool _numericSequence;
    private bool _disposed;
    private string _editError => _state.EditError;
    private bool _regionSourceSelector;
    private readonly bool _allowUnboundReference;
    private readonly bool _composedNumeric;
    private readonly bool _canReference;
    private readonly FieldRowModel? _model;
    private readonly ParameterFieldHost _host;
    private readonly List<DeclaredFieldRow> _children = new();
    private UIElement? _slotContent;
    private bool _presentationVisible = true;
    private double? _labelWidth;

    internal DeclaredFieldState StateForHost => _state;
    internal FieldPresentation? Presentation => _presentation;
    internal ReferenceFieldOptions? ReferenceOptionsForHost => _referenceOptions;
    internal Func<OutputReference,string>? FormatReferenceForHost => _formatReference;
    internal TextExpressionOptions? TextExpressionOptionsForHost => _textExpressions;
    internal bool InlineConstantReference => _inlineConstantReference;
    internal bool NumericSequence => _numericSequence;

    internal void SetSubData(object? value) => _model?.SetSubData(value);

    public string FieldId => _port.Id;
    internal string TemplateKey => _model?.TemplateKey ?? "Slot";
    /// <summary>主输入元素（数值文本框/文本框/订阅删除框/枚举下拉）。</summary>
    public FrameworkElement? InputElement => _host.InputElement;
    public ParameterSetting Setting => _setting;
    internal bool IsDisposed => _disposed || _state.IsDisposed;
    internal bool HasNumericModel => _state.NumericModel is not null;
    internal long NumericEditVersion => _host.NumericEditVersion;
    public bool HasInvalidText => _state.HasInvalidInput || ChoiceError is not null || _children.Any(child => child.HasInvalidText);
    public bool HasPendingEdits => !_disposed && (HasInvalidText || _host.HasPendingEdits || _children.Any(child => child.HasPendingEdits));
    public string Error => _state.Error.Length != 0 ? _state.Error : ChoiceError ?? _children.FirstOrDefault(child => child.HasInvalidText)?.Error ?? "";
    internal NativeParameterItemState NativeItemState => NativeParameterRow.ProjectState(new[] {_state},
        ChoiceError is not null || _children.Any(child => child.NativeItemState != NativeParameterItemState.Normal));
    public event Action? Changed;

    public DeclaredFieldRow(Port port, string label, ParameterSetting initial, Action<ParameterSetting> write,
        ReferenceFieldOptions? references = null, ConstantSetting? constantBackup = null,
        Func<OutputReference,string>? formatReference = null, IReadOnlyDictionary<string,string>? choiceLabels = null,
        FieldPresentation? presentation = null, bool allowUnboundReference = false,
        NativeIntegerRadioOptions? integerRadioOptions = null, DeclaredChoiceDisplayOptions? choiceDisplay = null,
        bool numericSequence = false)
        : this(new DeclaredFieldState(port, initial, write, constantBackup, allowUnboundReference,
            proposal => choiceDisplay?.ValidateProposal(proposal) ?? integerRadioOptions?.Validate(proposal) ?? references?.ValidateProposal?.Invoke(port, proposal),
            readCurrentValue: references?.ReadCurrentValue is {} read ? () => read(port) : null,
            referenceBehavior: references?.ReadBehavior?.Invoke(port) ?? ParameterReferenceBehavior.StrictExactlyOne),
            label, references, formatReference, choiceLabels, presentation, allowUnboundReference,
            integerRadioOptions, ownsState: true, choiceDisplay: choiceDisplay, numericSequence: numericSequence)
    { }

    internal DeclaredFieldRow(DeclaredFieldState state, string label,
        ReferenceFieldOptions? references = null, Func<OutputReference,string>? formatReference = null,
        IReadOnlyDictionary<string,string>? choiceLabels = null, FieldPresentation? presentation = null,
        bool allowUnboundReference = false, NativeIntegerRadioOptions? integerRadioOptions = null, bool ownsState = false,
        bool inlineConstantReference = false, DeclaredChoiceDisplayOptions? choiceDisplay = null, bool numericSequence = false)
    {
        var port = state.Port;
        _state = state; _port = state.Port; _referenceOptions = references; _ownsState = ownsState;
        _formatReference = formatReference; _choiceLabels = choiceLabels; _presentation = presentation;
        _choiceDisplay = choiceDisplay; _integerRadioOptions = integerRadioOptions;
        _allowUnboundReference = allowUnboundReference;
        _numericSequence = numericSequence;
        _inlineConstantReference = inlineConstantReference;
        if (numericSequence && !NativeNumericSequenceProjection.Applies(port))
            throw new ArgumentException("原数值序列投影需要正式浮点序列声明。");
        try
        {
            _choiceDisplay?.ValidatePort(port);
            if (_choiceDisplay is not null && (integerRadioOptions is not null || inlineConstantReference))
                throw new ArgumentException("枚举展示与其他字段组合冲突。");
            if (inlineConstantReference)
            {
                if (port.Type.Shape != ValueShape.Scalar || port.Type.Scalar is not (ScalarKind.Int64 or ScalarKind.Float64)
                    || port.EditModes is not (FieldEditModes.Constant or (FieldEditModes.Constant|FieldEditModes.Reference)))
                    throw new ArgumentException("内联常量需要数值常量或常量/引用字段。");
            }

            if (integerRadioOptions is not null)
            {
                integerRadioOptions.ValidatePort(port);
                if (integerRadioOptions.Validate(_state.Accepted) is {} error)
                    _state.SetEditError(error);
            }

            if (!inlineConstantReference && integerRadioOptions is null && port.Choices is null
                && (numericSequence || port.EditModes != FieldEditModes.Reference && port.Type.Shape == ValueShape.Scalar && port.Type.Scalar is ScalarKind.Int64 or ScalarKind.Float64))
                _state.EnableNumericModel(presentation?.Layout);

            var composed = numericSequence || port.EditModes != FieldEditModes.Reference
                && port.Type.Shape == ValueShape.Scalar && port.Type.Scalar is ScalarKind.Int64 or ScalarKind.Float64;
            _composedNumeric = composed && DeclaredFieldTemplateRegistry.UsesOriginalModes(port, presentation, numericSequence);
            _canReference = references is not null && (port.EditModes & FieldEditModes.Reference) != 0
                && (presentation?.Layout?.AllowReference != false || _setting is (ReferenceSetting or VariableSetting));

            var key = ResolveRowTemplateKey(port, presentation, numericSequence, integerRadioOptions is not null, inlineConstantReference);
            _label = label;
            _model = BuildModel(key, label);
            _host = new ParameterFieldHost(this, _model.TemplateKey, _canReference, numericSequence);

            Tag = port.Id;
            Margin = new Thickness(0);
            MinHeight = 32;
            _slotContent = BuildSlotContent(label);
            if (_slotContent is not null)
            {
                Content = _slotContent;
            }
            else
            {
                Content = _model;
                ContentTemplateSelector = ParameterRowTemplateSelector.Instance;
                Loaded += ApplyLabelWidthWhenRealized;
            }
            _state.Changed += OnStateChanged;
            _state.ExplicitlyAccepted += OnExplicitlyAccepted;
            ProjectTemplate();
        }
        catch (Exception failure)
        {
            try
            {
                Dispose();
            }
            catch (Exception cleanup)
            {
                throw new AggregateException("字段行构造和清理均失败。", failure, cleanup);
            }

            throw;
        }
    }

    /// <summary>分类入口给模板键；没有展示声明的字段按端口类型就地归类。</summary>
    private static string ResolveRowTemplateKey(Port port, FieldPresentation? presentation, bool numericSequence,
        bool hasIntegerRadio, bool inlineConstantReference)
    {
        if (presentation is not null)
        {
            return DeclaredFieldTemplateRegistry.ResolveTemplateKey(port, presentation,
                numericSequence: numericSequence, hasIntegerRadio: hasIntegerRadio,
                inlineConstantReference: inlineConstantReference, isDialogPrimary: presentation.Layout?.DialogPrimary == true);
        }

        if (hasIntegerRadio)
            return DeclaredFieldTemplateRegistry.IntegerRadio;
        if (inlineConstantReference)
            return DeclaredFieldTemplateRegistry.Reference;
        return port.Type.Shape switch
        {
            ValueShape.Scalar => port.Type.Scalar switch
            {
                ScalarKind.Boolean => DeclaredFieldTemplateRegistry.Boolean,
                ScalarKind.Int64 => DeclaredFieldTemplateRegistry.Integer,
                ScalarKind.Float64 => DeclaredFieldTemplateRegistry.NumericPlain,
                ScalarKind.Text => DeclaredFieldTemplateRegistry.String,
                _ => throw new NotSupportedException($"未模板化的标量类型不支持行内模板：{port.Id}（{port.Type}）")
            },
            _ => throw new NotSupportedException($"未模板化的字段类型不支持行内模板：{port.Id}（{port.Type}）")
        };
    }

    private FieldRowModel BuildModel(string key, string label)
    {
        var layout = _presentation?.Layout;
        double minimum = -double.MaxValue, maximum = double.MaxValue, increment = 1;
        int? decimals = null;
        double radioWidth = 0;
        List<FieldRowModel.Entry> entries = new();
        var hasReferenceBranch = false;
        switch (key)
        {
            case DeclaredFieldTemplateRegistry.Integer:
            case DeclaredFieldTemplateRegistry.Float:
                if (layout is {Minimum: not null, Maximum: not null})
                {
                    minimum = layout.Minimum.Value;
                    maximum = layout.Maximum.Value;
                }
                else if (_numericSequence)
                {
                    minimum = -double.MaxValue;
                    maximum = double.MaxValue;
                }
                else
                {
                    var range = NumericStorageRules.IntegerRange(NumericStorageRules.Effective(_port.Type, _port.Storage)!.Value);
                    minimum = range.Minimum;
                    maximum = range.Maximum;
                }

                increment = layout?.Increment ?? 1;
                if (key == DeclaredFieldTemplateRegistry.Float)
                    decimals = layout?.DecimalPlaces ?? 2;
                break;
            case DeclaredFieldTemplateRegistry.NumericPlain:
                minimum = layout?.Minimum ?? -double.MaxValue;
                maximum = layout?.Maximum ?? double.MaxValue;
                increment = layout?.Increment ?? 1;
                break;
            case DeclaredFieldTemplateRegistry.IntegerRadio:
                radioWidth = 110;
                entries = _integerRadioOptions!.Choices.Select(choice => new FieldRowModel.Entry(choice.Label, (int)choice.Value)).ToList();
                break;
            case DeclaredFieldTemplateRegistry.Enumeration:
                entries = BuildChoiceEntries();
                break;
            case DeclaredFieldTemplateRegistry.String:
            case DeclaredFieldTemplateRegistry.Reference:
            case DeclaredFieldTemplateRegistry.Boolean:
            case DeclaredFieldTemplateRegistry.BooleanEye:
                hasReferenceBranch = key != DeclaredFieldTemplateRegistry.Boolean && key != DeclaredFieldTemplateRegistry.BooleanEye;
                break;
        }

        var model = new FieldRowModel(this, key, label, minimum, maximum, increment, decimals, radioWidth, entries, hasReferenceBranch);
        if (key == DeclaredFieldTemplateRegistry.IntegerRadio && _integerRadioOptions!.UseFiveSlotTemplate)
            model.TemplateKey = "RadioSelecter3";
        return model;
    }

    private List<FieldRowModel.Entry> BuildChoiceEntries()
    {
        var choices = _port.Choices!;
        if (_choiceDisplay is {} display)
        {
            return display.Items.Select(item => new FieldRowModel.Entry(item.Label, item.FormalKey, item.IsAvailable, item.UnavailableReason)).ToList();
        }

        return choices.Keys.Select(key => new FieldRowModel.Entry(_choiceLabels?.GetValueOrDefault(key, key) ?? key, key)).ToList();
    }

    // ---- 组合槽（卡外字段族：图像来源、区域来源、记录/颜色、资源占位） ----
    /// <summary>没有对应行模板族、且工具已显式给出组合控件的字段，沿用原组合槽（单一控件，不逐字段造树）。</summary>
    private UIElement? BuildSlotContent(string label)
    {
        if (_imageSources is not null)
            return CreateImageSourceSlot();
        if (_port.Type.Shape == ValueShape.Resource)
            return new TextBlock {Text = "运行时资源，请选择来源", TextWrapping = TextWrapping.Wrap};
        if (_setting is UnsetSetting)
        {
            var panel = new Grid();
            panel.Children.Add(new TextBlock {Text = "未设置", Opacity = .6, VerticalAlignment = VerticalAlignment.Center});
            return panel;
        }

        var value = _setting switch
        {
            ConstantSetting constant => constant.Value,
            DisplayExpressionSetting expression when _port.Type == DataType.Text && (_port.EditModes & FieldEditModes.DisplayExpression) != 0 => expression.Text,
            _ => throw new ArgumentException("字段设置与原编辑器声明不匹配：" + _port.Id)
        };
        if (_port.Type.Shape == ValueShape.Resource)
            return new TextBlock {Text = "运行时资源，请选择来源", TextWrapping = TextWrapping.Wrap};
        if (_regionSourceSelector && _port.Choices is not null)
        {
            return new RegionSourceModeSelector((string)value!, key => CurrentSlotGate && Set(new ConstantSetting(key)));
        }

        if (_port.Type.Shape == ValueShape.Record && value is RecordValue record)
        {
            if (_port.Type.Equals(PresentationDeclarations.Rgba))
            {
                var input = new NativeColorPresenter {Height = 32, SelectedColor = Color.FromArgb(
                    checked((byte)record.Get<long>("a")), checked((byte)record.Get<long>("r")),
                    checked((byte)record.Get<long>("g")), checked((byte)record.Get<long>("b")))};
                input.SelectedColorChanged += OnNativeColorChanged;
                return input;
            }

            var stack = new StackPanel();
            foreach (var field in _port.Type.Fields)
            {
                var child = new DeclaredFieldRow(field, field.Id,
                    record.Contains(field.Id) ? new ConstantSetting(record.Get<object?>(field.Id)) : new UnsetSetting(), next =>
                    {
                        var current = (RecordValue)((ConstantSetting)_setting).Value!;
                        var values = current.Declaration.Fields.Where(p => current.Contains(p.Id)).ToDictionary(p => p.Id, p => current.Get<object?>(p.Id));
                        if (next is UnsetSetting)
                            values.Remove(field.Id);
                        else
                            values[field.Id] = ((ConstantSetting)next).Value;
                        if (!Set(new ConstantSetting(new RecordValue(_port.Type, values))))
                            throw new ArgumentException(Error);
                    });
                child.Margin = new Thickness(10, 2, 0, 2);
                _children.Add(child);
                stack.Children.Add(child);
            }

            return stack;
        }

        if (_port.Type.Shape is ValueShape.Geometry or ValueShape.Sequence)
            return new TextBlock {Text = "此字段由专用区域或集合编辑器处理。", TextWrapping = TextWrapping.Wrap};
        return null;
    }

    private bool CurrentSlotGate => !_disposed && IsEnabled && _presentationVisible;

    private void OnNativeColorChanged(object sender, RoutedPropertyChangedEventArgs<Color> e)
    {
        if (_disposed || _updatingInput || sender is not NativeColorPresenter input || !ReferenceEquals(input, _slotContent))
            return;
        if (CurrentSlotGate && IsLoaded && IsVisible && Set(new ConstantSetting(PresentationDeclarations.Color(e.NewValue.R, e.NewValue.G, e.NewValue.B, e.NewValue.A))))
            return;
        var accepted = (RecordValue)((ConstantSetting)_setting).Value!;
        _updatingInput = true;
        try
        {
            input.SetCurrentValue(NativeColorPresenter.SelectedColorProperty, Color.FromArgb(
                checked((byte)accepted.Get<long>("a")), checked((byte)accepted.Get<long>("r")),
                checked((byte)accepted.Get<long>("g")), checked((byte)accepted.Get<long>("b"))));
        }
        finally
        {
            _updatingInput = false;
        }
    }

    private void ApplyLabelWidthWhenRealized(object sender, RoutedEventArgs args)
    {
        if (_labelWidth is not {} width)
            return;
        var body = FindDescendant<Native.Composition.ParameterPresentation.ParamTab.LeftRightControl>(this);
        body?.SetCurrentValue(Native.Composition.ParameterPresentation.ParamTab.LeftRightControl.ChildLeftWidthProperty, width);
        Loaded -= ApplyLabelWidthWhenRealized;
    }

    private static T? FindDescendant<T>(DependencyObject root) where T : DependencyObject
    {
        if (root is T match)
            return match;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            if (FindDescendant<T>(VisualTreeHelper.GetChild(root, i)) is {} child)
                return child;
        return null;
    }

    // ---- 状态投影（模板动态绑定） ----
    private bool CurrentTemplateGate => !_disposed && !_updatingInput && IsEnabled && _presentationVisible && IsLoaded && IsVisible;

    private void OnStateChanged()
    {
        if (_disposed)
            return;
        ProjectTemplate();
        Changed?.Invoke();
    }

    private void OnExplicitlyAccepted()
    {
        if (_disposed)
            return;
        ProjectTemplate();
    }

    private void ProjectTemplate()
    {
        if (_disposed || _model is null)
            return;
        _updatingInput = true;
        try
        {
            var unset = _setting is UnsetSetting;
            _model.SetIsSub(_referenceMode);
            _model.SetSubscribeVisible(_canReference && !_composedNumeric && (_port.EditModes & FieldEditModes.Constant) != 0);
            _model.SetClearVisible(!_numericSequence && !_port.Required && !unset && _port.EditModes != FieldEditModes.Reference);
            _model.SetUnset(unset,
                unset && _port.Type.Shape != ValueShape.Resource
                && (_port.EditModes == FieldEditModes.None || (_port.EditModes & FieldEditModes.Constant) != 0));
            switch (_model.TemplateKey)
            {
                case DeclaredFieldTemplateRegistry.Boolean:
                case DeclaredFieldTemplateRegistry.BooleanEye:
                    _model.SetCurValue(_setting is ConstantSetting {Value: bool accepted} && accepted);
                    break;
                case DeclaredFieldTemplateRegistry.Enumeration:
                    var current = (_setting as ConstantSetting)?.Value as string;
                    _model.SetCurValue(current);
                    if (current is not null && !_port.Choices!.Keys.Contains(current))
                        _state.SetEditError("当前枚举值不在声明选项中，请重新选择。");
                    break;
                case "RadioSelecter" or "RadioSelecter3" when _integerRadioOptions is not null:
                    _model.SetCurValue(_setting is ConstantSetting {Value: long radio}
                        && _integerRadioOptions.Choices.Any(choice => choice.Value == radio) ? (int)radio : null);
                    break;
            }
        }
        finally
        {
            _updatingInput = false;
        }
    }

    // ---- 模板命令回调 ----
    internal void HandleTemplateValueProposal(object? proposed)
    {
        if (_model is not null && proposed is null
            && _model.TemplateKey is DeclaredFieldTemplateRegistry.Boolean or DeclaredFieldTemplateRegistry.BooleanEye)
            return;
        if (_model is null || !CurrentTemplateGate)
        {
            RestoreTemplateProjection();
            return;
        }

        try
        {
            switch (proposed)
            {
                case bool boolean when _model.TemplateKey is DeclaredFieldTemplateRegistry.Boolean or DeclaredFieldTemplateRegistry.BooleanEye:
                    if (_setting is ConstantSetting {Value: bool accepted} && boolean == accepted && _editError.Length == 0)
                        return;
                    if (Set(new ConstantSetting(boolean)))
                        return;
                    break;
                case string key when _model.TemplateKey == DeclaredFieldTemplateRegistry.Enumeration:
                    if (_choiceDisplay is {} display)
                    {
                        var entry = display.Items.FirstOrDefault(item => Equals(item.FormalKey, key));
                        var error = entry is null ? "选项展示快照已经失效。" : !entry.IsAvailable ? entry.UnavailableReason : null;
                        if (error is not null)
                        {
                            _state.SetEditError(error);
                            break;
                        }
                    }

                    if (_setting is ConstantSetting {Value: string acceptedKey} && acceptedKey == key && _editError.Length == 0)
                        return;
                    Set(new ConstantSetting(key));
                    break;
                case int radio when _model.TemplateKey is "RadioSelecter" or "RadioSelecter3":
                    if (Set(new ConstantSetting((long)radio)))
                        return;
                    break;
                default:
                    throw new InvalidOperationException($"模板值提案与行模板不匹配（字段 {_port.Id}）：{proposed?.GetType().Name ?? "null"}");
            }
        }
        finally
        {
            RestoreTemplateProjection();
        }
    }

    internal void HandleTypeChange()
    {
        if (!CurrentTemplateGate)
        {
            RestoreTemplateProjection();
            return;
        }

        try
        {
            if (_referenceMode)
            {
                _state.TryUseConstant();
            }
            else
            {
                _state.EnterReferenceMode();
            }
        }
        finally
        {
            RestoreTemplateProjection();
        }
    }

    internal void HandleUnsetExit()
    {
        if (!CurrentTemplateGate)
            return;
        try
        {
            _state.TryUseConstant();
        }
        finally
        {
            RestoreTemplateProjection();
        }
    }

    internal void HandleClear()
    {
        if (!CurrentTemplateGate)
            return;
        try
        {
            Set(new UnsetSetting());
        }
        finally
        {
            RestoreTemplateProjection();
        }
    }

    private void RestoreTemplateProjection()
    {
        if (_disposed)
            return;
        ProjectTemplate();
    }

    private bool Set(ParameterSetting next)
    {
        if (_disposed)
            return false;
        if (_choiceDisplay?.ValidateProposal(next) is {} choiceError)
        {
            _state.SetEditError(choiceError);
            return false;
        }

        if (_integerRadioOptions?.Validate(next) is {} error)
        {
            _state.SetEditError(error);
            return false;
        }

        return _state.TrySet(next);
    }

    // ---- 公开窄 API（对齐原编辑器消费者） ----
    private string _label;
    public void SetLabel(string text) => _model?.SetDisplayName(text);

    /// <summary>为文本字段启用显示表达式会话（必须在行 Loaded 前调用）。</summary>
    public void UseTextExpressions(TextExpressionOptions options)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_textExpressions is not null)
            throw new InvalidOperationException("文本表达式已配置。");
        _textExpressions = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>把枚举字段切换为区域来源模式选择器组合槽（卡外组合，沿用原控件）。</summary>
    public void UseRegionSourceSelector()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_imageSources is not null)
            throw new InvalidOperationException("图像来源与区域来源选择互斥。");
        _regionSourceSelector = true;
        var slot = BuildSlotContent(_label);
        if (slot is null)
            throw new InvalidOperationException("区域来源选择需要枚举字段且当前设置可转换为选择值。");
        _slotContent = slot;
        Content = slot;
        ContentTemplateSelector = null;
    }
    internal void SetLabelWidth(double width) => _labelWidth = width;
    internal bool TryApplySetting(ParameterSetting setting) => Set(setting) && !IsDisposed;
    public bool TrySetConstant(object value)
    {
        if (_host.TrySetConstant(value))
            return true;
        return false;
    }

    public bool TryCommitPending()
    {
        if (IsDisposed || ChoiceError is not null)
            return false;
        if (_imageSources is not null && _state.HasInvalidInput)
            return false;
        if (!_host.TryCommitPending())
            return false;
        return _children.All(child => child.TryCommitPending());
    }

    /// <summary>Retire transient interactions without accepting or discarding pending input.</summary>
    public void RetireInteractions()
    {
        if (_disposed)
            return;
        var resume = _presentationVisible && IsVisible;
        ParameterCleanup.Run(new Action[]
        {
            () => ((IParameterVisibility)this).PrepareForVisibility(false),
            () => { if (resume && !_disposed && IsVisible) ((IParameterVisibility)this).PrepareForVisibility(true); }
        });
    }

    internal void RestoreUiProjectionRaw(string raw)
    {
        if (_disposed)
            return;
        _host.RestoreUiProjectionRaw(new NumericPendingEdit(raw, _state.AcceptanceVersion));
        PrepareForVisibilityCore(_presentationVisible && IsVisible);
    }

    internal NumericModelReadResult ReadNumericModel(Func<bool> current, Func<ConstantSetting?>? readCurrentValue = null)
        => _state.ReadNumericModel(() => !_disposed && current(), readCurrentValue);

    void IParameterVisibility.PrepareForVisibility(bool visible)
    {
        if (_disposed)
            return;
        PrepareForVisibilityCore(visible);
    }

    private void PrepareForVisibilityCore(bool visible)
    {
        _presentationVisible = visible;
        var cleanup = new List<Action>
        {
            () => _host.PrepareForVisibility(visible)
        };
        cleanup.AddRange(_children.ToArray().Select(child => (Action)(() => ((IParameterVisibility)child).PrepareForVisibility(visible))));
        if (!visible && _slotContent is ComboBox combo)
            cleanup.Add(() => combo.SetCurrentValue(ComboBox.IsDropDownOpenProperty, false));
        if (!visible && _slotContent is NativeColorPresenter color)
            cleanup.Add(color.ClosePalette);
        ParameterCleanup.Run(cleanup);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _imageSources = null;
        _state.Changed -= OnStateChanged;
        _state.ExplicitlyAccepted -= OnExplicitlyAccepted;
        Changed = null;
        ParameterCleanup.Run(new Action[]
        {
            () => _children.ToArray().Select(child => (Action)child.Dispose).ToList().ForEach(action => action()),
            _host.Dispose,
            DetachImageSource,
            () =>
            {
                if (_ownsState)
                    _state.Dispose();
            },
            () => { _children.Clear(); _slotContent = null; Content = null; _model?.DisposeProjection(); }
        });
    }

    private sealed record ChoiceItem(string Key, string Label);
}
