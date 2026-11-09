using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using SunEyeVision.Application;
using SunEyeVision.Domain.Persistence;
using SunEyeVision.Infrastructure.Persistence;
using SunEyeVision.Persistence;
using SunEyeVision.UI.Features.Dialogs;
namespace SunEyeVision.UI.Shell.Windows;
public partial class MainWindowViewModel
{
 private static readonly JsonSerializerOptions DocumentJsonOptions=new(){WriteIndented=true};
 private static FrozenVariableTypeCatalog CaptureDocumentVariableVocabulary(DocumentSession session)
 {
  // Freeze only type declarations after the one editor acceptance, without encoding or borrowing values.
  var declarations=JsonSerializer.SerializeToUtf8Bytes(new{GlobalVariables=session.Document.GlobalVariables.Select(variable=>new{variable.Type}).ToArray()});
  return VariableVocabularyCapture.FromCandidate(declarations);
 }
 private bool SaveCurrentDocumentWithPrompt(Window? owner=null,bool saveAs=false)
 {
  owner=ResolveSolutionDialogOwner(owner);
  try
  {
   var session=DocumentManager.CurrentSession;if(session is null)return false;
   if(IsRunning)throw new InvalidOperationException("请先停止并等待正式运行排空后保存方案。");
   if(!TryValidateSolutionEditors(out var error))throw new InvalidOperationException(error);
   var target=saveAs?null:session.LastWrittenSnapshot?.TargetPath;
   if(string.IsNullOrEmpty(target))
   {
    var dialog=new SaveFileDialog{Title=saveAs?"另存方案":"保存方案",Filter="方案文件 (*.solution)|*.solution",DefaultExt=".solution",AddExtension=true,OverwritePrompt=true,
     InitialDirectory=SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters.ServiceInitializer.DocumentFilesDirectory,FileName=session.Document.Name};
    if(dialog.ShowDialog(owner)!=true)return false;target=Path.GetFullPath(dialog.FileName);
   }
   Services.Startup.StartupLaunchContext.Current.ValidatePath(target);
   if(!ReferenceEquals(DocumentManager.CurrentSession,session))throw new InvalidOperationException("文件选择期间方案已变化。");
   if(!TryCommitSolutionEditors(out error))throw new InvalidOperationException(error);
   var catalog=CaptureDocumentVariableVocabulary(session);var directory=Path.GetDirectoryName(target)!;
   var recovery=Path.Combine(directory,"."+Path.GetFileName(target)+".recovery");
   var baseline=session.LastWrittenSnapshot;
   FrozenSaveReceipt receipt;
   if(!saveAs&&baseline is not null&&string.Equals(baseline.TargetPath,target,StringComparison.OrdinalIgnoreCase))
    receipt=session.Save(target,recovery,catalog,DocumentJsonOptions,scope=>new ComplexVariableValueCodec(scope));
   else
   {
    var expected=File.Exists(target)?TargetExpectation.Match(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(target)))):TargetExpectation.Missing;
    receipt=session.SaveAs(target,recovery,session.ResourceBaseDirectory??directory,catalog,DocumentJsonOptions,scope=>new ComplexVariableValueCodec(scope),expected);
   }
   UpdateCurrentSolutionInfo();(System.Windows.Application.Current as App)?.RecordRecentSolution(session);
   LogSuccess("方案文件已保存："+receipt.WrittenSnapshot.TargetPath);
   if(receipt.Warning is {Length:>0} warning)ThemeMessageDialog.Show(warning,"方案已保存",MessageBoxButton.OK,ThemeMessageDialog.MessageIcon.Warning,owner);
   return true;
  }
  catch(Exception failure)
  {
   UpdateCurrentSolutionInfo();ThemeMessageDialog.Show(failure.Message,"保存方案失败",MessageBoxButton.OK,ThemeMessageDialog.MessageIcon.Error,owner);return false;
  }
 }
 private static void ValidateDocumentCandidateBytes(ReadOnlySpan<byte> bytes)
 {
  using var parsed=JsonDocument.Parse(bytes.ToArray());SolutionFormatPolicy.ValidateReadable(parsed.RootElement);
  if(Services.Startup.StartupLaunchContext.Current.IsIsolated)
   foreach(var property in new[]{"Devices","Communications"})
    if(parsed.RootElement.TryGetProperty(property,out var value)&&value.ValueKind==JsonValueKind.Array&&value.GetArrayLength()!=0)
     throw new InvalidOperationException("隔离启动验收拒绝启用设备或通讯配置。");
 }
 private async Task<bool> OpenFormalDocumentAsync(string path,bool reload)
 {
  path=Path.GetFullPath(path);Services.Startup.StartupLaunchContext.Current.ValidatePath(path);
  if(!reload&&string.Equals(DocumentManager.CurrentSession?.LastWrittenSnapshot?.TargetPath,path,StringComparison.OrdinalIgnoreCase))return true;
  // Basic file/format checks precede stop/save prompts. The manager alone fully admits and installs the candidate.
  var bytes=await Task.Run(()=>File.ReadAllBytes(path));
  await Task.Run(()=>ValidateDocumentCandidateBytes(bytes));
  _=await Task.Run(()=>VariableVocabularyCapture.FromCandidate(bytes));
  if(!await PrepareSolutionTransitionAsync(reload))return false;
  DocumentSessionManager.PreparedDocumentOpen? prepared=null;
  try
  {
   await SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters.ServiceInitializer.DisposeCommunicationRuntimeAsync();
   prepared=await Task.Run(()=>DocumentManager.PrepareOpen(bytes,path,data=>VariableVocabularyCapture.FromCandidate(data.Span),scope=>new ComplexVariableValueCodec(scope),DocumentJsonOptions));
   var current=DocumentManager.OpenPrepared(prepared,discardPendingEdits:true);

   UpdateCurrentSolutionInfo();
   if(DocumentManager.LastObserverError is {} observerError)throw new InvalidOperationException("方案已切换，但部分界面观察者未完成更新。",observerError);
   if(DocumentManager.LastCloseError is {} closeError)throw new InvalidOperationException("方案已切换，但旧方案部分资源清理未完成。",closeError);
   if(System.Windows.Application.Current is App app){app.RecordRecentSolution(current);await app.CompleteOpenedWorkspaceAsync();}
   return true;
  }
  finally{EndSolutionTransition();}
 }
}

