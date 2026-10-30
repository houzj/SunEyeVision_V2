using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using SunEyeVision.Plugin.Wpf.Presenters.Input.ImageSources;
using SunEyeVision.UI.Services.Thumbnail;
using SunEyeVision.UI.Shared;

namespace SunEyeVision.UI.Services.Images;

public sealed partial class ImageSourceStripSession
{
    private CancellationTokenSource? _fileImportCancellation;
    private long _fileImportGeneration;
    private void CancelFileImport()
    {
        _dispatcher.VerifyAccess();
        long generation = ++_fileImportGeneration;
        var previous = _fileImportCancellation; _fileImportCancellation = null;
        bool wasImporting = IsImporting; IsImporting = false;
        previous?.Cancel();
        if (generation != _fileImportGeneration) return;
        if (wasImporting) SetStatus("导入已取消；如有已接纳的引用将保留");
        if (!_disposed) PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsImporting)));
        // The owning ImportAsync scope disposes its CTS only after its work has returned.
    }
    internal Task ImportFilePathsAsync(string[] paths)
        => ImportAsync(token => ImageFileImport.ExistingFilesAsync(paths, token));
    internal Task ImportDirectoryAsync(string directory)
        => ImportAsync(token => ImageFileImport.DirectoryFilesAsync(directory, token));
    private async Task ImportFilesRequestAsync(ImageSourceStripRequest request, CancellationToken cancellationToken)
    {
        var paths = request.Paths?.ToArray();
        if (paths is null)
        {
            var owner = _dialogOwner?.Invoke(); RequireCurrent(request.ExpectedVersion);
            var dialog = new OpenFileDialog
            {
                Title = "添加图像", Multiselect = true, CheckFileExists = true,
                Filter = "图像文件|*.jpg;*.jpeg;*.png;*.bmp;*.tiff;*.tif|所有文件|*.*"
            };
            bool? accepted = owner is null ? dialog.ShowDialog() : dialog.ShowDialog(owner);
            cancellationToken.ThrowIfCancellationRequested(); RequireCurrent(request.ExpectedVersion);
            if (accepted != true) return;
            paths = dialog.FileNames;
        }
        cancellationToken.ThrowIfCancellationRequested(); RequireCurrent(request.ExpectedVersion);
        // File imports never enumerate directories. The distinct folder action owns that behavior.
        var frozen = paths.ToArray();
        await ImportCoreAsync(token=>ImageFileImport.ExistingFilesAsync(frozen,token),cancellationToken);
    }
    private async Task ImportDirectoryRequestAsync(ImageSourceStripRequest request, CancellationToken cancellationToken)
    {
        var directories = request.Paths?.ToArray();
        if (directories is null)
        {
            var owner = _dialogOwner?.Invoke(); RequireCurrent(request.ExpectedVersion);
            var dialog = new OpenFolderDialog { Title = "添加图像文件夹", Multiselect = false };
            bool? accepted = owner is null ? dialog.ShowDialog() : dialog.ShowDialog(owner);
            cancellationToken.ThrowIfCancellationRequested(); RequireCurrent(request.ExpectedVersion);
            if (accepted != true) return;
            directories = new[] { dialog.FolderName };
        }
        RequireCurrent(request.ExpectedVersion);
        var frozen = directories.ToArray();
        await ImportCoreAsync(async token =>
        {
            var files = new System.Collections.Generic.List<string>();
            foreach (var directory in frozen)
            {
                token.ThrowIfCancellationRequested();
                files.AddRange(await ImageFileImport.DirectoryFilesAsync(directory, token).ConfigureAwait(false));
            }
            return files.ToArray();
        }, cancellationToken);
    }
    // This is the actual production enumeration/atomic-acceptance path; no synthetic import fixture or alternate writer.
    private Task ImportAsync(Func<CancellationToken, Task<string[]>> enumerate) => ImportCoreAsync(enumerate, CancellationToken.None);
    private async Task ImportCoreAsync(Func<CancellationToken, Task<string[]>> enumerate, CancellationToken externalCancellation)
    {
        if (!_dispatcher.CheckAccess())
        { await _dispatcher.InvokeAsync(() => ImportCoreAsync(enumerate, externalCancellation)).Task.Unwrap(); return; }
        ArgumentNullException.ThrowIfNull(enumerate);
        long expectedVersion = _source.EditVersion; RequireCurrent(expectedVersion);
        long nextGeneration = _fileImportGeneration + 1;
        CancelFileImport();
        if (_fileImportGeneration != nextGeneration) return;
        RequireCurrent(expectedVersion);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token, externalCancellation);
        _fileImportCancellation = cancellation; long generation = _fileImportGeneration;
        IsImporting = true;
        try
        {
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsImporting)));
            if (!CanAccept(expectedVersion) || generation != _fileImportGeneration) return;
            SetStatus("正在读取文件列表…");
            if (!CanAccept(expectedVersion) || generation != _fileImportGeneration) return;
            cancellation.Token.ThrowIfCancellationRequested();
            var files = await enumerate(cancellation.Token).ConfigureAwait(false);
            cancellation.Token.ThrowIfCancellationRequested();
            // Enumeration providers may produce duplicates or absent paths; normalize once before making immutable rows.
            var existingFiles = await ImageFileImport.ExistingFilesAsync(files, cancellation.Token).ConfigureAwait(false);
            var prepared = await Task.Run(() => existingFiles.Select(path =>
            {
                cancellation.Token.ThrowIfCancellationRequested();
                return new ImageInfo { FilePath = path, Name = System.IO.Path.GetFileNameWithoutExtension(path) };
            }).ToArray(), cancellation.Token).ConfigureAwait(false);
            await _dispatcher.InvokeAsync(() =>
            {
                if (cancellation.IsCancellationRequested || generation != _fileImportGeneration || !CanAccept(expectedVersion)) return;
                var existing = _source.Images.Select(image => image.FilePath).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var additions = prepared.Where(image => existing.Add(image.FilePath)).ToArray();
                if (additions.Length == 0) { SetStatus("没有找到新的可导入图像文件"); return; }
                // One action, one acceptance. Notifications may cancel or retire this session after acceptance;
                // no partially accepted chunks and no rollback of already accepted references.
                var receipt=_source.AddImages(additions);
                if (cancellation.IsCancellationRequested || generation != _fileImportGeneration || !OwnerIsCurrent()) return;
                if(receipt.ExecutionError is { } failure)
                {
                    SetStatus("图像引用已接纳，但运行游标不可用："+failure);
                    if(OwnerIsCurrent())Refresh();
                    return;
                }
                if (receipt.ProjectionVersion is not { } projection || _source.EditVersion!=projection)
                {
                    SetStatus(receipt.FormalAccepted?"图像引用已接纳；显示已更新":"私有草稿已更改；显示已更新");
                    return;
                }
                SetStatus(receipt.FormalAccepted?$"已导入 {additions.Length} 个图像引用":$"已添加 {additions.Length} 个图像到私有草稿");
                Refresh();
            }, DispatcherPriority.Background, cancellation.Token);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            if (!_dispatcher.HasShutdownStarted)
                await _dispatcher.InvokeAsync(() =>
                { if (generation == _fileImportGeneration && OwnerIsCurrent()) SetStatus("导入已取消；如有已接纳的引用将保留"); });
        }
        catch (Exception ex)
        {
            if (!_dispatcher.HasShutdownStarted)
                await _dispatcher.InvokeAsync(() => { if (generation == _fileImportGeneration && OwnerIsCurrent()) SetStatus(ex.Message); });
            throw;
        }
        finally
        {
            if (!_dispatcher.HasShutdownStarted)
                await _dispatcher.InvokeAsync(() =>
                {
                    if (_disposed || generation != _fileImportGeneration || !ReferenceEquals(_fileImportCancellation, cancellation)) return;
                    _fileImportCancellation = null; IsImporting = false;
                    PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsImporting)));
                });
        }
    }
}
