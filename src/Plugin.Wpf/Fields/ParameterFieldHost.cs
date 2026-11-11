using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Adapters.Fields;
using SunEyeVision.Plugin.Wpf.Adapters.References;
using SunEyeVision.Plugin.Wpf.Adapters.Text;
using SunEyeVision.Plugin.Wpf.Presenters.Input;
using SunEyeVision.Plugin.Wpf.Presenters.Numeric;
using SunEyeVision.Plugin.Wpf.Presenters.References;
using DeleteTextBox=SunEyeVision.Plugin.Wpf.Controls.References.Primitives.DeleteTextBox;
using NativePopup=SunEyeVision.Plugin.Wpf.Controls.Popups.PopupButton;
using NativeText=SunEyeVision.Plugin.Wpf.Controls.References.Primitives.DeleteTextBox;
using Numbers=SunEyeVision.Plugin.Wpf.Native.Numeric.Interaction.ListView;

namespace SunEyeVision.Plugin.Wpf.Fields;

/// <summary>
/// 模板声明控件与现成事务逻辑的接缝：数值交给 <see cref="NativeNumberCoordinator"/>，
/// 文本交给 <see cref="NativeTextSession"/>，订阅交给 <see cref="ReferenceFieldSession"/>。
/// 视觉全部来自行模板（ButtonSelecter_sub/StringConfig_sub/SliderArrange），这里只创建会话、
/// 传递事件、投影会话对象，不构造任何逐字段控件树。
/// </summary>
internal sealed class ParameterFieldHost : IDisposable
{
    private readonly DeclaredFieldRow _row;
    private readonly DeclaredFieldState _state;
    private readonly FieldPresentation? _presentation;
    private readonly ReferenceFieldOptions? _references;
    private readonly Func<OutputReference,string>? _formatReference;
    private readonly string _templateKey;
    private readonly bool _numericSequence;
    private readonly bool _canReference;
    private readonly bool _isTextKind;
    private readonly bool _isReferenceKind;
    private readonly bool _isPlainNumericKind;

    // 数值
    private NativeNumberCoordinator? _numeric;
    // 文本
    private NativeTextSession? _textSession;
    private NativePopup? _textPopup;
    private NativeText? _textInput;
    private Window? _owner;
    private DispatcherOperation? _pending;
    private bool _disposed;
    private bool _hidden;
    private bool _closing;
    private bool _listening;
    private bool _attaching;
    private long _generation;
    private long _loadGeneration;
    // 订阅
    private ReferenceFieldSession? _referenceSession;
    private string? _referenceSummary;
    internal void SetReferenceSummary(string? summary)
    {
        _referenceSummary = summary;
        _referenceSession?.SetReadOnlySummary(summary);
    }
    private ReferenceExpansionCoordinator? _expansion;
    private ReferencePopupScope? _scope;
    private DeleteTextBox? _deleteText;
    // 枚举/图像来源组合槽
    private ComboBox? _combo;

    internal ParameterFieldHost(DeclaredFieldRow row, string templateKey, bool canReference, bool numericSequence)
    {
        _row = row;
        _state = row.StateForHost;
        _presentation = row.Presentation;
        _references = row.ReferenceOptionsForHost;
        _formatReference = row.FormatReferenceForHost;
        _templateKey = templateKey;
        _numericSequence = numericSequence;
        _canReference = canReference;
        _isTextKind = templateKey is DeclaredFieldTemplateRegistry.String or DeclaredFieldTemplateRegistry.FolderPath;
        _isReferenceKind = templateKey == DeclaredFieldTemplateRegistry.Reference;
        _isPlainNumericKind = templateKey == DeclaredFieldTemplateRegistry.NumericPlain;
        row.Loaded += OnRowLoaded;
        row.Unloaded += OnRowUnloaded;
        row.LayoutUpdated += OnLayout;
        ((UIElement)row).IsVisibleChanged += OnVisibility;
        row.IsEnabledChanged += OnEnabled;
        row.PreviewKeyDown += OnKey;
        row.LostKeyboardFocus += OnLostFocus;
    }

