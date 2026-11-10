using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Adapters.References;
using SunEyeVision.Plugin.Wpf.Presenters.Input;

namespace SunEyeVision.Plugin.Wpf.Fields;

/// <summary>图像来源组合槽（卡外组合，沿用原 ComboBox/ListViewComboBoxStyle 逻辑）。</summary>
public sealed partial class DeclaredFieldRow
{
    private Func<IReadOnlyList<DeclaredReferenceCandidate>>? _imageSources;
    private string _imageRefreshError => _state.SourceError;
    private Action? _detachImageSource;

    private void DetachImageSource()
    {
        var detach = _detachImageSource;
        _detachImageSource = null;
        detach?.Invoke();
    }

    /// <summary>VM EnumerationG uses ComboBox/ListViewComboBoxStyle, not a ButtonSelecter link popup.
    /// The host supplies declarations and owns validation, draft writes and execution.</summary>
    public void UseImageSourceSelector(Func<IReadOnlyList<DeclaredReferenceCandidate>> sources)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_port.Type != TypeRef.ResourceOf(ResourceKind.Image) || _port.EditModes != FieldEditModes.Reference)
            throw new ArgumentException("An image source selector requires a declared image reference input.");
        _imageSources = sources ?? throw new ArgumentNullException(nameof(sources));
        _slotContent = CreateImageSourceSlot();
        Content = _slotContent;
        ContentTemplateSelector = null;
    }

    private UIElement CreateImageSourceSlot()
    {
        var combo = NativeInputFactory.Enumeration();
        _host.AttachEnumeration(combo);
        var refreshing = false;
        var candidatesValid = false;
        long candidateRevision = 0;
        var activationItems = new List<ComboBoxItem>();
        void DetachItems()
        {
            foreach (var item in activationItems)
                item.PreviewMouseLeftButtonDown -= OnItemActivated;
            activationItems.Clear();
        }

        bool Current() => !_disposed;
        void ChangeRefreshError(string value)
        {
            if (!Current() || _imageRefreshError == value)
                return;
            _state.SetSourceError(value);
        }

        bool Matches(ComboBoxItem item) => item.Tag is ReferenceSetting selected && _setting is ReferenceSetting current
            ? SameImageReference(selected.Reference, current.Reference) : item.Tag is UnsetSetting && _setting is UnsetSetting;
        void RestoreSelection()
        {
            if (!Current())
                return;
            refreshing = true;
            try
            {
                combo.SelectedItem = combo.Items.OfType<ComboBoxItem>().FirstOrDefault(Matches);
            }
            finally
            {
                refreshing = false;
            }
        }

        void Refresh()
        {
            if (!Current())
                return;
            var revision = ++candidateRevision;
            candidatesValid = false;
            bool Active() => Current() && revision == candidateRevision;
            try
            {
                // 先构建完整候选快照，再替换已接受的显示。
                var items = new List<ComboBoxItem>();
                if (!_port.Required)
                    items.Add(new ComboBoxItem {Content = "", Tag = new UnsetSetting(), ToolTip = "清除图像来源"});
                var candidates = _imageSources!() ?? throw new InvalidOperationException("图像来源函数返回了空列表。");
                if (!Active())
                    return;
                foreach (var candidate in candidates)
                {
                    if (!Active())
                        return;
                    // 标量图像输入不能静默取序列首图。
                    if (candidate.Output.Type.Shape == ValueShape.Sequence || candidate.SelectedType != _port.Type)
                        continue;
                    var reference = new ReferenceSetting(candidate.Reference(BindingSelection.All));
                    if (items.Any(item => item.Tag is ReferenceSetting prior && SameImageReference(prior.Reference, reference.Reference)))
                        continue;
                    items.Add(new ComboBoxItem {Content = candidate.Label, Tag = reference});
                }

                var selected = items.FirstOrDefault(Matches);
                var retainedUnavailable = false;
                if (selected is null && _setting is ReferenceSetting retained)
                {
                    // 保留被移除/变更的来源供检视；刷新绝不改写其身份或草稿。
                    retainedUnavailable = true;
                    selected = new ComboBoxItem
                    {
                        Content = _formatReference?.Invoke(retained.Reference) ?? retained.Reference.NodeId + " · " + retained.Reference.PortId,
                        Tag = retained, IsEnabled = false, ToolTip = "当前来源不在可选图像声明中，请重新选择"
                    };
                    items.Add(selected);
                }

                if (!Active())
                    return;
                refreshing = true;
                try
                {
                    DetachItems();
                    combo.Items.Clear();
                    if (!Active())
                        return;
                    foreach (var item in items)
                    {
                        item.PreviewMouseLeftButtonDown += OnItemActivated;
                        activationItems.Add(item);
                        combo.Items.Add(item);
                        if (!Active())
                            return;
                    }

                    if (!Active())
                        return;
                    combo.SelectedItem = selected;
                }
                finally
                {
                    refreshing = false;
                }

                if (!Active())
                    return;
                candidatesValid = true;
                ChangeRefreshError(retainedUnavailable ? "当前来源不在可选图像声明中，请重新选择" : "");
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
                || combo.SelectedItem is not ComboBoxItem {IsEnabled: true, Tag: ParameterSetting selected})
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

        bool ConfirmCurrent(ComboBoxItem item)
        {
            if (!Current() || !candidatesValid || refreshing || !combo.IsDropDownOpen || !_presentationVisible || !IsEnabled
                || !combo.IsLoaded || !combo.IsVisible || !combo.IsEnabled
                || !item.IsEnabled || !ReferenceEquals(combo.SelectedItem, item) || item.Tag is not ReferenceSetting selected
                || _setting is not ReferenceSetting current || !SameImageReference(selected.Reference, current.Reference)
                || current.OriginId is null && string.IsNullOrEmpty(current.RebindingReason) && _state.EditError.Length == 0)
                return false;
            var revision = candidateRevision;
            var accepted = Set(selected);
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
            if (sender is ComboBoxItem item && ConfirmCurrent(item))
                args.Handled = true;
        }

        void OnKeyDown(object sender, KeyEventArgs args)
        {
            if (args.Key == Key.Enter && combo.IsDropDownOpen && combo.SelectedItem is ComboBoxItem item && ConfirmCurrent(item))
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
            combo.DropDownOpened -= OnOpened;
            combo.SelectionChanged -= OnSelectionChanged;
            combo.Unloaded -= OnUnloaded;
            combo.Loaded -= OnLoaded;
            combo.IsEnabledChanged -= OnActivationChanged;
            combo.IsVisibleChanged -= OnActivationChanged;
            combo.PreviewKeyDown -= OnKeyDown;
            DetachItems();
            combo.SetCurrentValue(ComboBox.IsDropDownOpenProperty, false);
            NativeInputFactory.ReleaseEnumeration(combo);
        };
        Refresh();
        return combo;
    }

    private static bool SameImageReference(OutputReference left, OutputReference right) => left.NodeId == right.NodeId
        && left.ScopeId == right.ScopeId && left.PortId == right.PortId && left.Type == right.Type && left.Storage == right.Storage
        && left.MemberFieldPath.SequenceEqual(right.MemberFieldPath) && Equals(left.Selection, right.Selection);
}
