using System.Reflection;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using SunEyeVision.Foundation.Versioning;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Plugin.Infrastructure.Managers.Plugin;
using Xunit;

namespace SunEyeVision.Plugin.Infrastructure.Tests;

public sealed class ReleasePayloadAuthorityTests
{
    [Theory]
    [InlineData("sourceCommit")]
    [InlineData("artifactVersion")]
    public void Nonempty_header_conflicting_with_approved_actual_Foundation_is_rejected(string field)
    {
        using var fixture=new PayloadFixture();
        var path=Path.Combine(fixture.Root,"release-payload.json");
        var payload=System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path))!;
        payload[field]=field=="sourceCommit"?new string('0',40):"conflicting-artifact-version";
        File.WriteAllText(path,payload.ToJsonString());
        var error=Assert.Throws<InvalidDataException>(()=>new ReleasePayloadAuthority(fixture.Root));
        Assert.Contains(field,error.Message);
    }

    [Fact]
    public void BuildId_is_an_opaque_nonempty_generator_identifier_not_a_PE_identity()
    {
        using var fixture=new PayloadFixture();
        var path=Path.Combine(fixture.Root,"release-payload.json");
        var payload=System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path))!;
        payload["buildId"]="different-generator-"+Guid.NewGuid().ToString("N");
        File.WriteAllText(path,payload.ToJsonString());
        Assert.Single(new ReleasePayloadAuthority(fixture.Root).ExpectedTools);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("bytes")]
    [InlineData("identity")]
    public void Product_anchor_must_approve_the_actual_Default_Foundation_identity_and_bytes(string corruption)
    {
        using var fixture=new PayloadFixture();
        var core=Path.Combine(fixture.Root,"SunEyeVision.Foundation.dll");
        if(corruption=="bytes")File.AppendAllText(core,"different-approved-copy");
        if(corruption=="identity")File.Copy(typeof(SunEyeVision.Plugin.SDK.Core.ToolAttribute).Assembly.Location,core,true);
        fixture.Write();
        if(corruption=="missing") {
            var path=Path.Combine(fixture.Root,"release-payload.json");
            var payload=System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path))!;
            var entries=payload["entries"]!.AsArray();
            entries.RemoveAt(1);File.WriteAllText(path,payload.ToJsonString());
        }
        Assert.ThrowsAny<Exception>(()=>new ReleasePayloadAuthority(fixture.Root));
    }

    [Fact]
    public void AppContext_style_root_with_trailing_separator_matches_its_payload_ancestors()
    {
        using var fixture=new PayloadFixture();
        var authority=new ReleasePayloadAuthority(fixture.Root+Path.DirectorySeparatorChar);
        authority.VerifyApprovedFile(fixture.Plugin);
        Assert.Single(authority.ExpectedTools);
    }
    [Theory]
    [InlineData("identity")]
    [InlineData("pack")]
    [InlineData("path")]
    [InlineData("empty")]
    public void Self_contained_framework_winner_provenance_cannot_conflict_with_payload(string corruption)
    {
        using var fixture=new PayloadFixture();
        var path=Path.Combine(fixture.Root,"release-payload.json");
        var payload=System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path))!;
        var asset=new {payloadPath="Probe.dll",runtimePath=corruption=="path"?"../escape.dll":"runtimes/win-x64/lib/net9.0/Probe.dll",
            packId=corruption=="pack"?"Arbitrary.Package":"Microsoft.NETCore.App.Runtime.win-x64",packVersion="9.0.20",
            assemblyIdentity=corruption=="identity"?"Incorrect, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null":AssemblyName.GetAssemblyName(fixture.Plugin).FullName};
        payload["runtimeFrameworks"]=JsonSerializer.SerializeToNode(new {deploymentMode="selfContained",frameworks=new[]{new{name="Microsoft.NETCore.App",version="9.0.20",managedAssets=corruption=="empty"?Array.Empty<object>():new object[]{asset}}}});
        File.WriteAllText(path,payload.ToJsonString());
        Assert.ThrowsAny<Exception>(()=>new ReleasePayloadAuthority(fixture.Root));
    }

    [Fact]
    public void Approved_bytes_are_rechecked_at_use_and_unlisted_paths_are_rejected()
    {
        using var fixture=new PayloadFixture();
        var authority=new ReleasePayloadAuthority(fixture.Root);
        authority.VerifyApprovedFile(fixture.Plugin);
        var unlisted=Path.Combine(fixture.Root,"extra.dll");File.Copy(fixture.Plugin,unlisted);
        Assert.Throws<InvalidDataException>(()=>authority.VerifyApprovedFile(unlisted));
        File.AppendAllText(fixture.Plugin,"changed-after-approval");
        Assert.Throws<InvalidDataException>(()=>authority.VerifyApprovedFile(fixture.Plugin));
    }

    [Theory]
    [InlineData("schema")]
    [InlineData("identity")]
    [InlineData("duplicate-path")]
    [InlineData("escape")]
    [InlineData("duplicate-tool")]
    [InlineData("unknown-framework-mode")]
    [InlineData("missing-frameworks")]
    public void Corrupt_or_ambiguous_authority_is_rejected(string corruption)
    {
        using var fixture=new PayloadFixture();
        fixture.Write(corruption);
        Assert.ThrowsAny<Exception>(()=>new ReleasePayloadAuthority(fixture.Root));
    }

    [Fact]
    public void Missing_authority_does_not_infer_approval_from_the_directory()
    {
        using var fixture=new PayloadFixture();
        File.Delete(Path.Combine(fixture.Root,"release-payload.json"));
        Assert.Throws<FileNotFoundException>(()=>new ReleasePayloadAuthority(fixture.Root));
    }

    private sealed class PayloadFixture : IDisposable
    {
        internal string Root{get;}=Path.Combine(Path.GetTempPath(),"SunEyeVision-A11-"+Guid.NewGuid().ToString("N"));
        internal string Plugin=>Path.Combine(Root,"Probe.dll");
        internal PayloadFixture(){Directory.CreateDirectory(Path.GetDirectoryName(Plugin)!);File.Copy(typeof(ReleasePayloadAuthorityTests).Assembly.Location,Plugin);File.Copy(typeof(AppVersionInfo).Assembly.Location,Path.Combine(Root,"SunEyeVision.Foundation.dll"));Write();}
        internal void Write(string? corruption=null)
        {
            var component=new Dictionary<string,object?>{{"componentId","test.runtime"},{"toolId","Probe"},{"role","Runtime"},{"contractMajor",FoundationContract.Major},{"contractMinor",FoundationContract.Minor},{"entryType","Probe.Tool"},{"companionToolId",null}};
            var components=new List<object>{component};if(corruption=="duplicate-tool")components.Add(component);
            var entry=new Dictionary<string,object?>{
                {"path",corruption=="escape"?"../escape.dll":"Probe.dll"},
                {"sha256",Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Plugin)))},
                {"assemblyIdentity",corruption=="identity"?"Different, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null":AssemblyName.GetAssemblyName(Plugin).FullName},
                {"components",components}};
            var core=Path.Combine(Root,"SunEyeVision.Foundation.dll");
            var coreEntry=new Dictionary<string,object?>{{"path","SunEyeVision.Foundation.dll"},{"sha256",Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(core)))},{"assemblyIdentity",AssemblyName.GetAssemblyName(core).FullName}};
            var entries=new List<object>{entry,coreEntry};if(corruption=="duplicate-path")entries.Add(entry);
            var payload=new Dictionary<string,object?>{
                {"payloadSchemaVersion",corruption=="schema"?1:2},{"buildId","A11-owned-fixture"},{"sourceCommit",AppVersionInfo.Commit},{"artifactVersion",AppVersionInfo.Product+(AppVersionInfo.Channel=="stable"?"":"_"+AppVersionInfo.Channel)},
                {"productComponents",new[]{new{componentId="test.runtime",toolId="Probe",role="Runtime",entryType="Probe.Tool",path="Probe.dll",companionToolId=""}}},{"entries",entries},
                {"runtimeFrameworks",new{deploymentMode=corruption=="unknown-framework-mode"?"guessed":"frameworkDependent",frameworks=new[]{new{name="Microsoft.NETCore.App",version="9.0.0"},new{name="Microsoft.WindowsDesktop.App",version="9.0.0"}}}}};
            if(corruption=="missing-frameworks")payload.Remove("runtimeFrameworks");
            File.WriteAllText(Path.Combine(Root,"release-payload.json"),JsonSerializer.Serialize(payload));
        }
        public void Dispose(){Directory.Delete(Root,true);}
    }
}
