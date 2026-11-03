using System.Reflection;
using System.Text.Json;
using SunEyeVision.Application;
using SunEyeVision.Domain.Persistence;
using SunEyeVision.Domain.Workflows;
using SunEyeVision.Persistence;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;


try {
 var root=Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
 var productRoot=Path.GetFullPath(Environment.GetEnvironmentVariable("SUNEYE_PRODUCT_ROOT") ?? throw new InvalidOperationException("SUNEYE_PRODUCT_ROOT is required."));
 var directory=new ReleasePayloadAuthority(productRoot).PrepareRuntimeDirectory();
 using var manager=new DocumentSessionManager(directory); var session=manager.CreateNew("新方案"); var flow=session.GetWorkflows().Single();
 var draft=session.PrepareGraphEdit(flow.Id);var node=session.CreateNode(draft,"ImageLoad",new CanvasPoint(0,0));session.CommitGraphEdit(draft,"新增");
 var catalog=new FrozenVariableTypeCatalog(Array.Empty<KeyValuePair<string,string>>()); var path=Path.Combine(root,"new.solution");
 var receipt=session.SaveAs(path,Path.Combine(root,"backup"),root,catalog,new JsonSerializerOptions(),_=>null);
 if(!File.Exists(path)||session.LastWrittenSnapshot!=receipt.WrittenSnapshot)throw new Exception("保存基线错误");
 var reopened=manager.Open(path,_=>catalog,_=>null,new JsonSerializerOptions());
 if(reopened.GetNodes(flow.Id).Single().Id!=node.Id)throw new Exception("重开节点错误");
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
 using var originalScope=new DecodedResourcesScope();
 var original=new SunEyeVision.Infrastructure.Persistence.ComplexVariableValueCodec(originalScope).CreateRaster(new StoredRasterLayout(3,2,3,StoredRasterFormat.Gray8),new byte[]{23,23,23,23,23,23});
 var variable=new SunEyeVision.Domain.Solutions.GlobalVariable("图像",original,"Image");
 using var writeScope=new DecodedResourcesScope();
 var matrix=new RecordValue(VariableValueDeclarations.StoredTransform,new Dictionary<string,object?> { ["m11"]=1d,["m12"]=2d,["m21"]=3d,["m22"]=4d,["offset-x"]=5d,["offset-y"]=6d });
 var matrixValues=new SequenceValue(TypeRef.SequenceOf(VariableValueDeclarations.StoredTransform),new object?[]{matrix,matrix});
 var point=new RecordValue(VariableValueDeclarations.StoredRoiPoint,new Dictionary<string,object?> { ["x"]=12d,["y"]=34d });
 var arc=new RecordValue(VariableValueDeclarations.StoredRoiShape,new Dictionary<string,object?> { ["kind"]="arc",["center"]=point,["radius"]=5d,["start-radians"]=0d,["end-radians"]=1d,["major-arc"]=true,["fill-color-argb"]=0L,["stroke-color-argb"]=0xff112233L,["stroke-thickness"]=2d,["opacity"]=0.75d });
 var roi=new SequenceValue(VariableValueDeclarations.StoredRoiSequence,new object?[]{arc});
 var archive=VariableArchive.Encode(new[]{variable,new SunEyeVision.Domain.Solutions.GlobalVariable("数据",new BinaryValue(new byte[]{1,2,255}),"Binary"),new SunEyeVision.Domain.Solutions.GlobalVariable("矩阵",matrixValues,"Transform"),new SunEyeVision.Domain.Solutions.GlobalVariable("区域",roi,"ROI")},new JsonSerializerOptions(),new SunEyeVision.Infrastructure.Persistence.ComplexVariableValueCodec(writeScope));
 using(var archiveScope=new DecodedResourcesScope())using(var archiveCandidate=VariableArchive.Read(archive,SunEyeVision.Infrastructure.Persistence.VariableVocabularyCapture.FromVariableArchive(archive),new JsonSerializerOptions(),archiveScope,new SunEyeVision.Infrastructure.Persistence.ComplexVariableValueCodec(archiveScope)))
  if(archiveCandidate.Variables.Count!=4 || !((BinaryValue)archiveCandidate.Variables.Single(v=>v.Name=="数据").Value!).ToArray().SequenceEqual(new byte[]{1,2,255}))throw new Exception("变量档案2非空值回读失败");
 var variableJson=System.Text.Json.Nodes.JsonNode.Parse(archive)!["Variables"]!.DeepClone();
 var solutionJson=System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path))!; solutionJson["GlobalVariables"]=variableJson;
 var imagePath=Path.Combine(root,"image.solution");File.WriteAllText(imagePath,solutionJson.ToJsonString());
 using var cancelOpen=new CancellationTokenSource(); TrackingCodec? tracking=null;
 try { manager.Open(imagePath,bytes=>SunEyeVision.Infrastructure.Persistence.VariableVocabularyCapture.FromCandidate(bytes.Span),
     scope=>{tracking=new TrackingCodec(scope);cancelOpen.Cancel();return tracking;},new JsonSerializerOptions(),cancellationToken:cancelOpen.Token);throw new Exception("打开未取消"); } catch(OperationCanceledException) { }
 if(!ReferenceEquals(manager.CurrentSession,reopened)||tracking?.Image is null||!IsDisposed(tracking.Image))throw new Exception("取消打开未保留原会话或未释放候选像素资产");
 Console.WriteLine("PASS 解码后取消打开：旧会话保留、候选像素资产 已释放");
 var pending=new PendingParticipant(); reopened.Editing.Register(pending); TrackingCodec? rejected=null; bool failedReplacement=false;
 try { manager.Open(imagePath,bytes=>SunEyeVision.Infrastructure.Persistence.VariableVocabularyCapture.FromCandidate(bytes.Span),
    scope=>rejected=new TrackingCodec(scope),new JsonSerializerOptions()); } catch(InvalidOperationException) { failedReplacement=true; }
 if(!failedReplacement||!ReferenceEquals(manager.CurrentSession,reopened)||rejected?.Image is null||!IsDisposed(rejected.Image))
    throw new Exception("拒绝替换时原会话或新像素资产 所有权错误");
 reopened.Editing.Unregister(pending.ScopeId);
 Console.WriteLine("PASS 未提交编辑拒绝替换：旧会话保留、已转移的候选像素资产 已释放");
 var imageSession=manager.Open(imagePath,bytes=>SunEyeVision.Infrastructure.Persistence.VariableVocabularyCapture.FromCandidate(bytes.Span),
    scope=>new SunEyeVision.Infrastructure.Persistence.ComplexVariableValueCodec(scope),new JsonSerializerOptions());
 var loaded=(IReadOnlyStoredRaster)imageSession.Document.GlobalVariables.Single(v=>v.Name=="图像").Value!;
 var loadedBinary=(BinaryValue)imageSession.Document.GlobalVariables.Single(v=>v.Name=="数据").Value!;
 var loadedMatrices=(SequenceValue)imageSession.Document.GlobalVariables.Single(v=>v.Name=="矩阵").Value!;
 var loadedRoi=(SequenceValue)imageSession.Document.GlobalVariables.Single(v=>v.Name=="区域").Value!;
 if(!loadedBinary.ToArray().SequenceEqual(new byte[]{1,2,255}) || loadedMatrices.Count!=2 || ((RecordValue)loadedMatrices[1]!).Get<double>("offset-y")!=6d || !((RecordValue)loadedRoi[0]!).Get<bool>("major-arc"))throw new Exception("方案 V5 复杂值回读失败");
 if(IsDisposed(loaded) || IsDisposed(original))throw new Exception("图像未正确接纳");
 var nativeMoved=Path.Combine(root,"native-moved"); Directory.CreateDirectory(nativeMoved);
 var nativeMovedPath=Path.Combine(nativeMoved,"image.solution");
 var nativeTypes=SunEyeVision.Infrastructure.Persistence.VariableVocabularyCapture.FromCandidate(File.ReadAllBytes(imagePath));
 var codecs=new List<TrackingCodec>();
 SunEyeVision.Persistence.Variables.IVariableValueExtensionCodec NewCodec(DecodedResourcesScope scope)
 { var value=new TrackingCodec(scope);codecs.Add(value);return value; }
 imageSession.SaveAs(nativeMovedPath,Path.Combine(root,"native-backup"),root,nativeTypes,new JsonSerializerOptions(),NewCodec);
 imageSession.Save(nativeMovedPath,Path.Combine(root,"native-backup"),nativeTypes,new JsonSerializerOptions(),NewCodec);
 if(codecs.Count!=4 || codecs.Any(codec=>codec.Image is null || !IsDisposed(codec.Image)) || IsDisposed(loaded) || IsDisposed(original))
    throw new Exception("保存与重定位必须各用独立解码范围并释放候选，保留正式借用图像");
 manager.CloseCurrent(); if(!IsDisposed(loaded) || IsDisposed(original))throw new Exception("关闭未释放本次解码图像或错误释放原图");
 Console.WriteLine("PASS 复杂图像值跨目录另存和再次保存：四次解码范围独立释放、正式图像关闭前保留");
 var savedSession=manager.Open(nativeMovedPath,bytes=>SunEyeVision.Infrastructure.Persistence.VariableVocabularyCapture.FromCandidate(bytes.Span),scope=>new SunEyeVision.Infrastructure.Persistence.ComplexVariableValueCodec(scope),new JsonSerializerOptions());
 var savedValues=savedSession.Document.GlobalVariables;
 var savedImage=(IReadOnlyStoredRaster)savedValues.Single(v=>v.Name=="图像").Value!;
 using(var reader=savedImage.AcquireReader())if(reader.GetRow(0)[0]!=23)throw new Exception("保存重开后的像素错误");
 if(!((BinaryValue)savedValues.Single(v=>v.Name=="数据").Value!).ToArray().SequenceEqual(new byte[]{1,2,255}) ||
    ((RecordValue)((SequenceValue)savedValues.Single(v=>v.Name=="矩阵").Value!)[1]!).Get<double>("offset-y")!=6d ||
    !((RecordValue)((SequenceValue)savedValues.Single(v=>v.Name=="区域").Value!)[0]!).Get<bool>("major-arc"))throw new Exception("保存重开后的二进制、矩阵或大弧错误");
 manager.CloseCurrent();if(!IsDisposed(savedImage))throw new Exception("保存重开后的像素资产未释放");
 Console.WriteLine("PASS 非空方案 V5：图像、二进制、矩阵数组及大弧另存、再次保存、重开与释放");
 Console.WriteLine("PASS 测试声明：真实会话编辑、另存、打开、外部并发拒绝、打开失败保留与取消");return 0;
} catch(Exception ex) { Console.Error.WriteLine(ex); return 1; }
static bool IsDisposed(IReadOnlyStoredRaster image) { try { using var reader=image.AcquireReader(); return false; } catch(ObjectDisposedException) { return true; } }

