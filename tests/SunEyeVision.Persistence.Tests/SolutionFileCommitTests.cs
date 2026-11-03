using System.Security.Cryptography;
using System.Text;
using SunEyeVision.Persistence;
using Xunit;

public sealed class SolutionFileCommitTests
{
    private static byte[] Bytes(string text)=>Encoding.UTF8.GetBytes(text);
    private static string Root(){var p=Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,Guid.NewGuid().ToString("N"));Directory.CreateDirectory(p);return p;}
    private static string Hash(string path)=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    private static FrozenSolutionDocument Document(string path,TargetExpectation? expected=null)=>new(path,Bytes("{\"SchemaVersion\":4}"),Array.Empty<PersistenceDiagnostic>(),Array.Empty<FrozenResourceReference>(),expected);
    [Fact] public void Save_uses_owned_bytes_and_expected_hash_and_preserves_previous()
    {
        var root=Root();var path=Path.Combine(root,"main.solution");var backup=Path.Combine(root,"backup.solution");File.WriteAllText(path,"original");
        var document=Document(path,TargetExpectation.Match(Hash(path)));var bytes=document.CopyBytes();bytes[0]=0;
        SolutionFileCommit.Write(document,backup);
        Assert.Equal("{\"SchemaVersion\":4}",File.ReadAllText(path));Assert.Equal("original",File.ReadAllText(backup));
    }
    [Fact] public void Missing_and_wrong_hash_refuse_to_overwrite()
    {
        var root=Root();var path=Path.Combine(root,"main.solution");var backup=Path.Combine(root,"backup.solution");File.WriteAllText(path,"original");
        Assert.Throws<IOException>(()=>SolutionFileCommit.Write(Document(path,TargetExpectation.Missing),backup));
        Assert.Throws<IOException>(()=>SolutionFileCommit.Write(Document(path,TargetExpectation.Match(new string('0',64))),backup));
        Assert.Equal("original",File.ReadAllText(path));Assert.False(File.Exists(backup));
    }
    [Fact] public void Cancel_before_replacement_preserves_target_and_backup()
    {
        var root=Root();var path=Path.Combine(root,"main.solution");var backup=Path.Combine(root,"backup.solution");File.WriteAllText(path,"original");File.WriteAllText(backup,"older");
        using var cts=new CancellationTokenSource();
        Assert.Throws<OperationCanceledException>(()=>SolutionFileCommit.Write(path,backup,Bytes("new"),true,point=>{if(point=="before-target-replace")cts.Cancel();},TargetExpectation.Unchecked,cts.Token));
        Assert.Equal("original",File.ReadAllText(path));Assert.Equal("older",File.ReadAllText(backup));
    }
    [Fact] public void Fault_after_replacement_compensates_both_files()
    {
        var root=Root();var path=Path.Combine(root,"main.solution");var backup=Path.Combine(root,"backup.solution");File.WriteAllText(path,"original");File.WriteAllText(backup,"older");
        Assert.Throws<IOException>(()=>SolutionFileCommit.Write(path,backup,Bytes("new"),true,point=>{if(point=="target-replaced")throw new IOException("injected");}));
        Assert.Equal("original",File.ReadAllText(path));Assert.Equal("older",File.ReadAllText(backup));
    }
    [Fact] public void Cancellation_after_replacement_finishes_transaction()
    {
        var root=Root();var path=Path.Combine(root,"main.solution");var backup=Path.Combine(root,"backup.solution");File.WriteAllText(path,"original");
        using var cts=new CancellationTokenSource();
        SolutionFileCommit.Write(path,backup,Bytes("new"),true,point=>{if(point=="target-replaced")cts.Cancel();},TargetExpectation.Unchecked,cts.Token);
        Assert.Equal("new",File.ReadAllText(path));Assert.Equal("original",File.ReadAllText(backup));
    }
    [Fact] public void Restore_requires_same_validated_backup_bytes_and_keeps_source()
    {
        var root=Root();var path=Path.Combine(root,"main.solution");var backup=Path.Combine(root,"backup.solution");
        File.WriteAllText(path,"current");File.WriteAllText(backup,"{\"SchemaVersion\":4}");
        var targetHash=Hash(path);var backupHash=Hash(backup);
        var wrong=new FrozenSolutionDocument(path,Bytes("{\"SchemaVersion\":4,\"Name\":\"other\"}"),Array.Empty<PersistenceDiagnostic>(),Array.Empty<FrozenResourceReference>());
        Assert.Throws<IOException>(()=>SolutionFileCommit.Restore(wrong,backup,targetHash,backupHash));
        Assert.Equal("current",File.ReadAllText(path));Assert.Equal(backupHash,Hash(backup));
        var document=Document(path);var receipt=SolutionFileCommit.Restore(document,backup,targetHash,backupHash);
        Assert.Same(document,receipt.WrittenSnapshot);Assert.Equal("{\"SchemaVersion\":4}",File.ReadAllText(path));Assert.Equal(backupHash,Hash(backup));
    }
}

