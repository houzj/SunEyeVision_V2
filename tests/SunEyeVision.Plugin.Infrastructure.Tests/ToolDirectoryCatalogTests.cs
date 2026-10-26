using System;
using System.IO;
using System.Linq;
using SunEyeVision.Plugin.Infrastructure.Managers.Plugin;
using Xunit;
namespace SunEyeVision.Plugin.Infrastructure.Tests;
public sealed class ToolDirectoryCatalogTests
{
    private static (string Root,string Groups,string Manifest) Fixture(string visible="true")
    {
        var root=Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT") ?? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","..","_validation","ToolboxCases")),Guid.NewGuid().ToString("N"));
        var directory=Path.Combine(root,"plugins","sample");Directory.CreateDirectory(directory);
        var groups=Path.Combine(root,"ToolGroups.xml");
        File.WriteAllText(groups,$"<ToolGroups SchemaVersion='1'><Group Name='采集' Order='1' Icon='a.png' HoverIcon='b.png'/><Tool Id='image' Category='采集' Directory='sample' IsVisible='{visible}'/></ToolGroups>");
        return(root,groups,Path.Combine(directory,"ToolItemInfo.xml"));
    }
    private static void WriteManifest(string path,string visible="true")=>File.WriteAllText(path,$"<ToolItems SchemaVersion='1' Assembly='Missing.dll'><Tool Id='image' DisplayName='图像源' Category='采集' Order='10' Icon='image' EntryType='Missing.Image' IsVisible='{visible}'/></ToolItems>");
    [Fact] public void Discovery_does_not_read_tool_manifest_or_load_missing_dll()
    {
        var f=Fixture();File.WriteAllText(f.Manifest,"not XML");
        var c=new ToolDirectoryCatalog(Path.Combine(f.Root,"plugins"),f.Groups);
        Assert.Equal("image",Assert.Single(c.ToolIds));Assert.Equal(1,c.CountCategory("采集"));
        Assert.ThrowsAny<Exception>(()=>c.GetCategory("采集"));
        WriteManifest(f.Manifest);Assert.Equal("image",Assert.Single(c.GetCategory("采集")).Id);
    }
    [Fact] public void Visibility_is_metadata_not_dll_availability()
    {
        var f=Fixture("false");WriteManifest(f.Manifest,"false");
        var c=new ToolDirectoryCatalog(Path.Combine(f.Root,"plugins"),f.Groups);
        Assert.Empty(c.GetCategory("采集"));Assert.NotNull(c.Find("image"));
        Assert.False(File.Exists(Path.Combine(Path.GetDirectoryName(f.Manifest)!,"Missing.dll")));
    }
    [Fact] public void Declared_identity_must_match_category_index()
    {
        var f=Fixture();WriteManifest(f.Manifest);File.WriteAllText(f.Manifest,File.ReadAllText(f.Manifest).Replace("Id='image'","Id='other'"));
        var c=new ToolDirectoryCatalog(Path.Combine(f.Root,"plugins"),f.Groups);
        Assert.Throws<InvalidDataException>(()=>c.GetCategory("采集"));
    }
    [Fact] public void Auxiliary_directory_cannot_create_a_tool()
    {
        var f=Fixture();WriteManifest(f.Manifest);Directory.CreateDirectory(Path.Combine(f.Root,"plugins","sample_wpftmp"));
        var c=new ToolDirectoryCatalog(Path.Combine(f.Root,"plugins"),f.Groups);
        Assert.Single(c.ToolIds);Assert.Single(c.Tools);
    }
}