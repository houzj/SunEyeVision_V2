using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;

namespace SunEyeVision.TestShared;

/// <summary>New Runtime tests bind product assemblies only from the sealed host root.</summary>
internal static class ProductRuntimeTestHost
{
    internal static string ProductRoot { get; private set; } = null!;
    // The sealed payload is the authority; no mutable SDK registration or test-only bypass.
    internal static SunEyeVision.Plugin.Runtime.RuntimeDirectorySnapshot CreateRuntimeDirectory()
        => new SunEyeVision.Plugin.Runtime.ReleasePayloadAuthority(ProductRoot).PrepareRuntimeDirectory();

    [ModuleInitializer]
    internal static void Initialize()
    {
        var metadata = typeof(ProductRuntimeTestHost).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(a => a.Key == "HostDebugRoot").Value;
        ProductRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(metadata
            ?? throw new InvalidOperationException("Missing formal product root metadata.")));
        using var document = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(ProductRoot, "release-payload.json")));
        if (document.RootElement.GetProperty("payloadSchemaVersion").GetInt32() != 2)
            throw new InvalidDataException("Runtime tests require the component product payload.");
        AssemblyLoadContext.Default.Resolving += (_, name) =>
        {
            var path = Path.Combine(ProductRoot, name.Name + ".dll");
            return File.Exists(path) ? AssemblyLoadContext.Default.LoadFromAssemblyPath(path) : null;
        };
        // Pre-bind exact formal product bytes before any test body is JIT-compiled.
        foreach (var entry in document.RootElement.GetProperty("entries").EnumerateArray())
        {
            if (!entry.TryGetProperty("assemblyIdentity", out var identity)) continue;
            var name = new AssemblyName(identity.GetString()!);
            if (!name.Name!.StartsWith("SunEyeVision.", StringComparison.Ordinal)) continue;
            var relative = entry.GetProperty("path").GetString()!;
            if (relative.Contains('/') || relative.Contains('\\')) throw new InvalidDataException("Product assembly must be in host root.");
            var path = Path.Combine(ProductRoot, relative);
            var expected = entry.GetProperty("sha256").GetString();
            if (!string.Equals(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))), expected, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Product assembly SHA mismatch: " + relative);
            var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(path);
            if (!string.Equals(Path.GetFullPath(assembly.Location), path, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Test loaded a product assembly outside formal Release: " + assembly.Location);
        }
        AppContext.SetData("APP_CONTEXT_BASE_DIRECTORY", ProductRoot + Path.DirectorySeparatorChar);
        Environment.SetEnvironmentVariable("PATH", ProductRoot + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH"));
    }
}
