using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Adapters.Fields;
using SunEyeVision.Plugin.Wpf.Adapters.References;
using SunEyeVision.Plugin.Wpf.Presenters.References;
using SunEyeVision.Plugin.Wpf.Presenters.Numeric;
using Numbers=SunEyeVision.Plugin.Wpf.Native.Numeric.Interaction.ListView;

namespace SunEyeVision.Plugin.Wpf.Fields;

/// <summary>
/// 一条原厂 between 组合承载两个借用状态的行宿主：视觉（SliderArrange SliderType=between +
/// 订阅模板）由行模板 IntegerBettween/FloatBettween 声明，组合提交/校验/恢复逻辑保留在本类。
/// </summary>
internal sealed class DeclaredRangeRow : ContentControl, IDisposable, IParameterVisibility
{
    private readonly DeclaredFieldState[] _states;
    private readonly FieldPresentation[] _presentations;
    private readonly NativeNumericCommit?[] _commits = new NativeNumericCommit?[2];
    private readonly Numbers.NumericControl?[] _leaves = new Numbers.NumericControl?[2];
    private readonly ReferenceFieldSession?[] _references = new ReferenceFieldSession?[2];
    private readonly ReferencePopupScope?[] _scopes = new ReferencePopupScope?[2];
    private readonly ReferenceExpansionCoordinator _expansion;
    private readonly Action<IReadOnlyDictionary<string,ParameterSetting>> _write;
    private readonly Func<IReadOnlyList<(DeclaredFieldState State,ParameterSetting Proposal)>,Action,bool>? _acceptPrepared;
    private readonly Func<IDisposable?>? _beginGroup;
    private readonly bool[] _allow;
    private readonly Func<Port,ParameterSetting,string?>? _validate;
    private readonly FieldRowModel _model;
    private readonly string _templateKey;
    private Window? _owner;
    private DispatcherOperation? _pending, _resume;
    private long _generation;
    private long _attachGeneration;
    private readonly long[] _observedAcceptance = new long[2];
    private readonly bool[] _observedReference = new bool[2];
    private bool _acceptedProjectionPending;
    private bool _disposed, _active, _syncing, _committing, _closing, _presentationHidden;
    private bool _nativeAttached;
    private Numbers.SliderArrangeBase? _nativeView;
    private ComboBox? _box;

    private Numbers.SliderArrangeBase NativeView => _nativeView ?? throw new InvalidOperationException("范围行模板尚未实例化，无法取到原厂滑条。");
    public TextBox? Input(int endpoint) => _commits[endpoint]?.Text;
    internal bool HasPendingEdits(int endpoint) => !_disposed && (_commits[endpoint]?.IsDirty == true || _references[endpoint]?.HasPendingText == true);
    private bool Invalid => _states.Any(state => state.HasInvalidInput);
    private static DependencyProperty ValueProperty(int i) => i == 0 ? Numbers.SliderArrangeBase.StartValueProperty : Numbers.SliderArrangeBase.EndValueProperty;
    private static DependencyProperty ModeProperty(int i) => i == 0 ? Numbers.SliderArrangeBase.IsSubscribeLeftProperty : Numbers.SliderArrangeBase.IsSubscribeRightProperty;

    public DeclaredRangeRow(DeclaredFieldState left, DeclaredFieldState right, FieldPresentation lower, FieldPresentation upper,
        Action<IReadOnlyDictionary<string,ParameterSetting>> write, ReferenceFieldOptions? references, Func<OutputReference,string>? format,
        Func<IReadOnlyList<(DeclaredFieldState State,ParameterSetting Proposal)>,Action,bool>? acceptPrepared = null, Func<IDisposable?>? beginGroup = null)
    {
        _states = new[] {left, right};
        _presentations = new[] {lower, upper};
        _write = write;
        _validate = references?.ValidateProposal;
        for (var i = 0; i < 2; i++)
        {
            _observedAcceptance[i] = _states[i].AcceptanceVersion;
            _observedReference[i] = _states[i].IsReferenceMode;
        }

        _acceptPrepared = acceptPrepared;
        _beginGroup = beginGroup;
        for (var i = 0; i < 2; i++)
        {
            var endpoint = i;
            _states[i].EnableNumericModel(_presentations[i].Layout);
            _states[i].NumericEndpoint = i;
            _states[i].NumericPeer = _states[1 - i];
            _states[i].NumericCheck = value => CheckModel(endpoint, value);
        }

        _allow = _presentations.Select(p => references is not null && p.Layout?.AllowReference != false).ToArray();
        _templateKey = left.Port.Type.Scalar == ScalarKind.Int64
            ? DeclaredFieldTemplateRegistry.IntegerBettween
            : DeclaredFieldTemplateRegistry.FloatBettween;
        _model = new FieldRowModel(null!, _templateKey, lower.Layout?.PairDisplayName ?? lower.DisplayName,
            0, 0, 1, null, 0, Array.Empty<FieldRowModel.Entry>(), false);
        _expansion = new ReferenceExpansionCoordinator(this, references?.WindowOwner);
        Tag = left.Port.Id;
        Margin = new Thickness(0);
        MinHeight = 32;
        Content = _model;
        ContentTemplateSelector = ParameterRowTemplateSelector.Instance;
        Resources.MergedDictionaries.Add(EditorResources.Shared);
        Resources.MergedDictionaries.Add(new ResourceDictionary
        {Source = new Uri("/SunEyeVision.Plugin.Wpf;component/Themes/Native/Composition/References/ReferenceField.xaml", UriKind.Relative)});
        for (var i = 0; i < 2; i++)
        {
            var endpoint = i;
            if ((_states[i].Port.EditModes & FieldEditModes.Reference) != 0 && (_allow[i] || _states[i].IsReferenceMode))
            {
                _references[i] = new(_states[i], references ?? ReferenceFieldOptions.Empty, format,
                    proposal => TryAccept(endpoint, proposal), () => TryCommitEndpoint(endpoint),
                    _expansion.Open, _expansion.Preference, selectVariable: _expansion.OpenVariable, isExpanded: _expansion.IsExpanded);
                _scopes[i] = new(this, _references[i]!, () => !_disposed && _states[endpoint].IsReferenceMode);
            }

            _states[i].Changed += OnStateChanged;
            _states[i].NumericModel!.Changed += OnNumericModelChanged;
        }

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        IsVisibleChanged += OnVisibility;
        IsEnabledChanged += OnEnabled;
        PreviewKeyDown += OnKey;
        PreviewMouseWheel += OnWheel;
        LostKeyboardFocus += OnLostFocus;
        if (left.Accepted is ConstantSetting l && right.Accepted is ConstantSetting r
            && Convert.ToDouble(l.Value, CultureInfo.InvariantCulture) > Convert.ToDouble(r.Value, CultureInfo.InvariantCulture))
            foreach (var state in _states)
                state.SetEditError("范围下限不能大于上限。");
    }

