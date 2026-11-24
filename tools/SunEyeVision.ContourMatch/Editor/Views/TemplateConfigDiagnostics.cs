using System;
using System.IO;
namespace SunEyeVision.Tool.ContourMatch.Wpf.Views;
internal static class TemplateConfigDiagnostics
{
    private static readonly object Gate = new();
    private static readonly string FilePath = Path.Combine(Path.GetTempPath(), "SunEyeVision.ContourMatch.TemplateConfig.log");
    public static void Write(string stage, string? detail = null)
    { try { lock (Gate) File.AppendAllText(FilePath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{stage}] {detail ?? string.Empty}{Environment.NewLine}"); } catch { } }
}
