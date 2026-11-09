using System;
using System.Windows;
using System.Windows.Media;
using SunEyeVision.UI.Themes.Base;

namespace SunEyeVision.UI.Services.Images;

/// <summary>Host palette for the unchanged original source templates; applied once while attached.
/// 固定主题：宿主只有一套主题字典，因此不再订阅切换事件，只按当前宿主画刷注入一次。</summary>
public sealed class ImageSourceThemeScope : IDisposable
{
    private readonly FrameworkElement _target;
    private ResourceDictionary? _palette;
    private bool _disposed;
    public ImageSourceThemeScope(FrameworkElement target)
    {
        _target=target;target.Loaded+=Loaded;target.Unloaded+=Unloaded;
        if(target.IsLoaded)Attach();
    }
    private void Loaded(object sender,RoutedEventArgs args)=>Attach();
    private void Unloaded(object sender,RoutedEventArgs args)=>Detach();
    private void Attach()
    {
        if(_disposed||_palette is not null)return;
        // Standalone designer/test surfaces without host theme resources keep the control's own original palette.
        if(_target.TryFindResource(ThemeKeys.PanelBackgroundBrush) is not Brush)return;
        Apply();
    }
    private void Apply()
    {
        _target.Dispatcher.VerifyAccess();
        object Brush(string key)=>_target.TryFindResource(key) as Brush
            ??throw new InvalidOperationException("图像源缺少宿主主题画刷："+key);
        var palette=new ResourceDictionary
        {
            ["ImageSources.Background"]=Brush(ThemeKeys.PanelBackgroundBrush),
            ["ImageSources.CheckBoxFillNormal"]=Brush(ThemeKeys.InputBackgroundBrush),
            ["ImageSources.CheckBoxStroke"]=Brush(ThemeKeys.InputBorderBrush),
            ["ImageSources.Foreground"]=Brush(ThemeKeys.TextPrimaryBrush),
            ["ImageSources.Border"]=Brush(ThemeKeys.BorderBrush),
            ["ImageSources.Hover"]=Brush(ThemeKeys.ButtonHoverBackgroundBrush),
            ["ImageSources.Scroll"]=Brush(ThemeKeys.ScrollBarThumbBrush),
            ["Col_ScrollBar_BorderBrush"]=Brush(ThemeKeys.ScrollBarThumbBrush),
            ["Col_ScrollBar_Fill"]=Brush(ThemeKeys.ScrollBarThumbBrush),
            ["ImageSources.MenuBackground"]=Brush(ThemeKeys.ContextMenuBackgroundBrush),
            ["Col_ComboBox_Background"]=Brush(ThemeKeys.InputBackgroundBrush),
            ["Col_ComboBox_BorderBrush"]=Brush(ThemeKeys.InputBorderBrush),
            ["Col_ComboBox_Foreground"]=Brush(ThemeKeys.TextPrimaryBrush),
            ["Col_ComboBox_Popup"]=Brush(ThemeKeys.ContextMenuBackgroundBrush),
            ["Col_ComboBox_Popup_BoderBrush"]=Brush(ThemeKeys.BorderBrush),
            ["Col_ComboBox_Popup_Selected_Background"]=Brush(ThemeKeys.ButtonHoverBackgroundBrush),
            ["Col_Theme"]=_target.FindResource("ImageSources.Accent")
        };
        var dictionaries=_target.Resources.MergedDictionaries;
        if(_palette is not null&&dictionaries.IndexOf(_palette) is var index&&index>=0)dictionaries[index]=palette;
        else dictionaries.Add(palette);
        _palette=palette;
    }
    private void Detach()
    {
        if(_palette is not null)_target.Resources.MergedDictionaries.Remove(_palette);
        _palette=null;
    }
    public void Dispose()
    {
        if(_disposed)return;_disposed=true;
        _target.Loaded-=Loaded;_target.Unloaded-=Unloaded;Detach();
    }
}
