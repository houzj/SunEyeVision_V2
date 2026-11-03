using System.Reflection;
using System.Text.Json;
using SunEyeVision.Application;
using SunEyeVision.Domain.Persistence;
using SunEyeVision.Domain.Workflows;
using SunEyeVision.Persistence;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.TestFixtures;

try {
 var root=Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
 var component=new ToolComponentRegistration(new ToolComponentMetadata("fixture","ResultWorkspaceProbe",ToolComponentRole.Runtime,FoundationContract.Major,FoundationContract.Minor),typeof(WorkspaceProbeDefinition));
 var directory=(RuntimeDirectorySnapshot)typeof(RuntimeDirectorySnapshot).GetMethod("Prepare",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,new object[]{new[]{component}})!;
 using var manager=new DocumentSessionManager(directory); var session=manager.CreateNew("新方案"); var flow=session.GetWorkflows().Single();
 var node=new NodeDefinition("n","节点","ResultWorkspaceProbe",new ToolConfiguration("ResultWorkspaceProbe",new FieldSettings(WorkspaceProbeDefinition.Schema.Inputs,new Dictionary<string,ParameterSetting>())));
 var draft=session.PrepareGraphEdit(flow.Id);draft.AddNode(node);session.CommitGraphEdit(draft,"新增");
 var catalog=new FrozenVariableTypeCatalog(Array.Empty<KeyValuePair<string,string>>()); var path=Path.Combine(root,"new.solution");
 var receipt=session.SaveAs(path,Path.Combine(root,"backup"),root,catalog,new JsonSerializerOptions(),_=>null);
 if(!File.Exists(path)||session.LastWrittenSnapshot!=receipt.WrittenSnapshot)throw new Exception("保存基线错误");
 var reopened=manager.Open(path,_=>catalog,_=>null,new JsonSerializerOptions());
 if(reopened.GetNodes(flow.Id).Single().Id!="n")throw new Exception("重开节点错误");
 var samePathReceipt=reopened.Save(path,Path.Combine(root,"backup"),catalog,new JsonSerializerOptions());
 if(!ReferenceEquals(reopened.LastWrittenSnapshot,samePathReceipt.WrittenSnapshot))throw new Exception("同路径保存基线错误");
 try { reopened.SaveAs(path,Path.Combine(root,"backup"),root,catalog,new JsonSerializerOptions(),_=>null);throw new Exception("另存未拒绝现有文件"); } catch(IOException) { }
 File.AppendAllText(path," ");
 try { reopened.Save(path,Path.Combine(root,"backup"),catalog,new JsonSerializerOptions());throw new Exception("未拒绝外部更改"); } catch(IOException) { }
 var invalid=Path.Combine(root,"invalid");File.WriteAllText(invalid,"{}");
 try { manager.Open(invalid,_=>catalog,_=>null,new JsonSerializerOptions());throw new Exception("未拒绝非法方案"); } catch(JsonException) { }
 if(!ReferenceEquals(manager.CurrentSession,reopened))throw new Exception("失败改变活动方案");
 using var cancel=new CancellationTokenSource();cancel.Cancel();
 try { reopened.SaveAs(Path.Combine(root,"cancel"),Path.Combine(root,"backup"),root,catalog,new JsonSerializerOptions(),_=>null,cancellationToken:cancel.Token);throw new Exception("未取消"); } catch(OperationCanceledException) { }
 Console.WriteLine("PASS 测试声明：真实会话编辑、另存、打开、外部并发拒绝、打开失败保留与取消");return 0;
} catch(Exception ex) { Console.Error.WriteLine(ex); return 1; }
