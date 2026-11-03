using System.IO;
using System.Security.Cryptography;
using SunEyeVision.Application.Editing;
using SunEyeVision.Plugin.Contracts;
using Xunit;
namespace SunEyeVision.WorkflowGraph.Tests;
public sealed class ResourcePersistenceTests
{
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
