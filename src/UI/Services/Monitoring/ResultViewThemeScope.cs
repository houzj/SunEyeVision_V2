using System;
using System.Windows;
using SunEyeVision.UI.Core;

namespace SunEyeVision.UI.Services.Monitoring;

public enum ResultViewThemeFamily { PropertyList, ResultShell }

/// <summary>Selects the original family's palette on the actual instance, after its standalone defaults.</summary>
public sealed class ResultViewThemeScope : IDisposable
{
    private readonly FrameworkElement _view;
    private readonly ResultViewThemeFamily _family;
    private ThemeManager? _manager;
    private ResourceDictionary? _palette;
    private bool _disposed;
    public ResultViewThemeScope(FrameworkElement view,ResultViewThemeFamily family)
    {
        if(!Enum.IsDefined(family))throw new ArgumentOutOfRangeException(nameof(family));
        _view=view;_family=family;view.Loaded+=Loaded;view.Unloaded+=Unloaded;
        if(view.IsLoaded)Attach();
    }
    private void Loaded(object sender,RoutedEventArgs args)=>Attach();
    private void Unloaded(object sender,RoutedEventArgs args)=>Detach();
    private void Attach()
    {
        if(_disposed||_manager is not null)return;
        _manager=ServiceLocator.Instance.GetService<ThemeManager>();
        if(_manager is null)return;
        _manager.ThemeChanged+=Apply;Apply(_manager.CurrentTheme);
    }
    private void Apply(AppTheme theme)
    {
        _view.Dispatcher.VerifyAccess();
        if(_disposed)return;
        var palette=new ResourceDictionary{Source=new Uri("/SunEyeVision.Plugin.Wpf;component/Themes/Native/Composition/Results/"
            +_family+"/"+theme+".xaml",UriKind.Relative)};
        var dictionaries=_view.Resources.MergedDictionaries;
        var index=_palette is null?-1:dictionaries.IndexOf(_palette);
        if(index<0)dictionaries.Add(palette);else dictionaries[index]=palette;
        _palette=palette;
    }
    private void Detach()
    {
        if(_manager is not null)_manager.ThemeChanged-=Apply;
        _manager=null;
        if(_palette is not null)_view.Resources.MergedDictionaries.Remove(_palette);
        _palette=null;
    }
    public void Dispose()
    {
        if(_disposed)return;_disposed=true;
        _view.Loaded-=Loaded;_view.Unloaded-=Unloaded;Detach();
    }
}