    /// <summary>主输入元素（数值文本框 / 文本框 / 订阅删除框 / 枚举下拉）。</summary>
    public FrameworkElement? InputElement => _referenceMode && _deleteText is not null ? _deleteText
        : _numeric?.TextEditor ?? (FrameworkElement?)_textInput ?? (FrameworkElement?)_deleteText ?? (FrameworkElement?)_combo;

    internal ComboBox? EnumerationCombo => _combo;
    internal void AttachEnumeration(ComboBox combo)
    {
        _combo = combo;
    }

    internal long NumericEditVersion => _numeric?.EditVersion ?? 0;

    internal void RestoreUiProjectionRaw(NumericPendingEdit raw)
    {
        if (_disposed || _numeric is null)
            return;
        _numeric.RestorePending(raw);
        _numeric.PrepareForVisibility(_hidden ? false : _row is {IsVisible: true, IsEnabled: true, IsLoaded: true});
    }

    internal bool HasPendingEdits => !_disposed
        && (_numeric?.HasPendingEdits == true
            || (_textSession?.HasPendingEdits == true && !_referenceMode)
            || (_referenceSession?.HasPendingText == true && _referenceMode));

    private bool _referenceMode => _state.IsReferenceMode;

    public bool TryCommitPending()
    {
        if (_disposed)
            return false;
        if (_numeric is not null && !_numeric.TryCommitPending())
            return false;
        if (_referenceMode && _referenceSession is not null && !_referenceSession.TryCommitPending())
            return false;
        if (!_referenceMode && _textSession is not null && !_textSession.TryCommitPending())
            return false;
        return true;
    }

    public bool TrySetConstant(object value)
    {
        if (_disposed)
            return false;
        if (_numeric is not null)
            return _numeric.TrySetConstant(value);
        if (_textSession is not null && !_referenceMode)
            return _textSession.TrySetConstant(value);
        return _state.TrySet(new ConstantSetting(value));
    }

    /// <summary>暂存/恢复交互（会话挂起、弹层关闭），沿用原编辑器 PrepareForVisibility 语义。</summary>
    public void PrepareForVisibility(bool visible)
    {
        if (_disposed)
            return;
        _hidden = !visible;
        var cleanup = new List<Action>
        {
            () => _numeric?.PrepareForVisibility(visible),
            () => _scope?.PrepareForVisibility(visible)
        };
        if (_textSession is not null)
        {
            cleanup.Add(() => _textSession.SetSuspended(!visible || !_row.IsVisible || !_row.IsEnabled || !_row.IsLoaded));
            if (!visible)
                cleanup.Add(CloseTextPopup);
        }

        if (!visible && _combo is not null)
            cleanup.Add(() => _combo.SetCurrentValue(ComboBox.IsDropDownOpenProperty, false));
        ParameterCleanup.Run(cleanup);
    }

    // ---- 行生命周期 ----
    private void OnRowLoaded(object sender, RoutedEventArgs args)
    {
        if (_disposed)
            return;
        AttachSessions();
        ++_loadGeneration;
        AttachTextInputs();
        RefreshReferenceInput();
        if (!_listening)
        {
            InputManager.Current.PreProcessInput += OnInput;
            _listening = true;
        }

        if (_owner is not null)
            _owner.Deactivated -= OnDeactivate;
        _owner = Window.GetWindow(_row);
        if (_owner is not null)
            _owner.Deactivated += OnDeactivate;
        _textSession?.SetSuspended(_hidden || !_row.IsLoaded || !_row.IsVisible || !_row.IsEnabled);
        _scope?.PrepareForVisibility(!_hidden);
    }

    private void OnRowUnloaded(object sender, RoutedEventArgs args)
    {
        if (_disposed)
            return;
        var generation = ++_loadGeneration;
        var owner = _owner;
        _owner = null;
        bool StillUnloaded() => !_disposed && generation == _loadGeneration && !_row.IsLoaded;
        ParameterCleanup.Run(new Action[]
        {
            () => { if (owner is not null) owner.Deactivated -= OnDeactivate; },
            StopListening,
            () => { if (StillUnloaded()) { _textSession?.SetSuspended(true); _scope?.PrepareForVisibility(!_hidden); } },
            () => { if (StillUnloaded()) CloseTextPopup(); }
        });
    }

