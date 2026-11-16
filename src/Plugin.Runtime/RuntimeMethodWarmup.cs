using System.Reflection;
using System.Runtime.CompilerServices;
using SunEyeVision.Foundation.Logging;

namespace SunEyeVision.Plugin.Runtime;

/// <summary>
/// Compiles hot managed methods before the first execution reaches them. HALCON's native
/// platform admission is already covered by the tool's runtime initialisation; what remains
/// on the first run is the CLR compiling this host's execution chain and the tool bodies.
/// Preparing a method compiles it without invoking it, so no tool state, solution resource
/// or execution identity is read or changed. A method that cannot be prepared is left for
/// its normal first call and reported, so a gap stays visible instead of being hidden.
/// </summary>
public static class RuntimeMethodWarmup
{
    private const BindingFlags DeclaredMethods = BindingFlags.Public | BindingFlags.NonPublic |
        BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    private const int ReportedGaps = 5;

    public static void Prepare(string label, params Assembly[] assemblies)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentNullException.ThrowIfNull(assemblies);
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var prepared = 0;
        var skipped = 0;
        var gaps = new List<string>();
        foreach (var assembly in assemblies.Distinct())
        {
            foreach (var type in LoadableTypes(assembly))
            {
                if (type.ContainsGenericParameters)
                {
                    skipped++;
                    RecordGap(gaps, type.FullName + " (open generic type)");
                    continue;
                }
                foreach (var method in type.GetMethods(DeclaredMethods))
                {
                    if (method.ContainsGenericParameters || method.IsAbstract || method.GetMethodBody() is null)
                    {
                        skipped++;
                        continue;
                    }
                    try
                    {
                        RuntimeHelpers.PrepareMethod(method.MethodHandle);
                        prepared++;
                    }
                    catch (Exception error)
                    {
                        skipped++;
                        RecordGap(gaps, type.FullName + "." + method.Name + " (" + error.GetType().Name + ")");
                    }
                }
            }
        }
        clock.Stop();
        PluginLogger.Info(
            $"[JitWarmup] label={label} assemblies={assemblies.Length} prepared={prepared} skipped={skipped} elapsed={clock.ElapsedMilliseconds}ms" +
            (gaps.Count == 0 ? string.Empty : " unprepared=" + string.Join("; ", gaps)),
            "ExecutionLifecycle");
    }

    private static void RecordGap(List<string> gaps, string description)
    {
        if (gaps.Count < ReportedGaps) gaps.Add(description);
    }

    private static IEnumerable<Type> LoadableTypes(Assembly assembly)
    {
        try { return assembly.GetTypes(); }
        catch (ReflectionTypeLoadException error) { return error.Types.OfType<Type>(); }
    }
}
