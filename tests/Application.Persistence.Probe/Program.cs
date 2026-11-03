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
 var hash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path)));
 bool activeRestoreRejected=false;
 try { manager.RestoreInactive(path,path,hash,hash,_=>catalog,_=>null,new JsonSerializerOptions()); } catch(InvalidOperationException) { activeRestoreRejected=true; }
 if(!activeRestoreRejected || !ReferenceEquals(manager.CurrentSession,reopened)) throw new Exception("活动方案恢复未受保护");
 var offlineTarget=Path.Combine(root,"offline.solution"); var offlineBackup=Path.Combine(root,"offline.backup");
 File.Copy(path,offlineBackup); File.WriteAllText(offlineTarget,"previous");
 var beforeHash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(offlineTarget)));
 manager.RestoreInactive(offlineTarget,offlineBackup,beforeHash,hash,_=>catalog,_=>null,new JsonSerializerOptions());
 if(!File.ReadAllBytes(offlineTarget).SequenceEqual(File.ReadAllBytes(offlineBackup)) || !ReferenceEquals(manager.CurrentSession,reopened)) throw new Exception("非活动方案恢复错误");
 bool wrongHashRejected=false;
 try { manager.RestoreInactive(offlineTarget,offlineBackup,new string('0',64),hash,_=>catalog,_=>null,new JsonSerializerOptions()); } catch(IOException) { wrongHashRejected=true; }
 if(!wrongHashRejected || !File.ReadAllBytes(offlineTarget).SequenceEqual(File.ReadAllBytes(offlineBackup))) throw new Exception("恢复身份冲突未保全文件");
 Console.WriteLine("PASS 非活动方案受验证恢复、活动路径保护与 SHA 冲突保全");
 reopened.SaveCopy(Path.Combine(root,"copy.solution"),Path.Combine(root,"copy.backup"),root,catalog,new JsonSerializerOptions(),_=>null);
 if(!ReferenceEquals(reopened.LastWrittenSnapshot,samePathReceipt.WrittenSnapshot))throw new Exception("保存副本改变当前基线");
 var rejectedCopy=false; try { reopened.SaveCopy(path,Path.Combine(root,"copy.backup"),root,catalog,new JsonSerializerOptions(),_=>null,TargetExpectation.Match(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path))))); } catch(InvalidOperationException) { rejectedCopy=true; }
 if(!rejectedCopy)throw new Exception("副本覆盖当前方案");
 try { reopened.SaveAs(path,Path.Combine(root,"backup"),root,catalog,new JsonSerializerOptions(),_=>null);throw new Exception("另存未拒绝现有文件"); } catch(IOException) { }
 File.AppendAllText(path," ");
 try { reopened.Save(path,Path.Combine(root,"backup"),catalog,new JsonSerializerOptions());throw new Exception("未拒绝外部更改"); } catch(IOException) { }
 var invalid=Path.Combine(root,"invalid");File.WriteAllText(invalid,"{}");
 try { manager.Open(invalid,_=>catalog,_=>null,new JsonSerializerOptions());throw new Exception("未拒绝非法方案"); } catch(JsonException) { }
 if(!ReferenceEquals(manager.CurrentSession,reopened))throw new Exception("失败改变活动方案");
 using var cancel=new CancellationTokenSource();cancel.Cancel();
 try { reopened.SaveAs(Path.Combine(root,"cancel"),Path.Combine(root,"backup"),root,catalog,new JsonSerializerOptions(),_=>null,cancellationToken:cancel.Token);throw new Exception("未取消"); } catch(OperationCanceledException) { }
 using var original=new OpenCvSharp.Mat(2,3,OpenCvSharp.MatType.CV_8UC1,OpenCvSharp.Scalar.All(23));
 var variable=new SunEyeVision.Domain.Solutions.GlobalVariable("图像",original,"Image");
 using var writeScope=new DecodedResourcesScope();
 var archive=VariableArchive.Encode(new[]{variable},new JsonSerializerOptions(),new SunEyeVision.Infrastructure.Persistence.SdkVariableValueExtensionCodec(writeScope));
 var variableJson=System.Text.Json.Nodes.JsonNode.Parse(archive)!["Variables"]![0]!.DeepClone();
 var solutionJson=System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path))!; solutionJson["GlobalVariables"]=new System.Text.Json.Nodes.JsonArray(variableJson);
 var imagePath=Path.Combine(root,"image.solution");File.WriteAllText(imagePath,solutionJson.ToJsonString());
 using var cancelOpen=new CancellationTokenSource(); TrackingCodec? tracking=null;
 try { manager.Open(imagePath,bytes=>SunEyeVision.Infrastructure.Persistence.SdkVariableVocabularyCapture.FromCandidate(bytes.Span),
     scope=>{tracking=new TrackingCodec(scope);cancelOpen.Cancel();return tracking;},new JsonSerializerOptions(),cancellationToken:cancelOpen.Token);throw new Exception("打开未取消"); } catch(OperationCanceledException) { }
 if(!ReferenceEquals(manager.CurrentSession,reopened)||tracking?.Image is null||!tracking.Image.IsDisposed)throw new Exception("取消打开未保留原会话或未释放候选 Mat");
 Console.WriteLine("PASS 解码后取消打开：旧会话保留、候选 Mat 已释放");
 var pending=new PendingParticipant(); reopened.Editing.Register(pending); TrackingCodec? rejected=null; bool failedReplacement=false;
 try { manager.Open(imagePath,bytes=>SunEyeVision.Infrastructure.Persistence.SdkVariableVocabularyCapture.FromCandidate(bytes.Span),
    scope=>rejected=new TrackingCodec(scope),new JsonSerializerOptions()); } catch(InvalidOperationException) { failedReplacement=true; }
 if(!failedReplacement||!ReferenceEquals(manager.CurrentSession,reopened)||rejected?.Image is null||!rejected.Image.IsDisposed)
    throw new Exception("拒绝替换时原会话或新 Mat 所有权错误");
 reopened.Editing.Unregister(pending.ScopeId);
 Console.WriteLine("PASS 未提交编辑拒绝替换：旧会话保留、已转移的候选 Mat 已释放");
 var imageSession=manager.Open(imagePath,bytes=>SunEyeVision.Infrastructure.Persistence.SdkVariableVocabularyCapture.FromCandidate(bytes.Span),
    scope=>new SunEyeVision.Infrastructure.Persistence.SdkVariableValueExtensionCodec(scope),new JsonSerializerOptions());
 var loaded=(OpenCvSharp.Mat)imageSession.Document.GlobalVariables.Single().Value!;
 if(loaded.IsDisposed || original.IsDisposed)throw new Exception("图像未正确接纳");
 manager.CloseCurrent(); if(!loaded.IsDisposed || original.IsDisposed)throw new Exception("关闭未释放本次解码图像或错误释放原图");
 Console.WriteLine("PASS 测试声明：真实会话编辑、另存、打开、外部并发拒绝、打开失败保留与取消");return 0;
} catch(Exception ex) { Console.Error.WriteLine(ex); return 1; }