    private void AttachSessions()
    {
        if (_disposed)
            return;
        // 数值：模板已实例化出 SliderArrange/NumericControl，交给现成协调器。
        if (_numeric is null
            && (_templateKey == DeclaredFieldTemplateRegistry.Integer
                || _templateKey == DeclaredFieldTemplateRegistry.Float
                || _isPlainNumericKind))
        {
            var input = FindVisual<FrameworkElement>(_row, element
                => element is Numbers.SliderArrangeBase or Numbers.NumericControl);
            if (input is not null)
            {
                input.Language = XmlLanguage.GetLanguage("en-US");
                try
                {
                    _numeric = new NativeNumberCoordinator(_state, _presentation, _row, input, _references, _formatReference);
                }
                catch (Exception failure)
                {
                    throw new InvalidOperationException($"数值协调器挂接到模板控件失败（字段 {_row.FieldId}）。", failure);
                }
            }
        }

        // 订阅会话：引用行常驻；默认数值/文本行创建（引用分支模板按 IsSubA 显隐）。
        if (_referenceSession is null && !_row.UsesImageSourceSelector
            && (_isReferenceKind || (_canReference && (_isPlainNumericKind || _isTextKind))))
        {
            _expansion = new ReferenceExpansionCoordinator(_row, _references?.WindowOwner);
            _referenceSession = new ReferenceFieldSession(_state, _references ?? ReferenceFieldOptions.Empty, _formatReference,
                expand: _expansion.Open, preference: _expansion.Preference,
                allowConstantText: _isReferenceKind && _row.InlineConstantReference,
                selectVariable: _expansion.OpenVariable, isExpanded: _expansion.IsExpanded);
            _referenceSession.SetReadOnlySummary(_referenceSummary);
            _scope = new ReferencePopupScope(_row, _referenceSession,
                () => !_state.IsDisposed && (_isReferenceKind || _state.IsReferenceMode));
            _row.SetReferenceData(_referenceSession);
        }

        // Each template branch receives its own session over the same field.
        if (_textSession is null && _isTextKind)
        {
            _textSession = new NativeTextSession(_state, _row.TextExpressionOptionsForHost,
                () => !_disposed && (_textInput is null
                    || Visuals<NativeText>(_row).Any(text => ReferenceEquals(text, _textInput))));
            _textSession.ProjectInsertionRequested += ProjectInsertion;
            _row.SetTextData(_textSession);
            AttachTextInputs();
        }
    }

    private void OnLayout(object? sender, EventArgs args)
    {
        if (_disposed)
            return;
        RefreshReferenceInput();
        if (_textInput is null || !Visuals<NativeText>(_row).Any(text => ReferenceEquals(text, _textInput)))
            AttachTextInputs();
    }

    private void AttachTextInputs()
    {
        if (_disposed || _attaching || _textSession is null)
            return;
        _attaching = true;
        try
        {
            _row.ApplyTemplate();
            _row.UpdateLayout();
            var popup = Visuals<NativePopup>(_row).FirstOrDefault(item => ReferenceEquals(item.DataContext, _textSession));
            if (popup is null)
            {
                _textPopup = null;
                _textInput = Visuals<NativeText>(_row).FirstOrDefault(item => ReferenceEquals(item.DataContext, _textSession));
                _textSession.AttachText(_textInput);
                return;
            }

            popup.ApplyTemplate();
            foreach (var toggle in Visuals<ToggleButton>(popup).ToArray())
                toggle.ApplyTemplate();
            var text = Visuals<NativeText>(popup).FirstOrDefault();
            _textPopup = popup;
            _textInput = text;
            _textSession.AttachText(text);
        }
        finally
        {
            _attaching = false;
        }
    }

    // ---- 文本弹层粘合（对齐原 NativeTextPresenter 行为） ----
    private static IEnumerable<T> Visuals<T>(DependencyObject root) where T : DependencyObject
    {
        if (root is T value)
            yield return value;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            foreach (var child in Visuals<T>(VisualTreeHelper.GetChild(root, i)))
                yield return child;
    }

