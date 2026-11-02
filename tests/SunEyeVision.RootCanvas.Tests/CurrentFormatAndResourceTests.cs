using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Workflow;
using Xunit;

namespace SunEyeVision.UI.Tests;

[Collection("VM canvas UI")]
public sealed class CurrentFormatAndResourceTests
{
    private static string Root()
    {
        var root=Environment.GetEnvironmentVariable("S2_CASE_ROOT") ?? throw new InvalidOperationException("Missing isolated test data root.");
        var directory=Path.Combine(root,"v3-solution",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);return directory;
    }
    private static string Hash(string path)=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    private static (SolutionManager Manager,Solution Document,string Path) NewDocument(string tool="ImageLoad")
    {
        var root=Root();var manager=new SolutionManager(Path.Combine(root,"solutions"),RootCanvasFixture.Directory);
        var document=manager.CreateUntitledSolution();
        document.Workflows[0].Nodes.Add(new WorkflowNodeBase("node","node","node",tool)
            {RuntimeConfiguration=RuntimeToolConfiguration.Create(RootCanvasFixture.Directory,tool)});
        var path=Path.Combine(root,"source.solution");Assert.True(manager.SaveSolution(path),manager.LastError);
        return(manager,document,path);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(0)]
    public void Old_unknown_and_missing_versions_are_rejected_without_modifying_bytes(int version)
    {
        var (manager,_,path)=NewDocument();var root=JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        if(version==0)root.Remove("SchemaVersion");else root["SchemaVersion"]=version;
        File.WriteAllText(path,root.ToJsonString());var before=Hash(path);
        var result=new SolutionRepository(directory:RootCanvasFixture.Directory).LoadCandidate(path);
        Assert.Null(result.Candidate);Assert.True(result.HasErrors);Assert.Equal(before,Hash(path));
        var metadata=manager.GetMetadata(manager.CurrentSolution!.Id)!;
        Assert.Throws<InvalidDataException>(()=>manager.UpdateMetadata(metadata));Assert.Equal(before,Hash(path));
    }

    [Fact]
    public void Unbound_blob_is_editable_and_persistable_without_bypassing_structural_validation()
    {
        var (_,document,path)=NewDocument("BlobAnalysis");
        Assert.Equal(3,JsonNode.Parse(File.ReadAllText(path))!["SchemaVersion"]!.GetValue<int>());
        var result=new SolutionRepository(directory:RootCanvasFixture.Directory).LoadCandidate(path);
        Assert.NotNull(result.Candidate);Assert.Equal(document.Id,result.Candidate!.Id);
        var malformed=JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        malformed["UnexpectedField"]=true;var invalid=Path.Combine(Path.GetDirectoryName(path)!,"invalid.solution");
        File.WriteAllText(invalid,malformed.ToJsonString());var before=Hash(invalid);
        Assert.Null(new SolutionRepository(directory:RootCanvasFixture.Directory).LoadCandidate(invalid).Candidate);
        Assert.Equal(before,Hash(invalid));
    }

    [Fact]
    public void Restore_previous_save_uses_current_format_and_preserves_the_backup()
    {
        var (manager,document,path)=NewDocument();var node=document.Workflows[0].Nodes[0];
        node.RuntimeConfiguration=node.RuntimeConfiguration!.WithFieldValues(new Dictionary<string,object?>{{"sequence.initial",42L}});
        Assert.True(manager.SaveSolution(),manager.LastError);
        var repository=(SolutionRepository)typeof(SolutionManager).GetField("_repository",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(manager)!;
        var backup=repository.GetRecoveryPath(path);var backupHash=Hash(backup);
        Assert.True(manager.HasPreviousSave(document.Id));
        var restored=manager.RestorePreviousSave(document.Id);
        Assert.Equal(1L,restored.Workflows[0].Nodes[0].RuntimeConfiguration!.ReadFieldValues().Get<long>("sequence.initial"));
        Assert.Equal(1L,new SolutionRepository(directory:RootCanvasFixture.Directory).LoadCandidate(path).Candidate!
            .Workflows[0].Nodes[0].RuntimeConfiguration!.ReadFieldValues().Get<long>("sequence.initial"));
        Assert.Equal(backupHash,Hash(backup));
    }

    [Fact]
    public void Image_source_paths_are_declared_and_copy_rebases_relative_constants()
    {
        var (manager,document,path)=NewDocument();var originalDirectory=Path.GetDirectoryName(path)!;
        var files=Path.Combine(originalDirectory,"assets");Directory.CreateDirectory(files);File.WriteAllText(Path.Combine(files,"a.png"),"fixture");
        var node=document.Workflows[0].Nodes[0];node.RuntimeConfiguration=node.RuntimeConfiguration!.WithFieldValues(new Dictionary<string,object?>
        { ["local.paths"]=new[]{"assets/a.png"},["local.selected"]=Array.Empty<string>() });
        Assert.True(manager.SaveSolution(),manager.LastError);
        var declarations=RootCanvasFixture.Directory.GetResourceFields("ImageLoad");
        Assert.Equal(new[]{"local.paths","local.selected"},declarations.Select(item=>item.FieldId).OrderBy(id=>id,StringComparer.Ordinal));
        Assert.All(declarations,item=>Assert.Equal(ResourcePurpose.FileRead,item.Purpose));
        Assert.Empty(RootCanvasFixture.Directory.GetResourceFields("BlobAnalysis"));
        var targetDirectory=Path.Combine(originalDirectory,"copies");Directory.CreateDirectory(targetDirectory);
        var copy=manager.CopySolution(path,"copy",targetDirectory:targetDirectory);Assert.NotNull(copy);
        var loaded=new SolutionRepository(directory:RootCanvasFixture.Directory).LoadCandidate(copy!.FilePath);
        Assert.NotNull(loaded.Candidate);
        var paths=loaded.Candidate!.Workflows[0].Nodes[0].RuntimeConfiguration!.ReadFieldValues().Get<SequenceValue>("local.paths");
        Assert.Equal(Path.GetFullPath(Path.Combine(files,"a.png")),Path.GetFullPath((string)paths[0]!,targetDirectory));
    }
}
