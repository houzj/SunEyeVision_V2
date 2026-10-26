using System;
using System.Diagnostics;
using System.IO;
using System.Xml;

namespace SunEyeVision.Plugin.Wpf.Native.Common;

/// <summary>Release-local help lookup, preserving the original helpUrl/GuidTemp layout.</summary>
internal static class NativeHelpDocument
{
    internal static bool TryResolve(string moduleXmlPath, string language, string releaseRoot, out Uri? uri)
    {
        uri = null;
        if (!File.Exists(moduleXmlPath)) return false;
        var document = new XmlDocument { XmlResolver = null };
        using var reader = XmlReader.Create(moduleXmlPath, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null
        });
        document.Load(reader);
        var helpId = document.SelectSingleNode("//ToolBoxItemData/helpUrl")?.InnerText;
        var helpRoot = Path.Combine(releaseRoot, "Help", language);
        if (string.IsNullOrWhiteSpace(helpId) || !File.Exists(Path.Combine(helpRoot, helpId + ".html")))
        {
            var fallback = Path.Combine(Path.GetDirectoryName(moduleXmlPath)!, "GuidTemp.txt");
            if (!File.Exists(fallback)) return false;
            helpId = File.ReadAllText(fallback).Trim();
        }
        var index = Path.Combine(helpRoot, "index.html");
        if (string.IsNullOrWhiteSpace(helpId) || !File.Exists(Path.Combine(helpRoot, helpId + ".html")) || !File.Exists(index)) return false;
        uri = new UriBuilder(Path.GetFullPath(index)) { Query = "guid=" + Uri.EscapeDataString(helpId + ".html") }.Uri;
        return true;
    }

    public static int OpenModuleHelpUrl(string moduleXmlPath, string language)
    {
        try
        {
            if (!TryResolve(moduleXmlPath, language, AppContext.BaseDirectory, out var uri)) return 2;
            Process.Start(new ProcessStartInfo(uri!.AbsoluteUri) { UseShellExecute = true });
            return 0;
        }
        catch (Exception error)
        {
            Trace.TraceError("打开本地帮助失败：{0}", error);
            return 1;
        }
    }
}
