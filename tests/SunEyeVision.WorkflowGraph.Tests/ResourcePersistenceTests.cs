using System.IO;
using System.Security.Cryptography;
using SunEyeVision.Application.Editing;
using SunEyeVision.Plugin.Contracts;
using Xunit;
namespace SunEyeVision.WorkflowGraph.Tests;
public sealed class ResourcePersistenceTests
{
 [Fact] public void Embedded_list_survives_source_deletion_save_as_reopen_and_scope_release_without_changing_live_fields()
 {
  using var f=new SessionGraphFixture();var node=f.Add();
  var a=Path.Combine(f.CaseDirectory,"a.bmp");var b=Path.Combine(f.CaseDirectory,"b.bmp");var bytes=new byte[]{1,2,3,4};
  File.WriteAllBytes(a,bytes);File.WriteAllBytes(b,bytes);
  var draft=f.Session.Editing.Host.BeginEdit(f.Root.Id,node.Id);var type=draft.SettingsSchema.Inputs.Single(port=>port.Id=="local.paths").Type;
  draft.ApplyFields(new Dictionary<string,ParameterSetting>{["local.paths"]=new ConstantSetting(new SequenceValue(type,new object?[]{a,b})),
   ["local.selected"]=new ConstantSetting(new SequenceValue(type,new object?[]{b})),["local.save-images"]=new ConstantSetting(true)});
  f.Session.CommitEdit(DocumentEditProposal.Prepare(f.Session.Editing.Host,nodes:new[]{draft}));
  var saved=f.Save("embedded.solution");
  var live=(SequenceValue)f.Session.FindNode(f.Root.Id,node.Id)!.Configuration.Fields.Get<object>("local.paths");
  Assert.Equal(new object?[]{a,b},live.ToArray());File.Delete(a);File.Delete(b);
  var opened=f.Manager.Open(saved,_=>SessionGraphFixture.Types,_=>null,SessionGraphFixture.Options);
  var fields=opened.FindNode(f.Root.Id,node.Id)!.Configuration.Fields;
  var references=fields.Get<SequenceValue>("local.paths").Cast<string>().ToArray();
  Assert.All(references,reference=>Assert.StartsWith("suneye-file:",reference));Assert.NotEqual(references[0],references[1]);
  Assert.Equal(references[1],Assert.Single(fields.Get<SequenceValue>("local.selected")));
  var resolver=opened.CreateResourcePathResolver();var target=resolver.Resolve("local.paths",references[0],ResourcePurpose.FileRead).Address;
  Assert.Equal(bytes,File.ReadAllBytes(target));
  var moved=Path.Combine(f.CaseDirectory,"second","again.solution");Directory.CreateDirectory(Path.GetDirectoryName(moved)!);
  opened.SaveAs(moved,Path.Combine(f.CaseDirectory,"backup"),Path.GetDirectoryName(saved)!,SessionGraphFixture.Types,SessionGraphFixture.Options,_=>null);
  var reopened=f.Manager.Open(moved,_=>SessionGraphFixture.Types,_=>null,SessionGraphFixture.Options);
  Assert.False(File.Exists(target));Assert.Throws<ObjectDisposedException>(()=>resolver.Resolve("local.paths",references[0],ResourcePurpose.FileRead));
  Assert.Equal(references,reopened.FindNode(f.Root.Id,node.Id)!.Configuration.Fields.Get<SequenceValue>("local.paths").Cast<string>());
  Assert.Equal(bytes,File.ReadAllBytes(reopened.CreateResourcePathResolver().Resolve("local.paths",references[0],ResourcePurpose.FileRead).Address));
  var disabled=reopened.Editing.Host.BeginEdit(f.Root.Id,node.Id);disabled.ApplyFields(new Dictionary<string,ParameterSetting>{{"local.save-images",new ConstantSetting(false)}});
  reopened.CommitEdit(DocumentEditProposal.Prepare(reopened.Editing.Host,nodes:new[]{disabled}));
  var external=Path.Combine(f.CaseDirectory,"external.solution");
  var receipt=reopened.SaveAs(external,Path.Combine(f.CaseDirectory,"backup"),Path.GetDirectoryName(moved)!,SessionGraphFixture.Types,SessionGraphFixture.Options,_=>null);
  Assert.Contains("原文件当前不可用",receipt.Warning);
  var externalJson=System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllBytes(external))!;Assert.Null(externalJson["EmbeddedFiles"]);
  Assert.Equal(references,reopened.FindNode(f.Root.Id,node.Id)!.Configuration.Fields.Get<SequenceValue>("local.paths").Cast<string>());
 }

 [Fact] public void Attachment_limit_keeps_the_remaining_external_files_and_reports_partial_embedding()
 {
  using var f=new SessionGraphFixture();var node=f.Add();var files=Enumerable.Range(0,121).Select(index=>Path.Combine(f.CaseDirectory,index+".bmp")).ToArray();
  foreach(var file in files)File.WriteAllBytes(file,new byte[]{1});
  var draft=f.Session.Editing.Host.BeginEdit(f.Root.Id,node.Id);var type=draft.SettingsSchema.Inputs.Single(port=>port.Id=="local.paths").Type;
  draft.ApplyFields(new Dictionary<string,ParameterSetting>{{"local.paths",new ConstantSetting(new SequenceValue(type,files.Cast<object?>()))},{"local.save-images",new ConstantSetting(true)}});
  f.Session.CommitEdit(DocumentEditProposal.Prepare(f.Session.Editing.Host,nodes:new[]{draft}));
  var path=Path.Combine(f.CaseDirectory,"limited.solution");
  var receipt=f.Session.SaveAs(path,Path.Combine(f.CaseDirectory,"backup"),f.CaseDirectory,SessionGraphFixture.Types,SessionGraphFixture.Options,_=>null);
  Assert.Contains("120",receipt.Warning);var root=System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllBytes(path))!;
  Assert.Equal(120,Assert.IsType<System.Text.Json.Nodes.JsonArray>(root["EmbeddedFiles"]).Count);
  var resources=root["Workflows"]![0]!["Nodes"]![0]!["Configuration"]!["Fields"]!["local.paths"]!["Value"]!.AsArray();
  Assert.Equal(121,resources.Count);Assert.Equal(files[120],resources[120]!.GetValue<string>());
  var excess=root["EmbeddedFiles"]![0]!.DeepClone();var sha=excess["Sha256"]!.GetValue<string>();
  var origin=excess["OriginIdentity"]!.GetValue<string>();
  var id=Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(origin+"\0"+Path.GetFullPath(files[120]).ToUpperInvariant()+"\0"+sha)));
  var reference="suneye-file:"+id+"/"+Path.GetFileName(files[120]);
  excess["Reference"]=reference;excess["OriginalAddress"]=files[120];excess["Name"]=Path.GetFileName(files[120]);
  root["EmbeddedFiles"]!.AsArray().Add(excess);resources[120]=reference;
  var invalid=Path.Combine(f.CaseDirectory,"over-limit.solution");File.WriteAllText(invalid,root.ToJsonString());
  var error=Assert.Throws<System.Text.Json.JsonException>(()=>f.Manager.Open(invalid,_=>SessionGraphFixture.Types,_=>null,SessionGraphFixture.Options));
  Assert.Contains("count exceeds",error.Message);Assert.Same(f.Session,f.Manager.CurrentSession);
 }

 [Fact] public void A_damaged_attachment_does_not_replace_the_active_solution()
 {
  using var f=new SessionGraphFixture();var node=f.Add();var source=Path.Combine(f.CaseDirectory,"a.bmp");File.WriteAllBytes(source,new byte[]{1,2,3});
  var draft=f.Session.Editing.Host.BeginEdit(f.Root.Id,node.Id);var type=draft.SettingsSchema.Inputs.Single(port=>port.Id=="local.paths").Type;
  draft.ApplyFields(new Dictionary<string,ParameterSetting>{{"local.paths",new ConstantSetting(new SequenceValue(type,new object?[]{source}))},{"local.save-images",new ConstantSetting(true)}});
  f.Session.CommitEdit(DocumentEditProposal.Prepare(f.Session.Editing.Host,nodes:new[]{draft}));var saved=f.Save();
  var json=System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllBytes(saved))!;json["EmbeddedFiles"]![0]!["Content"]=Convert.ToBase64String(new byte[]{3,2,1});
  var corrupt=Path.Combine(f.CaseDirectory,"corrupt.solution");File.WriteAllText(corrupt,json.ToJsonString());
  Assert.Throws<System.Text.Json.JsonException>(()=>f.Manager.Open(corrupt,_=>SessionGraphFixture.Types,_=>null,SessionGraphFixture.Options));
  Assert.Same(f.Session,f.Manager.CurrentSession);
 }

 [Fact] public void Missing_source_aborts_embedding_before_replacing_the_written_solution()
 {
  using var f=new SessionGraphFixture();var node=f.Add();var saved=f.Save();var original=File.ReadAllBytes(saved);var previous=f.Session.LastWrittenSnapshot;
  var draft=f.Session.Editing.Host.BeginEdit(f.Root.Id,node.Id);var type=draft.SettingsSchema.Inputs.Single(port=>port.Id=="local.paths").Type;
  draft.ApplyFields(new Dictionary<string,ParameterSetting>{["local.paths"]=new ConstantSetting(new SequenceValue(type,new object?[]{Path.Combine(f.CaseDirectory,"missing.bmp")})),
   ["local.save-images"]=new ConstantSetting(true)});
  f.Session.CommitEdit(DocumentEditProposal.Prepare(f.Session.Editing.Host,nodes:new[]{draft}));
  Assert.Throws<FileNotFoundException>(()=>f.Session.Save(saved,Path.Combine(f.CaseDirectory,"backup"),SessionGraphFixture.Types,SessionGraphFixture.Options));
  Assert.Equal(original,File.ReadAllBytes(saved));Assert.Same(previous,f.Session.LastWrittenSnapshot);
 }

 [Fact] public void SaveAs_and_subsequent_save_preserve_live_relative_resource_base_and_reopen()
 {
  using var f=new SessionGraphFixture();var node=f.Add();var asset=Path.Combine(f.CaseDirectory,"image.bmp");File.WriteAllBytes(asset,new byte[]{0x42,0x4d});
  var draft=f.Session.Editing.Host.BeginEdit(f.Root.Id,node.Id);var port=draft.SettingsSchema.Inputs.Single(p=>p.Id=="local.paths");
  draft.ApplyFields(new Dictionary<string,ParameterSetting>{{"local.paths",new ConstantSetting(new SequenceValue(port.Type,new object?[]{"image.bmp"}))}});
  f.Session.CommitEdit(DocumentEditProposal.Prepare(f.Session.Editing.Host,nodes:new[]{draft}));
  var movedDir=Path.Combine(f.CaseDirectory,"moved");Directory.CreateDirectory(movedDir);var path=Path.Combine(movedDir,"moved.solution");
  var first=f.Session.SaveAs(path,Path.Combine(f.CaseDirectory,"backup"),f.CaseDirectory,SessionGraphFixture.Types,SessionGraphFixture.Options,_=>null);
  var second=f.Session.Save(path,Path.Combine(f.CaseDirectory,"backup"),SessionGraphFixture.Types,SessionGraphFixture.Options);
  Assert.Equal(asset,first.WrittenSnapshot.ResourcePlan.Single(p=>p.Reference.EndsWith("image.bmp")).AbsolutePath,true);
  Assert.Equal(asset,second.WrittenSnapshot.ResourcePlan.Single(p=>p.Reference.EndsWith("image.bmp")).AbsolutePath,true);
  Assert.Equal(asset,f.Session.CreateResourcePathResolver().Resolve("local.paths","image.bmp",ResourcePurpose.FileRead).Address,true);
  var opened=f.Manager.Open(path,_=>SessionGraphFixture.Types,_=>null,SessionGraphFixture.Options);var actual=opened.FindNode(f.Root.Id,node.Id)!;
  var saved=(SequenceValue)Assert.IsType<ConstantSetting>(actual.Configuration.Fields.Read("local.paths")).Value!;
  Assert.Equal(asset,opened.CreateResourcePathResolver().Resolve("local.paths",Assert.IsType<string>(saved.Single()),ResourcePurpose.FileRead).Address,true);
 }
 [Fact] public void Restore_rejects_wrong_target_identity_and_preserves_target_backup_and_active_session()
 {
  using var f=new SessionGraphFixture();f.Add();var saved=f.Save();var target=Path.Combine(f.CaseDirectory,"inactive.solution");var backup=Path.Combine(f.CaseDirectory,"restore.solution");File.Copy(saved,target);File.Copy(saved,backup);
  var targetBytes=File.ReadAllBytes(target);var backupBytes=File.ReadAllBytes(backup);var backupHash=Convert.ToHexString(SHA256.HashData(backupBytes));
  Assert.ThrowsAny<Exception>(()=>f.Manager.RestoreInactive(target,backup,new string('0',64),backupHash,_=>SessionGraphFixture.Types,_=>null,SessionGraphFixture.Options));
  Assert.Equal(targetBytes,File.ReadAllBytes(target));Assert.Equal(backupBytes,File.ReadAllBytes(backup));Assert.Same(f.Session,f.Manager.CurrentSession);
 }
 [Fact] public void Restore_inactive_target_accepts_valid_backup_without_modifying_backup_or_active_session()
 {
  using var f=new SessionGraphFixture();f.Add();var saved=f.Save();var target=Path.Combine(f.CaseDirectory,"target.solution");var backup=Path.Combine(f.CaseDirectory,"previous.solution");
  File.Copy(saved,target);File.Copy(saved,backup);var bytes=File.ReadAllBytes(backup);var hash=Convert.ToHexString(SHA256.HashData(bytes));
  f.Manager.RestoreInactive(target,backup,hash,hash,_=>SessionGraphFixture.Types,_=>null,SessionGraphFixture.Options);
  Assert.Equal(bytes,File.ReadAllBytes(target));Assert.Equal(bytes,File.ReadAllBytes(backup));Assert.Same(f.Session,f.Manager.CurrentSession);
 }
 [Fact] public void Restore_active_target_is_rejected_without_modifying_file()
 {
  using var f=new SessionGraphFixture();f.Add();var target=f.Save();var backup=Path.Combine(f.CaseDirectory,"previous.solution");File.Copy(target,backup);
  var bytes=File.ReadAllBytes(target);var hash=Convert.ToHexString(SHA256.HashData(bytes));
  Assert.Throws<InvalidOperationException>(()=>f.Manager.RestoreInactive(target,backup,hash,hash,_=>SessionGraphFixture.Types,_=>null,SessionGraphFixture.Options));
  Assert.Equal(bytes,File.ReadAllBytes(target));Assert.Same(f.Session,f.Manager.CurrentSession);
 }
}
