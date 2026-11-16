using System.Text.Json;
using System.Text.Json.Nodes;
using System.Security.Cryptography;
using System.Text;
using SunEyeVision.Domain.Persistence;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Persistence;

internal static class EmbeddedFileCapture
{
    internal static bool HasWork(SunEyeVision.Domain.Solutions.SolutionDefinition document,
        IReadOnlyDictionary<string,FrozenToolDeclarations> declarations,EmbeddedFileCatalog? source)
    {
        if(source?.HasFiles==true)return true;
        foreach(var node in document.Workflows.SelectMany(flow=>flow.Nodes))
        {
            var tool=declarations[node.ToolType];
            foreach(var field in tool.HostDeclarations.ResourceFields.Where(field=>field.Embedding is not null))
            {
                var setting=node.Configuration.Fields.Read(field.Embedding!.EnabledFieldId);
                if(setting is UnsetSetting)setting=tool.SettingsDefaults.Read(field.Embedding.EnabledFieldId);
                if(setting is ConstantSetting{Value:true})return true;
            }
        }
        return false;
    }
    internal static byte[] Capture(byte[] bytes,IReadOnlyDictionary<string,FrozenToolDeclarations> declarations,
        string? baseDirectory,EmbeddedFileCatalog? source,List<PersistenceDiagnostic> diagnostics)
    {
        var root=JsonNode.Parse(bytes) as JsonObject??throw new JsonException("Invalid solution root.");
        var resolver=new SolutionResourceResolver(baseDirectory,source);
        var captured=new Dictionary<string,EmbeddedFileCatalog.Entry>(StringComparer.Ordinal);long total=0;
        var externalFiles=new Dictionary<string,EmbeddedFileCatalog.Entry>(StringComparer.OrdinalIgnoreCase);
        void Own(EmbeddedFileCatalog.Entry entry)
        {
            if(captured.ContainsKey(entry.Reference))return;
            if(entry.Bytes.LongLength>EmbeddedFileCatalog.MaximumBytes-total)throw new InvalidOperationException("方案编码附件超过宿主200MiB预算。");
            captured.Add(entry.Reference,entry);total+=entry.Bytes.LongLength;
        }
        foreach(var (fields,tool,path) in SolutionResourceResolver.DeclaredOwners(root,declarations))
        {
            var policyField=tool.HostDeclarations.ResourceFields.SingleOrDefault(field=>field.Embedding is not null);
            bool Flag(string id)
            {
                if(fields[id] is JsonObject setting&&setting["Kind"]?.GetValue<string>()=="Constant"&&setting["Value"] is JsonValue value
                    &&value.TryGetValue<bool>(out var flag))return flag;
                if(tool.SettingsDefaults.Read(id) is ConstantSetting{Value:bool fallback})return fallback;
                throw new JsonException("File attachment policy requires a frozen Boolean: "+id);
            }
            var enabled=policyField?.Embedding is {} policy&&Flag(policy.EnabledFieldId);
            if(enabled&&policyField!.Embedding!.DynamicListFieldId is {} dynamicField&&Flag(dynamicField))
                throw new NotSupportedException("订阅目录的运行列表尚未接入保存快照；不能用手工列表冒充目录存图。请关闭保存图像后保存。");

            // Disabling embedding restores original external identities in the output copy only.
            var restored=new HashSet<string>(StringComparer.Ordinal);
            string External(string reference)
            {
                var address=(source??throw new InvalidDataException("Embedded source owner missing.")).OriginalAddress(reference);
                if(!File.Exists(address)&&restored.Add(reference))diagnostics.Add(new("RESOURCE_EMBED_EXTERNAL_MISSING","Resource","Warning",path,
                    "已改为保存外部路径，原文件当前不可用："+address));
                return address;
            }
            string Restore(string reference)=>!enabled&&policyField is not null&&EmbeddedFileCatalog.IsReference(reference)?External(reference):reference;
            foreach(var declaration in tool.HostDeclarations.ResourceFields.Where(field=>field.Purpose==ResourcePurpose.FileRead))
                Transform(fields,declaration,Restore);

            var replacements=new Dictionary<string,string>(StringComparer.Ordinal);
            if(enabled&&fields[policyField!.FieldId] is JsonObject list&&list["Kind"]?.GetValue<string>()=="Constant")
            {
                var values=list["Value"] as JsonArray??throw new JsonException("Embedded source requires a file list.");
                var referenceGroup=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(values.ToJsonString())));
                if(values.Count>policyField.Embedding!.MaximumFiles)
                    diagnostics.Add(new("RESOURCE_EMBED_PARTIAL","Resource","Warning",path+"."+policyField.FieldId,
                        $"按前{policyField.Embedding.MaximumFiles}项收集附件，其余独立文件保留外部路径。"));
                foreach(var value in values.Take(policyField.Embedding!.MaximumFiles))
                {
                    var reference=value?.GetValue<string>()??throw new JsonException("File reference missing.");
                    if(replacements.ContainsKey(reference))continue;
                    EmbeddedFileCatalog.Entry entry;
                    if(EmbeddedFileCatalog.IsReference(reference))entry=captured.GetValueOrDefault(reference)
                        ??(source??throw new InvalidDataException("Embedded source owner missing.")).Copy(reference);
                    else
                    {
                        var address=resolver.ResolveAddress(policyField.FieldId,reference);
                        if(!externalFiles.TryGetValue(address,out entry!))
                        {
                            using var input=new FileStream(address,FileMode.Open,FileAccess.Read,FileShare.Read);
                            if(input.Length>EmbeddedFileCatalog.MaximumBytes-total)throw new InvalidOperationException("方案编码附件超过宿主200MiB预算。");
                            var content=new byte[checked((int)input.Length)];input.ReadExactly(content);
                            entry=EmbeddedFileCatalog.Create(address,content);externalFiles.Add(address,entry);
                        }
                        // Distinct logical references must not collapse a selected subset after reopening.
                        // Existing URIs keep their stored identity; a new external group receives a new one.
                        entry=EmbeddedFileCatalog.Create(address,entry.Bytes,referenceGroup+"\0"+reference);
                    }
                    Own(entry);replacements[reference]=entry.Reference;
                }
                foreach(var value in values.Skip(policyField.Embedding.MaximumFiles))
                {
                    var reference=value?.GetValue<string>()??throw new JsonException("File reference missing.");
                    if(EmbeddedFileCatalog.IsReference(reference)&&!replacements.ContainsKey(reference))replacements[reference]=External(reference);
                }
                foreach(var declaration in tool.HostDeclarations.ResourceFields.Where(field=>field.Purpose==ResourcePurpose.FileRead))
                    Transform(fields,declaration,reference=>replacements.GetValueOrDefault(reference,reference));
            }
        }
        // Existing owned references survive hidden source modes and node copy/undo without using cache paths.
        foreach(var (setting,declaration,_) in SolutionResourceResolver.DeclaredSettings(root,declarations))
        {
            if(setting["Kind"]?.GetValue<string>()!="Constant")continue;
            var values=declaration.Cardinality==ResourceReferenceCardinality.Scalar?new[]{setting["Value"]}:
                (setting["Value"] as JsonArray??throw new JsonException("Resource list missing.")).ToArray();
            foreach(var value in values)
            {
                var reference=value?.GetValue<string>()??string.Empty;
                if(!EmbeddedFileCatalog.IsReference(reference))continue;
                if(declaration.Purpose!=ResourcePurpose.FileRead)throw new JsonException("Embedded resource is not a read-only file.");
                if(!captured.ContainsKey(reference))Own((source??throw new InvalidDataException("Embedded source owner missing.")).Copy(reference));
            }
        }
        if(captured.Count>0)root["EmbeddedFiles"]=new JsonArray(captured.Values.OrderBy(entry=>entry.Reference,StringComparer.Ordinal)
            .Select(entry=>(JsonNode)EmbeddedFileCatalog.Write(entry)).ToArray());
        return System.Text.Encoding.UTF8.GetBytes(root.ToJsonString());
    }

    private static void Transform(JsonObject fields,ResourceFieldDeclaration declaration,Func<string,string> map)
    {
        if(fields[declaration.FieldId] is not JsonObject setting||setting["Kind"]?.GetValue<string>()!="Constant")return;
        if(declaration.Cardinality==ResourceReferenceCardinality.Scalar)
        {if(setting["Value"] is JsonValue value&&value.TryGetValue<string>(out var reference))setting["Value"]=map(reference);}
        else if(setting["Value"] is JsonArray values)
            for(var index=0;index<values.Count;index++)values[index]=map(values[index]?.GetValue<string>()??throw new JsonException("Resource reference missing."));
    }
}
