using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SunEyeVision.Persistence;

public sealed record SolutionFileMetadata(string Id,string Name,string Description,string Version,string FilePath,
    DateTime CreatedTime,DateTime ModifiedTime,int WorkflowCount,int GlobalVariableCount,string SourceSha256,
    IReadOnlyList<SuspendedStructure> SuspendedStructures);
public sealed record SolutionMetadataReadResult(string FilePath,SolutionFileMetadata? Metadata,string? Error);

/// <summary>Read-only metadata scan; never decodes variables, changes source bytes, or activates a document.</summary>
public static class SolutionMetadataReader
{
    public static SolutionMetadataReadResult Read(string path)
    {
        var full=Path.GetFullPath(path);
        try
        {
            var bytes=File.ReadAllBytes(full);var json=new UTF8Encoding(false,true).GetString(bytes).TrimStart('\uFEFF');
            SolutionDocumentValidator.CheckDuplicateKeys(json);
            using var document=JsonDocument.Parse(json);var root=document.RootElement;SolutionFormatPolicy.ValidateReadable(root);
            if(!root.TryGetProperty("Id",out var id)||id.ValueKind!=JsonValueKind.String||string.IsNullOrWhiteSpace(id.GetString()))throw new JsonException("方案缺少有效身份。");
            var info=new FileInfo(full);
            static string Text(JsonElement root,string name,string fallback) => root.TryGetProperty(name,out var value) && value.ValueKind==JsonValueKind.String ? value.GetString() ?? fallback : fallback;
            static int Count(JsonElement root,string name) => root.TryGetProperty(name,out var value) && value.ValueKind==JsonValueKind.Array ? value.GetArrayLength() : 0;
            var metadata=new SolutionFileMetadata(id.GetString()!,Text(root,"Name",Path.GetFileNameWithoutExtension(full)),Text(root,"Description",string.Empty),Text(root,"Version","1.0"),full,
                info.CreationTime,info.LastWriteTime,Count(root,"Workflows"),Count(root,"GlobalVariables"),Convert.ToHexString(SHA256.HashData(bytes)),SuspendedStructurePreflight.Inspect(root));
            return new(full,metadata,null);
        }
        catch(Exception error) when(error is IOException or UnauthorizedAccessException or JsonException or DecoderFallbackException or ArgumentException)
        { return new(full,null,error.Message); }
    }
    public static IReadOnlyList<SolutionMetadataReadResult> ReadBatch(IEnumerable<string> paths)
        => Array.AsReadOnly((paths ?? throw new ArgumentNullException(nameof(paths))).Select(Read).ToArray());
    public static IReadOnlyList<SolutionMetadataReadResult> Scan(string directory,string searchPattern="*.solution")
        => ReadBatch(Directory.EnumerateFiles(Path.GetFullPath(directory),searchPattern,SearchOption.TopDirectoryOnly));
}
