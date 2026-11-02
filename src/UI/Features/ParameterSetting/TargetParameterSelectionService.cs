using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using SunEyeVision.Plugin.Wpf.Controls.References.Variables;
using SunEyeVision.UI.Features.Dialogs;
using SunEyeVision.Workflow;

namespace SunEyeVision.UI.Features.ParameterSetting;

/// <summary>Adapts the original target-selection composition to document-owned candidate and edit state.</summary>
internal static class TargetParameterSelectionService
{
    internal static List<ConsumerBinding>? Choose(IEnumerable<Workflow.Workflow> workflows,
        List<ConsumerBinding>? existingBindings, string? targetTypeName, IReadOnlyCollection<ConsumerBinding> occupiedBindings)
    {
        var viewModel = new TargetParameterViewModel(workflows, existingBindings, targetTypeName);
        var dialog = new TargetParameterSelectionDialog { DataContext = viewModel, ShowInTaskbar = false };
        var owner = Application.Current?.Windows.OfType<Window>().FirstOrDefault(window => window.IsActive);
        if (owner is not null) { dialog.Owner = owner; dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner; }
        List<ConsumerBinding>? result = null;
        EventHandler accepted = (_, _) =>
        {
            var occupied = occupiedBindings.Select(binding => binding.BindingKey).ToHashSet(StringComparer.Ordinal);
            if (viewModel.ResultBindings.Any(binding => occupied.Contains(binding.BindingKey)))
            {
                ThemeMessageDialog.Show("所选目标参数已绑定其他全局变量，请先解除原绑定。", "目标参数",
                    MessageBoxButton.OK, ThemeMessageDialog.MessageIcon.Warning);
                return;
            }
            result = viewModel.ResultBindings;
            dialog.Close();
        };
        EventHandler confirm = (_, _) => { if (viewModel.SaveCommand.CanExecute(null)) viewModel.SaveCommand.Execute(null); };
        Action<object?> remove = item => { if (viewModel.RemoveConsumerCommand.CanExecute(item)) viewModel.RemoveConsumerCommand.Execute(item); };
        viewModel.RequestClose += accepted;
        dialog.ConfirmRequested += confirm;
        dialog.RemoveRequested += remove;
        try { dialog.ShowDialog(); return result; }
        finally
        {
            viewModel.RequestClose -= accepted;
            dialog.ConfirmRequested -= confirm;
            dialog.RemoveRequested -= remove;
        }
    }
}