    private static T? FindVisual<T>(DependencyObject root, Func<DependencyObject, bool> predicate) where T : DependencyObject
    {
        if (root is T match && predicate(match))
            return match;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            if (FindVisual<T>(VisualTreeHelper.GetChild(root, i), predicate) is { } child)
                return child;
        return null;
    }

    private void ProjectInsertion(Action project, string proposed, int caret, Func<bool> ownsCaret)
    {
        if (_disposed || !ownsCaret() || _textInput is not { } text)
        {
            project();
            return;
        }

        var binding = text.GetBindingExpression(TextBox.TextProperty);
        bool Current() => !_disposed && ReferenceEquals(text, _textInput) && _row.IsLoaded && _row.IsVisible && _row.IsEnabled
            && ReferenceEquals(binding, text.GetBindingExpression(TextBox.TextProperty)) && ownsCaret();
        try
        {
            CloseTextPopup();
        }
        catch (Exception failure)
        {
            try
            {
                project();
            }
            catch (Exception cleanup)
            {
                throw new AggregateException(failure, cleanup);
            }

            throw;
        }

        if (!Current())
        {
            project();
            return;
        }

        try
        {
            text.Focus();
        }
        catch (Exception failure)
        {
            try
            {
                project();
            }
            catch (Exception cleanup)
            {
                throw new AggregateException(failure, cleanup);
            }

            throw;
        }

        if (!Current() || !text.IsKeyboardFocused)
        {
            project();
            return;
        }

        text.BeginChange();
        Exception? error = null;
        try
        {
            project();
            if (Current())
                binding?.UpdateTarget();
            if (Current() && text.Text == proposed)
                text.Select(Math.Clamp(caret, 0, text.Text.Length), 0);
        }
        catch (Exception failure)
        {
            error = failure;
        }

        try
        {
            text.EndChange();
        }
        catch (Exception failure)
        {
            error = error is null ? failure : new AggregateException(error, failure);
        }

        if (error is not null)
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
    }

    private bool Owns(DependencyObject? node)
    {
        var visited = new HashSet<DependencyObject>();
        while (node is not null && visited.Add(node))
        {
            if (ReferenceEquals(node, _row) || ReferenceEquals(node, _textPopup))
                return true;
            node = node is Popup popup ? popup.PlacementTarget : LogicalTreeHelper.GetParent(node)
                ?? (node is Visual ? VisualTreeHelper.GetParent(node) : null);
        }

        return false;
    }

    private void OnInput(object sender, PreProcessInputEventArgs args)
    {
        if (_disposed || _textPopup?.IsDropDownOpen != true)
            return;
        if (args.StagingItem.Input is MouseButtonEventArgs {ButtonState: MouseButtonState.Pressed} mouse
            && !Owns(Mouse.DirectlyOver as DependencyObject ?? mouse.OriginalSource as DependencyObject))
            CloseTextPopup();
    }

    private void OnKey(object sender, KeyEventArgs args)
    {
        if (_disposed || _hidden || !_row.IsEnabled || _textSession is null)
            return;
        if (args.Key == Key.Escape)
        {
            CloseTextPopup();
            args.Handled = true;
            return;
        }

        if (args.Key != Key.Enter)
            return;
        if (!_row.IsLoaded || !_row.IsVisible || _textInput is not {IsKeyboardFocused: true} text
            || args.OriginalSource is not DependencyObject origin || !IsWithinText(origin, text))
            return;
        // 在父级先拦截，避免原 InteractionTextBox 的 MoveFocus(Next) 抢先。
        if (!TryCommitPending())
        {
            args.Handled = true;
            return;
        }

        if (_disposed)
        {
            args.Handled = true;
            return;
        }

        CloseTextPopup();
    }

    private static bool IsWithinText(DependencyObject? node, TextBox text)
    {
        var visited = new HashSet<DependencyObject>();
        while (node is not null && visited.Add(node))
        {
            if (ReferenceEquals(node, text))
                return true;
            node = LogicalTreeHelper.GetParent(node) ?? (node is Visual ? VisualTreeHelper.GetParent(node) : null);
        }

        return false;
    }

