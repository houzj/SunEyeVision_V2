using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace SunEyeVision.Plugin.Wpf;

/// <summary>Small vector affordances shared by fields; no tool icons or runtime services.</summary>
internal static class EditorIcons
{
    public static FrameworkElement Eye(bool visible)=>Asset(visible?"see_normal.png":"not-see_normal.png",visible?"see_click.png":"not-_see_click.png",28);
    public static FrameworkElement Pencil()=>Asset("edit_normal.png","edit_click.png",28);
    public static FrameworkElement Link()=>Asset("ic_btn_link切换_normal.png","ic_btn_link切换_normal.png",28);
    private static FrameworkElement Asset(string normal,string hover,int size)
    {
        static BitmapImage Source(string name)
        {
            var bitmap=new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource=new Uri("pack://application:,,,/SunEyeVision.Plugin.Wpf;component/Themes/Assets/"+name,UriKind.Absolute);
            bitmap.CacheOption=BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        var style=new Style(typeof(Image));style.Setters.Add(new Setter(Image.SourceProperty,Source(normal)));
        var mouse=new DataTrigger{Binding=new Binding("IsMouseOver"){RelativeSource=new RelativeSource(RelativeSourceMode.FindAncestor,typeof(ButtonBase),1)},Value=true};
        mouse.Setters.Add(new Setter(Image.SourceProperty,Source(hover)));style.Triggers.Add(mouse);
        return new Image{Style=style,Width=size,Height=size,Stretch=Stretch.None,SnapsToDevicePixels=true};
    }
}
