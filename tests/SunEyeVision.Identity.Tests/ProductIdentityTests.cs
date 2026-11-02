using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using SunEyeVision.Foundation.Versioning;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Infrastructure.Managers.Plugin;
using Xunit;

public sealed class ProductIdentityTests
{
    [Fact] public void Valid_payload_uses_neutral_foundation_anchor()
    {
        using var f=new Fixture();
        Assert.Single(new ReleasePayloadAuthority(f.Root).ExpectedTools);
        Assert.Equal("SunEyeVision.Foundation",typeof(AppVersionInfo).Assembly.GetName().Name);
        Assert.DoesNotContain(typeof(AppVersionInfo).Assembly.GetReferencedAssemblies(),a=>a.Name is "PresentationFramework" or "PresentationCore" or "WindowsBase" or "System.Xaml" or "SunEyeVision.Plugin.SDK");
    }
    [Theory][InlineData("sourceCommit")][InlineData("artifactVersion")]
    public void Header_must_match_approved_anchor(string field)
    {
        using var f=new Fixture();var path=Path.Combine(f.Root,"release-payload.json");var json=JsonNode.Parse(File.ReadAllText(path))!;
        json[field]="incorrect";File.WriteAllText(path,json.ToJsonString());
        Assert.Contains(field,Assert.Throws<InvalidDataException>(()=>new ReleasePayloadAuthority(f.Root)).Message);
    }
    [Theory][InlineData("missing")][InlineData("bytes")][InlineData("identity")]
    public void Anchor_must_match_actual_loaded_bytes_and_identity(string corruption)
    {
        using var f=new Fixture();var anchor=Path.Combine(f.Root,"SunEyeVision.Foundation.dll");
        if(corruption=="bytes")File.AppendAllText(anchor,"different");
        if(corruption=="identity")File.Copy(typeof(ProductIdentityTests).Assembly.Location,anchor,true);
        f.Write();
        if(corruption=="missing"){var p=Path.Combine(f.Root,"release-payload.json");var j=JsonNode.Parse(File.ReadAllText(p))!;j["entries"]!.AsArray().RemoveAt(1);File.WriteAllText(p,j.ToJsonString());}
        Assert.ThrowsAny<Exception>(()=>new ReleasePayloadAuthority(f.Root));
    }
    private sealed class Fixture:IDisposable
    {
        public string Root {get;}=Path.Combine(Path.GetTempPath(),"suneye-identity-"+Guid.NewGuid().ToString("N"));
        public Fixture(){Directory.CreateDirectory(Root);File.Copy(typeof(ProductIdentityTests).Assembly.Location,Path.Combine(Root,"Probe.dll"));File.Copy(typeof(AppVersionInfo).Assembly.Location,Path.Combine(Root,"SunEyeVision.Foundation.dll"));Write();}
        public void Write()
        {
            var component=new{componentId="test.runtime",toolId="Probe",role="Runtime",contractMajor=FoundationContract.Major,contractMinor=FoundationContract.Minor,entryType="Probe.Tool",companionToolId=(string?)null};
            object Entry(string name,object[]? components=null){var f=Path.Combine(Root,name);return new{path=name,sha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(f))),assemblyIdentity=AssemblyName.GetAssemblyName(f).FullName,components=components??Array.Empty<object>()};}
            var payload=new{payloadSchemaVersion=2,buildId="test",sourceCommit=AppVersionInfo.Commit,artifactVersion=AppVersionInfo.Product+(AppVersionInfo.Channel=="stable"?"":"_"+AppVersionInfo.Channel),entries=new[]{Entry("Probe.dll",new object[]{component}),Entry("SunEyeVision.Foundation.dll")},productComponents=new[]{new{componentId="test.runtime",toolId="Probe",role="Runtime",entryType="Probe.Tool",path="Probe.dll",companionToolId=""}},runtimeFrameworks=new{deploymentMode="frameworkDependent",frameworks=new[]{new{name="Microsoft.NETCore.App",version="9.0.0"}}}};
            File.WriteAllText(Path.Combine(Root,"release-payload.json"),JsonSerializer.Serialize(payload));
        }
        public void Dispose()=>Directory.Delete(Root,true);
    }
}