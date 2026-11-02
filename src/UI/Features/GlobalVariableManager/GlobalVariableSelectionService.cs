using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Controls.References.Variables;
using SunEyeVision.UI.Features.Dialogs;
using SunEyeVision.Workflow;

namespace SunEyeVision.UI.Features.GlobalVariableManager;

/// <summary>Connects the shared variable selector to the current document's identity and candidate snapshot.</summary>
internal static class GlobalVariableSelectionService
{
    internal static GlobalVariable? Choose(SolutionManager manager, IReadOnlyCollection<GlobalVariable>? candidates = null, string? filterType = null)
    {
        ArgumentNullException.ThrowIfNull(manager);
        var session = manager.Session;
        if (session is null || !ReferenceEquals(manager.CurrentSolution, session.Document)) return null;
        var snapshot = session.Editing.Host.CaptureVariablesForSelection();
        var permitted = candidates?.Select(variable => variable.Id).ToHashSet(StringComparer.Ordinal);
        var items = new List<VariableSelectionItem>();
        foreach (var variable in snapshot.Variables)
        {
            if (permitted is not null && !permitted.Contains(variable.Id)) continue;
            if (!string.IsNullOrEmpty(filterType) && !string.Equals(variable.Type, filterType, StringComparison.OrdinalIgnoreCase)) continue;
            if (!GlobalProducerBindingRules.TryDescribeTarget(variable.Type, out var port, out _, out _)) continue;
            items.Add(new VariableSelectionItem(variable.Name, variable.Group, variable.Index, variable.Description,
                DisplayValue(variable.Value), new VariableReference(snapshot.SolutionId, variable.Id, port.Type, storage: port.Storage)));
        }
        if (items.Count == 0)
        {
            ThemeMessageDialog.Show("没有可用的全局变量；资源或组合类型尚不支持此选择入口。", "选择变量",
                MessageBoxButton.OK, ThemeMessageDialog.MessageIcon.Info);
            return null;
        }

        var owner = Application.Current?.Windows.OfType<Window>().FirstOrDefault(window => window.IsActive);
        var dialog = new VariableSelectionDialog(items) { ShowInTaskbar = false };
        if (owner is not null) { dialog.Owner = owner; dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner; }
        dialog.ShowDialog();
        var selected = dialog.IsConfirm ? dialog.SelectedVarItem : null;
        if (selected is null || !ReferenceEquals(manager.Session, session) || !ReferenceEquals(manager.CurrentSolution, session.Document)
            || !string.Equals(snapshot.SolutionId, session.Document.Id, StringComparison.Ordinal)) return null;
        var current = session.Document.GlobalVariables.Where(variable => variable.Id == selected.Reference.VariableId).ToArray();
        var original = snapshot.Variables.SingleOrDefault(variable => variable.Id == selected.Reference.VariableId);
        if (current.Length != 1 || original is null || !string.Equals(current[0].Type, original.Type, StringComparison.Ordinal)
            || permitted is not null && !permitted.Contains(current[0].Id)) return null;
        return current[0];
    }

    private static string DisplayValue(object? value)
    {
        if (value is Array array) value = array.Rank == 1 && array.Length > 0 ? array.GetValue(0) : null;
        return value is string or bool or sbyte or byte or short or ushort or int or uint or long or ulong or decimal or float or double
            ? Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty : string.Empty;
    }
}
