using System;
using System.Collections.Generic;

namespace SunEyeVision.Plugin.SDK.Models.Communication;

public sealed record CommunicationDelimiterOption(string DisplayName, string Value)
{
    public override string ToString() => DisplayName;
}

/// <summary>Authoritative preset values for manager and protocol tools; custom values stay local.</summary>
public static class CommunicationDelimiterCatalog
{
    public static IReadOnlyList<CommunicationDelimiterOption> Defaults { get; } = Array.AsReadOnly(new[]
    {
        new CommunicationDelimiterOption("无", ""), new(",", ","), new(";", ";"),
        new("空格", " "), new("|", "|"), new(@"\r", "\r"), new(@"\n", "\n"), new(@"\r\n", "\r\n")
    });
}
