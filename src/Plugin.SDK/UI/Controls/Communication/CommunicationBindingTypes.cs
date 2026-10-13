using System;
using System.Collections.Generic;
using SunEyeVision.Plugin.SDK.Models.Communication;

namespace SunEyeVision.Plugin.SDK.UI.Controls.Communication;

public static class CommunicationBindingTypes
{
    public static IReadOnlyList<Type> Parse { get; } = Array.AsReadOnly(new[] { typeof(string), typeof(BinaryPayload) });
    public static IReadOnlyList<Type> Assemble { get; } = Array.AsReadOnly(new[] { typeof(string), typeof(long), typeof(double) });
    public static IReadOnlyList<Type> Send { get; } = Array.AsReadOnly(new[] { typeof(string), typeof(long), typeof(double), typeof(bool), typeof(BinaryPayload) });
}
