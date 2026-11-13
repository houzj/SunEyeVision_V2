using System.Linq;
using System.Windows.Controls;
using SunEyeVision.Application;
using SunEyeVision.Plugin.Wpf.Fields;
using SunEyeVision.Plugin.Wpf.Native.Numeric.ParameterPresentation.ParamTabControl;
using SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters;
using Xunit;
namespace SunEyeVision.UI.Tests;
[Collection("Editor commit UI")]
public sealed class ZzNumericProbeTests
{
 /// <summary>
 /// 回归（用户实机 2026-11-13）：结果显示页加载后，字号（声明默认 10、范围 6-72，
 /// BlobResultDisplay.cs:64）的 ValueText 被 float 单值滑条路径写成滑条默认上限
 /// "100.00"（SliderArrangeBase.EndValueF 在 !isBetween 分支落 Maximum，
 /// SliderArrangeBase.cs:425-441；NativeNumericCommit 绑定 EndValueProperty→
 /// OnEndValueChange 推值），RangeRule 报 [6 - 72] 红框。与拼接高度
 /// （090211fa8，Integer 行）同族，float 行未修。
 /// </summary>
 [Fact] public void Font_size_row_keeps_its_declared_default_without_range_error_after_page_load()=>UiRuntime.Run(()=>
 {
  using var fixture = new DocumentUiFixture();
  var source = fixture.Add();
  var blob = fixture.Add("BlobAnalysis", 350, 240);
  fixture.ConfigureImage(source, fixture.Image(180, 12));
  fixture.Connect(source, blob);
  fixture.Main.SelectedNode = blob;
  fixture.Main.OpenDebugWindowCommand.Execute(blob);UiRuntime.Pump();
  var window = System.Windows.Application.Current.Windows.OfType<SunEyeVision.UI.Services.Editing.RuntimeToolEditorWindow>().Single();
  UiRuntime.AwaitEditorReady(window);
  window.UpdateLayout();UiRuntime.Pump();
  var tab=UiRuntime.Visuals<TabItem>(window).FirstOrDefault(item=>item.Header?.ToString()?.Contains("结果显示")==true);
  Assert.NotNull(tab);
  tab!.IsSelected=true;UiRuntime.Pump();window.UpdateLayout();UiRuntime.Pump();
  var row=Assert.Single(UiRuntime.Visuals<DeclaredFieldRow>(window),item=>item.FieldId.EndsWith("t.size"));
  var numeric=Assert.Single(UiRuntime.Visuals<NumericControl>(row));
  var text=Assert.IsType<TextBox>(numeric.GetType().GetField("ValueText",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Public)!.GetValue(numeric));
  var errors=System.Windows.Controls.Validation.GetErrors(text);
  Assert.True(errors.Count==0,$"字号文本被写坏：'{text.Text}'，校验错误：{string.Join("；",errors.Select(e=>e.ErrorContent))}");
 });
}
