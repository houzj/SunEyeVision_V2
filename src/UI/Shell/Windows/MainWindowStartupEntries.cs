using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using SunEyeVision.UI.Core;
using SunEyeVision.UI.Features.Dialogs;
using SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters;

namespace SunEyeVision.UI.Shell.Windows;
public partial class MainWindowViewModel
{
    private ICommand? _newSolutionEntryCommand, _openSolutionEntryCommand, _startPageEntryCommand;
    public ICommand NewSolutionCommand => _newSolutionEntryCommand ??= new RelayCommand(async () => await NewEmptySolutionAsync());
    public ICommand OpenSolutionCommand => _openSolutionEntryCommand ??= new RelayCommand(async () =>
    {
        if (!CanAcceptExternalActivation) return;
        var dialog = new OpenFileDialog { Filter = "方案文件 (*.solution)|*.solution", CheckFileExists = true, Title = "打开方案" };
        if (dialog.ShowDialog(Application.Current?.MainWindow) == true) await OpenExternalSolutionAsync(dialog.FileName);
    });
    public ICommand ShowStartPageCommand => _startPageEntryCommand ??= new RelayCommand(async () =>
    {
        if (!CanAcceptExternalActivation) return;
        try
        {
            if (Application.Current is App app && Application.Current.MainWindow is MainWindow main)
                await app.ShowStartPageAsync(main);
        }
        catch (Exception ex) { ThemeMessageDialog.Show(ex.Message, "起始页", MessageBoxButton.OK, ThemeMessageDialog.MessageIcon.Error); }
    });
    internal async Task<bool> NewEmptySolutionAsync()
    {
        if (!CanAcceptExternalActivation) return false;
        try
        {
            if (!await PrepareSolutionTransitionAsync()) return false;
            ServiceInitializer.SolutionManager.CloseSolution(saveChanges: false);
            _isWorkflowModified = false;
            Title = "太阳眼视觉 - 未命名方案";
            LogInfo("已创建空白工作区，首次保存时选择方案位置。");
            return true;
        }
        catch (Exception ex) { ThemeMessageDialog.Show(ex.Message, "新建方案未完成", MessageBoxButton.OK, ThemeMessageDialog.MessageIcon.Error); return false; }
        finally { EndSolutionTransition(); }
    }
}
