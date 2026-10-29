using System;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Plugin.Wpf.Presenters.Input.ImageSources;

/// <summary>Host input composition bound to the tool editor's sole private settings draft.</summary>
public interface IImageSourceSettingsContent
{
    event Action? Changed;
    void BindSourceDraft(IToolSettingsDraft draft, Action prepareEdit, Action<FieldSettings> acceptCandidate);
    void RefreshSourceDraft();
}
