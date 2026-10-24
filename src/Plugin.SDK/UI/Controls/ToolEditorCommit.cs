using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;

namespace SunEyeVision.Plugin.SDK.UI.Controls;

/// <summary>可选的专属编辑事务；未实现此接口的工具保持原有提交规则。</summary>
public interface IToolEditorCommitParticipant
{
    bool TryCommitEditor(out string error);
    bool TryValidateEditor(out string error);
}

/// <summary>Optional UI host draft; its implementation owns publication and document scope.</summary>
public interface IToolEditorDraftContext : IToolEditorCommitParticipant
{
    object FormalNode { get; }
    void Discard();
}

/// <summary>UI-only pending edit submission. Never reads display text to reconstruct parameters.</summary>
public static class ToolEditorCommit
{
    private static readonly List<WeakReference<ToolDebugControlBase>> Editors = new();
    private static readonly DependencyProperty DraftContextProperty = DependencyProperty.RegisterAttached(
        "DraftContext", typeof(IToolEditorDraftContext), typeof(ToolEditorCommit));
    public static void AttachDraftContext(FrameworkElement editor, IToolEditorDraftContext context)
    { editor.Dispatcher.VerifyAccess(); editor.SetValue(DraftContextProperty, context ?? throw new ArgumentNullException(nameof(context))); }
    public static IToolEditorDraftContext? GetDraftContext(FrameworkElement editor) => editor.GetValue(DraftContextProperty) as IToolEditorDraftContext;
    private static object? FormalNode(ToolDebugControlBase editor) => GetDraftContext(editor)?.FormalNode ?? editor.CurrentNode;
    /// <summary>Cancel pending control edits only. Already live-written parameters are not rolled back.</summary>
    public static void DiscardPendingEdits(FrameworkElement root)
    {
        root.Dispatcher.VerifyAccess();
        var elements = Walk(root).OfType<FrameworkElement>().ToArray();
        GetDraftContext(root)?.Discard();
        foreach (var numeric in elements.OfType<NumericUpDown>()) numeric.DiscardPendingEdit();
        foreach (var editor in elements.OfType<ToolDebugControlBase>()) editor.SuppressClosePersistence = true;
        foreach (var grid in elements.OfType<DataGrid>())
        { grid.CancelEdit(DataGridEditingUnit.Cell); grid.CancelEdit(DataGridEditingUnit.Row); }
        foreach (var group in elements.Select(e => e.BindingGroup).OfType<BindingGroup>().Distinct()) group.CancelEdit();
        foreach (var binding in elements.SelectMany(Bindings).Distinct())
        { binding.UpdateTarget(); System.Windows.Controls.Validation.ClearInvalid(binding); }
    }
    internal static void Register(ToolDebugControlBase editor)
    { lock (Editors) { Editors.RemoveAll(e => !e.TryGetTarget(out var target) || ReferenceEquals(target, editor)); Editors.Add(new(editor)); } }
    internal static void Unregister(ToolDebugControlBase editor)
    { lock (Editors) Editors.RemoveAll(e => !e.TryGetTarget(out var target) || ReferenceEquals(target, editor)); }

    public static void RefreshCommittedBindings()
    {
        ToolDebugControlBase[] editors;
        lock (Editors) editors = Editors.Select(e => e.TryGetTarget(out var target) ? target : null).OfType<ToolDebugControlBase>().ToArray();
        foreach (var editor in editors.Where(e => e.Dispatcher.CheckAccess() && e.IsLoaded))
        {
            editor.RefreshCommittedBindings(onlyInvalidated: true);
        }
    }

    /// <summary>Validate every matching open editor before any save-side source updates.</summary>
    public static bool TryValidateOpenEditors(Func<string, bool>? includeNode, out string error) => TryValidateOpenEditors(includeNode, out error, null);
    public static bool TryValidateOpenEditors(Func<string, bool>? includeNode, out string error, Func<object, bool>? ownsNode)
    {
        ToolDebugControlBase[] editors;
        lock (Editors) editors = Editors.Select(e => e.TryGetTarget(out var target) ? target : null).OfType<ToolDebugControlBase>().ToArray();
        foreach (var editor in editors.Where(editor => editor.Dispatcher.CheckAccess() && editor.IsLoaded && PresentationSource.FromVisual(editor) != null &&
            (includeNode == null || editor.CurrentNode?.GetType().GetProperty("Id")?.GetValue(editor.CurrentNode) is string id && includeNode(id)) &&
            (ownsNode == null || FormalNode(editor) is { } formalNode && ownsNode(formalNode))))
            if (!TryValidate(editor, out error)) return false;
        error = ""; return true;
    }

