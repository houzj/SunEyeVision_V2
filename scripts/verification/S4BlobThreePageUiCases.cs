using AppsPopup=SunEyeVision.Plugin.Wpf.Controls.Popups.PopupButton;
using System.Collections;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Fields;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using SunEyeVision.Plugin.Wpf.Presenters.Numeric;
using SunEyeVision.Plugin.Wpf.Presenters.Display;
using SunEyeVision.Plugin.Wpf.Adapters.Fields;
using SunEyeVision.UI.Services.Editing;
using SunEyeVision.Plugin.SDK.UI.Controls;

internal static partial class S2DocumentEditingCases
{
    const string ThreeLow="field:43bda03261df55b88f963bd18f2ba4d9";
    static object[] ThreeItems(object owner,string name)=>((IEnumerable)Get(owner,name)).Cast<object>().ToArray();
    static void ThreeCall(object owner,string method,params object?[] args)=>owner.GetType().GetMethod(method,Flags)!.Invoke(owner,args);
    static void VerifyRealThreePageUi(Func<NativeRuntimeEditorWindow> open,string path,Func<object> formal,Func<int> history,Func<bool> save,Action reopen)
    {
        var file=File.ReadAllBytes(path);var formalBefore=System.Text.Json.JsonSerializer.Serialize(formal());var position=history();
        var window=open();var control=window.EditorControl;var blob=control.Content;var composition=Get(blob,"_composition");
        var editor=(DeclaredSettingsView)Get(blob,"Editor");var fields=(DeclaredFieldsView)editor.Content;
        var run=Get(composition,"RunUi");var threshold=(NativeThresholdUiDraftControls)Get(run,"Threshold");
        var low=fields.Fields.Single(field=>field.FieldId==ThreeLow);var lowSetting=low.Setting;
        var draft=((ProductionEditorContext)ToolEditorCommit.GetDraftContext(control)!).DraftNode;
        string Private()=>System.Text.Json.JsonSerializer.Serialize(draft.RuntimeConfiguration);
        var privateBefore=Private();
        void Unchanged(){Require(formalBefore==System.Text.Json.JsonSerializer.Serialize(formal())&&position==history()&&file.SequenceEqual(File.ReadAllBytes(path)),"UI预览污染正式配置、历史或文件");Require(privateBefore==Private(),"UI预览污染私有已支持Fields/CurrentValues");}
        void Page(int page){editor.SelectedPageIndex=page;window.UpdateLayout();Pump();}
        void Reject(){Require(!control.TryCapture(out var error)&&!string.IsNullOrWhiteSpace(error),"未支持预览被实际捕获");Require(!window.TryConfirm(out error)&&window.IsVisible,"未支持预览被真实确认");var task=control.RunDebugAsync();PumpUntil(()=>task.IsCompleted);try{task.GetAwaiter().GetResult();throw new Exception("未支持预览进入真实执行");}catch(InvalidOperationException){}Unchanged();}
        void Ready(){var ok=control.TryCapture(out var error);if(!ok)Console.WriteLine("UI_DIAG mode="+threshold.SelectedMode+" threshold="+threshold.HasUiChanges+" run="+Get(run,"HasUiChanges")+" rows="+string.Join(";",ThreeItems(threshold,"_editors").Cast<DeclaredFieldEditor>().Select(e=>e.FieldId+":"+e.Setting+"/p="+e.HasPendingEdits+"/bad="+e.HasInvalidText+"/raw="+(e.InputElement as TextBox)?.Text)));Require(ok,"恢复Single无法捕获："+error);}
        Case("真实三页使用同一owner，Single正式字段身份与默认可执行",()=>{Require(composition.GetType().Name=="BlobUiComposition"&&threshold.IsSingleMode,"真实组装未接入");Ready();Unchanged();});
        var expected=new Dictionary<string,string[]>{
            ["BinaryImage"]=["mode","polarity"],["SingleThreshold"]=["mode"],["DoubleThreshold"]=["mode","low","high"],
            ["AutoThreshold"]=["mode","polarity"],["FixedSoftThreshold"]=["mode","polarity","softness"],
            ["RelativeSoftThreshold"]=["mode","polarity","softness","left","right","bottom","top"]};
        Case("六模式真实可见字段身份顺序与原件范围，五模式拒绝真实出口",()=>{
            Page(1);
            foreach(var pair in expected)
            {
                Require(threshold.TrySelectMode(pair.Key),"模式不可选择："+pair.Key);Pump();
                var visible=ThreeItems(threshold,"_editors").Cast<DeclaredFieldEditor>().Where(item=>item.Visibility==Visibility.Visible).Select(item=>item.FieldId.Replace("ui-only-threshold.","")).ToArray();
                Require(visible.SequenceEqual(pair.Value),pair.Key+"显隐/顺序错误："+string.Join(",",visible));
                Require(((FrameworkElement)Get(threshold,"_softRange")).Visibility==(pair.Key=="FixedSoftThreshold"?Visibility.Visible:Visibility.Collapsed),"软阈值范围显隐错误");
                Require(low.View.IsVisible==(pair.Key=="SingleThreshold")&&fields.Fields.Single(field=>field.FieldId=="field:88b45ab61b965761b901670fd587903f").View.IsVisible==(pair.Key=="SingleThreshold"),"真实Single字段显隐错误："+pair.Key);
                if(pair.Key=="FixedSoftThreshold"){var children=threshold.Children.Cast<FrameworkElement>().Where(item=>item.Visibility==Visibility.Visible).ToArray();Require(children.Select(item=>ReferenceEquals(item,Get(threshold,"_softRange"))?"range":((DeclaredFieldEditor)item).FieldId.Split('.').Last()).SequenceEqual(new[]{"mode","polarity","range","softness"}),"固定软阈值实际行顺序错误");Capture(editor,"three-page-fixed-soft.png");}
                Require(ReferenceEquals(low,fields.Fields.Single(field=>field.FieldId==ThreeLow))&&Equals(low.Setting,lowSetting),"切模式替换正式Single字段/值");
                if(pair.Key!="SingleThreshold")Reject();
                Require(threshold.TrySelectMode("SingleThreshold"),"Single恢复失败");Pump();Ready();Unchanged();
            }
            foreach(var field in ThreeItems(threshold,"_editors").Cast<DeclaredFieldEditor>().Where(item=>item.FieldId is not "ui-only-threshold.mode" and not "ui-only-threshold.polarity"))
            {
                var layout=((FieldPresentation)Get(field,"_presentation")).Layout!;var id=field.FieldId.Split('.').Last();
                Require(layout.Minimum==0&&layout.Maximum==(id=="softness"?254:id is "low" or "high"?255:100)&&layout.Increment==1&&layout.DecimalPlaces==0,"原阈值边界错误："+id);
            }
            var soft=Get(threshold,"_softRange");var presentations=ThreeItems(soft,"_presentations").Cast<FieldPresentation>().ToArray();Require(presentations[0].Layout!.Minimum==1&&presentations[1].Layout!.Minimum==0,"软范围两端最小值混同");
        });
        Case("正式Single raw跨模式跨页隐藏恢复，未支持分支raw不丢不泄漏",()=>{
            Page(1);var input=(TextBox)low.InputElement;var raw=input.Text;input.Text="bad-single";Pump();threshold.TrySelectMode("DoubleThreshold");Page(2);Page(1);threshold.TrySelectMode("SingleThreshold");Pump();Require(input.Text=="bad-single","正式Single raw丢失");input.Text=raw;Pump();
            // Capturing commits the original valid raw through the existing production editor.
            Ready();privateBefore=Private();lowSetting=low.Setting;
            threshold.TrySelectMode("DoubleThreshold");Pump();var high=ThreeItems(threshold,"_editors").Cast<DeclaredFieldEditor>().Single(item=>item.FieldId.EndsWith(".high"));var highInput=(TextBox)high.InputElement;var original=highInput.Text;highInput.Text="bad-preview";Pump();Page(0);editor.Visibility=Visibility.Collapsed;Pump();editor.Visibility=Visibility.Visible;Page(1);Require(highInput.Text=="bad-preview","预览raw隐藏后丢失");threshold.TrySelectMode("SingleThreshold");Reject();threshold.TrySelectMode("DoubleThreshold");Pump();highInput=(TextBox)high.InputElement;highInput.Text=original;Pump();Require(high.TryCommitPending(),"预览高阈值恢复提交失败");threshold.TrySelectMode("SingleThreshold");Pump();Ready();Unchanged();
        });
        Case("全部高级范围与十三排序集中接入，未支持值保护与恢复",()=>{
            Page(1);var ranges=ThreeItems(run,"_ranges").Cast<NativeUiDraftRange>().ToArray();Require(ranges.Length==8,"高级范围未完整接入");
            var specs=new[]{(-9000d,9000d,-9000d,9000d,1d),(1d,1e6,100d,200d,1d),(1d,1e6,100d,200d,1d),(1d,999999999d,10d,999999999d,1d),(1d,999999999d,1d,999999999d,1d),(1d,999999999d,10d,999999999d,1d),(0d,9999999.99,0d,9999999.99,.01),(.01,1d,.01,1d,.01)};
            for(var i=0;i<ranges.Length;i++)
            {
                var pair=ranges[i];var native=Get(pair,"_range");var p=ThreeItems(native,"_presentations").Cast<FieldPresentation>().ToArray();var s=specs[i];
                Require(p[0].Layout!.Minimum==s.Item1&&p[1].Layout!.Minimum==(i==7?0:s.Item1)&&p.All(item=>item.Layout!.Maximum==s.Item2&&item.Layout.Increment==s.Item5),"高级范围元数据错误："+i);
                Require(Convert.ToDouble(((ConstantSetting)Get(Get(pair,"_lower"),"Accepted")).Value)==s.Item3&&Convert.ToDouble(((ConstantSetting)Get(Get(pair,"_upper"),"Accepted")).Value)==s.Item4,"高级范围默认错误："+i);
                var toggle=(DeclaredFieldEditor)Get(pair,"_toggle");toggle.TrySetConstant(true);Pump();Require(((FrameworkElement)native).Visibility==Visibility.Visible,"范围未展开");Reject();toggle.TrySetConstant(false);Pump();
            }
            var sort=(DeclaredFieldEditor)Get(run,"_sort");var initial=sort.Setting;var port=(Port)Get(sort,"_port");Require(((TextChoiceConstraint)port.Choices!).Keys.Count==13,"排序未保留十三项");
            foreach(var key in new[]{"SortFeatureBoxAngle","SortFeatureBoxWidth","SortFeatureBoxHeight","SortFeatureRectX","SortFeatureRectY","SortFeatureAxisAngle","SortFeatureAxisRatio"}){Require(sort.TrySetConstant(key),"排序不可预览");Reject();}
            sort.TrySetConstant(((ConstantSetting)initial).Value);Pump();Ready();Unchanged();
        });
        Case("结果判定零目标加十五范围逐端默认/边界/精度及显隐",()=>{
            Page(2);var judgment=(NativeJudgmentUiDraftControls)Get(composition,"_judgmentUi");var ranges=ThreeItems(judgment,"_ranges").Cast<NativeUiDraftRange>().ToArray();Require(ranges.Length==15,"判定范围不完整");
            ((NativeAdvancedPresenter)Get(judgment,"_advanced")).IsExpanded=true;Pump();Require(!judgment.HasUiChanges,"高级导航被当修改");
            var minima=new[]{1d,1,1,-99999,-99999,-180,-99999,-99999,1,1,1,1,1,0,0};var maxima=new[]{99999d,999999999,999999999,99999,99999,180,99999,99999,99999,99999,999999999,999999999,999999999,1,1};
            for(var i=0;i<ranges.Length;i++)
            {
                var range=ranges[i];var p=ThreeItems(Get(range,"_range"),"_presentations").Cast<FieldPresentation>().ToArray();
                Require(p[0].Layout!.Minimum==minima[i]&&p[1].Layout!.Minimum==(i==0?0:minima[i])&&p.All(item=>item.Layout!.Maximum==maxima[i]&&item.Layout.Increment==(i==5?.1:i>=13?.01:1)&&item.Layout.DecimalPlaces==(i==0?0:2)),"判定两端原XML元数据不符："+i);
                var toggle=(DeclaredFieldEditor)Get(range,"_toggle");toggle.TrySetConstant(true);Pump();Require(((FrameworkElement)Get(range,"_range")).Visibility==Visibility.Visible,"判定范围未显示");Reject();toggle.TrySetConstant(false);Pump();
            }
            var zero=(DeclaredFieldEditor)Get(judgment,"_zero");zero.TrySetConstant(true);Reject();zero.TrySetConstant(false);Pump();Ready();Unchanged();
        });
        Case("四缺producer眼笔原件启用，UI改动受保护且不伪造结果",()=>{
            Page(2);var display=(NativeDisplayUiDraftRows)Get(composition,"_displayUi");var baseline=ThreeItems(display,"_baseline");Require(baseline.Length>0,"显示原件状态缺失");
            var contents=ThreeItems(display,"_contents").OfType<FrameworkElement>().ToArray();Require(contents.Length==4&&contents.All(item=>item.IsEnabled),"四缺值显示原件未启用");
            var states=((IDictionary)Get(display,"_baseline")).Keys.Cast<object>().ToArray();var visible=states.Where(state=>((Port)Get(state,"Port")).Id.EndsWith(".visible")).ToArray();Require(visible.Length==4,"四眼状态缺失");
            foreach(var state in visible){var accepted=(ConstantSetting)Get(state,"Accepted");ThreeCall(state,"TrySet",new ConstantSetting(!(bool)accepted.Value));Pump();Reject();ThreeCall(state,"TrySet",accepted);Pump();}
            var style=contents.First(item=>item.IsVisible&&Visual(item).OfType<AppsPopup>().Any());var popup=Visual(style).OfType<AppsPopup>().Single();popup.IsDropDownOpen=true;window.UpdateLayout();Pump();Require((bool)Get(style,"IsOpen"),"缺值样式原Popup未打开");
            var numbers=(IDictionary)Get(style,"_numbers");var opacityKey=numbers.Keys.Cast<string>().Single(key=>key.EndsWith(".opacity"));var text=(TextBox)Get(numbers[opacityKey]!,"TextEditor");var original=text.Text;text.Text="bad-style";Pump();Require(display.HasPendingEdits,"缺值样式raw未入UI作用域");
            Page(0);Require(!popup.IsDropDownOpen,"页切换遗留旧样式弹层");Page(2);popup.IsDropDownOpen=true;window.UpdateLayout();Pump();numbers=(IDictionary)Get(style,"_numbers");text=(TextBox)Get(numbers[opacityKey]!,"TextEditor");Require(text.Text=="bad-style","缺值样式raw关闭后丢失");text.Text=original;Pump();ThreeCall(style,"TryCommitPending",opacityKey);popup.IsDropDownOpen=false;Pump();
            Ready();Unchanged();
            Capture(editor,"three-page-results-expanded.png");
        });
        Case("基本页图像图形及四继承、屏蔽和位置修正交叉显隐",()=>{
            Page(0);Capture(editor,"three-page-basic.png");var roi=Get(composition,"_roiUi");var radios=ThreeItems(roi,"_radios");
            ThreeCall(radios[0],"TrySetConstant",1L);Pump();Require(((FrameworkElement)Get(roi,"_imageInput")).Visibility==Visibility.Visible&&((FrameworkElement)Get(roi,"_detectionRadio")).Visibility==Visibility.Collapsed,"图像ROI显隐错误");
            Require(((ComboBox)Get(Get(roi,"_imageInput"),"RightContent")).Items.Count==0,"缺producer造了假图像候选");
            ThreeCall(radios[0],"TrySetConstant",0L);ThreeCall(radios[1],"TrySetConstant",1L);Pump();
            for(var mode=0;mode<4;mode++)
            {
                ThreeCall(radios[2],"TrySetConstant",(long)mode);Pump();
                Require(((FrameworkElement)Get(roi,"_rectangleParameters")).Visibility==(mode==1?Visibility.Visible:Visibility.Collapsed)&&((FrameworkElement)Get(roi,"_circleParameters")).Visibility==(mode==3?Visibility.Visible:Visibility.Collapsed),"继承参数交叉显隐错误："+mode);
            }
            ThreeCall(radios[1],"TrySetConstant",0L);ThreeCall(radios[2],"TrySetConstant",0L);
            ThreeCall(radios[3],"TrySetConstant",1L);Pump();Require(((FrameworkElement)Get(roi,"_maskSource")).Visibility==Visibility.Visible&&((FrameworkElement)Get(roi,"_maskShapes")).Visibility==Visibility.Collapsed,"屏蔽继承显隐错误");ThreeCall(radios[3],"TrySetConstant",0L);
            var assist=Get(composition,"_assistUi");foreach(var mode in new[]{1L,2L,0L}){ThreeCall(assist,"TrySetMode",mode);Pump();var expectedCount=mode==1?8:mode==2?10:1;Require(((Panel)assist).Children.OfType<DeclaredFieldEditor>().Count(item=>item.Visibility==Visibility.Visible)==expectedCount,"位置修正字段显隐错误");}
            Pump();privateBefore=Private();Ready();Unchanged();
        });
        Case("设计入口独立数据使用真实同组装owner与正式资源",()=>{
            var sample=SunEyeVision.Tool.BlobAnalysis.Wpf.Design.BlobDesignData.CreateParameterContext();var view=new DeclaredSettingsView();
            SunEyeVision.Tool.BlobAnalysis.Wpf.Design.BlobDesignData.BindParameterContext(view,sample);
            var method=typeof(SunEyeVision.Tool.BlobAnalysis.Wpf.Design.BlobDesignData).GetMethod("GetComposition",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)!;var sameOwner=method.Invoke(null,new object[]{view})!;Require(sameOwner.GetType()==composition.GetType(),"设计时使用另一套组装");
            var designThreshold=(NativeThresholdUiDraftControls)Get(Get(sameOwner,"RunUi"),"Threshold");
            var host=new Window{Content=view,Width=720,Height=800,ShowInTaskbar=false,ShowActivated=false};host.Show();Pump();
            foreach(var key in expected.Keys){designThreshold.TrySelectMode(key);view.SelectedPageIndex=1;host.UpdateLayout();Pump();Capture(view,"three-page-design-"+key+".png");}
            host.Close();((IDisposable)sameOwner).Dispose();view.Dispose();
        });
        Case("UI-only不入方案，取消退役后恢复Single真实调试确认保存重开",()=>{
            Page(1);Capture(editor,"three-page-run.png");threshold.TrySelectMode("AutoThreshold");Reject();_=save();Require(file.SequenceEqual(File.ReadAllBytes(path)),"预览保存尝试改变文件");threshold.TrySelectMode("SingleThreshold");Pump();Ready();
            window.Close();Pump();var replacement=open();Require(!ReferenceEquals(Get(replacement.EditorControl.Content,"_composition"),composition),"关闭后复用退役owner");
            Require(replacement.EditorControl.TryCapture(out var issue),issue);var task=replacement.EditorControl.RunDebugAsync();PumpUntil(()=>task.IsCompleted);task.GetAwaiter().GetResult();Require(replacement.TryConfirm(out issue),issue);if(replacement.IsVisible)replacement.Close();Pump();Require(save(),"恢复Single正式保存失败");reopen();
        });
    }
}
