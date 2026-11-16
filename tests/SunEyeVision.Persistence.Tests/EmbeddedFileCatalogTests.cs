using System.Text.Json;
using System.Text.Json.Nodes;
using SunEyeVision.Plugin.Contracts;
using Xunit;

namespace SunEyeVision.Persistence.Tests;

public sealed class EmbeddedFileCatalogTests
{
    [Fact]
    public void Distinct_encoded_references_cannot_collide_in_a_case_insensitive_materialization_directory()
    {
        var upper=EmbeddedFileCatalog.Create(Path.Combine(Path.GetTempPath(),"Case.bmp"),new byte[]{1},"same-origin-token");
        var lower=EmbeddedFileCatalog.Create(Path.Combine(Path.GetTempPath(),"case.bmp"),new byte[]{1},"same-origin-token");
        using var catalog=EmbeddedFileCatalog.Parse(new JsonObject{["EmbeddedFiles"]=new JsonArray(EmbeddedFileCatalog.Write(upper),EmbeddedFileCatalog.Write(lower))});
        catalog.MaterializeForAdmission();
        var first=catalog.ResolveAddress(upper.Reference);var second=catalog.ResolveAddress(lower.Reference);
        Assert.False(string.Equals(first,second,StringComparison.OrdinalIgnoreCase));
        Assert.Equal(new byte[]{1},File.ReadAllBytes(first));Assert.Equal(new byte[]{1},File.ReadAllBytes(second));
    }

    [Fact]
    public void Decoded_scope_transfer_keeps_encoded_files_readable_until_the_actual_owner_closes()
    {
        var bytes=new byte[]{1,2,3,4};
        var entry=EmbeddedFileCatalog.Create(Path.Combine(Path.GetTempPath(),"original-image.bmp"),bytes);
        var root=new JsonObject{["EmbeddedFiles"]=new JsonArray(EmbeddedFileCatalog.Write(entry))};
        var files=EmbeddedFileCatalog.Parse(root);using var scope=new DecodedResourcesScope();scope.RegisterNew(files);
        files.ValidateReferences(new[]{new FrozenResourceReference("$.field",entry.Reference,ResourcePurpose.FileRead,null)});
        var snapshot=files.Copy(entry.Reference);snapshot.Bytes[0]=99;
        var resolver=new SolutionResourceResolver(null,files);
        var target=resolver.Resolve("field",entry.Reference,ResourcePurpose.FileRead).Address;
        Assert.Equal(bytes,File.ReadAllBytes(target));
        Assert.Throws<IOException>(()=>File.WriteAllBytes(target,new byte[]{99}));
        var owner=scope.Transfer();
        try{scope.Dispose();Assert.Equal(bytes,File.ReadAllBytes(target));}
        finally{owner.Dispose();}
        Assert.False(File.Exists(target));Assert.Throws<ObjectDisposedException>(()=>resolver.Resolve("field",entry.Reference,ResourcePurpose.FileRead));
    }

    [Theory]
    [InlineData("digest")]
    [InlineData("length")]
    [InlineData("name")]
    [InlineData("budget")]
    [InlineData("duplicate")]
    public void Corrupt_or_ambiguous_encoded_assets_are_rejected_before_file_materialization(string corruption)
    {
        var entry=EmbeddedFileCatalog.Create(Path.Combine(Path.GetTempPath(),"evidence.bmp"),new byte[]{10,20,30});
        var file=EmbeddedFileCatalog.Write(entry);var assets=new JsonArray(file);
        switch(corruption)
        {
            case "digest":file["Content"]=Convert.ToBase64String(new byte[]{30,20,10});break;
            case "length":file["Length"]=2;break;
            case "name":file["Name"]="../escape.bmp";break;
            case "budget":file["Length"]=EmbeddedFileCatalog.MaximumBytes+1;break;
            case "duplicate":assets.Add(file.DeepClone());break;
        }
        Assert.Throws<JsonException>(()=>EmbeddedFileCatalog.Parse(new JsonObject{["EmbeddedFiles"]=assets}));
    }

    [Fact]
    public void A_file_catalog_rejects_orphans_and_dangling_declarations()
    {
        var entry=EmbeddedFileCatalog.Create(Path.Combine(Path.GetTempPath(),"owned.bmp"),new byte[]{1});
        using var catalog=EmbeddedFileCatalog.Parse(new JsonObject{["EmbeddedFiles"]=new JsonArray(EmbeddedFileCatalog.Write(entry))});
        Assert.Throws<JsonException>(()=>catalog.ValidateReferences(Array.Empty<FrozenResourceReference>()));
        Assert.Throws<JsonException>(()=>catalog.ValidateReferences(new[]{new FrozenResourceReference("$.field","suneye-file:missing",ResourcePurpose.FileRead,null)}));
    }
}