sealed class TrackingCodec(DecodedResourcesScope scope) : SunEyeVision.Persistence.Variables.IVariableValueExtensionCodec
{
 private readonly SunEyeVision.Infrastructure.Persistence.SdkVariableValueExtensionCodec _inner=new(scope);
 public DecodedResourcesScope Scope=>scope;
 public OpenCvSharp.Mat? Image { get; private set; }
 public bool SupportsType(string typeName)=>_inner.SupportsType(typeName);
 public object? Read(string typeName,JsonElement value,JsonSerializerOptions options) { var result=_inner.Read(typeName,value,options); Image=result as OpenCvSharp.Mat ?? Image;return result; }
 public bool TryWrite(object value,JsonSerializerOptions options,out string typeName,out object? serializedValue)=>_inner.TryWrite(value,options,out typeName,out serializedValue);
}
sealed class PendingParticipant : SunEyeVision.Application.Editing.IDocumentEditParticipant
{
 public Guid ScopeId { get; }=Guid.NewGuid(); public string ScopeKey=>"pending"; public bool HasPendingChanges=>true; public bool IsValid=>true;
 public SunEyeVision.Application.Editing.DocumentEditProposal Prepare(SunEyeVision.Application.Editing.NodeConfigurationEditHost host)=>throw new NotSupportedException();
 public void Cancel() { }
}
