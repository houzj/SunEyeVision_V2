using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using Microsoft.CodeAnalysis;

namespace SunEyeVision.Tool.Script.ScriptEngine;

/// <summary>宿主引用随进程固定；用户引用每次按字节核验，只共享不可变元数据。</summary>
internal static class SharedMetadataReferenceProvider
{
    private static readonly object Gate = new();
    private static readonly Dictionary<string, ScriptReferenceImage> HostImages = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, ScriptReferenceImage> UserImages = new(StringComparer.Ordinal);
    private const int MaxUserImages = 128;

    internal static ScriptReferenceImage Capture(string file, bool isUserReference)
    {
        var path = Path.GetFullPath(file);
        lock (Gate)
        {
            if (!isUserReference && HostImages.TryGetValue(path, out var host)) return host;
            // 摘要和元数据从相同字节生成，不存在 hash 后再次打开另一份 DLL 的窗口。
            var bytes = ImmutableArray.CreateRange(File.ReadAllBytes(path));
            var sha = Convert.ToHexString(SHA256.HashData(bytes.AsSpan()));
            var key = path.ToUpperInvariant() + "|" + sha;
            if (isUserReference && UserImages.TryGetValue(key, out var cached)) return cached;
            using var pe = new PEReader(bytes);
            if (!pe.HasMetadata) throw new BadImageFormatException($"脚本引用不是托管程序集: {path}");
            var reader = pe.GetMetadataReader();
            if (!reader.IsAssembly) throw new BadImageFormatException($"脚本引用不是程序集: {path}");
            var definition = reader.GetAssemblyDefinition();
            var name = new AssemblyName
            {
                Name = reader.GetString(definition.Name), Version = definition.Version,
                CultureName = definition.Culture.IsNil ? null : reader.GetString(definition.Culture)
            };
            if (!definition.PublicKey.IsNil) name.SetPublicKey(reader.GetBlobBytes(definition.PublicKey));
            var image = new ScriptReferenceImage(path, name.Name!, name.FullName!, sha, isUserReference,
                bytes, MetadataReference.CreateFromImage(bytes, filePath: path));
            if (isUserReference)
            {
                if (UserImages.Count >= MaxUserImages) UserImages.Clear();
                UserImages[key] = image;
            }
            else HostImages[path] = image;
            return image;
        }
    }

    public static void InvalidateScriptRefs(string directory)
    {
        lock (Gate) UserImages.Clear();
    }
}