    public static bool TryCommitOpenEditors(Func<string, bool>? includeNode, out string error) => TryCommitOpenEditors(includeNode, out error, null);
    public static bool TryCommitOpenEditors(Func<string, bool>? includeNode, out string error, Func<object, bool>? ownsNode)
    {
        ToolDebugControlBase[] editors;
        lock (Editors) editors = Editors.Select(e => e.TryGetTarget(out var target) ? target : null).OfType<ToolDebugControlBase>().ToArray();
        var selected = editors.Where(editor => editor.Dispatcher.CheckAccess() && editor.IsLoaded && PresentationSource.FromVisual(editor) != null &&
            (includeNode == null || editor.CurrentNode?.GetType().GetProperty("Id")?.GetValue(editor.CurrentNode) is string id && includeNode(id)) &&
            (ownsNode == null || FormalNode(editor) is { } formalNode && ownsNode(formalNode))).ToArray();
        foreach (var editor in selected)
            if (!TryValidate(editor, out error)) return false;
        foreach (var editor in selected)
        {
            if (!TryCommit(editor, out error)) return false;
            if (GetDraftContext(editor) is null) editor.PersistParameterBindings();
        }
        error = ""; return true;
    }

    public static bool TryCommit(FrameworkElement root, out string error)
    {
        if (!TryFlush(root, out error)) return false;
        if (root is ToolDebugControlBase control)
        {
            control.PersistParameterBindings(); // CurrentNode is the private draft for production contexts.
            if (!control.ValidateParameters()) { error = "参数验证失败，请检查输入参数。"; return false; }
        }
        if (GetDraftContext(root) is { } context)
        {
            if (!context.TryValidateEditor(out error)) return false;
            return context.TryCommitEditor(out error); // Never call the legacy live query callback.
        }
        if (root is IToolEditorCommitParticipant participant && !participant.TryCommitEditor(out error)) return false;
        if (root is ToolDebugControlBase editor && editor.DataProvider is SunEyeVision.Plugin.SDK.Execution.Parameters.DataSourceQueryService query &&
            editor.CurrentNode?.GetType().GetProperty("Id")?.GetValue(editor.CurrentNode) is string nodeId)
        {
            try { query.CommitOutputConfiguration?.Invoke(nodeId); }
            catch (Exception ex) { error = "输出配置校验失败：" + ex.Message; return false; }
        }
        error = ""; return true;
    }

    public static bool TryValidate(FrameworkElement root, out string error)
    {
        root.Dispatcher.VerifyAccess();
        if (GetDraftContext(root) is { } context)
        {
            // Flushing is safe here: every source belongs to the private editor draft.
            if (!TryFlush(root, out error)) return false;
            if (root is ToolDebugControlBase editor)
            {
                editor.PersistParameterBindings();
                if (!editor.ValidateParameters()) { error = "参数验证失败，请检查输入参数。"; return false; }
            }
            return context.TryValidateEditor(out error);
        }
        var elements = Walk(root).OfType<FrameworkElement>().ToArray();
        foreach (var numeric in elements.OfType<NumericUpDown>())
            if (!numeric.TryValidatePendingEdit(out error)) return false;
        var bindings = elements.SelectMany(Bindings).Where(b => b.IsDirty).Distinct().ToArray();
        var groups = elements.Select(e => e.BindingGroup).OfType<BindingGroup>().Where(g => g.IsDirty).Distinct().ToArray();
        if (bindings.Any(b => !b.ValidateWithoutUpdate()) || groups.Any(g => !g.ValidateWithoutUpdate()) ||
            bindings.Length == 0 && groups.Length == 0 && elements.Any(System.Windows.Controls.Validation.GetHasError))
        { error = "配置中有无效或未完成的编辑，请检查当前输入。"; return false; }
        if (root is IToolEditorCommitParticipant participant && !participant.TryValidateEditor(out error)) return false;
        error = ""; return true;
    }

    private static bool TryFlush(FrameworkElement root, out string error)
    {
        root.Dispatcher.VerifyAccess();
        var elements = Walk(root).OfType<FrameworkElement>().ToArray();
        var numerics = elements.OfType<NumericUpDown>().ToArray();
        foreach (var numeric in numerics) if (!numeric.TryValidatePendingEdit(out error)) return false;
        var bindings = elements.SelectMany(Bindings).Where(b => b.IsDirty).Distinct().ToArray();
        var groups = elements.Select(e => e.BindingGroup).OfType<BindingGroup>().Where(g => g.IsDirty).Distinct().ToArray();
        if (bindings.Any(b => !b.ValidateWithoutUpdate()) || groups.Any(g => !g.ValidateWithoutUpdate()))
        { error = "配置中有无效或未完成的编辑，请检查当前输入。"; return false; }
        foreach (var numeric in numerics) if (!numeric.TryCommitPendingEdit(out error)) return false;
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
