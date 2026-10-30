using System;
using System.Windows;
using System.Windows.Media;
using SunEyeVision.UI.Core;
using SunEyeVision.UI.Themes.Base;

namespace SunEyeVision.UI.Services.Images;

/// <summary>Host palette for the unchanged original source templates; subscribed only while attached.</summary>
public sealed class ImageSourceThemeScope : IDisposable
{
    private readonly FrameworkElement _target;
    private readonly ThemeManager? _providedManager;
    private ThemeManager? _manager;
    private ResourceDictionary? _palette;
    private bool _disposed;
    public ImageSourceThemeScope(FrameworkElement target,ThemeManager? manager=null)
    {
        _target=target;_providedManager=manager;target.Loaded+=Loaded;target.Unloaded+=Unloaded;
        if(target.IsLoaded)Attach();
    }
    private void Loaded(object sender,RoutedEventArgs args)=>Attach();
    private void Unloaded(object sender,RoutedEventArgs args)=>Detach();
    private void Attach()
    {
        if(_disposed||_manager is not null)return;
        _manager=_providedManager??ServiceLocator.Instance.GetService<ThemeManager>();
        // Standalone designer/test surfaces use the control's own original palette.
        if(_manager is null)return;
        _manager.ThemeChanged+=ThemeChanged;Apply();
    }
    private void ThemeChanged(AppTheme theme)
    {
        if(!_disposed&&_target.IsLoaded)Apply();
    }
    private void Apply()
    {
        _target.Dispatcher.VerifyAccess();
        object Brush(string key)=>_target.TryFindResource(key) as Brush
            ??throw new InvalidOperationException("图像源缺少宿主主题画刷："+key);
        var palette=new ResourceDictionary
        {
            ["ImageSources.Background"]=Brush(ThemeKeys.PanelBackgroundBrush),
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
        if(_manager is not null)_manager.ThemeChanged-=ThemeChanged;
        _manager=null;
        if(_palette is not null)_target.Resources.MergedDictionaries.Remove(_palette);
        _palette=null;
    }
    public void Dispose()
    {
        if(_disposed)return;_disposed=true;
        _target.Loaded-=Loaded;_target.Unloaded-=Unloaded;Detach();
    }
}
