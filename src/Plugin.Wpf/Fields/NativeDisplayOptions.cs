namespace SunEyeVision.Plugin.Wpf.Fields;

/// <summary>Stable field roles for the original visibility and four-row graphic style composition.</summary>
public sealed record NativeGraphicDisplayOptions(string GroupId,string VisibleFieldId,string OkColorFieldId,
    string NgColorFieldId,string OpacityFieldId,string? ThicknessFieldId=null,bool ShowStyleButton=true);

/// <summary>Stable field roles for the seven original inline text display rows.</summary>
public sealed record NativeTextDisplayOptions(string ContentFieldId,string OkColorFieldId,string NgColorFieldId,
    string FontSizeFieldId,string OpacityFieldId,string PositionXFieldId,string PositionYFieldId);

/// <summary>The original eye-only row over one formal Boolean field; no hidden style state.</summary>
public sealed record NativeVisibilityDisplayOptions(string VisibleFieldId);
