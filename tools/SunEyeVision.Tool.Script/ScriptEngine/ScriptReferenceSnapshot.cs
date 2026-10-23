using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using CommonControl;
using Microsoft.CodeAnalysis;

namespace SunEyeVision.Tool.Script.ScriptEngine;

/// <summary>一次引用解析的不可变结果，语义与 Emit 使用相同内容身份。</summary>
internal sealed class ScriptReferenceSnapshot
{
    public ImmutableArray<ScriptReferenceImage> Entries { get; }
    public string Identity { get; }
    public double PreparationMs { get; }
    public ScriptAnalysisReferences AnalysisReferences { get; }

    private ScriptReferenceSnapshot(IEnumerable<ScriptReferenceImage> entries, double preparationMs)
    {
        Entries = entries.OrderBy(e => e.Path, StringComparer.OrdinalIgnoreCase).ToImmutableArray();
        Identity = Hash(Entries.SelectMany(e => new[] { e.Path.ToUpperInvariant(), e.AssemblyIdentity, e.ContentHash }));
        PreparationMs = preparationMs;
        AnalysisReferences = new ScriptAnalysisReferences(Identity, Entries.Select(e => e.Reference));
    }

    public static ScriptReferenceSnapshot Capture(IReadOnlyList<string>? names, string? directory)
    {
        var timer = Stopwatch.StartNew();
        // null 代表系统默认，显式空列表不得静默扫描目录或退回默认。
        var selected = names ?? MetadataReferenceProvider.RequiredAssemblyNames;
        if (selected.Count == 0) throw new InvalidOperationException("脚本引用列表为空，请恢复系统必需引用。");
        var entries = new Dictionary<string, ScriptReferenceImage>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in selected.ToArray())
        {
            if (string.IsNullOrWhiteSpace(name)) throw new InvalidOperationException("脚本引用名称不能为空。");
            var resolved = MetadataReferenceProvider.ResolveAssemblyFile(name.Trim(), directory);
            var image = SharedMetadataReferenceProvider.Capture(resolved.Path, resolved.IsUserReference);
            entries[image.Path] = image;
        }
        foreach (var group in entries.Values.GroupBy(e => e.AssemblyName, StringComparer.OrdinalIgnoreCase))
            if (group.Select(e => e.ContentHash).Distinct().Skip(1).Any())
                throw new InvalidOperationException($"程序集 '{group.Key}' 存在多个不同内容的引用，请保留一个版本。");
        return new ScriptReferenceSnapshot(entries.Values, timer.Elapsed.TotalMilliseconds);
    }

    internal static string Hash(IEnumerable<string> parts)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
            foreach (var part in parts) writer.Write(part);
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }
}

internal sealed record ScriptReferenceImage(
    string Path, string AssemblyName, string AssemblyIdentity, string ContentHash,
    bool IsUserReference, ImmutableArray<byte> Image, PortableExecutableReference Reference);
