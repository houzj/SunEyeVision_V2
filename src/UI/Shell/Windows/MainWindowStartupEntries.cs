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
        if (dialog.ShowDialog(System.Windows.Application.Current?.MainWindow) == true) await OpenExternalSolutionAsync(dialog.FileName);
    });
    private ICommand? _reloadSolutionCommand;
    public ICommand ReloadSolutionCommand => _reloadSolutionCommand ??= new RelayCommand(async () =>
    {
        if (CanAcceptExternalActivation && DocumentManager.CurrentSession?.LastWrittenSnapshot?.TargetPath is { Length: > 0 } path)
            await OpenExternalSolutionAsync(path, reload: true);
    }, () => CanAcceptExternalActivation && !string.IsNullOrEmpty(DocumentManager.CurrentSession?.LastWrittenSnapshot?.TargetPath));
    public ICommand ShowStartPageCommand => _startPageEntryCommand ??= new RelayCommand(async () =>
    {
        if (!CanAcceptExternalActivation) return;
        try
        {
            if (System.Windows.Application.Current is App app && System.Windows.Application.Current.MainWindow is MainWindow main)
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
            await ServiceInitializer.DisposeCommunicationRuntimeAsync();
            DocumentManager.CreateNew(discardPendingEdits:true);UpdateCurrentSolutionInfo();
            if(DocumentManager.LastObserverError is {} observerError)throw new InvalidOperationException("方案已建立，但部分界面观察者未完成更新。",observerError);
            try { InitializeCommunicationAutomation(); }
            catch (Exception error) { StatusText = "方案已创建，但通讯初始化未完成：" + error.Message; }
            UpdateCurrentSolutionInfo();
            LogInfo("已创建空白工作区，首次保存时选择方案位置。");
            return true;
        }
        catch (Exception ex) { ThemeMessageDialog.Show(ex.Message, "新建方案未完成", MessageBoxButton.OK, ThemeMessageDialog.MessageIcon.Error); return false; }
        finally { EndSolutionTransition(); }
    }
}
