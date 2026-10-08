using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace SunEyeVision.UI.Features.CommunicationManager;

public sealed record CommunicationSeparatorOption(string DisplayName, string Value)
{
    public override string ToString() => DisplayName;
}

/// <summary>One catalog for all communication separators; custom values stay local to each event.</summary>
public static class CommunicationSeparators
{
    public static IReadOnlyList<CommunicationSeparatorOption> Defaults { get; } =
        System.Array.AsReadOnly(new CommunicationSeparatorOption[]
        {
            new CommunicationSeparatorOption("无", ""), new(",", ","), new(";", ";"),
            new("空格", " "), new("|", "|"), new(@"\r", "\r"), new(@"\n", "\n"), new(@"\r\n", "\r\n")
        });

    public static ObservableCollection<CommunicationSeparatorOption> CreateOptions() => new(Defaults);

    public static void EnsureValue(ObservableCollection<CommunicationSeparatorOption> options, string value)
    {
        if (!options.Any(option => option.Value == value))
            options.Add(new CommunicationSeparatorOption($"自定义：{value}", value));
    }
}
