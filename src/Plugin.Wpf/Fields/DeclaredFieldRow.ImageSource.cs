using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Adapters.Fields;
using SunEyeVision.Plugin.Wpf.Adapters.References;
using SunEyeVision.Plugin.Wpf.Presenters.Input;

namespace SunEyeVision.Plugin.Wpf.Fields;

/// <summary>图像来源组合槽（卡外组合，沿用原 ComboBox/ListViewComboBoxStyle 逻辑）。</summary>
public sealed partial class DeclaredFieldRow
{
    public sealed record ImageSourceChoice(ParameterSetting Setting, string Label, bool Enabled = true, string? ToolTip = null);
    private Func<ReferenceCandidates>? _imageSources;
    private string _imageRefreshError => _state.SourceError;
    private Action? _detachImageSource;
    private Action? _refreshImageSource;
    private Action? _projectImageSourceSelection;
    private long _imageSourceGeneration;
    internal bool UsesImageSourceSelector => _imageSources is not null;

    private void DetachImageSource()
    {
        var detach = _detachImageSource;
        _detachImageSource = null;
        _refreshImageSource = null;
        _projectImageSourceSelection = null;
        detach?.Invoke();
    }

    /// <summary>VM EnumerationG uses ComboBox/ListViewComboBoxStyle, not a ButtonSelecter link popup.
    /// The host supplies declarations and owns validation, draft writes and execution.</summary>
    public void UseImageSourceSelector(Func<IReadOnlyList<DeclaredReferenceCandidate>> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);
        UseImageSourceSelector(() => new ReferenceCandidates(sources(), Array.Empty<DeclaredReferenceCandidate>()));
    }

    internal void UseImageSourceSelector(Func<ReferenceCandidates> sources)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_port.Type != TypeRef.ResourceOf(ResourceKind.Image) || _port.EditModes != FieldEditModes.Reference)
            throw new ArgumentException("An image source selector requires a declared image reference input.");
        ArgumentNullException.ThrowIfNull(sources);
        var generation = ++_imageSourceGeneration;
        DetachImageSource();
        if (_disposed || generation != _imageSourceGeneration)
            return;
        _imageSources = sources;
        var slot = CreateImageSourceSlot();
        // Candidate providers can reenter the host and replace or dispose the
        // row. Never install the retired selector after that callback returns.
        if (_disposed || generation != _imageSourceGeneration)
            return;
        _slotContent = slot;
        if (_model.TemplateKey != DeclaredFieldTemplateRegistry.ImageSource)
        {
            _model.TemplateKey = DeclaredFieldTemplateRegistry.ImageSource;
            ContentTemplateSelector = null;
            if (_disposed || generation != _imageSourceGeneration)
                return;
        }
        ContentTemplateSelector = ParameterRowTemplateSelector.Instance;
        if (!_disposed && generation == _imageSourceGeneration)
            _model.SetImageSourceSelector(slot);
    }

    private UIElement CreateImageSourceSlot()
    {
        var generation = _imageSourceGeneration;
        var retired = false;
        var sources = _imageSources!;
        var combo = NativeInputFactory.Enumeration();
        var refreshing = false;
        var candidatesValid = false;
        ParameterSetting? projected = null;
        long candidateRevision = 0;
        combo.DisplayMemberPath = nameof(ImageSourceChoice.Label);
        combo.SelectedValuePath = nameof(ImageSourceChoice.Setting);
        combo.SetBinding(FrameworkElement.ToolTipProperty, new Binding("SelectedItem.Label") { Source = combo });
        var itemStyle = new Style(typeof(ComboBoxItem), combo.ItemContainerStyle);
        itemStyle.Setters.Add(new Setter(UIElement.IsEnabledProperty, new Binding(nameof(ImageSourceChoice.Enabled))));
        itemStyle.Setters.Add(new Setter(FrameworkElement.ToolTipProperty, new Binding(nameof(ImageSourceChoice.ToolTip))));
        itemStyle.Setters.Add(new EventSetter(UIElement.PreviewMouseLeftButtonDownEvent, new MouseButtonEventHandler(OnItemActivated)));
        combo.ItemContainerStyle = itemStyle;
        _host.AttachEnumeration(combo);

        bool Current() => !retired && !_disposed && generation == _imageSourceGeneration;
        void ChangeRefreshError(string value)
        {
            if (!Current() || _imageRefreshError == value)
                return;
            _state.SetSourceError(value);
        }

        bool MatchesSetting(ParameterSetting? setting) => (setting, _setting) switch
        {
            (ReferenceSetting selected, ReferenceSetting current) => SameImageReference(selected.Reference, current.Reference),
            (VariableSetting selected, VariableSetting current) => selected.Reference.SolutionId == current.Reference.SolutionId
                && selected.Reference.VariableId == current.Reference.VariableId && selected.Reference.Type == current.Reference.Type,
            (UnsetSetting, UnsetSetting) => true,
            _ => false
        };
        ImageSourceChoice? FindSelection() => combo.Items.OfType<ImageSourceChoice>().FirstOrDefault(item => MatchesSetting(item.Setting));
        void RestoreSelection()
        {
            if (!Current())
                return;
            refreshing = true;
            try
            {
                combo.SelectedItem = FindSelection();
            }
            finally
            {
                refreshing = false;
            }
        }

        void ProjectAcceptedSelection()
        {
            if(!Current()||refreshing||projected is not null&&DeclaredFieldState.SameSetting(projected,_setting))return;
            projected=_setting;
            var match=FindSelection();
            if(match is null&&_setting is (ReferenceSetting or VariableSetting))
            {
                Refresh();
                return;
            }
            RestoreSelection();
            if(candidatesValid)
                ChangeRefreshError(match is {Enabled:false}?"当前来源不在可选图像声明中，请重新选择":"");
        }

        void Refresh()
        {
            if (!Current() || refreshing)
                return;
            var revision = ++candidateRevision;
            candidatesValid = false;
            bool Active() => Current() && revision == candidateRevision;
            try
            {
                // 先构建完整候选快照，再替换已接受的显示。
                var items = new List<ImageSourceChoice>();
                if (!_port.Required)
                {
                    // 空项显示沿原件 Holder.NullName（RunParam_None），zh-cn "无"/en-us "None"。
                    var noneLabel = Native.Common.Localization.AppLocalizationService.GetLang("RunParam_None", "无");
                    items.Add(new(new UnsetSetting(), noneLabel, ToolTip: "清除图像来源"));
                }
                var candidates = sources() ?? throw new InvalidOperationException("图像来源函数返回了空列表。");
                if (!Active())
                    return;
                foreach (var candidate in candidates.Sources)
                {
                    if (!Active())
                        return;
                    // 标量图像输入不能静默取序列首图。
                    if (candidate.Output.Type.Shape == ValueShape.Sequence || candidate.SelectedType != _port.Type)
                        continue;
                    var reference = new ReferenceSetting(candidate.Reference(BindingSelection.All));
                    if (items.Any(item => item.Setting is ReferenceSetting prior && SameImageReference(prior.Reference, reference.Reference)))
                        continue;
                    items.Add(new(reference, candidate.Label));
                }
                foreach (var candidate in candidates.Variables)
                {
                    if (!Active()) return;
                    if (candidate.Reference.Type == _port.Type)
                        items.Add(new(new VariableSetting(candidate.Reference), "变量 · " + candidate.Name));
                }

                var accepted = _setting;
                var selected = items.FirstOrDefault(item => MatchesSetting(item.Setting));
                if (selected is null && _setting is ReferenceSetting retained)
                {
                    // 保留被移除/变更的来源供检视；刷新绝不改写其身份或草稿。
                    selected = new(retained,
                        _formatReference?.Invoke(retained.Reference) ?? retained.Reference.NodeId + " · " + retained.Reference.PortId,
                        Enabled: false, ToolTip: "当前来源不在可选图像声明中，请重新选择");
                    items.Add(selected);
                }
                if (selected is null && _setting is VariableSetting retainedVariable)
                {
                    selected = new(retainedVariable, "变量 · " + retainedVariable.Reference.VariableId,
                        Enabled: false, ToolTip: "当前变量不在可选图像声明中，请重新选择");
                    items.Add(selected);
                }

                if (!Active())
                    return;
                refreshing = true;
                try
                {
                    var unchanged = combo.ItemsSource is IReadOnlyList<ImageSourceChoice> previous && previous.Count == items.Count
                        && previous.Zip(items).All(pair => DeclaredFieldState.SameSetting(pair.First.Setting, pair.Second.Setting)
                            && pair.First.Label == pair.Second.Label && pair.First.ToolTip == pair.Second.ToolTip
                            && pair.First.Enabled == pair.Second.Enabled);
                    if (!unchanged)
                        combo.ItemsSource = items.AsReadOnly();

                    if (!Active())
                        return;
                    combo.SelectedItem = FindSelection();
                }
                finally
                {
                    refreshing = false;
                }

                if (!Active())
                    return;
                candidatesValid = true;
                projected = accepted;
                if(!DeclaredFieldState.SameSetting(accepted,_setting))
                    ProjectAcceptedSelection();
                else
                    ChangeRefreshError(selected is {Enabled:false}?"当前来源不在可选图像声明中，请重新选择":"");
            }
            catch (Exception error) when (error is ArgumentException or InvalidOperationException)
            {
                if (Active())
                    ChangeRefreshError(error.Message);
            }
        }

        void OnOpened(object? sender, EventArgs args) => Refresh();
        void OnSelectionChanged(object sender, SelectionChangedEventArgs args)
        {
            if (!Current() || !_presentationVisible || !IsEnabled || !combo.IsLoaded || !combo.IsVisible || !combo.IsEnabled || refreshing
                || combo.SelectedItem is not ImageSourceChoice {Enabled: true, Setting: var selected})
                return;
            if (!candidatesValid)
            {
                RestoreSelection();
                return;
            }

            var revision = candidateRevision;
            if (!Set(selected) && Current() && revision == candidateRevision)
                RestoreSelection();
        }

        bool ConfirmCurrent(ImageSourceChoice item)
        {
            if (!Current() || !candidatesValid || refreshing || !combo.IsDropDownOpen || !_presentationVisible || !IsEnabled
                || !combo.IsLoaded || !combo.IsVisible || !combo.IsEnabled
                || !item.Enabled || !ReferenceEquals(combo.SelectedItem, item) || !MatchesSetting(item.Setting)
                || _setting is not (ReferenceSetting or VariableSetting))
                return false;
            var needsConfirmation = _setting is ReferenceSetting current
                ? current.OriginId is not null || !string.IsNullOrEmpty(current.RebindingReason)
                : !string.IsNullOrEmpty(((VariableSetting)_setting).RebindingReason);
            if (!needsConfirmation && _state.EditError.Length == 0) return false;
            var revision = candidateRevision;
            var accepted = Set(item.Setting);
            if (Current() && revision == candidateRevision)
            {
                if (!accepted)
                    RestoreSelection();
                else
                    combo.SetCurrentValue(ComboBox.IsDropDownOpenProperty, false);
            }

            return true;
        }

        void OnItemActivated(object sender, MouseButtonEventArgs args)
        {
            if (sender is ComboBoxItem {Content: ImageSourceChoice item} && ConfirmCurrent(item))
                args.Handled = true;
        }

        void OnKeyDown(object sender, KeyEventArgs args)
        {
            if (args.Key == Key.Enter && combo.IsDropDownOpen && combo.SelectedItem is ImageSourceChoice item && ConfirmCurrent(item))
                args.Handled = true;
        }

        void RestoreWhenActive()
        {
            if (Current() && _presentationVisible && combo.IsLoaded && combo.IsVisible && combo.IsEnabled)
                RestoreSelection();
        }

        void OnLoaded(object sender, RoutedEventArgs args) => RestoreWhenActive();
        void OnActivationChanged(object sender, DependencyPropertyChangedEventArgs args)
        {
            if (!Current())
                return;
            if (!combo.IsEnabled || !combo.IsVisible)
                combo.SetCurrentValue(ComboBox.IsDropDownOpenProperty, false);
            else
                RestoreWhenActive();
        }

        void OnUnloaded(object sender, RoutedEventArgs args) => combo.SetCurrentValue(ComboBox.IsDropDownOpenProperty, false);
        combo.DropDownOpened += OnOpened;
        combo.SelectionChanged += OnSelectionChanged;
        combo.Unloaded += OnUnloaded;
        combo.Loaded += OnLoaded;
        combo.IsEnabledChanged += OnActivationChanged;
        combo.IsVisibleChanged += OnActivationChanged;
        combo.PreviewKeyDown += OnKeyDown;
        _detachImageSource = () =>
        {
            retired = true;
            combo.DropDownOpened -= OnOpened;
            combo.SelectionChanged -= OnSelectionChanged;
            combo.Unloaded -= OnUnloaded;
            combo.Loaded -= OnLoaded;
            combo.IsEnabledChanged -= OnActivationChanged;
            combo.IsVisibleChanged -= OnActivationChanged;
            combo.PreviewKeyDown -= OnKeyDown;
            combo.SetCurrentValue(ComboBox.IsDropDownOpenProperty, false);
            combo.ItemsSource = null;
            combo.ClearValue(ItemsControl.ItemContainerStyleProperty);
            NativeInputFactory.ReleaseEnumeration(combo);
        };
        _refreshImageSource = Refresh;
        _projectImageSourceSelection = ProjectAcceptedSelection;
        Refresh();
        return combo;
    }

    private static bool SameImageReference(OutputReference left, OutputReference right) => left.NodeId == right.NodeId
        && left.ScopeId == right.ScopeId && left.PortId == right.PortId && left.Type == right.Type && left.Storage == right.Storage
        && left.MemberFieldPath.SequenceEqual(right.MemberFieldPath) && Equals(left.Selection, right.Selection);
}
