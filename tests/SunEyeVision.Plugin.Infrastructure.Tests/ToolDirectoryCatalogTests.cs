using System.IO;
using SunEyeVision.Plugin.Infrastructure.Managers.Plugin;
using Xunit;

namespace SunEyeVision.Plugin.Infrastructure.Tests;
public sealed class ToolDirectoryCatalogTests
{
    private sealed class Fixture:IDisposable
    {
        public string Root { get; }=Path.Combine(Path.GetTempPath(),"SunEyeDirectory-"+Guid.NewGuid().ToString("N"));
        public string Groups=>Path.Combine(Root,"ToolGroups.xml");
        public Fixture(){Directory.CreateDirectory(Root);File.WriteAllText(Groups,"<ToolGroups SchemaVersion='1'><Group Name='采集' Order='1' Icon='a.png' HoverIcon='b.png'/><Group Name='测量' Order='2' Icon='c.png' HoverIcon='d.png'/></ToolGroups>");}
        public void Dispose()=>Directory.Delete(Root,true);
    }
    private static DirectoryTool Tool(string id,int order=1,bool visible=true,string category="采集")=>new(id,id,category,order,"generic","",id+".Entry",id+".dll",visible);
    [Fact] public void Only_admitted_entries_form_directory_and_category_order()
    {
        using var f=new Fixture();var calls=new List<string>();var a=Tool("a",2);var b=Tool("b",1);var hidden=Tool("hidden",3,false);
        var c=new ToolDirectoryCatalog(f.Groups,new[]{a,b,hidden},calls.Add);
        Assert.Equal(new[]{f.Groups},calls);Assert.Equal(new[]{"b","a"},c.GetCategory("采集").Select(x=>x.Id));
        Assert.Equal(2,c.CountCategory("采集"));Assert.Equal(hidden,c.Find("hidden"));Assert.Equal(3,c.ToolIds.Count);
        Assert.Equal("a",a.Id);Assert.Equal("采集",a.Category);
    }
    [Fact] public void Duplicate_identity_unknown_category_and_empty_runtime_are_rejected()
    {
        using var f=new Fixture();Assert.Throws<InvalidDataException>(()=>new ToolDirectoryCatalog(f.Groups,new[]{Tool("a"),Tool("a")}, _ => {}));
        Assert.Throws<InvalidDataException>(()=>new ToolDirectoryCatalog(f.Groups,new[]{Tool("a",category:"不存在")}, _ => {}));
        Assert.Throws<InvalidDataException>(()=>new ToolDirectoryCatalog(f.Groups,Array.Empty<DirectoryTool>(), _ => {}));
    }
    [Fact] public void Group_manifest_cannot_declare_tool_producers_or_duplicate_categories()
    {
        using var f=new Fixture();File.WriteAllText(f.Groups,"<ToolGroups SchemaVersion='1'><Group Name='采集' Order='1' Icon='a' HoverIcon='b'/><Tool Id='fake'/></ToolGroups>");
        Assert.Throws<InvalidDataException>(()=>new ToolDirectoryCatalog(f.Groups,new[]{Tool("a")}, _ => {}));
        File.WriteAllText(f.Groups,"<ToolGroups SchemaVersion='1'><Group Name='采集' Order='1' Icon='a' HoverIcon='b'/><Group Name='采集' Order='2' Icon='a' HoverIcon='b'/></ToolGroups>");
        Assert.Throws<InvalidDataException>(()=>new ToolDirectoryCatalog(f.Groups,new[]{Tool("a")}, _ => {}));
    }
    [Fact] public void Group_xml_rejects_external_entity()
    {
        using var f=new Fixture();File.WriteAllText(f.Groups,"<!DOCTYPE x [<!ENTITY a SYSTEM 'file:///missing'>]><ToolGroups SchemaVersion='1'>&a;</ToolGroups>");
        Assert.ThrowsAny<Exception>(()=>new ToolDirectoryCatalog(f.Groups,new[]{Tool("a")}, _ => {}));
    }
}
