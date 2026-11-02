using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using SunEyeVision.Workflow;
using Xunit;

namespace SunEyeVision.UI.Tests;

public sealed class RestoreCommitBoundaryTests
{
    private static readonly Type Commit=typeof(Solution).Assembly.GetType("SunEyeVision.Workflow.Persistence.SolutionFileCommit",true)!;
    private static readonly MethodInfo Restore=Commit.GetMethod("Restore",BindingFlags.Static|BindingFlags.NonPublic)!;
    private static readonly MethodInfo Confirm=Commit.GetMethod("ConfirmRecovery",BindingFlags.Static|BindingFlags.NonPublic)!;
    private static string Hash(string path)=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    private static (string Target,string Backup) Files()
    {
        var root=Environment.GetEnvironmentVariable("S2_CASE_ROOT") ?? throw new InvalidOperationException("Missing isolated validation root.");
        var directory=Path.Combine(root,"restore-boundary",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
        var target=Path.Combine(directory,"current.solution");var backup=Path.Combine(directory,"previous.solution");
        File.WriteAllText(target,"current",new UTF8Encoding(false));File.WriteAllText(backup,"previous",new UTF8Encoding(false));
        return(target,backup);
    }
    private static object? Invoke(string target,string backup,string targetHash,string backupHash,Action<string>? boundary=null)
        =>Restore.Invoke(null,new object?[]{target,backup,"previous",targetHash,backupHash,boundary});

    [Fact]
    public void Successful_restore_preserves_source_backup()
    {
        var (target,backup)=Files();var before=Hash(backup);
        Invoke(target,backup,Hash(target),before);
        Assert.Equal("previous",File.ReadAllText(target));Assert.Equal(before,Hash(backup));
    }

    [Fact]
    public void External_target_change_before_replace_is_not_overwritten()
    {
        var (target,backup)=Files();var targetHash=Hash(target);var backupHash=Hash(backup);
        var error=Assert.Throws<TargetInvocationException>(()=>Invoke(target,backup,targetHash,backupHash,
            phase=>{if(phase=="before-target-replace")File.WriteAllText(target,"external-target");}));
        Assert.IsAssignableFrom<IOException>(error.InnerException);Assert.Equal("external-target",File.ReadAllText(target));Assert.Equal(backupHash,Hash(backup));
    }

    [Fact]
    public void Recovery_change_after_replace_rolls_back_target_without_overwriting_external_backup()
    {
        var (target,backup)=Files();var targetHash=Hash(target);var backupHash=Hash(backup);
        var error=Assert.Throws<TargetInvocationException>(()=>Invoke(target,backup,targetHash,backupHash,
            phase=>{if(phase=="target-replaced")File.WriteAllText(backup,"external-backup");}));
        Assert.IsAssignableFrom<IOException>(error.InnerException);Assert.Equal(targetHash,Hash(target));Assert.Equal("external-backup",File.ReadAllText(backup));
    }

    [Fact]
    public void Failed_compensation_blocks_restore_until_explicit_recovery()
    {
        var (target,backup)=Files();var targetHash=Hash(target);var backupHash=Hash(backup);FileStream? held=null;
        try
        {
            var error=Assert.Throws<TargetInvocationException>(()=>Invoke(target,backup,targetHash,backupHash,phase=>
            {
                if(phase!="target-replaced")return;
                held=new FileStream(target,FileMode.Open,FileAccess.Read,FileShare.None);
                throw new IOException("injected post-replace failure");
            }));
            var recovery=Assert.IsType<SunEyeVision.Workflow.Persistence.SolutionSaveRecoveryRequiredException>(error.InnerException);
            var blocked=Assert.Throws<TargetInvocationException>(()=>Invoke(target,backup,targetHash,backupHash));
            Assert.IsType<SunEyeVision.Workflow.Persistence.SolutionSaveRecoveryRequiredException>(blocked.InnerException);
            held!.Dispose();held=null;
            File.Replace(recovery.LocalPreviousTargetPath,target,null,true);
            Confirm.Invoke(null,new object[]{target});
            Assert.Equal(targetHash,Hash(target));Assert.Equal(backupHash,Hash(backup));
        }
        finally{held?.Dispose();}
    }
}