sealed class TrackingCodec(DecodedResourcesScope scope) : SunEyeVision.Persistence.Variables.IVariableValueExtensionCodec
{
 private readonly SunEyeVision.Infrastructure.Persistence.ComplexVariableValueCodec _inner=new(scope);
 public DecodedResourcesScope Scope=>scope;
 public IReadOnlyStoredRaster? Image { get; private set; }
 public bool SupportsType(string typeName)=>_inner.SupportsType(typeName);
 public object? Read(string typeName,JsonElement value,JsonSerializerOptions options) { var result=_inner.Read(typeName,value,options); Image=result as IReadOnlyStoredRaster ?? Image;return result; }
 public bool TryGetToken(object value,out string typeName)=>_inner.TryGetToken(value,out typeName);
 public void WriteValue(Utf8JsonWriter writer,string typeName,object value,JsonSerializerOptions options)=>_inner.WriteValue(writer,typeName,value,options);
}
sealed class PendingParticipant : SunEyeVision.Application.Editing.IDocumentEditParticipant
{
 public Guid ScopeId { get; }=Guid.NewGuid(); public string ScopeKey=>"pending"; public bool HasPendingChanges=>true; public bool IsValid=>true;
 public SunEyeVision.Application.Editing.DocumentEditProposal Prepare(SunEyeVision.Application.Editing.NodeConfigurationEditHost host)=>throw new NotSupportedException();
 public void Acknowledge(SunEyeVision.Application.Editing.DocumentEditProposal proposal, SunEyeVision.Application.DocumentEditReceipt receipt) => throw new NotSupportedException();
 public void Cancel() { }
}



