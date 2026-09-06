using System;
using System.Windows;

namespace SunEyeVision.Plugin.SDK.UI.Windows
{
    /// <summary>
    /// 跟随主体主题的对话框基类：创建时把系统标题栏刷成当前主体明暗主题（DWM）。
    /// tools 中所有独立 Window（模板编辑、引用管理等）派生此类即可让标题栏受主体管控，
    /// 与宿主对话框（ThemeDialogWindow）行为一致。
    /// </summary>
    public class SdkDialogWindow : Window
    {
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            SdkWindowTheme.Apply(this);
        }
    }
}
