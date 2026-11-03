using System.IO;
using System.Reflection;
using System.Windows.Threading;
using SunEyeVision.Application;
using SunEyeVision.Application.Editing;
using SunEyeVision.Domain.Workflows;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Infrastructure.ImageSources;
using SunEyeVision.Plugin.Runtime;
namespace SunEyeVision.ImageInput.Runtime.Tests;
internal static class RuntimeHost
{
 internal static string ProductRoot=>typeof(RuntimeHost).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().Single(a=>a.Key=="HostDebugRoot").Value!;
 private static readonly Lazy<RuntimeDirectorySnapshot> Directory=new(()=>new ReleasePayloadAuthority(ProductRoot).PrepareRuntimeDirectory());
 internal static RuntimeDirectorySnapshot Runtime=>Directory.Value;
 internal static void Run(Action action)=>SunEyeVision.TestShared.WpfTestDispatcher.Invoke(action,loadShellResources:false);
 internal static void Pump(){var frame=new DispatcherFrame();Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background,new Action(()=>frame.Continue=false));Dispatcher.PushFrame(frame);}
 internal static T Wait<T>(Task<T> task){var end=DateTime.UtcNow.AddSeconds(25);while(!task.IsCompleted){if(DateTime.UtcNow>end)throw new TimeoutException("正式运行未排空。");Pump();Thread.Sleep(1);}return task.GetAwaiter().GetResult();}
 internal static void Wait(Task task)=>Wait(Task.Run(async()=>{await task;return true;}));
}
internal sealed class RuntimeFixture:IDisposable
{
 internal readonly string DirectoryPath;
 internal readonly DocumentSessionManager Manager;
 internal readonly DocumentSession Session;
 internal readonly WorkflowDefinition Flow;
 internal readonly NodeDefinition Node;
 internal readonly ImageSourceHostInputFactory Factory;
 private readonly List<DocumentDebugSession> _scopes=new();
 internal RuntimeFixture(SunEyeVision.Plugin.Contracts.Acquisition.ICameraFrameProvider? camera=null)
 {
  DirectoryPath=Path.GetFullPath(Path.Combine(RuntimeHost.ProductRoot,"..","..","_validation","ImageInputRuntime",Guid.NewGuid().ToString("N")));Directory.CreateDirectory(DirectoryPath);
  Manager=new(RuntimeHost.Runtime);Session=Manager.CreateNew("正式图像输入回归");Flow=Session.GetWorkflows().Single();var graph=Session.PrepareGraphEdit(Flow.Id);Node=Session.CreateNode(graph,"ImageLoad",new(150,120));Session.CommitGraphEdit(graph,"建立实际图像源");Factory=new(camera);
  Edit(("sequence.initial",new ConstantSetting(7L)));
 }
 internal string Write(string name,byte pixel){var file=Path.Combine(DirectoryPath,name);File.WriteAllBytes(file,System.Text.Encoding.ASCII.GetBytes("P5\n2 1\n255\n").Concat(new[]{pixel,pixel}).ToArray());return file;}
 internal void Paths(params string[] paths)=>Edit(("local.paths",new ConstantSetting(paths)),("local.start",new ConstantSetting(paths.Length==0?-1L:0L)));
 internal void Edit(params (string,ParameterSetting)[] changes){var draft=Draft();draft.ApplyFields(changes.ToDictionary(p=>p.Item1,p=>p.Item2));Session.CommitEdit(DocumentEditProposal.Prepare(Session.Editing.Host,nodes:new[]{draft}));}
 internal NodeConfigurationEditDraft Draft()=>Session.Editing.Host.BeginEdit(Flow.Id,Node.Id);
 internal DocumentDebugSession Scope(){var scope=Session.OpenDebugSession(Flow.Id,Node);_scopes.Add(scope);return scope;}
 internal DocumentDebugInvocation Capture(DocumentDebugSession scope,NodeConfigurationEditDraft? draft=null,CancellationToken cancellation=default,IHostInputFactory? factory=null)=>scope.Capture(draft??Draft(),null,_=>{},cancellation,hostFactory:factory??Factory);
 internal DeclaredOutputSnapshot Execute(DocumentDebugInvocation invocation)=>RuntimeHost.Wait(Task.Run(invocation.ExecuteAsync));
 internal DeclaredOutputReader Formal(IHostInputFactory? factory=null)
 {
  using var batch=Session.CaptureRunBatch(new[]{Flow.Id},hostFactory:factory??Factory);using var result=RuntimeHost.Wait(Task.Run(()=>batch.RunAsync(_=>{})));Session.DrainRuntimeChanges();return Session.AcquirePublishedResult(Flow.Id,Node.Id)??throw new InvalidOperationException("正式结果未发布。");
 }
 internal void Camera(string device="camera")=>Edit(("source.kind",new ConstantSetting("camera")),("camera.device",new ConstantSetting(device)),("camera.timeout-ms",new ConstantSetting(1234L)));
 internal static IReadOnlyImage Image(DeclaredOutputReader reader)=>reader.Values!.Get<RecordValue>("output.image").Get<IReadOnlyImage>("data");
 public void Dispose(){foreach(var scope in _scopes)scope.Dispose();Factory.Dispose();Manager.CloseCurrent(true);Manager.Dispose();}
}
