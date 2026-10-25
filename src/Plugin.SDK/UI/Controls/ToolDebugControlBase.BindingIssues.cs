using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using SunEyeVision.Plugin.SDK.Execution.Parameters;

namespace SunEyeVision.Plugin.SDK.UI.Controls;

public abstract partial class ToolDebugControlBase
{
    private TextBlock? _bindingIssueText;
    private ScrollViewer? _bindingIssueSummary;
    private bool _bindingIssueRefreshQueued;
    private System.Windows.Documents.AdornerDecorator? _bindingContentHost;

    /// <summary>Replace a dynamically composed editor without removing the host's issue summary.</summary>
    protected void SetEditorContent(UIElement content)
    {if(_bindingContentHost is not null)_bindingContentHost.Child=content;else Content=content;}

    private void InstallBindingIssueSummary()
    {
        if (_bindingIssueSummary != null || Content is not UIElement original) return;
        _bindingIssueText = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = Brushes.OrangeRed, Margin = new Thickness(8, 4, 8, 4) };
        _bindingIssueText.SetResourceReference(TextBlock.ForegroundProperty, Themes.SdkThemeKeys.ErrorBrush);
        _bindingIssueSummary = new ScrollViewer { Content = _bindingIssueText, MaxHeight = 100,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Visibility = Visibility.Collapsed };
        var layout = new Grid();
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        Content = null;
        layout.Children.Add(_bindingIssueSummary);
        var adornerHost = new System.Windows.Documents.AdornerDecorator { Child = original };
        _bindingContentHost=adornerHost;
        Grid.SetRow(adornerHost, 1);
        layout.Children.Add(adornerHost);
        Content = layout;
    }

    internal void QueueBindingIssueRefresh()
    {
        if (_bindingIssueRefreshQueued || Dispatcher.HasShutdownStarted) return;
        _bindingIssueRefreshQueued = true;
        Dispatcher.BeginInvoke(new Action(() =>
        {
            _bindingIssueRefreshQueued = false;
            if (IsLoaded) RefreshBindingIssues();
        }), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    internal void RefreshBindingIssues()
    {
        var parameters = CurrentNode?.GetType().GetProperty("Parameters")?.GetValue(CurrentNode) as ToolParameters
            ?? _previewParameters ?? DataContext as ToolParameters;
        if (parameters == null) return;
        var inputs = BindingInputCatalog.Describe(parameters);
        string? Issue(string key)
        {
            if (parameters.RebindingRequired.TryGetValue(key, out var required)) return "需要重新绑定：" + required;
            return parameters.BindingValidationPending.TryGetValue(key, out var pending) ? "待校验：" + pending : null;
        }
        foreach (var control in WalkBindingVisuals(this).OfType<ParamBindingBase>())
        {
            var key = control is ConfigSetting setting ? setting.ParameterName : (control as ParamBinding)?.ParameterName;
            if (key == "位置修正") key = "PositionCorrectionBindingSource";
            var input = inputs.FirstOrDefault(i => i.Key == key);
            if (input == null && control is ParamBinding binding)
            {
                var expression = binding.GetBindingExpression(ParamBinding.BindingSourceProperty);
                var owner = expression?.ResolvedSource ?? binding.DataContext;
                var members = inputs.Where(i => i.Owner != null && ReferenceEquals(i.Owner, owner)).ToArray();
                input = members.FirstOrDefault(i => i.MemberPath == expression?.ResolvedSourcePropertyName)
                    ?? (members.Length == 1 ? members[0] : null);
                if (input == null && !string.IsNullOrEmpty(binding.BindingSource))
                {
                    var candidates = inputs.Where(i => i.Read() == binding.BindingSource).ToArray();
                    if (candidates.Length == 1) input = candidates[0];
                }
            }
            var active = input?.Active ?? (key != null && BindingInputCatalog.IsInputActive(parameters, key));
            control.SetBindingIssue(active ? Issue(input?.Key ?? key ?? "") : null);
        }
        InstallBindingIssueSummary();
        if (_bindingIssueText == null || _bindingIssueSummary == null) return;
        var keys = parameters.RebindingRequired.Keys.Concat(parameters.BindingValidationPending.Keys).Distinct();
        var messages = keys.Where(k => BindingInputCatalog.IsInputActive(parameters, k))
            .Where(k => inputs.Any(i => i.Key == k) || !k.Contains(':'))
            .Select(k => $"{inputs.FirstOrDefault(i => i.Key == k)?.Name ?? k} [{k}]：{Issue(k)}").ToArray();
        _bindingIssueText.Text = string.Join("\n", messages);
        _bindingIssueSummary.Visibility = messages.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    private static IEnumerable<DependencyObject> WalkBindingVisuals(DependencyObject root)
    {
        yield return root;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            foreach (var child in WalkBindingVisuals(VisualTreeHelper.GetChild(root, i))) yield return child;
    }
}
