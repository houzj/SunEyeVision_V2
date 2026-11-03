using System.Reflection;
using System.Windows;
using System.Windows.Media;
using SunEyeVision.UI.Services;
using SunEyeVision.UI.Themes.Base;
using SunEyeVision.Plugin.SDK.UI.Themes;
using SunEyeVision.Workflow;
using Xunit;
namespace SunEyeVision.UI.Tests;
/// <summary>独立进程执行实际主题启动链，不能用手工合并字典代替契约校验。</summary>
[Collection("VM canvas UI")]
public sealed class ThemeStartupContractTests
{
    [Fact]
    public void Actual_theme_initialization_validates_all_sdk_keys_and_keeps_image_checkerboard_separate_from_workflow_grid()
    {
        Exception? failure=null;var thread=new Thread(()=>{try{
            if(Application.Current==null)_=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
            foreach(var theme in new[]{AppTheme.Dark,AppTheme.Light}){
                var settings=new SolutionSettings();settings.SetPreference(ThemeManager.PreferenceKey,theme.ToString());
                var manager=new ThemeManager(settings);manager.Initialize();Assert.Equal(theme,manager.CurrentTheme);
                var dictionary=Application.Current.Resources.MergedDictionaries.Last();
                foreach(var field in typeof(SdkThemeKeys).GetFields(BindingFlags.Public|BindingFlags.Static).Where(f=>f.IsLiteral&&f.FieldType==typeof(string)))
                    Assert.True(dictionary.Contains(field.GetRawConstantValue()!),"缺失SDK主题资源："+field.Name);
                var image=Assert.IsType<DrawingBrush>(dictionary[ThemeKeys.CheckerboardBackgroundBrush]);
                var workflow=Assert.IsType<DrawingBrush>(dictionary[ThemeKeys.WorkflowCanvasGridBrush]);
                Assert.Equal(new Rect(0,0,32,32),image.Viewport);Assert.Equal(new Rect(0,0,6,6),workflow.Viewport);Assert.NotSame(image,workflow);
                Assert.IsType<SolidColorBrush>(dictionary[ThemeKeys.WorkflowCanvasSurfaceBrush]);
                Assert.Null(Assert.IsType<GeometryDrawing>(Assert.IsType<DrawingGroup>(workflow.Drawing).Children.Single()).Brush);
            }
        }catch(Exception error){failure=error;}});thread.SetApartmentState(ApartmentState.STA);thread.Start();Assert.True(thread.Join(TimeSpan.FromSeconds(20)));
        if(failure!=null)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
