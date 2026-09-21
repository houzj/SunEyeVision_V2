using System;

namespace SunEyeVision.Plugin.SDK.Execution.Parameters;

public enum InputSlotKind
{
    Image,
    Roi,
    Geometry,
    Collection,
    Scalar,
    Any
}

public sealed record InputSlotDescriptor(
    string PropertyName,
    string DisplayName,
    InputSlotKind Kind,
    string? TypeVocabulary = null,
    Type? ValueType = null,
    bool Required = true,
    bool Bindable = true);

public sealed record RuntimeParameterDescriptor(
    string PropertyName,
    string DisplayName,
    string? TypeVocabulary = null,
    Type? ValueType = null,
    string? GroupName = null,
    bool Bindable = true);
