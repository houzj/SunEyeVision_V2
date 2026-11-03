using System.Diagnostics;
using System.IO;
using System.Reflection;
using SunEyeVision.Plugin.Contracts;
using Xunit;
namespace SunEyeVision.UI.Tests;
[Collection("Canvas UI")]
public sealed class DocumentFilesSessionTests
{
 [Fact] public void Save_and_actual_main_reopen_retire_old_selection_projection_and_presentation()=>UiRuntime.Run(()=>
 {
  using var f=new DocumentUiFixture();var node=f.Add();f.ConfigureImage(node,f.Image());f.Main.SelectedNode=node;var cap=f.Session.AdmitPresentation(f.Root.Id,node.Id);
  f.Save();Assert.False(f.Session.HasUnsavedChanges);var target=f.Session.LastWrittenSnapshot!.TargetPath;
  f.Fields(node,("sequence.initial",new ConstantSetting(17L)));Assert.True(f.Session.HasUnsavedChanges);
  Assert.True((bool)UiRuntime.Call(f.Main,"SaveCurrentDocumentWithPrompt",f.Host,false)!);Assert.False(f.Session.HasUnsavedChanges);
  var copy=Path.Combine(f.DirectoryPath,"copy.solution");File.Copy(target,copy);
  var opened=(Task<bool>)UiRuntime.Call(f.Main,"OpenExternalSolutionAsync",copy,false)!;UiRuntime.Await(opened);Assert.True(opened.Result);
  var current=SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters.ServiceInitializer.SolutionManager.CurrentSession!;
  Assert.NotSame(f.Session,current);Assert.False(cap.IsCurrent);Assert.Null(f.Main.SelectedNode);Assert.False(current.HasUnsavedChanges);
  var actual=current.GetNodes(f.Root.Id).Single();Assert.NotSame(node,actual);Assert.Equal(17L,Assert.IsType<ConstantSetting>(actual.Configuration.Fields.Read("sequence.initial")).Value);
  Assert.Same(actual,f.Main.WorkflowTabViewModel.SelectedTab!.DocumentCanvas!.Nodes.Single().Definition);
 });
 [Theory]
 [InlineData("debug")][InlineData("formal")][InlineData("empty")][InlineData("cancel")][InlineData("wait")][InlineData("preferences")]
 public async Task Actual_main_process_exercises_file_activation_cancel_and_resource_drain(string scenario)
 {
  var product=UiRuntime.ProductRoot;var worktree=Directory.GetParent(product.TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar))!.Parent!.Parent!.FullName;
  var probe=Path.Combine(worktree,"Release","_validation","ShutdownLifecycleProbe","ShutdownLifecycleProbe.dll");Assert.True(File.Exists(probe),probe);
  var cases=Path.Combine(worktree,"Release","_validation","UiSessionProcessTests");Directory.CreateDirectory(cases);
  var start=new ProcessStartInfo("dotnet"){WorkingDirectory=product,RedirectStandardOutput=true,RedirectStandardError=true,UseShellExecute=false,CreateNoWindow=true};
  start.ArgumentList.Add(probe);start.ArgumentList.Add(scenario);start.Environment["SUNEYE_SHUTDOWN_PROBE_ROOT"]=cases;
  using var process=Process.Start(start)!;var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();
  using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(65));
  try{await process.WaitForExitAsync(timeout.Token);}catch(OperationCanceledException){process.Kill(true);throw new TimeoutException("实际主窗进程未按时排空："+scenario);}
  var text=await stdout+await stderr;File.WriteAllText(Path.Combine(cases,scenario+".log"),text);Assert.True(process.ExitCode==0,text);
 }
}
