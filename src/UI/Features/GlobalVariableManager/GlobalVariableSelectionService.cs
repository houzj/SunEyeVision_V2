using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using SunEyeVision.Application;
using SunEyeVision.Domain.Solutions;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Controls.References.Variables;
using SunEyeVision.UI.Features.Dialogs;

namespace SunEyeVision.UI.Features.GlobalVariableManager;

/// <summary>Selection retains the actual document and variable instances throughout the modal interaction.</summary>
internal static class GlobalVariableSelectionService
{
    internal static GlobalVariable? Choose(DocumentSession expected, IReadOnlyCollection<GlobalVariable>? actualCandidates, Func<bool> isCurrent)
    {
        ArgumentNullException.ThrowIfNull(expected); ArgumentNullException.ThrowIfNull(isCurrent);
        if (!isCurrent()) return null;
        var actual = expected.Document.GlobalVariables.ToArray();
        var candidates = actualCandidates is null ? actual : actual.Where(v => actualCandidates.Any(c => ReferenceEquals(c, v))).ToArray();
        var definitions = GlobalVariable.CaptureDefinitions(candidates);
        var items = new List<VariableSelectionItem>();
        for (var i = 0; i < definitions.Count; i++)
        {
            var variable = definitions[i];
            if (!VariableValueDeclarations.TryGetPort(variable.Type, out var port)) continue;
            items.Add(new VariableSelectionItem(variable.Name, variable.Group, variable.Index, variable.Description,
                DisplayValue(variable.Value), new VariableReference(expected.Document.Id, variable.Id, port.Type, storage: port.Storage)));
        }
        if (items.Count == 0)
        {
            ThemeMessageDialog.Show("没有可用的全局变量；资源或组合类型尚不支持此选择入口。", "选择变量", MessageBoxButton.OK, ThemeMessageDialog.MessageIcon.Info);
            return null;
        }
        var owner = System.Windows.Application.Current?.Windows.OfType<Window>().FirstOrDefault(window => window.IsActive);
        var dialog = new VariableSelectionDialog(items) { ShowInTaskbar = false };
        if (owner is not null) { dialog.Owner = owner; dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner; }
        dialog.ShowDialog();
        var selected = dialog.IsConfirm ? dialog.SelectedVarItem : null;
        if (selected is null || !isCurrent() || selected.Reference.SolutionId != expected.Document.Id) return null;
        var index = Array.FindIndex(candidates, variable => variable.Id == selected.Reference.VariableId);
        if (index < 0 || !expected.Document.GlobalVariables.Any(variable => ReferenceEquals(variable, candidates[index])) ||
            candidates[index].Type != definitions[index].Type || !isCurrent()) return null;
        return candidates[index];
    }
    private static string DisplayValue(object? value)
    {
        if (value is Array array) return $"数组（{array.Length} 项）";
        return value is string or bool or sbyte or byte or short or ushort or int or uint or long or ulong or decimal or float or double
            ? Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty : string.Empty;
    }
}
