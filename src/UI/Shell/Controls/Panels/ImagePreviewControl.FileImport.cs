using SunEyeVision.UI.Shared;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using SunEyeVision.UI.Services.Thumbnail;

namespace SunEyeVision.UI.Shell.Controls.Panels;

public partial class ImagePreviewControl
{
    private CancellationTokenSource? _fileImportCancellation;
    private long _fileImportGeneration;
    private bool _isImporting;
    private string _importStatus = string.Empty;
    public string ImportStatus
    {
        get => _importStatus;
        private set { _importStatus = value; OnPropertyChanged(nameof(ImportStatus)); }
    }
    public bool IsImporting
    {
        get => _isImporting;
        private set { _isImporting = value; OnPropertyChanged(nameof(IsImporting)); CommandManager.InvalidateRequerySuggested(); }
    }
    public ICommand CancelImportCommand { get; private set; } = null!;

    private void CancelFileImport()
    {
        if (IsImporting) ImportStatus = "导入已取消；已正式接纳的引用不回滚";
        _fileImportGeneration++;
        _fileImportCancellation?.Cancel();
        _fileImportCancellation = null;
        IsImporting = false;
    }

    internal Task ImportFilePathsAsync(string[] paths) => ImportAsync(token => ImageFileImport.ExistingFilesAsync(paths, token));
    internal Task ImportDirectoryAsync(string directory) => ImportAsync(token => ImageFileImport.DirectoryFilesAsync(directory, token));

    private async Task ImportAsync(Func<CancellationToken, Task<string[]>> enumerate)
    {
        var source = ActiveInputSource ?? throw new InvalidOperationException("请先选择本地图像来源。");
        if (source.IsReadOnly) throw new InvalidOperationException("图像来源已锁定，无法导入。");
        CancelFileImport();
        using var cancellation = new CancellationTokenSource();
        _fileImportCancellation = cancellation;
        var generation = _fileImportGeneration;
        IsImporting = true;
        ImportStatus = "正在读取文件列表…";
        try
        {
            var files = await enumerate(cancellation.Token).ConfigureAwait(false);
            cancellation.Token.ThrowIfCancellationRequested();
            // Build file references off the dispatcher; no image decode or live collection mutation.
            var prepared=await Task.Run(()=>files.Select(path=>
            {
                cancellation.Token.ThrowIfCancellationRequested();
                return new ImageInfo{FilePath=path,Name=Path.GetFileNameWithoutExtension(path)};
            }).ToArray(),cancellation.Token).ConfigureAwait(false);
            await Dispatcher.InvokeAsync(()=>
            {
                if(cancellation.IsCancellationRequested||generation!=_fileImportGeneration||
                    !ReferenceEquals(ActiveInputSource,source)||source.IsReadOnly)return;
                var existing=source.Images.Select(image=>image.FilePath).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var additions=prepared.Where(image=>existing.Add(image.FilePath)).ToArray();
                if(additions.Length==0){ImportStatus="没有找到新的可导入图像文件";return;}
                // One user action, one formal acceptance; initial selection belongs to that same edit.
                source.AddImages(additions,selectFirstAdded:true);
                if(cancellation.IsCancellationRequested||generation!=_fileImportGeneration||
                    !ReferenceEquals(ActiveInputSource,source))return;
                ImportStatus=$"已导入 {additions.Length} 个图像引用";
            },DispatcherPriority.Background,cancellation.Token);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        finally
        {
            if (!Dispatcher.HasShutdownStarted) await Dispatcher.InvokeAsync(() =>
            {
                if (generation != _fileImportGeneration) return;
                _fileImportCancellation = null;
                IsImporting = false;
            });
        }
    }
}