    // ---- 模板物化后挂接原厂滑条 ----
    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        if (_disposed)
            return;
        AttachNativeView();
        _active = true;
        NativeView.PrepareBorrowedInteractions(_active && IsVisible && IsEnabled && !_presentationHidden);
        AttachInputs();
        if (_owner is not null)
            _owner.Deactivated -= OnDeactivated;
        _owner = Window.GetWindow(this);
        if (_owner is not null)
            _owner.Deactivated += OnDeactivated;
        Project(false);
        foreach (var scope in _scopes)
            scope?.PrepareForVisibility(!_presentationHidden && IsVisible && IsEnabled);
        QueueResume();
    }

    private void AttachNativeView()
    {
        if (_disposed || _nativeAttached && _nativeView is not null)
        {
            if (_nativeAttached)
                return;
        }

        var view = FindDescendant<Numbers.SliderArrangeBase>(this);
        if (view is null)
            return;
        if (_nativeAttached)
            return;
        _nativeAttached = true;
        _nativeView = view;
        Language = XmlLanguage.GetLanguage("en-US");
        view.Language = XmlLanguage.GetLanguage("en-US");
        var bounds = _states.Select((s, i) => Bounds(s.Port, _presentations[i].Layout!)).ToArray();
        view.Minimum = _presentations[0].Layout!.PairMinimum ?? Math.Min(bounds[0].Min, bounds[1].Min);
        view.Maximum = _presentations[0].Layout!.PairMaximum ?? Math.Max(bounds[0].Max, bounds[1].Max);
        view.MinimumLeft = bounds[0].Min;
        view.MaximumLeft = bounds[0].Max;
        view.MinimumRight = bounds[1].Min;
        view.MaximumRight = bounds[1].Max;
        view.IsNeedCheckExtremumByXML = _states[0].Port.Type.Scalar == ScalarKind.Int64;
        view.SliderType = Numbers.SliderArrangeType.between;
        view.SliderTickFrequency = _presentations[0].Layout!.Increment;
        view.Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102));
        view.FontSize = 14;
        view.PrepareBorrowedInteractions(false);
        for (var i = 0; i < 2; i++)
            view.SetBinding(ValueProperty(i), new Binding(nameof(NativeNumericModel.NativeValue))
            {
                Source = _states[i].NumericModel, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            });
        view.SubscribeTemplate = (DataTemplate)FindResource("ButtonSelecter_sub");
        view.SubscribeDataLeft = _references[0];
        view.SubscribeDataRight = _references[1];
        view.OnStartValueChange += OnLeftValue;
        view.OnEndValueChange += OnRightValue;
        view.OnLeftTypeChange += OnLeftMode;
        view.OnRightTypeChange += OnRightMode;
        _box = (ComboBox)view.FindName("CBox");
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

    private static (double Min, double Max) Bounds(Port port, FieldLayout layout)
    {
        if (port.Type.Scalar == ScalarKind.Int64)
        {
            var r = NumericStorageRules.IntegerRange(NumericStorageRules.Effective(port.Type, port.Storage)!.Value);
            return (Math.Max(layout.Minimum ?? r.Minimum, r.Minimum), Math.Min(layout.Maximum ?? r.Maximum, r.Maximum));
        }

        return (layout.Minimum ?? -double.MaxValue, layout.Maximum ?? double.MaxValue);
    }

    private void AttachInputs()
    {
        var generation = ++_attachGeneration;
        var old = _commits.ToArray();
        var pending = old.Select(commit => commit?.Pending).ToArray();
        var edits = old.Select(commit => commit?.EditVersion).ToArray();
        NativeView.ApplyTemplate();
        NativeView.UpdateLayout();
        _box!.ApplyTemplate();
        if (_disposed || generation != _attachGeneration || old.Where((commit, i) => !ReferenceEquals(commit, _commits[i])).Any())
            return;
        var pair = NativeView.EnsureTemplateInputs();
        var leaves = new[] {pair.Left, pair.Right};
        for (var i = 0; i < 2; i++)
        {
            if (_commits[i] is {} current && ReferenceEquals(current.Text, leaves[i].FindName("ValueText")))
                continue;
            var raw = edits[i] == old[i]?.EditVersion ? pending[i] : null;
            old[i]?.Dispose();
            var endpoint = i;
            var created = new NativeNumericCommit(_states[i], _presentations[i].Layout, leaves[i], NativeView, ValueProperty(i), paired: true,
                isCurrent: () => !_disposed && ReferenceEquals(_box.Template.FindName(endpoint == 0 ? "T1" : "T2", _box), leaves[endpoint]));
            if (_disposed || generation != _attachGeneration || !ReferenceEquals(old[i], _commits[i]))
            {
                created.Dispose();
                return;
            }

            _commits[i] = created;
            _leaves[i] = leaves[i];
            _commits[i]!.RestorePending(raw);
            _commits[i]!.ValidateInitial();
        }
    }

    private bool ProjectValues(ParameterSetting[] values, Action action)
    {
        bool Next(int i)
        {
            if (i == 2)
            {
                action();
                return true;
            }

            if (values[i] is not ConstantSetting number)
                return Next(i + 1);
            bool nested = false;
            return _commits[i]!.ProjectCandidate(number, () => nested = Next(i + 1)) && nested;
        }

        return Next(0);
    }

    private void OnStateChanged()
    {
        bool accepted = false;
        for (var i = 0; i < 2; i++)
        {
            accepted |= _observedAcceptance[i] != _states[i].AcceptanceVersion || _observedReference[i] != _states[i].IsReferenceMode;
            _observedAcceptance[i] = _states[i].AcceptanceVersion;
            _observedReference[i] = _states[i].IsReferenceMode;
        }

        if (_syncing && accepted)
            _acceptedProjectionPending = true;
        if (!_committing)
            Project(false);
    }

    private void OnNumericModelChanged()
    {
        if (_disposed || _syncing || _committing)
            return;
        NativeView.RetireBorrowedInteractions();
        Project(false);
    }

    internal void ValidateReplay(IReadOnlyDictionary<string,AcceptedFieldValue> values)
    {
        if (values[_states[0].Port.Id].Setting is ConstantSetting low && values[_states[1].Port.Id].Setting is ConstantSetting high
            && Convert.ToDouble(low.Value, CultureInfo.InvariantCulture) > Convert.ToDouble(high.Value, CultureInfo.InvariantCulture))
            throw new ArgumentException("历史范围下限不能大于上限。");
    }

    internal void ReplayAccepted()
    {
        if (_disposed)
            return;
        ++_generation;
        _pending?.Abort();
        _pending = null;
        _resume?.Abort();
        _resume = null;
        _expansion.Close();
        foreach (var scope in _scopes)
            scope?.ClosePopups();
        Project(true);
    }

    private void Project(bool accepted, Func<int,bool>? mayReplace = null)
    {
        if (_disposed || _syncing || !_nativeAttached)
            return;
        if (accepted)
            NativeView.RetireBorrowedInteractions();
        _syncing = true;
        try
        {
            for (var i = 0; i < 2; i++)
                NativeView.SetCurrentValue(ModeProperty(i), _states[i].IsReferenceMode);
            // 订阅先于 Loaded 激活会让原右列塌陷；两端都可编辑时恢复原布局（不接受也不发明常量值）。
            if (!NativeView.IsSubscribeLeft && !NativeView.IsSubscribeRight)
                NativeView.InitUI();
            NativeView.IsSubscribeVisible = _references.Any(session => session is not null) ? Visibility.Visible : Visibility.Collapsed;
            for (var i = 0; i < 2; i++)
            {
                if ((!accepted || mayReplace?.Invoke(i) != false) && (accepted || _states[i].IsReferenceMode || _commits[i]?.IsDirty != true))
                    _commits[i]?.ProjectAccepted();
                if (accepted && mayReplace?.Invoke(i) != false)
                    _references[i]?.ProjectAccepted();
                if (!_states[i].IsReferenceMode)
                    _scopes[i]?.ClosePopups();
            }

            foreach (var toggle in Visuals<ToggleButton>(NativeView))
            {
                var path = toggle.GetBindingExpression(ToggleButton.IsCheckedProperty)?.ParentBinding.Path?.Path;
                var i = path == "IsSubscribeLeft" ? 0 : path == "IsSubscribeRight" ? 1 : -1;
                if (i >= 0)
                    toggle.Visibility = _references[i] is not null && (_states[i].Port.EditModes & FieldEditModes.Constant) != 0 && (_allow[i] || _states[i].IsReferenceMode)
                        ? Visibility.Visible : Visibility.Collapsed;
            }
        }
        finally
        {
            _syncing = false;
            if (_acceptedProjectionPending && !_committing)
            {
                _acceptedProjectionPending = false;
                Project(false);
            }
        }
    }

    internal bool ContainsField(string fieldId) => _states.Any(state => state.Port.Id == fieldId);

    internal bool TrySetReferenceFromCommand(string fieldId, ParameterSetting proposal)
    {
        var endpoint = Array.FindIndex(_states, state => state.Port.Id == fieldId);
        if (_disposed || _committing || endpoint < 0 || proposal is not (ReferenceSetting or VariableSetting))
            return false;
        try
        {
            ReferenceFieldSession.ValidateSelectionShape(proposal);
        }
        catch (ArgumentException error)
        {
            _states[endpoint].SetEditError(error.Message);
            return false;
        }

        return TryAcceptEndpoint(endpoint, proposal);
    }

    public bool TrySetConstant(int endpoint, object value)
    {
        if (_disposed || _committing)
            return false;
        ConstantSetting proposal;
        try
        {
            proposal = _commits[endpoint]!.PrepareValue(value);
        }
        catch (Exception error) when (error is ArgumentException or FormatException or OverflowException)
        {
            _states[endpoint].SetEditError(error.Message);
            return false;
        }

        return TryAcceptEndpoint(endpoint, proposal, value);
    }

    private bool Prepare(int i, out ParameterSetting proposal)
    {
        proposal = null!;
        if (_states[i].IsReferenceMode)
            return _references[i]?.TryPreparePending(out proposal) ?? false;
        if (_commits[i]!.IsDirty)
        {
            if (!_commits[i]!.TryReadProposal(out var value))
                return false;
            proposal = value;
            return true;
        }

        proposal = _states[i].Accepted;
        if (proposal is not ConstantSetting stored)
            return false;
        try
        {
            _commits[i]!.ValidateStored(stored);
            return true;
        }
        catch (Exception error) when (error is ArgumentException or FormatException or OverflowException)
        {
            _states[i].SetEditError(error.Message);
            return false;
        }
    }

    private bool TryAccept(int? endpoint, ParameterSetting? explicitProposal)
    {
        if (endpoint is {} selected)
            return TryAcceptEndpoint(selected, explicitProposal ?? throw new ArgumentNullException(nameof(explicitProposal)));
        if (_disposed || _states.Any(state => state.IsDisposed || state.SourceError.Length != 0))
            return false;
        if (_committing)
            return false;
        _committing = true;
        IDisposable? group = null;
        var raw = _commits.Select(commit => commit?.Pending).ToArray();
        bool candidateInstalled = false, accepted = false, projected = false;
        var versions = _states.Select(state => state.AcceptanceVersion).ToArray();
        var modelVersions = _states.Select(state => state.NumericModel!.Version).ToArray();
        var modes = _states.Select(state => state.IsReferenceMode).ToArray();
        var commits = _commits.ToArray();
        var edits = commits.Select(commit => commit?.EditVersion).ToArray();
        bool Unchanged() => !_disposed && Enumerable.Range(0, 2).All(i => !_states[i].IsDisposed && versions[i] == _states[i].AcceptanceVersion
            && modelVersions[i] == _states[i].NumericModel!.Version && modes[i] == _states[i].IsReferenceMode
            && ReferenceEquals(commits[i], _commits[i]) && edits[i] == commits[i]?.EditVersion);
        try
        {
            if (_beginGroup is not null)
            {
                group = _beginGroup();
                if (group is null)
                    return false;
            }

            var proposals = new ParameterSetting[2];
            for (var i = 0; i < 2; i++)
            {
                if (endpoint == i)
                    proposals[i] = explicitProposal!;
                else if (!Prepare(i, out proposals[i]))
                    return false;
            }

            candidateInstalled = true;
            _syncing = true;
            try
            {
                for (var i = 0; i < 2; i++)
                    if (proposals[i] is ConstantSetting number && !StageModel(i, number, modelVersions))
                        return false;
            }
            finally
            {
                _syncing = false;
            }

            if (!Unchanged())
                return false;
            for (var i = 0; i < 2; i++)
                if (proposals[i] is ConstantSetting number)
                    try
                    {
                        CheckModel(i, number.Value!);
                    }
                    catch (ArgumentException error)
                    {
                        _states[i].SetEditError(error.Message);
                        return false;
                    }
            for (var i = 0; i < 2; i++)
            {
                var error = _validate?.Invoke(_states[i].Port, proposals[i]);
                if (!Unchanged())
                    return false;
                if (!string.IsNullOrEmpty(error))
                {
                    _states[i].SetEditError(error);
                    return false;
                }
            }

            if (proposals[0] is ConstantSetting low && proposals[1] is ConstantSetting high
                && Convert.ToDouble(low.Value, CultureInfo.InvariantCulture) > Convert.ToDouble(high.Value, CultureInfo.InvariantCulture))
            {
                foreach (var state in _states)
                    state.SetEditError("范围下限不能大于上限。");
                return false;
            }

            // 两端先备齐，再给原厂 RangeRules 建立完整候选上下文。
            _syncing = true;
            candidateInstalled = true;
            try
            {
                if (!ProjectValues(proposals, () =>
                    {
                        for (var i = 0; i < 2; i++)
                            if (proposals[i] is ConstantSetting number)
                            {
                                NativeView.SetCurrentValue(ValueProperty(i), Convert.ToDouble(number.Value, CultureInfo.InvariantCulture));
                                if (!Unchanged())
                                    return;
                            }
                    }))
                    return false;
                for (var i = 0; i < 2; i++)
                    if (proposals[i] is ConstantSetting number && !ValidateEndpointBinding(i, number, proposals[1 - i]))
                        return false;
            }
            finally
            {
                _syncing = false;
            }

            if (!Unchanged())
                return false;
            var prepared = new[] {(_states[0], proposals[0]), (_states[1], proposals[1])};
            accepted = _acceptPrepared is {} submit
                ? submit(prepared, () => accepted = true)
                : DeclaredFieldState.TrySetGroup(prepared, _write, installed: () => accepted = true);
            if (_disposed || _states.Any(state => state.IsDisposed))
                return false;
            if (accepted)
            {
                Project(true, i => ReferenceEquals(commits[i], _commits[i]) && edits[i] == commits[i]?.EditVersion);
                projected = true;
            }

            return accepted && !Invalid;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        finally
        {
            try
            {
                if (!_disposed && accepted && !projected && !_states.Any(state => state.IsDisposed))
                    Project(true, i => ReferenceEquals(commits[i], _commits[i]) && edits[i] == commits[i]?.EditVersion);
                if (candidateInstalled && !accepted && Unchanged())
                {
                    _syncing = true;
                    try
                    {
                        if (RestoreModels(new[] {0, 1}, modelVersions, Unchanged))
                            for (var i = 0; i < 2; i++)
                            {
                                if (!Unchanged())
                                    break;
                                _commits[i]!.RestoreRejected(raw[i]);
                                if (!Unchanged())
                                    break;
                            }
                    }
                    finally
                    {
                        _syncing = false;
                    }
                }

                _committing = false;
                if (!_disposed)
                    Project(false);
            }
            finally
            {
                _committing = false;
                group?.Dispose();
            }
        }
    }

    private bool ValidateEndpointBinding(int endpoint, ConstantSetting value, ParameterSetting? other)
    {
        var commits = _commits.ToArray();
        var commit = commits[endpoint] ?? throw new InvalidOperationException("范围数值输入尚未初始化。");
        var leaf = _leaves[endpoint] ?? throw new InvalidOperationException("范围数值输入尚未初始化。");
        var minBinding = BindingOperations.GetBindingExpression(leaf, Numbers.NumericControl.MinimumProperty);
        var maxBinding = BindingOperations.GetBindingExpression(leaf, Numbers.NumericControl.MaximumProperty);
        double oldMin = leaf.Minimum, oldMax = leaf.Maximum;
        var acceptance = _states[endpoint].AcceptanceVersion;
        var mode = _states[endpoint].IsReferenceMode;
        var edit = commit.EditVersion;
        var bounds = Bounds(_states[endpoint].Port, _presentations[endpoint].Layout!);
        double minimum = oldMin, maximum = oldMax;
        bool OwnsLeaf() => !_disposed && ReferenceEquals(_leaves[endpoint], leaf) && ReferenceEquals(_box?.Template.FindName(endpoint == 0 ? "T1" : "T2", _box), leaf);
        bool Current() => OwnsLeaf() && !_states[endpoint].IsDisposed && acceptance == _states[endpoint].AcceptanceVersion
            && mode == _states[endpoint].IsReferenceMode && edit == commit.EditVersion && ReferenceEquals(commits[endpoint], _commits[endpoint])
            && ReferenceEquals(minBinding, BindingOperations.GetBindingExpression(leaf, Numbers.NumericControl.MinimumProperty))
            && ReferenceEquals(maxBinding, BindingOperations.GetBindingExpression(leaf, Numbers.NumericControl.MaximumProperty));
        Exception? failure = null;
        bool valid = false;
        try
        {
            if (Current())
            {
                leaf.SetCurrentValue(Numbers.NumericControl.MinimumProperty, minimum);
                if (Current())
                {
                    leaf.SetCurrentValue(Numbers.NumericControl.MaximumProperty, maximum);
                    if (Current())
                        valid = commit.ValidatePreparedBindings(value);
                }
            }
        }
        catch (Exception error)
        {
            failure = error;
        }

        void Restore(DependencyProperty property, BindingExpression? binding, double oldValue)
        {
            try
            {
                if (!OwnsLeaf() || !ReferenceEquals(binding, BindingOperations.GetBindingExpression(leaf, property)))
                    return;
                if (binding is null)
                    leaf.SetCurrentValue(property, oldValue);
                else
                    binding.UpdateTarget();
            }
            catch (Exception cleanup)
            {
                failure = failure is null ? cleanup : new AggregateException(failure, cleanup);
            }
        }

        Restore(Numbers.NumericControl.MinimumProperty, minBinding, oldMin);
        Restore(Numbers.NumericControl.MaximumProperty, maxBinding, oldMax);
        if (failure is not null)
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        return valid && Current();
    }

    private bool TryCommitEndpoint(int endpoint)
    {
        if (_disposed || _committing)
            return false;
        if (!_states[endpoint].IsReferenceMode && _commits[endpoint]?.IsDirty != true
            && Equals(_states[endpoint].NumericModel!.CurValue, _states[endpoint].NumericModel!.OldValue))
            return !_states[endpoint].HasInvalidInput;
        if (!Prepare(endpoint, out var proposal))
            return false;
        return TryAcceptEndpoint(endpoint, proposal);
    }

    private bool TryAcceptEndpoint(int endpoint, ParameterSetting proposal, object? originalValue = null)
    {
        if (_disposed || _committing || endpoint is < 0 or > 1 || _states.Any(state => state.IsDisposed))
            return false;
        var state = _states[endpoint];
        _committing = true;
        IDisposable? group = null;
        var commits = _commits.ToArray();
        var versions = _states.Select(item => item.AcceptanceVersion).ToArray();
        var modelVersions = _states.Select(item => item.NumericModel!.Version).ToArray();
        var modes = _states.Select(item => item.IsReferenceMode).ToArray();
        var edits = commits.Select(item => item?.EditVersion).ToArray();
        var raw = commits[endpoint]?.Pending;
        bool candidateInstalled = false, accepted = false, projected = false;
        bool Unchanged() => !_disposed && Enumerable.Range(0, 2).All(i => !_states[i].IsDisposed && versions[i] == _states[i].AcceptanceVersion
            && modelVersions[i] == _states[i].NumericModel!.Version && modes[i] == _states[i].IsReferenceMode
            && ReferenceEquals(commits[i], _commits[i]) && edits[i] == commits[i]?.EditVersion);
        try
        {
            if (_beginGroup is not null)
            {
                group = _beginGroup();
                if (group is null)
                    return false;
            }

            var other = new ConstantSetting(_states[1 - endpoint].NumericModel!.CurValue);
            if (proposal is ConstantSetting staged)
            {
                candidateInstalled = true;
                _syncing = true;
                try
                {
                    if (!StageModel(endpoint, staged, modelVersions, originalValue))
                        return false;
                }
                finally
                {
                    _syncing = false;
                }

                if (!Unchanged())
                    return false;
                try
                {
                    CheckModel(endpoint, staged.Value!);
                }
                catch (Exception error) when (error is ArgumentException)
                {
                    state.SetEditError(error.Message);
                    return false;
                }
            }

            if (proposal is ConstantSetting number)
            {
                try
                {
                    commits[endpoint]!.ValidateStored(number);
                }
                catch (Exception error) when (error is ArgumentException or FormatException or OverflowException)
                {
                    state.SetEditError(error.Message);
                    return false;
                }
            }

            var validation = _validate?.Invoke(state.Port, proposal);
            if (!Unchanged())
                return false;
            if (!string.IsNullOrEmpty(validation))
            {
                state.SetEditError(validation);
                return false;
            }

            if (proposal is ConstantSetting candidate)
            {
                _syncing = true;
                candidateInstalled = true;
                try
                {
                    if (!commits[endpoint]!.ProjectCandidate(candidate,
                            () => NativeView.SetCurrentValue(ValueProperty(endpoint), Convert.ToDouble(candidate.Value, CultureInfo.InvariantCulture))))
                        return false;
                    if (!Unchanged() || !ValidateEndpointBinding(endpoint, candidate, other))
                        return false;
                }
                finally
                {
                    _syncing = false;
                }
            }

            if (!Unchanged())
                return false;
            var prepared = new[] {(state, proposal)};
            accepted = _acceptPrepared is {} submit
                ? submit(prepared, () => accepted = true)
                : DeclaredFieldState.TrySetGroup(prepared, _write, installed: () => accepted = true);
            if (_disposed || state.IsDisposed)
                return false;
            if (accepted)
            {
                Project(true, i => i == endpoint && ReferenceEquals(commits[i], _commits[i]) && edits[i] == commits[i]?.EditVersion);
                projected = true;
            }

            return accepted;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        finally
        {
            try
            {
                if (!_disposed && accepted && !projected && !state.IsDisposed)
                    Project(true, i => i == endpoint && ReferenceEquals(commits[i], _commits[i]) && edits[i] == commits[i]?.EditVersion);
                if (candidateInstalled && !accepted && Unchanged())
                {
                    _syncing = true;
                    try
                    {
                        if (RestoreModels(new[] {endpoint}, modelVersions, Unchanged) && Unchanged())
                            commits[endpoint]!.RestoreRejected(raw);
                    }
                    finally
                    {
                        _syncing = false;
                    }
                }

                _committing = false;
                if (!_disposed)
                    Project(false);
            }
            finally
            {
                _committing = false;
                group?.Dispose();
            }
        }
    }

    public bool TryCommitPending(bool explicitRetry = false)
    {
        if (_disposed)
            return false;
        if (_committing)
            return !Invalid;
        if (!explicitRetry && !_commits.Any(commit => commit?.IsDirty == true) && !_references.Any(session => session?.HasPendingText == true))
            return !Invalid;
        return TryAccept(null, null);
    }

    private bool StageModel(int endpoint, ConstantSetting value, long[] versions, object? originalValue = null)
    {
        var model = _states[endpoint].NumericModel!;
        var raw = NativeNumericSequenceProjection.Single(_states[endpoint].Port, originalValue ?? value.Value!);
        var changed = !Equals(model.CurValue, raw);
        versions[endpoint] = model.Version + (changed ? 1 : 0);
        return _commits[endpoint]!.ProjectCandidate(value, () => model.NativeValue = Convert.ToDouble(raw, CultureInfo.InvariantCulture))
            && model.Version == versions[endpoint];
    }

    private bool RestoreModels(IEnumerable<int> endpoints, long[] versions, Func<bool> current)
    {
        var old = _states.Select(state => state.NumericModel!.CaptureOldValue()).ToArray();
        foreach (var i in endpoints)
        {
            if (!current())
                return false;
            var model = _states[i].NumericModel!;
            var saved = old[i];
            var value = new ConstantSetting(saved.Value);
            versions[i] = model.Version + (!Equals(model.CurValue, saved.Value) ? 1 : 0);
            if (!_commits[i]!.ProjectCandidate(value, () => model.RestoreOldValue(saved.Value, saved.Known)) || !current())
                return false;
        }

        return current();
    }

    private void CheckModel(int endpoint, object value)
    {
        var bounds = Bounds(_states[endpoint].Port, _presentations[endpoint].Layout!);
        double min = bounds.Min, max = bounds.Max;
        var peer = _states[1 - endpoint].NumericModel!;
        var versions = _states.Select(state => state.AcceptanceVersion).ToArray();
        var models = _states.Select(state => state.NumericModel!.Version).ToArray();
        bool Current() => !_disposed && Enumerable.Range(0, 2).All(i => !_states[i].IsDisposed && versions[i] == _states[i].AcceptanceVersion && models[i] == _states[i].NumericModel!.Version);
        if (!Current())
            throw new OperationCanceledException("范围owner已退役。");
        if (endpoint == 1)
        {
            var left = Convert.ToDouble(peer.CurValue, CultureInfo.InvariantCulture);
            if (left <= max)
                min = left;
        }
        else
        {
            double comparison;
            if (peer.IsInitValue)
                comparison = Convert.ToDouble(peer.CurValue, CultureInfo.InvariantCulture);
            else
            {
                try
                {
                    var text = _states[1].ReadNumericComparisonString();
                    if (!Current())
                        throw new OperationCanceledException("临时比较读取后owner已退役。");
                    if (_states[0].Port.Type.Scalar == ScalarKind.Int64)
                    {
                        int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed);
                        comparison = parsed;
                    }
                    else
                    {
                        double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var parsed);
                        comparison = parsed;
                    }
                }
                catch
                {
                    if (!Current())
                        throw new OperationCanceledException("临时比较读取后owner已退役。");
                    comparison = bounds.Max;
                }
            }

            if (comparison >= min)
                max = comparison;
        }

        var number = Convert.ToDouble(value, CultureInfo.InvariantCulture);
        if (!double.IsFinite(number) || min - number > 1e-8 || number - max > 1e-8)
            throw new ArgumentException("数值超出原双端模型比较范围。");
    }

    private void OnLeftValue(object sender, RoutedPropertyChangedEventArgs<double> args) => OnNativeValue(0);
    private void OnRightValue(object sender, RoutedPropertyChangedEventArgs<double> args) => OnNativeValue(1);

    private void OnNativeValue(int endpoint)
    {
        if (!_active || _disposed || !IsEnabled || _syncing)
            return;
        if (_states[endpoint].IsReferenceMode)
        {
            _commits[endpoint]?.ProjectAccepted();
            return;
        }

        if (!_closing && !_presentationHidden)
            TryCommitEndpoint(endpoint);
    }

    private void OnLeftMode(object sender, RoutedPropertyChangedEventArgs<double> args) => ChangeMode(0);
    private void OnRightMode(object sender, RoutedPropertyChangedEventArgs<double> args) => ChangeMode(1);

    private void ChangeMode(int i)
    {
        if (_disposed || _syncing || _presentationHidden)
            return;
        if (!IsEnabled)
        {
            Project(false);
            return;
        }

        try
        {
            if ((bool)NativeView.GetValue(ModeProperty(i)))
            {
                if (!_allow[i])
                {
                    _states[i].SetEditError("此字段不允许新增订阅。");
                    return;
                }

                if (TryCommitEndpoint(i))
                    _states[i].EnterReferenceMode();
            }
            else if (_states[i].NumericModel is not null)
            {
                if (!_states[i].TryUseConstant(value => TryAcceptEndpoint(i, value)) && !_states[i].IsReferenceMode)
                    _commits[i]?.BeginConstantEntry();
            }
            else if (_states[i].ConstantBackup is {} backup)
                TrySetConstant(i, backup.Value!);
            else
            {
                var state = _states[i];
                var commit = _commits[i];
                var acceptance = state.AcceptanceVersion;
                var edit = commit?.EditVersion;
                _states[i].TryUseConstant();
                if (!_disposed && !state.IsDisposed && !state.IsReferenceMode && state.ConstantBackup is null
                    && acceptance == state.AcceptanceVersion && ReferenceEquals(commit, _commits[i]) && edit == commit?.EditVersion)
                    commit?.BeginConstantEntry();
            }
        }
        finally
        {
            Project(false);
            foreach (var scope in _scopes)
                scope?.Refresh();
        }
    }

    private void QueueResume()
    {
        var generation = ++_generation;
        _resume?.Abort();
        _resume = null;
        if (_disposed || !_active || !IsVisible || _presentationHidden || !IsEnabled)
            return;
        _resume = Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            if (_disposed || !_active || !IsVisible || _presentationHidden || !IsEnabled || generation != _generation)
                return;
            _resume = null;
            foreach (var commit in _commits)
                commit?.Resume();
        }));
    }

    private void OnEnabled(object sender, DependencyPropertyChangedEventArgs args)
    {
        if (_disposed || !_nativeAttached)
            return;
        if (!IsEnabled)
        {
            NativeView.PrepareBorrowedInteractions(false);
            _generation++;
            _resume?.Abort();
            _resume = null;
            ParameterCleanup.Run(_commits.Where(commit => commit is not null).Select(commit => (Action)commit!.Suspend)
                .Concat(_scopes.Where(scope => scope is not null).Select(scope => (Action)(() => scope!.PrepareForVisibility(false))))
                .Concat(new Action[] {_expansion.Close, ClosePopups}));
        }
        else
        {
            NativeView.PrepareBorrowedInteractions(_active && IsVisible && !_presentationHidden);
            foreach (var scope in _scopes)
                scope?.PrepareForVisibility(!_presentationHidden && IsVisible);
            QueueResume();
        }
    }

    private void OnKey(object sender, KeyEventArgs args)
    {
        if (_disposed || !IsEnabled || _presentationHidden || !_nativeAttached)
            return;
        var i = Array.FindIndex(_commits, commit => commit?.Text.IsKeyboardFocusWithin == true);
        if (i < 0 || _states[i].IsReferenceMode)
            return;
        if (args.Key == Key.Escape)
        {
            ClosePopups();
            args.Handled = true;
            return;
        }

        if (args.Key != Key.Enter)
            return;
        args.Handled = true;
        if (TryCommitEndpoint(i))
        {
            ClosePopups();
            FocusManager.SetFocusedElement(FocusManager.GetFocusScope(Input(i)!), null);
            Keyboard.ClearFocus();
        }
    }

    private void Queue(Action action)
    {
        _pending?.Abort();
        var generation = _generation;
        _pending = Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
        {
            if (_disposed || !_active || !IsVisible || !IsEnabled || _presentationHidden || generation != _generation)
                return;
            _pending = null;
            action();
        }));
    }

    private void OnWheel(object sender, MouseWheelEventArgs args)
    {
        var endpoint = Array.FindIndex(_commits, commit => commit?.Text.IsKeyboardFocusWithin == true);
        if (endpoint >= 0)
            Queue(() => TryCommitEndpoint(endpoint));
    }

    private void OnLostFocus(object sender, KeyboardFocusChangedEventArgs args)
    {
        if (_disposed || !IsEnabled || _presentationHidden || !_nativeAttached)
            return;
        var endpoint = Array.FindIndex(_commits, commit => ReferenceEquals(commit?.Text, args.OldFocus));
        Queue(() =>
        {
            if (endpoint >= 0)
                TryCommitEndpoint(endpoint);
            if (!IsKeyboardFocusWithin && _box!.Template.FindName("PART_Popup", _box) is not Popup {Child: {IsKeyboardFocusWithin: true}})
                ClosePopups();
        });
    }

    private void ClosePopups()
    {
        _pending?.Abort();
        _pending = null;
        _closing = true;
        try
        {
            ParameterCleanup.Run(new Action[] {() => _box?.SetCurrentValue(ComboBox.IsDropDownOpenProperty, false)}
                .Concat(_scopes.Where(scope => scope is not null).Select(scope => (Action)scope!.ClosePopups)));
        }
        finally
        {
            _closing = false;
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs args)
    {
        if (_nativeAttached)
            NativeView.PrepareBorrowedInteractions(false);
        _active = false;
        _generation++;
        _resume?.Abort();
        _resume = null;
        var owner = _owner;
        _owner = null;
        ParameterCleanup.Run(new Action[] {() => { if (owner is not null) owner.Deactivated -= OnDeactivated; }}
            .Concat(_commits.Where(commit => commit is not null).Select(commit => (Action)commit!.Suspend))
            .Concat(new Action[] {ClosePopups}));
    }

    void IParameterVisibility.PrepareForVisibility(bool visible) => PrepareForVisibilityCore(visible);

    private void PrepareForVisibilityCore(bool visible)
    {
        if (_disposed)
            return;
        if (!visible)
        {
            if (_nativeAttached)
                NativeView.PrepareBorrowedInteractions(false);
            _presentationHidden = true;
            _generation++;
            _resume?.Abort();
            _resume = null;
            ParameterCleanup.Run(_scopes.Where(scope => scope is not null).Select(scope => (Action)(() => scope!.PrepareForVisibility(false)))
                .Concat(_commits.Where(commit => commit is not null).Select(commit => (Action)commit!.Suspend))
                .Concat(new Action[] {ClosePopups}));
        }
        else
        {
            _presentationHidden = false;
            if (_nativeAttached)
            {
                NativeView.PrepareBorrowedInteractions(_active && IsVisible && IsEnabled && !_presentationHidden);
            }

            foreach (var scope in _scopes)
                scope?.PrepareForVisibility(IsEnabled);
            if (!_active || !IsVisible || !_commits.Any(commit => commit?.IsSuspended == true))
                return;
            QueueResume();
        }
    }

    private void OnVisibility(object sender, DependencyPropertyChangedEventArgs args) => PrepareForVisibilityCore(IsVisible);
    private void OnDeactivated(object? sender, EventArgs args) => ClosePopups();

    private static IEnumerable<T> Visuals<T>(DependencyObject root) where T : DependencyObject
    {
        if (root is T match)
            yield return match;
        if (root is not Visual)
            yield break;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            foreach (var item in Visuals<T>(VisualTreeHelper.GetChild(root, i)))
                yield return item;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        if (_nativeAttached)
        {
            NativeView.PrepareBorrowedInteractions(false);
            NativeView.OnStartValueChange -= OnLeftValue;
            NativeView.OnEndValueChange -= OnRightValue;
            NativeView.OnLeftTypeChange -= OnLeftMode;
            NativeView.OnRightTypeChange -= OnRightMode;
            BindingOperations.ClearBinding(NativeView, ValueProperty(0));
            BindingOperations.ClearBinding(NativeView, ValueProperty(1));
        }

        _active = false;
        _generation++;
        _resume?.Abort();
        _resume = null;
        foreach (var state in _states)
        {
            state.Changed -= OnStateChanged;
            state.NumericModel!.Changed -= OnNumericModelChanged;
            state.NumericPeer = null;
            state.NumericCheck = null;
        }

        var owner = _owner;
        _owner = null;
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
        IsVisibleChanged -= OnVisibility;
        IsEnabledChanged -= OnEnabled;
        PreviewKeyDown -= OnKey;
        PreviewMouseWheel -= OnWheel;
        LostKeyboardFocus -= OnLostFocus;
        ParameterCleanup.Run(new Action[]
            {
                () => { if (owner is not null) owner.Deactivated -= OnDeactivated; },
                ClosePopups,
                () =>
                {
                    if (_nativeAttached)
                    {
                        NativeView.SubscribeDataLeft = null;
                        NativeView.SubscribeDataRight = null;
                    }
                },
                _expansion.Dispose
            }
            .Concat(_commits.Where(commit => commit is not null).Select(commit => (Action)commit!.Dispose))
            .Concat(_scopes.Where(scope => scope is not null).Select(scope => (Action)scope!.Dispose))
            .Concat(_references.Where(reference => reference is not null).Select(reference => (Action)reference!.Dispose))
            .Concat(new Action[] {() => Content = null}));
    }
}