    private void OnLostFocus(object sender, KeyboardFocusChangedEventArgs args)
    {
        if (_disposed || _closing || _hidden || !_row.IsEnabled)
            return;
        CancelPending();
        var generation = _generation;
        _pending = _row.Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
        {
            if (_disposed || generation != _generation || _hidden || !_row.IsLoaded || !_row.IsVisible || !_row.IsEnabled)
                return;
            _pending = null;
            if (_row.IsKeyboardFocusWithin || _textPopup?.Template?.FindName("PART_Popup", _textPopup) is Popup {Child: {IsKeyboardFocusWithin: true}})
                return;
            TryCommitPending();
            if (!_disposed)
                CloseTextPopup();
        }));
    }

    private void OnVisibility(object sender, DependencyPropertyChangedEventArgs args)
    {
        if (_disposed)
            return;
        _textSession?.SetSuspended(_hidden || !_row.IsVisible || !_row.IsEnabled);
        if (!_row.IsVisible)
            CloseTextPopup();
    }

    private void OnEnabled(object sender, DependencyPropertyChangedEventArgs args)
    {
        if (_disposed)
            return;
        _textSession?.SetSuspended(_hidden || !_row.IsVisible || !_row.IsEnabled);
        if (!_row.IsEnabled)
            CloseTextPopup();
    }

    private void OnDeactivate(object? sender, EventArgs args) => CloseTextPopup();

    private void CancelPending()
    {
        _generation++;
        _pending?.Abort();
        _pending = null;
    }

    private void CloseTextPopup()
    {
        CancelPending();
        var wasClosing = _closing;
        _closing = true;
        var popup = _textPopup;
        var generation = _generation;
        try
        {
            ParameterCleanup.Run(new Action[]
            {
                () => _textSession?.Close(),
                () =>
                {
                    if (generation == _generation && ReferenceEquals(popup, _textPopup) && !(_textSession?.IsTextDropDownOpen ?? false))
                        popup?.SetCurrentValue(ComboBox.IsDropDownOpenProperty, false);
                }
            });
        }
        finally
        {
            _closing = wasClosing;
        }
    }

    private void StopListening()
    {
        if (_listening)
            InputManager.Current.PreProcessInput -= OnInput;
        _listening = false;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        ParameterCleanup.Run(new Action[]
        {
            () => _textSession?.SetSuspended(true),
            () => _referenceSession?.SetSuspended(true),
            CloseTextPopup,
            StopListening,
            () =>
            {
                if (_owner is not null)
                    _owner.Deactivated -= OnDeactivate;
                _owner = null;
            },
            () =>
            {
                _row.Loaded -= OnRowLoaded;
                _row.Unloaded -= OnRowUnloaded;
                _row.LayoutUpdated -= OnLayout;
                ((UIElement)_row).IsVisibleChanged -= OnVisibility;
                _row.IsEnabledChanged -= OnEnabled;
                _row.PreviewKeyDown -= OnKey;
                _row.LostKeyboardFocus -= OnLostFocus;
                if (_textSession is not null)
                    _textSession.ProjectInsertionRequested -= ProjectInsertion;
            },
            () =>
            {
                _deleteText = null;
            },
            () => { if (_combo is not null) _combo.SetCurrentValue(ComboBox.IsDropDownOpenProperty, false); },
            () => _scope?.Dispose(),
            () => _expansion?.Dispose(),
            () => _referenceSession?.Dispose(),
            () => _textSession?.Dispose(),
            () => _numeric?.Dispose(),
            () => _row.SetReferenceData(null),
            () => _row.SetTextData(null)
        });
    }

    // The native DeleteTextBox owns Delete-key handling. Track only the current
    // template's reference input and let the scope detach its previous listener.
    private void RefreshReferenceInput()
    {
        if (_disposed || _referenceSession is null || !_row.IsLoaded)
            return;
        var input = Visuals<DeleteTextBox>(_row).FirstOrDefault(item => ReferenceEquals(item.DataContext, _referenceSession));
        if (ReferenceEquals(input, _deleteText))
            return;
        _deleteText = input;
        _scope?.Refresh();
    }
}
