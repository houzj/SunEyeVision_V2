using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;

namespace SunEyeVision.Plugin.SDK.UI.Controls;

/// <summary>UI-only pending edit submission. Never reads display text to reconstruct parameters.</summary>
public static class ToolEditorCommit
{
    private static readonly List<WeakReference<ToolDebugControlBase>> Editors = new();
    internal static void Register(ToolDebugControlBase editor)
    { lock (Editors) { Editors.RemoveAll(e => !e.TryGetTarget(out var target) || ReferenceEquals(target, editor)); Editors.Add(new(editor)); } }
    internal static void Unregister(ToolDebugControlBase editor)
    { lock (Editors) Editors.RemoveAll(e => !e.TryGetTarget(out var target) || ReferenceEquals(target, editor)); }

    public static bool TryCommitOpenEditors(Func<string, bool>? includeNode, out string error)
    {
        ToolDebugControlBase[] editors;
        lock (Editors) editors = Editors.Select(e => e.TryGetTarget(out var target) ? target : null).OfType<ToolDebugControlBase>().ToArray();
        var selected = editors.Where(editor => editor.Dispatcher.CheckAccess() && editor.IsLoaded && PresentationSource.FromVisual(editor) != null &&
            (includeNode == null || editor.CurrentNode?.GetType().GetProperty("Id")?.GetValue(editor.CurrentNode) is string id && includeNode(id))).ToArray();
        foreach (var editor in selected)
            if (!TryValidate(editor, out error)) return false;
        foreach (var editor in selected)
        {
            if (!TryCommit(editor, out error)) return false;
            editor.PersistParameterBindings();
        }
        error = ""; return true;
    }

    public static bool TryCommit(FrameworkElement root, out string error)
    {
        root.Dispatcher.VerifyAccess();
        var elements = Walk(root).OfType<FrameworkElement>().ToArray();
        var bindings = elements.SelectMany(Bindings).Where(b => b.IsDirty).Distinct().ToArray();
        var groups = elements.Select(e => e.BindingGroup).OfType<BindingGroup>().Where(g => g.IsDirty).Distinct().ToArray();
        // Validate pending values before updating sources; do not cancel invalid edits.
        if (bindings.Any(b => !b.ValidateWithoutUpdate()) || groups.Any(g => !g.ValidateWithoutUpdate()))
        { error = "配置中有无效或未完成的编辑，请检查当前输入。"; return false; }
        foreach (var binding in bindings) binding.UpdateSource();
        foreach (var grid in elements.OfType<DataGrid>())
        {
            if (grid.Items is System.ComponentModel.IEditableCollectionView view && (view.IsEditingItem || view.IsAddingNew))
                if (!grid.CommitEdit(DataGridEditingUnit.Cell, true) || !grid.CommitEdit(DataGridEditingUnit.Row, true))
                { error = "表格编辑提交失败，请检查当前行。"; return false; }
        }
        foreach (var group in groups)
            if (group.IsDirty && !group.CommitEdit()) { error = "参数编辑提交失败，请检查当前输入。"; return false; }
        if (elements.Any(System.Windows.Controls.Validation.GetHasError)) { error = "参数验证失败，修改已保留，请检查当前输入。"; return false; }
        error = ""; return true;
    }

    private static bool TryValidate(FrameworkElement root, out string error)
    {
        root.Dispatcher.VerifyAccess();
        var elements = Walk(root).OfType<FrameworkElement>().ToArray();
        var bindings = elements.SelectMany(Bindings).Where(b => b.IsDirty).Distinct().ToArray();
        var groups = elements.Select(e => e.BindingGroup).OfType<BindingGroup>().Where(g => g.IsDirty).Distinct().ToArray();
        if (bindings.Any(b => !b.ValidateWithoutUpdate()) || groups.Any(g => !g.ValidateWithoutUpdate()) ||
            bindings.Length == 0 && groups.Length == 0 && elements.Any(System.Windows.Controls.Validation.GetHasError))
        { error = "配置中有无效或未完成的编辑，请检查当前输入。"; return false; }
        error = ""; return true;
    }

    private static IEnumerable<BindingExpression> Bindings(FrameworkElement element)
    {
        var properties = new HashSet<DependencyProperty> { TextBox.TextProperty, Selector.SelectedItemProperty, Selector.SelectedValueProperty, Selector.SelectedIndexProperty, ToggleButton.IsCheckedProperty };
        var locals = element.GetLocalValueEnumerator();
        while (locals.MoveNext()) properties.Add(locals.Current.Property);
        foreach (var property in properties)
            if (BindingOperations.GetBindingExpression(element, property) is { } binding && binding.ParentBinding.Mode != BindingMode.OneWay && binding.ParentBinding.Mode != BindingMode.OneTime)
                yield return binding;
    }
    private static IEnumerable<DependencyObject> Walk(DependencyObject root)
    {
        yield return root;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            foreach (var child in Walk(VisualTreeHelper.GetChild(root, i))) yield return child;
    }
}
