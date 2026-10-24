using System.Reflection;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using SunEyeVision.Plugin.Infrastructure.Managers.Plugin;
using Xunit;

namespace SunEyeVision.Plugin.Infrastructure.Tests;

public sealed class ReleasePayloadAuthorityTests
{
    [Fact]
    public void Actual_Default_host_allows_lower_request_but_rejects_conflicting_local_bytes_and_unapproved_System_name()
    {
        var root=Environment.GetEnvironmentVariable("A11_SHARED_FIXTURE_ROOT")??throw new InvalidOperationException("Shared fixture facility was not supplied.");
        var app=Path.Combine(root,"app02");
        var authority=new ReleasePayloadAuthority(app);
        var actual=System.Runtime.Loader.AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(app,"System.A11.SharedFixture.dll"));
        var request=new AssemblyName(actual.FullName!){Version=new Version(9,0,0,0)};
        var bound=System.Runtime.Loader.AssemblyLoadContext.Default.LoadFromAssemblyName(request);
        Assert.Same(actual,bound);
        Assert.Equal("host10",actual.GetType("SharedProbe")!.GetMethod("Read")!.Invoke(null,null));
        authority.VerifyShared(request,bound,null);
        authority.VerifyShared(request,bound,Path.Combine(root,"run02-same-identity-same-bytes10","System.A11.SharedFixture.dll"));
        foreach(var candidate in new[]{"run02-local9","run02-same-identity-different-bytes10"})
            Assert.Throws<FileLoadException>(()=>authority.VerifyShared(request,bound,Path.Combine(root,candidate,"System.A11.SharedFixture.dll")));
        using var unrelated=new PayloadFixture();
        Assert.ThrowsAny<Exception>(()=>new ReleasePayloadAuthority(unrelated.Root).VerifyShared(request,bound,null));
        Assert.Throws<FileLoadException>(()=>authority.VerifyShared(new AssemblyName(actual.FullName!){Version=new Version(11,0,0,0)},bound,null));
    }

    [Fact]
    public void Default_host_request_satisfaction_allows_CLR_upward_binding_without_relaxing_BAML_identity()
    {
        var host=new AssemblyName("System.Collections.Immutable, Version=10.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
        var lower=new AssemblyName("System.Collections.Immutable, Version=9.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
        Assert.True(ReleasePayloadAuthority.HostSatisfiesDefaultRequest(lower,host));
        Assert.False(PluginLoadContext.MatchesRequestedIdentity(lower,host));
        Assert.False(ReleasePayloadAuthority.HostSatisfiesDefaultRequest(new AssemblyName("System.Collections.Immutable, Version=11.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a"),host));
        Assert.False(ReleasePayloadAuthority.HostSatisfiesDefaultRequest(new AssemblyName("System.Collections.Immutable, Version=9.0.0.0, Culture=neutral, PublicKeyToken=0011223344556677"),host));
        Assert.False(ReleasePayloadAuthority.HostSatisfiesDefaultRequest(new AssemblyName("System.Collections.Immutable, Version=9.0.0.0, Culture=fr, PublicKeyToken=b03f5f7f11d50a3a"),host));
        Assert.False(ReleasePayloadAuthority.HostSatisfiesDefaultRequest(new AssemblyName(lower.FullName){ContentType=AssemblyContentType.WindowsRuntime},host));
    }
    [Fact]
    public void AppContext_style_root_with_trailing_separator_matches_its_payload_ancestors()
    {
        using var fixture=new PayloadFixture();
        var authority=new ReleasePayloadAuthority(fixture.Root+Path.DirectorySeparatorChar);
        authority.VerifyPlugin(fixture.Plugin);
        Assert.Single(authority.ExpectedTools);
    }
    [Theory]
    [InlineData("A","B")]
    [InlineData("B","A")]
    public void Private_same_name_dependencies_keep_values_paths_hashes_and_contexts_in_both_orders(string first,string second)
    {
        var root=Environment.GetEnvironmentVariable("A11_PRIVATE_FIXTURE_ROOT")??throw new InvalidOperationException("Private fixture facility was not supplied.");
        var authority=new ReleasePayloadAuthority(root);
        var contexts=new List<PluginLoadContext>();
        var dependencies=new List<Assembly>();
        foreach(var label in new[]{first,second})
        {
            var main=Path.Combine(root,"plugins","PrivateProbe"+label,"PrivateProbe"+label+".dll");
            var context=new PluginLoadContext(main,authority);contexts.Add(context);
            var assembly=context.LoadFromAssemblyPath(main);
            Assert.Equal(label,assembly.GetType("PrivateProbe")!.GetMethod("Read")!.Invoke(null,null));
            var dependency=Assert.Single(context.Assemblies.Where(a=>a.GetName().Name=="A11.PrivateDependency"));dependencies.Add(dependency);
            Assert.Same(context,System.Runtime.Loader.AssemblyLoadContext.GetLoadContext(dependency));
            Assert.Equal(Path.Combine(Path.GetDirectoryName(main)!,"A11.PrivateDependency.dll"),dependency.Location);
            authority.VerifyApprovedFile(dependency.Location);
            Assert.Contains("A11.PrivateDependency",context.LocallyResolvedDependencies);
        }
        Assert.NotSame(contexts[0],contexts[1]);Assert.NotSame(dependencies[0],dependencies[1]);
        Assert.Equal(dependencies[0].FullName,dependencies[1].FullName);
        Assert.NotEqual(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(dependencies[0].Location))),Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(dependencies[1].Location))));
    }

    [Fact]
    public void Framework_dependent_CLR_selected_core_and_WPF_origins_are_accepted()
    {
        using var fixture=new PayloadFixture();
        var authority=new ReleasePayloadAuthority(fixture.Root);
        foreach(var assembly in new[]{typeof(object).Assembly,typeof(System.Windows.Window).Assembly})
        {
            authority.VerifyShared(assembly.GetName(),assembly,null);
            var local=Path.Combine(fixture.Root,Path.GetFileName(assembly.Location));
            File.Copy(assembly.Location,local);
            authority.VerifyShared(assembly.GetName(),assembly,local);
            File.AppendAllText(local,"conflicting-framework-bytes");
            Assert.Throws<FileLoadException>(()=>authority.VerifyShared(assembly.GetName(),assembly,local));
        }
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
        var asset=new {payloadPath="plugins/Probe/Probe.dll",runtimePath=corruption=="path"?"../escape.dll":"runtimes/win-x64/lib/net9.0/Probe.dll",
            packId=corruption=="pack"?"Arbitrary.Package":"Microsoft.NETCore.App.Runtime.win-x64",packVersion="9.0.20",
            assemblyIdentity=corruption=="identity"?"Incorrect, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null":AssemblyName.GetAssemblyName(fixture.Plugin).FullName};
        payload["runtimeFrameworks"]=JsonSerializer.SerializeToNode(new {deploymentMode="selfContained",frameworks=new[]{new{name="Microsoft.NETCore.App",version="9.0.20",managedAssets=corruption=="empty"?Array.Empty<object>():new object[]{asset}}}});
        File.WriteAllText(path,payload.ToJsonString());
        Assert.ThrowsAny<Exception>(()=>new ReleasePayloadAuthority(fixture.Root));
    }

    [Fact]
    public void Application_assembly_is_not_approved_by_framework_declaration()
    {
        using var fixture=new PayloadFixture();
        var authority=new ReleasePayloadAuthority(fixture.Root);
        var assembly=typeof(ReleasePayloadAuthority).Assembly;
        Assert.ThrowsAny<Exception>(()=>authority.VerifyShared(assembly.GetName(),assembly,null));
    }

    [Fact]
    public void Approved_bytes_are_rechecked_at_use_and_unlisted_paths_are_rejected()
    {
        using var fixture=new PayloadFixture();
        var authority=new ReleasePayloadAuthority(fixture.Root);
        authority.VerifyPlugin(fixture.Plugin);
        var unlisted=Path.Combine(fixture.Root,"extra.dll");File.Copy(fixture.Plugin,unlisted);
        Assert.Throws<InvalidDataException>(()=>authority.VerifyApprovedFile(unlisted));
        File.AppendAllText(fixture.Plugin,"changed-after-approval");
        Assert.Throws<InvalidDataException>(()=>authority.VerifyPlugin(fixture.Plugin));
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
        internal string Plugin=>Path.Combine(Root,"plugins","Probe","Probe.dll");
        internal PayloadFixture(){Directory.CreateDirectory(Path.GetDirectoryName(Plugin)!);File.Copy(typeof(ReleasePayloadAuthorityTests).Assembly.Location,Plugin);Write();}
        internal void Write(string? corruption=null)
        {
            var tool=new Dictionary<string,object?>{{"id","Probe"},{"typeName","Probe.Tool"}};
            var tools=new List<object>{tool};if(corruption=="duplicate-tool")tools.Add(tool);
            var entry=new Dictionary<string,object?>{
                {"path",corruption=="escape"?"../escape.dll":"plugins/Probe/Probe.dll"},
                {"sha256",Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Plugin)))},
                {"assemblyIdentity",corruption=="identity"?"Different, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null":AssemblyName.GetAssemblyName(Plugin).FullName},
                {"tools",tools}};
            var entries=new List<object>{entry};if(corruption=="duplicate-path")entries.Add(entry);
            var payload=new Dictionary<string,object?>{
                {"payloadSchemaVersion",corruption=="schema"?2:1},{"buildId","A11-owned-fixture"},{"sourceCommit","fixture-source"},{"artifactVersion","fixture-version"},
                {"expectedPluginAssemblies",new[]{"Probe"}},{"entries",entries},
                {"runtimeFrameworks",new{deploymentMode=corruption=="unknown-framework-mode"?"guessed":"frameworkDependent",frameworks=new[]{new{name="Microsoft.NETCore.App",version="9.0.0"},new{name="Microsoft.WindowsDesktop.App",version="9.0.0"}}}}};
            if(corruption=="missing-frameworks")payload.Remove("runtimeFrameworks");
            File.WriteAllText(Path.Combine(Root,"release-payload.json"),JsonSerializer.Serialize(payload));
        }
        public void Dispose(){Directory.Delete(Root,true);}
    }
}
