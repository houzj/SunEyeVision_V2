using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using NSubstitute;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.UI.Features.WorkflowUI;
using SunEyeVision.UI.Services;
using SunEyeVision.UI.Services.Interaction;
using SunEyeVision.UI.Services.Workflow;
using SunEyeVision.UI.Shared;

using SunEyeVision.UI.Services.Images;
using SunEyeVision.Plugin.Wpf.Controls.Native.Composition.ImageSources;
using SunEyeVision.Plugin.Wpf.Presenters.Input.ImageSources;
using SunEyeVision.Workflow;
using Xunit;

namespace SunEyeVision.UI.Tests;

public sealed partial class SolutionManagerUiTests
{
    private static void PreviewCall(ImageSourceStrip control, string method, params object[] args) =>
        typeof(ImageSourceStrip).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(control, args);

    private static Task SourceImport(ImageSourceStripSession session,string method,params object[] arguments)
        =>(Task)typeof(ImageSourceStripSession).GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(session,arguments)!;

    private static void PumpPreviewTask(ImageSourceStrip control, Task task)
    {
        var frame = new System.Windows.Threading.DispatcherFrame();
        var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
        timer.Tick += (_, _) => frame.Continue = false;
        timer.Start();
        _ = task.ContinueWith(_ => control.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ContextIdle,new Action(() => frame.Continue = false)));
        System.Windows.Threading.Dispatcher.PushFrame(frame);
        timer.Stop();
        Assert.True(task.IsCompleted, "图像导入超时");
        task.GetAwaiter().GetResult();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Source_items_reentry_stops_refresh_after_release_or_owner_change(bool release)
    {
        Sta(()=>
        {
            var source=new ImageInputSource("reentrant-items");var current=true;var invoked=false;
            using var session=new ImageSourceStripSession(source,()=>current);
            var notifications=0;session.PropertyChanged+=(_,_)=>notifications++;
            var collection=Assert.IsAssignableFrom<System.Collections.Specialized.INotifyCollectionChanged>(session.Items);
            collection.CollectionChanged+=(_,_)=>
            {
                if(invoked)return;invoked=true;
                if(release)session.Dispose();else current=false;
            };
            source.AddImages(Enumerable.Range(0,3).Select(index=>new ImageInfo{FilePath="reentry-"+index+".png"}));
            typeof(ImageSourceStripSession).GetMethod("Refresh",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(session,null);
            Assert.True(invoked);Assert.True(session.IsReadOnly);Assert.Equal(0,notifications);
            Assert.Equal(release?0:1,session.Items.Count);
        });
    }

    [Theory]
    [InlineData(false,false,0)]
    [InlineData(true,false,2)]
    [InlineData(false,true,1)]
    [InlineData(true,true,2)]
    public void First_button_uses_original_auto_switch_reset_position(bool automatic,bool selected,int expected)
    {
        Sta(()=>
        {
            var source=new ImageInputSource("first-reset");
            source.AddImages(Enumerable.Range(0,3).Select(index=>new ImageInfo{FilePath="reset-"+index+".png"}));
            source.AutoSwitchEnabled=automatic;
            if(selected){source.SetRunSelection(source.Images.Skip(1).Select(image=>image.Id));source.RunMode=ImageRunMode.运行选择;}
            source.SelectImage(expected);
            using var fixture=new NativeSourceFixture(source);Assert.False(fixture.Control.IsFirstResetActive);
            var task=(Task)typeof(ImageSourceStrip).GetMethod("SelectFirstAsync",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(fixture.Control,null)!;
            PumpPreviewTask(fixture.Control,task);
            Assert.Equal(expected,source.CapturePolicy().CurrentIndex);
            Assert.True(fixture.Control.IsFirstResetActive);
            fixture.Session.UpdateVisibleItems(Array.Empty<string>());PumpPreviewTask(fixture.Control,Task.CompletedTask);
            Assert.True(fixture.Control.IsFirstResetActive);
            var image=source.Images[expected];
            Assert.True(source.TryPresentRuntimeCursor(source.EditVersion,image.Id,image.SourceReference??image.FilePath,image.FilePath));
            PumpPreviewTask(fixture.Control,Task.CompletedTask);Assert.False(fixture.Control.IsFirstResetActive);
        });
    }

    [Fact]
    public void Actual_source_run_check_restores_original_focus_states_and_theme_lifetime()
    {
        Sta(()=>
        {
            var source=new ImageInputSource("focus-source");
            source.AddImages(new[]{new ImageInfo{FilePath="focus-source.png"}},selectFirstAdded:true);
            source.RunMode=ImageRunMode.运行选择;
            using var fixture=new NativeSourceFixture(source);
            var control=fixture.Control;
            IEnumerable<DependencyObject> Visuals(DependencyObject parent)
            {
                for(var i=0;i<System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);i++)
                {
                    var child=System.Windows.Media.VisualTreeHelper.GetChild(parent,i);yield return child;
                    foreach(var descendant in Visuals(child))yield return descendant;
                }
            }
            void Drain(){control.Dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ContextIdle);control.UpdateLayout();}
            Drain();var check=Assert.Single(Visuals(control).OfType<CheckBox>());
            var emptyFocus=Assert.IsType<Style>(control.FindResource("ImageSources.EmptyCheckBoxFocusVisual"));
            var contentFocus=Assert.IsType<Style>(control.FindResource("ImageSources.CheckRadioFocusVisual"));
            Assert.Same(emptyFocus,check.FocusVisualStyle);
            void VerifyFocus(Style style,Thickness margin)
            {
                var template=Assert.IsType<ControlTemplate>(Assert.Single(style.Setters.OfType<Setter>()).Value);
                var rectangle=Assert.IsType<System.Windows.Shapes.Rectangle>(template.LoadContent());
                Assert.Equal(margin,rectangle.Margin);Assert.Equal(1d,rectangle.StrokeThickness);
                Assert.Equal(new[]{1d,2d},rectangle.StrokeDashArray);
                Assert.Equal(SystemColors.ControlTextColor,Assert.IsType<System.Windows.Media.SolidColorBrush>(rectangle.Stroke).Color);
            }
            VerifyFocus(emptyFocus,new Thickness(1));VerifyFocus(contentFocus,new Thickness(14,0,0,0));
            check.Content="运行";Drain();Assert.Same(contentFocus,check.FocusVisualStyle);Assert.Equal(new Thickness(4,0,0,0),check.Padding);
            check.Content=null;Drain();Assert.Same(emptyFocus,check.FocusVisualStyle);
            Assert.False(check.IsChecked??false);
            source.SetRunSelection(new[]{source.Images[0].Id});Drain();Assert.True(check.IsChecked);
            var icon=Assert.IsType<Image>(check.Template.FindName("im",check));
            Assert.Contains("run-checked.png",Assert.IsAssignableFrom<System.Windows.Media.Imaging.BitmapSource>(icon.Source).ToString());
            check.IsEnabled=false;Drain();Assert.Equal(SystemColors.GrayTextColor,Assert.IsType<System.Windows.Media.SolidColorBrush>(check.Foreground).Color);
            check.ClearValue(UIElement.IsEnabledProperty);Drain();
            var manager=new ThemeManager(new SolutionSettings());manager.Initialize();var originalTheme=manager.CurrentTheme;
            using var palette=new ImageSourceThemeScope(control,manager);
            void VerifyPalette()
            {
                Drain();
                foreach(var pair in new[]{("ImageSources.CheckBoxFillNormal",SunEyeVision.UI.Themes.Base.ThemeKeys.InputBackgroundBrush),
                    ("ImageSources.CheckBoxStroke",SunEyeVision.UI.Themes.Base.ThemeKeys.InputBorderBrush)})
                    Assert.Equal(Assert.IsType<System.Windows.Media.SolidColorBrush>(Application.Current.FindResource(pair.Item2)).Color,
                        Assert.IsType<System.Windows.Media.SolidColorBrush>(control.FindResource(pair.Item1)).Color);
                Assert.Equal(Assert.IsType<System.Windows.Media.SolidColorBrush>(control.FindResource("ImageSources.CheckBoxFillNormal")).Color,
                    Assert.IsType<System.Windows.Media.SolidColorBrush>(check.Background).Color);
                Assert.Equal(Assert.IsType<System.Windows.Media.SolidColorBrush>(control.FindResource("ImageSources.CheckBoxStroke")).Color,
                    Assert.IsType<System.Windows.Media.SolidColorBrush>(check.BorderBrush).Color);
            }
            try
            {
                manager.ApplyTheme(AppTheme.Dark);VerifyPalette();manager.ApplyTheme(AppTheme.Light);VerifyPalette();
                control.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
                manager.ApplyTheme(AppTheme.Dark);
                Assert.Equal(System.Windows.Media.Color.FromRgb(244,244,244),Assert.IsType<System.Windows.Media.SolidColorBrush>(control.FindResource("ImageSources.CheckBoxFillNormal")).Color);
                control.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));VerifyPalette();
            }
            finally{manager.ApplyTheme(originalTheme);}
        });
    }
    [Fact]
    public void Thumbnail_refresh_retains_selection_focus_and_the_next_direction_key()
    {
        Sta(()=>
        {
            System.Windows.Media.Imaging.BitmapSource Pixel(byte value)
            {
                var image=System.Windows.Media.Imaging.BitmapSource.Create(1,1,96,96,System.Windows.Media.PixelFormats.Gray8,null,new[]{value,(byte)0,(byte)0,(byte)0},4);
                image.Freeze();return image;
            }
            var source=new ImageInputSource("refresh-focus");
            source.AddImages(Enumerable.Range(0,3).Select(index=>new ImageInfo{FilePath="refresh-focus-"+index+".png",Thumbnail=Pixel((byte)(index+1))}),selectFirstAdded:true);
            var activations=0;
            using var fixture=new NativeSourceFixture(source,activate:(_,_,_)=>{activations++;return Task.CompletedTask;});
            var list=Assert.IsType<ListBox>(fixture.Control.FindName("lst"));
            void Drain(){list.Dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ContextIdle);list.UpdateLayout();}
            Drain();fixture.ActivateForInput();Drain();
            list.ScrollIntoView(list.SelectedItem);list.UpdateLayout();
            var container=Assert.IsType<ListBoxItem>(list.ItemContainerGenerator.ContainerFromIndex(0));
            Assert.True(container.Focus());Drain();Assert.True(list.IsKeyboardFocusWithin);
            var item=list.Items[0];source.Images[0].Thumbnail=Pixel(99);Drain();
            var focusRetained=list.IsKeyboardFocusWithin;
            var rowRetained=ReferenceEquals(item,list.Items[0]);
            var containerRetained=ReferenceEquals(container,list.ItemContainerGenerator.ContainerFromIndex(0));
            var audit=new{rowRetained,containerRetained,focusRetained,focusedType=System.Windows.Input.Keyboard.FocusedElement?.GetType().FullName,selectedIndex=list.SelectedIndex,activations};
            File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")??throw new InvalidOperationException("Missing validation root"),"source-item-refresh-identity.json"),System.Text.Json.JsonSerializer.Serialize(audit));
            Assert.Equal(0,activations);Assert.Equal(0,source.CurrentIndex);
            Assert.True(focusRetained,"缩略图刷新使键盘焦点离开列表。");
            var target=Assert.IsAssignableFrom<UIElement>(System.Windows.Input.Keyboard.FocusedElement);
            var input=PresentationSource.FromVisual(target)!;
            var preview=new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice,input,0,System.Windows.Input.Key.Right){RoutedEvent=System.Windows.Input.Keyboard.PreviewKeyDownEvent};
            target.RaiseEvent(preview);
            if(!preview.Handled)target.RaiseEvent(new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice,input,0,System.Windows.Input.Key.Right){RoutedEvent=System.Windows.Input.Keyboard.KeyDownEvent});
            Drain();Assert.Equal(1,source.CurrentIndex);Assert.Equal(1,activations);
        });
    }
    [Fact]
    public void Original_content_switch_keeps_new_owner_and_localized_mode_during_reentrant_acceptance()
    {
        Sta(()=>
        {
            var language=SunEyeVision.Plugin.Wpf.Native.Common.Localization.LocalizationResourceDictionary.Instance;
            var keys=new[]{"运行全部","运行选中","RunParam_Auto Switch","Lang_ImgSoc"};
            var saved=keys.ToDictionary(key=>key,key=>language[key]);
            var first=new ImageInputSource("old-switch"){AutoSwitchEnabled=false};
            var second=new ImageInputSource("new-switch"){AutoSwitchEnabled=false,RunMode=ImageRunMode.运行选择};
            var oldWrites=0;var newWrites=0;second.ConfigurationWriter=_=>newWrites++;
            using var secondSession=new ImageSourceStripSession(second,()=>true);
            using var fixture=new NativeSourceFixture(first);
            var strip=fixture.Control;
            var control=Assert.IsType<SunEyeVision.Plugin.Wpf.Controls.Inputs.ContentToggleSwitch>(strip.FindName("AutoSwitch"));
            var toggle=Assert.IsType<ToggleButton>(control.FindName("TO"));
            try
            {
                language["运行全部"]="Run all (audit)";language["运行选中"]="Run selected (audit)";
                language["RunParam_Auto Switch"]="Automatic switch (audit)";language["Lang_ImgSoc"]="Image source (audit)";
                first.ConfigurationWriter=_=>{oldWrites++;strip.Session=secondSession;};
                typeof(ToggleButton).GetMethod("OnClick",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(toggle,null);
                strip.Dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ContextIdle);strip.UpdateLayout();
                Assert.Equal(1,oldWrites);Assert.Equal(0,newWrites);Assert.True(first.AutoSwitchEnabled);
                Assert.Same(secondSession,strip.Session);Assert.False(second.AutoSwitchEnabled);Assert.False(control.IsChecked);Assert.False(toggle.IsChecked);
                var mode=Assert.IsType<ComboBox>(strip.FindName("Mode"));Assert.Equal(1,mode.SelectedIndex);
                Assert.Equal(new[]{"Run all (audit)","Run selected (audit)"},mode.Items.Cast<object>().Select(item=>(string)item.GetType().GetProperty("DisplayName")!.GetValue(item)!));
                Assert.Equal("Automatic switch (audit)",strip.AutoSwitchText);
                var independent=new ImageSourceStrip{Session=secondSession};
                var other=Assert.IsType<SunEyeVision.Plugin.Wpf.Controls.Inputs.ContentToggleSwitch>(independent.FindName("AutoSwitch"));
                Assert.NotSame(control.OffContent,other.OffContent);Assert.NotSame(control.OnContent,other.OnContent);
                foreach(var image in new[]{Assert.IsType<Image>(control.OffContent),Assert.IsType<Image>(control.OnContent)})Assert.Equal(System.Windows.Media.Stretch.None,image.Stretch);
                Assert.Contains("/Inputs/Switch/off-dark.png",Assert.IsType<Image>(control.OffContent).Source.ToString());
                first.AutoSwitchEnabled=false;strip.Dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ContextIdle);
                Assert.False(control.IsChecked);Assert.Equal(0,newWrites);Assert.Equal(1,mode.SelectedIndex);
                independent.Session=null;
            }
            finally
            {
                foreach(var pair in saved)language[pair.Key]=pair.Value;
                strip.Session=null;
            }
        });
    }
    private sealed class NativeSourceFixture : IDisposable
    {
        public ImageInputSource Source {get;}
        public ImageSourceStripSession Session {get;}
        public ImageSourceStrip Control {get;}
        private readonly Window _window;
        public NativeSourceFixture(ImageInputSource source,Func<bool>? current=null,Func<ImageInfo,int,long,Task>? activate=null)
        {
            Source=source;Session=new(source,current??(()=>true),async(image,index,version)=>
            {
                Assert.Equal(version,source.EditVersion);source.SelectImage(index);
                if(activate is not null)await activate(image,index,version);
            });
            Control=new ImageSourceStrip{Session=Session};
            _window=new Window{Content=Control,Width=600,Height=180,Left=-10000,ShowActivated=false};
            _window.Show();_window.UpdateLayout();
            Control.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
        }
        public Task Request(ImageSourceStripRequestKind kind,string? itemId=null,IEnumerable<string>? paths=null)
            =>Session.RequestAsync(new(kind,Session.Version,itemId,paths:paths),CancellationToken.None);
        public void ActivateForInput(){_window.Activate();Control.UpdateLayout();}
        public void Dispose(){_window.Close();Control.Session=null;Session.Dispose();}
    }

    [Fact]
    public void Viewport_release_failure_still_detaches_session_and_cancels_requests()
    {
        Sta(()=>
        {
            var session=new ThrowingViewportSession();
            var control=new ImageSourceStrip {Session=session};
            var window=new Window {Content=control,Width=600,Height=180,Left=-10000,ShowActivated=false};
            try
            {
                window.Show();window.UpdateLayout();
                var field=typeof(ImageSourceStrip).GetField("_lifetime",BindingFlags.NonPublic|BindingFlags.Instance)!;
                var lifetime=(CancellationTokenSource)field.GetValue(control)!;
                Assert.Equal(1,session.Subscribers);
                session.ThrowOnRelease=true;
                var error=Assert.Throws<TargetInvocationException>(()=>PreviewCall(control,"Detach"));
                Assert.IsType<AggregateException>(error.InnerException);
                Assert.True(lifetime.IsCancellationRequested);
                Assert.Equal(0,session.Subscribers);
                Assert.Null(field.GetValue(control));
            }
            finally { window.Close();control.Session=null; }
        });
    }

    [Fact]
    public void Viewport_release_reentry_keeps_the_new_session_attached()
    {
        Sta(()=>
        {
            var oldSession=new ThrowingViewportSession();
            var newSession=new ThrowingViewportSession();
            var control=new ImageSourceStrip {Session=oldSession};
            var window=new Window {Content=control,Width=600,Height=180,Left=-10000,ShowActivated=false};
            try
            {
                window.Show();window.UpdateLayout();
                oldSession.OnRelease=()=>control.Session=newSession;
                control.Session=null;
                Assert.Same(newSession,control.Session);
                Assert.Equal(0,oldSession.Subscribers);
                Assert.Equal(1,newSession.Subscribers);
                Assert.NotNull(typeof(ImageSourceStrip).GetField("_lifetime",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(control));
            }
            finally {window.Close();control.Session=null;}
        });
    }

    [Fact]
    public void Refresh_reentry_stops_old_notifications_after_session_replacement()
    {
        Sta(()=>
        {
            var oldSession=new ThrowingViewportSession();
            var newSession=new ThrowingViewportSession();
            var control=new ImageSourceStrip {Session=oldSession};
            var window=new Window {Content=control,Width=600,Height=180,Left=-10000,ShowActivated=false};
            try
            {
                window.Show();window.UpdateLayout();
                var changed=false;var statusNotifications=0;
                control.PropertyChanged+=(_,args)=>
                {
                    if(args.PropertyName==nameof(ImageSourceStrip.Items)&&!changed)
                    {changed=true;control.Session=newSession;}
                    if(args.PropertyName==nameof(ImageSourceStrip.Status))statusNotifications++;
                };
                PreviewCall(control,"Refresh");
                Assert.Same(newSession,control.Session);
                Assert.Equal(1,statusNotifications);
                Assert.Equal(0,oldSession.Subscribers);
                Assert.Equal(1,newSession.Subscribers);
            }
            finally {window.Close();control.Session=null;}
        });
    }

    [Fact]
    public void Late_first_request_does_not_scroll_the_replacement_session()
    {
        Sta(()=>
        {
            var pending=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var oldSession=Substitute.For<IImageSourceStripSession>();
            var newSession=Substitute.For<IImageSourceStripSession>();
            var oldItems=new[]{new ImageSourceStripItem("old","旧图",null,true)};
            var newItems=System.Linq.Enumerable.Range(0,40)
                .Select(index=>new ImageSourceStripItem("new-"+index,"图"+index,null,true)).ToArray();
            oldSession.Items.Returns(oldItems);oldSession.DisplayIndex.Returns(-1);oldSession.ConfiguredStartIndex.Returns(0);
            oldSession.RequestAsync(Arg.Any<ImageSourceStripRequest>(),Arg.Any<CancellationToken>()).Returns(pending.Task);
            newSession.Items.Returns(newItems);newSession.DisplayIndex.Returns(-1);newSession.ConfiguredStartIndex.Returns(0);
            var control=new ImageSourceStrip {Session=oldSession};
            var window=new Window {Content=control,Width=500,Height=180,Left=-10000,ShowActivated=false};
            try
            {
                window.Show();window.UpdateLayout();
                var select=(Task)typeof(ImageSourceStrip).GetMethod("SelectFirstAsync",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(control,null)!;
                Assert.False(select.IsCompleted);
                control.Session=newSession;window.UpdateLayout();
                var list=(ListBox)control.FindName("lst");list.UpdateLayout();
                var scroll=(ScrollViewer)typeof(ImageSourceStrip).GetMethod("FindScrollViewer",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,new object[]{list})!;
                scroll.ScrollToHorizontalOffset(10);scroll.UpdateLayout();
                var offset=scroll.HorizontalOffset;Assert.True(offset>0,"测试需要可横向滚动的缩略图列表");
                pending.SetResult();PumpPreviewTask(control,select);scroll.UpdateLayout();
                Assert.Equal(offset,scroll.HorizontalOffset);
            }
            finally {window.Close();control.Session=null;}
        });
    }

    private sealed class ThrowingViewportSession : IImageSourceStripSession,IImageSourceStripViewportSession
    {
        private System.ComponentModel.PropertyChangedEventHandler? _changed;
        public int Subscribers {get;private set;}
        public bool ThrowOnRelease {get;set;}
        public Action? OnRelease {get;set;}
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged
        { add {_changed+=value;Subscribers++;} remove {_changed-=value;Subscribers--;} }
        public IReadOnlyList<ImageSourceStripItem> Items=>Array.Empty<ImageSourceStripItem>();
        public long Version=>0;
        public int DisplayIndex=>-1;
        public int ConfiguredStartIndex=>-1;
        public bool IsReadOnly=>false;
        public bool CanEdit=>true;
        public bool CanNavigate=>true;
        public bool IsSelectMode=>false;
        public bool AutoSwitchEnabled=>false;
        public bool IsImporting=>false;
        public string? Status=>null;
        public Task RequestAsync(ImageSourceStripRequest request,CancellationToken cancellationToken)=>Task.CompletedTask;
        public void UpdateVisibleItems(IReadOnlyList<string> itemIds)
        {
            if(itemIds.Count!=0)return;
            if(ThrowOnRelease)throw new InvalidOperationException("release observer failed");
            OnRelease?.Invoke();
        }
    }

    [Fact]
    public void Local_import_buttons_dispatch_directly_without_a_source_menu()
    {
        Sta(()=>
        {
            var session=Substitute.For<IImageSourceStripSession>();
            session.Items.Returns(Array.Empty<ImageSourceStripItem>());
            session.DisplayIndex.Returns(-1);
            session.Version.Returns(42);
            session.RequestAsync(Arg.Any<ImageSourceStripRequest>(),Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
            var control=new ImageSourceStrip {Session=session};
            var window=new Window {Content=control,Width=600,Height=180,Left=-10000,ShowActivated=false};
            try
            {
                window.Show();window.UpdateLayout();
                ((Button)control.FindName("BtnImageAdd")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                ((Button)control.FindName("BtnImageAddFolder")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                session.Received(1).RequestAsync(Arg.Is<ImageSourceStripRequest>(request=>request.Kind==ImageSourceStripRequestKind.ImportFiles&&request.ExpectedVersion==42),Arg.Any<CancellationToken>());
                session.Received(1).RequestAsync(Arg.Is<ImageSourceStripRequest>(request=>request.Kind==ImageSourceStripRequestKind.ImportDirectory&&request.ExpectedVersion==42),Arg.Any<CancellationToken>());
                Assert.Null(control.FindName("Pop1"));Assert.Null(control.FindName("Pop2"));
            }
            finally {window.Close();control.Session=null;}
        });
    }

    [Fact]
    public void Unchecked_activation_in_selection_mode_preserves_start_history_and_execution()
    {
        Sta(()=>
        {
            var source=new ImageInputSource("node");
            var selected=new ImageInfo{FilePath="selected.png"};var uncheckedImage=new ImageInfo{FilePath="unchecked.png"};
            source.AddImages(new[]{selected,uncheckedImage},selectFirstAdded:true);
            source.SetRunSelection(new[]{selected.Id});source.RunMode=ImageRunMode.运行选择;
            var version=source.EditVersion;var writes=0;var executions=0;
            source.ConfigurationWriter=_=>writes++;
            using var fixture=new NativeSourceFixture(source,activate:(_,_,_)=>{executions++;return Task.CompletedTask;});
            PumpPreviewTask(fixture.Control,fixture.Request(ImageSourceStripRequestKind.Activate,uncheckedImage.Id));
            PumpPreviewTask(fixture.Control,fixture.Request(ImageSourceStripRequestKind.SelectStart,uncheckedImage.Id));
            Assert.Throws<InvalidOperationException>(()=>source.SelectImage(1));
            Assert.Equal(0,writes);Assert.Equal(0,executions);Assert.Equal(version,source.EditVersion);
            Assert.Equal(0,source.CapturePolicy().CurrentIndex);Assert.Same(selected,source.CurrentImage);
            Assert.False(uncheckedImage.IsForRun);Assert.Equal(0,((ListBox)fixture.Control.FindName("lst")).SelectedIndex);
            Assert.Contains("已勾选",fixture.Session.Status);
            PumpPreviewTask(fixture.Control,fixture.Request(ImageSourceStripRequestKind.Activate,selected.Id));
            Assert.Equal(1,executions);
        });
    }

    [Fact]
    public void Selection_and_removal_preserve_current_image_identity_and_one_way_display()
    {
        Sta(()=>
        {
            var files=new[]{Path.GetTempFileName(),Path.GetTempFileName(),Path.GetTempFileName()};
            try
            {
                var source=new ImageInputSource("node");foreach(var file in files)source.AddImage(new ImageInfo{FilePath=file});
                using var fixture=new NativeSourceFixture(source);
                source.SelectImage(2);
                PumpPreviewTask(fixture.Control,Task.CompletedTask);
                var list=(ListBox)fixture.Control.FindName("lst");
                Assert.Equal(2,list.SelectedIndex);var selected=source.CurrentImage;
                PumpPreviewTask(fixture.Control,fixture.Request(ImageSourceStripRequestKind.Remove,source.Images[0].Id));
                Assert.Equal(1,source.CurrentIndex);Assert.Equal(1,list.SelectedIndex);Assert.Same(selected,source.CurrentImage);
                Assert.Equal(files[2],source.CurrentImage!.FilePath);Assert.All(files,file=>Assert.True(File.Exists(file)));
                // Programmatic list selection is presentation only and cannot write formal source selection.
                list.SelectedIndex=0;Assert.Equal(1,source.CurrentIndex);
            }
            finally{foreach(var file in files)File.Delete(file);}
        });
    }

    [Fact]
    public void Restoring_images_preserves_missing_references_selection_and_run_flags_without_rewriting_configuration()
    {
        Sta(() =>
        {
            var root = Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT") ?? throw new InvalidOperationException("Missing validation root"), "restore-images", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            try
            {
                var a = Path.Combine(root, "a.png"); var b = Path.Combine(root, "b.png");
                File.WriteAllBytes(a, new byte[] { 1 }); File.WriteAllBytes(b, new byte[] { 2 });
                var config = new ImageSourcePolicy { FilePaths = new() { Path.Combine(root, "missing.png"), a, Path.Combine(root, "lost.png"), b }, CurrentIndex = 3, ForRunPaths = new() { b } };
                var source = new ImageInputSource("node");
                var resolver = new SunEyeVision.Workflow.Persistence.SolutionResourceResolver(root);
                source.LoadFromPolicy(config, resolver);
                Assert.Equal(4, source.Count); Assert.Equal(3, source.CurrentIndex);
                Assert.Equal(b, source.CurrentImage!.FilePath); Assert.True(source.CurrentImage.IsForRun);
                Assert.Equal(config.FilePaths, source.CapturePolicy().FilePaths);
                Assert.Equal(config.ForRunPaths, source.CapturePolicy().ForRunPaths);
                config.CurrentIndex = 0; source.LoadFromPolicy(config, resolver); Assert.Equal(0, source.CurrentIndex);
                Assert.Equal(config.FilePaths[0], source.CurrentImage!.SourceReference);
                Assert.False(File.Exists(source.CurrentImage.FilePath));
                source.ClearImages(); Assert.True(File.Exists(a)); Assert.True(File.Exists(b));
            }
            finally { Directory.Delete(root, true); }
        });
    }

    [Fact]
    public void Unresolved_relative_image_keeps_reference_and_can_be_resolved_without_current_directory_fallback()
    {
        Sta(() =>
        {
            var root = Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")
                ?? throw new InvalidOperationException("Missing validation root"), "relative-image", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var file = Path.Combine(root, "sample.png"); File.WriteAllBytes(file, new byte[] { 1 });
            try
            {
                var config = new ImageSourcePolicy { FilePaths = new() { "sample.png" }, ForRunPaths = new() { "sample.png" }, CurrentIndex = 0 };
                var source = new ImageInputSource("relative");
                source.LoadFromPolicy(config, new SunEyeVision.Workflow.Persistence.SolutionResourceResolver(null));
                var unresolved = Assert.Single(source.Images);
                Assert.Equal("sample.png", unresolved.SourceReference); Assert.Empty(unresolved.FilePath);
                Assert.False(string.IsNullOrWhiteSpace(unresolved.ResourceError)); Assert.True(unresolved.IsForRun);
                Assert.Equal(config.FilePaths, source.CapturePolicy().FilePaths);
                source.LoadFromPolicy(config, new SunEyeVision.Workflow.Persistence.SolutionResourceResolver(root));
                var resolved = Assert.Single(source.Images);
                Assert.Equal(file, resolved.FilePath); Assert.Null(resolved.ResourceError); Assert.True(resolved.IsForRun);
                Assert.Equal("sample.png", Assert.Single(source.CapturePolicy().FilePaths));
                source.ClearImages(); Assert.True(File.Exists(file));
            }
            finally { Directory.Delete(root, true); }
        });
    }

    [Fact]
    public void Right_click_and_programmatic_selection_do_not_execute_left_and_keys_execute_once()
    {
        Sta(()=>
        {
            var source=new ImageInputSource("node");source.AddImages(new[]{new ImageInfo{FilePath="a.png"},new ImageInfo{FilePath="b.png",IsForRun=true}});
            var executions=0;
            using var fixture=new NativeSourceFixture(source,activate:(_,_,_)=>{executions++;return Task.CompletedTask;});
            fixture.ActivateForInput();
            var list=(ListBox)fixture.Control.FindName("lst");list.SelectedIndex=1;Assert.Equal(0,executions);
            list.UpdateLayout();
            var first=Assert.IsType<ListBoxItem>(list.ItemContainerGenerator.ContainerFromIndex(0));first.ApplyTemplate();
            System.Collections.Generic.IEnumerable<DependencyObject> Descendants(DependencyObject parent)
            {
                for(var i=0;i<System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);i++)
                {var child=System.Windows.Media.VisualTreeHelper.GetChild(parent,i);yield return child;foreach(var nested in Descendants(child))yield return nested;}
            }
            var row=Assert.Single(Descendants(first).OfType<Grid>().Where(grid=>grid.Width==72));
            void Click(MouseButton button)=>row.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,0,button){RoutedEvent=Mouse.MouseDownEvent});
            Click(MouseButton.Right);Assert.Equal(0,executions);
            // Establish a distinct restored selection, then use the real visual's routed mouse input.
            list.SelectedIndex=1;Click(MouseButton.Left);
            Assert.Equal(1,executions);Assert.Equal(0,source.CurrentIndex);Assert.True(first.IsKeyboardFocusWithin);
            Click(MouseButton.Left);Assert.Equal(1,executions);
            var input=PresentationSource.FromVisual(list)!;
            void KeyInput(Key key)
            {
                var target=(UIElement)(Keyboard.FocusedElement??list);
                var preview=new KeyEventArgs(Keyboard.PrimaryDevice,input,0,key){RoutedEvent=Keyboard.PreviewKeyDownEvent};
                target.RaiseEvent(preview);
                if(!preview.Handled)target.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,input,0,key){RoutedEvent=Keyboard.KeyDownEvent});
            }
            KeyInput(Key.Right);Assert.Equal(2,executions);Assert.Equal(1,source.CurrentIndex);
            KeyInput(Key.Right);Assert.Equal(2,executions);
            KeyInput(Key.Left);Assert.Equal(3,executions);Assert.Equal(0,source.CurrentIndex);
            KeyInput(Key.Left);Assert.Equal(3,executions);
            KeyInput(Key.Enter);KeyInput(Key.Delete);Assert.Equal(3,executions);Assert.Equal(2,source.Count);
            source.SetRunSelection(source.Images.Select(image=>image.Id));source.RunMode=ImageRunMode.运行选择;
            PumpPreviewTask(fixture.Control,Task.CompletedTask);
            KeyInput(Key.Right);Assert.Equal(4,executions);Assert.Equal(1,source.CurrentIndex);
            PreviewCall(fixture.Control,"FirstImage_Click",fixture.Control,new RoutedEventArgs());Assert.Equal(4,executions);
        });
    }
    [Fact]
    public void Adding_missing_or_duplicate_images_does_not_change_current_selection()
    {
        Sta(() =>
        {
            var file = Path.GetTempFileName();
            try
            {
                var source = new ImageInputSource("node"); source.AddImages(new[]{new ImageInfo { FilePath = file }},selectFirstAdded:true);
                var selected=source.CurrentImage;var version=source.EditVersion;
                using var fixture=new NativeSourceFixture(source);
                PumpPreviewTask(fixture.Control,fixture.Request(ImageSourceStripRequestKind.ImportFiles,paths:new[]{file,file+".missing"}));
                Assert.Single(source.Images);Assert.Equal(0,source.CurrentIndex);
                Assert.Same(selected,source.CurrentImage);Assert.Equal(version,source.EditVersion);
            }
            finally { File.Delete(file); }
        });
    }

    [Fact]
    public void Manual_folder_import_expands_one_level_in_vm_entry_order_as_one_edit()
    {
        Sta(()=>
        {
            var root=Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")??throw new InvalidOperationException("Missing validation root"),"folder-order",Guid.NewGuid().ToString("N"));
            var child=Path.Combine(root,"bb");var nested=Path.Combine(child,"nested");Directory.CreateDirectory(nested);
            var childImage=Path.Combine(child,"x.jpg");var first=Path.Combine(root,"a.png");var last=Path.Combine(root,"cccc.bmp");
            File.WriteAllBytes(childImage,new byte[]{1});File.WriteAllBytes(first,new byte[]{1});File.WriteAllBytes(last,new byte[]{1});
            File.WriteAllBytes(Path.Combine(root,"skip.txt"),new byte[]{1});File.WriteAllBytes(Path.Combine(nested,"ignored.png"),new byte[]{1});
            try
            {
                var source=new ImageInputSource("source");var writes=0;source.ConfigurationWriter=_=>writes++;
                using var fixture=new NativeSourceFixture(source);
                PumpPreviewTask(fixture.Control,fixture.Request(ImageSourceStripRequestKind.ImportDirectory,paths:new[]{root}));
                Assert.Equal(new[]{childImage,first,last},source.Images.Select(image=>image.FilePath));
                Assert.Equal(1,writes);Assert.Equal(-1,source.CurrentIndex);Assert.Null(source.CurrentImage);
            }
            finally{Directory.Delete(root,true);}
        });
    }

    [Fact]
    public void Folder_import_failure_after_an_earlier_directory_does_not_accept_a_partial_batch()
    {
        Sta(()=>
        {
            var root=Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")??throw new InvalidOperationException("Missing validation root"),"folder-failure",Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);var file=Path.Combine(root,"first.png");File.WriteAllBytes(file,new byte[]{1});
            try
            {
                var source=new ImageInputSource("source");var existing=new ImageInfo{FilePath="existing.png"};
                source.AddImages(new[]{existing},selectFirstAdded:true);var initialVersion=source.EditVersion;
                var writes=0;source.ConfigurationWriter=_=>writes++;
                using var fixture=new NativeSourceFixture(source);
                Assert.Throws<DirectoryNotFoundException>(()=>PumpPreviewTask(fixture.Control,
                    fixture.Request(ImageSourceStripRequestKind.ImportDirectory,paths:new[]{root,Path.Combine(root,"missing")})));
                Assert.Equal(0,writes);Assert.Single(source.Images);Assert.Same(existing,source.CurrentImage);
                Assert.Equal(initialVersion,source.EditVersion);Assert.False(fixture.Session.IsImporting);
                Assert.True(File.Exists(file));
            }
            finally{Directory.Delete(root,true);}
        });
    }

    [Fact]
    public void Import_preserves_current_selection_without_emitting_an_explicit_activation()
    {
        Sta(()=>
        {
            var root=Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")??throw new InvalidOperationException("Missing validation root"),"import-reentrant",Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);var path=Path.Combine(root,"new.png");File.WriteAllBytes(path,new byte[]{1});
            try
            {
                var source=new ImageInputSource("source");var original=new ImageInfo{FilePath="existing.png"};
                source.AddImages(new[]{original},selectFirstAdded:true);var writes=0;source.ConfigurationWriter=_=>writes++;
                using var fixture=new NativeSourceFixture(source);
                var activations=0;source.ExplicitSelectionAccepted+=(_,_)=>activations++;
                PumpPreviewTask(fixture.Control,fixture.Request(ImageSourceStripRequestKind.ImportFiles,paths:new[]{path}));
                Assert.Equal(1,writes);Assert.Equal(0,activations);Assert.Equal(2,source.Count);Assert.Same(original,source.CurrentImage);
                Assert.Contains("已添加",fixture.Session.Status??string.Empty);
            }
            finally{Directory.Delete(root,true);}
        });
    }

    [Fact]
    public void Import_cancel_before_acceptance_does_not_publish_or_change_selection()
    {
        Sta(() =>
        {
            var source = new ImageInputSource("source");
            source.AddImages(new[] { new ImageInfo { FilePath = "existing.png" } }, selectFirstAdded: true);
            using var fixture=new NativeSourceFixture(source);
            var files = new TaskCompletionSource<string[]>(TaskCreationOptions.RunContinuationsAsynchronously);
            var writes = 0; source.ConfigurationWriter = _ => writes++;
            var task=SourceImport(fixture.Session,"ImportAsync",(Func<CancellationToken,Task<string[]>>)(_=>files.Task));
            fixture.Request(ImageSourceStripRequestKind.CancelImport).GetAwaiter().GetResult();
            files.SetResult(new[] { "late.png" });
            PumpPreviewTask(fixture.Control,task);
            Assert.Equal(0, writes); Assert.Single(source.Images);
            Assert.Equal("existing.png", source.CurrentImage!.FilePath);
            Assert.False(fixture.Session.IsImporting);
            Assert.Contains("取消",fixture.Session.Status);Assert.DoesNotContain("已正式接纳",fixture.Session.Status);
        });
    }

    [Fact]
    public void Import_cancel_after_atomic_acceptance_keeps_the_complete_committed_batch()
    {
        Sta(() =>
        {
            var directory = Path.Combine(Path.GetTempPath(), "SunEyeVision-ImportCancel", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                var paths = Enumerable.Range(0, 600).Select(index => Path.Combine(directory, index.ToString("D4") + ".png")).ToArray();
                foreach (var path in paths) File.WriteAllBytes(path, new byte[] { 1 });
                var source = new ImageInputSource("import");
                using var fixture=new NativeSourceFixture(source);
                source.CollectionChanged+=(_,_)=>fixture.Request(ImageSourceStripRequestKind.CancelImport).GetAwaiter().GetResult();
                PumpPreviewTask(fixture.Control,fixture.Request(ImageSourceStripRequestKind.ImportDirectory,paths:new[]{directory}));
                Assert.Equal(600, source.Count); Assert.Equal(600, source.CapturePolicy().FilePaths.Count);
                Assert.False(fixture.Session.IsImporting);Assert.Contains("取消",fixture.Session.Status);
                Assert.All(paths, path => Assert.True(File.Exists(path)));
            }
            finally { Directory.Delete(directory, true); }
        });
    }

    [Fact]
    public void Import_source_switch_during_commit_cannot_change_new_source_selection()
    {
        Sta(() =>
        {
            var directory = Path.Combine(Path.GetTempPath(), "SunEyeVision-ImportSwitch", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                var paths = Enumerable.Range(0, 600).Select(index => Path.Combine(directory, index.ToString("D4") + ".png")).ToArray();
                foreach (var path in paths) File.WriteAllBytes(path, new byte[] { 1 });
                var old = new ImageInputSource("old"); var next = new ImageInputSource("next");
                next.AddImages(paths.Take(2).Select(path => new ImageInfo { FilePath = path })); next.CurrentIndex = 1;
                ImageInputSource current=old;
                using var fixture=new NativeSourceFixture(old,()=>ReferenceEquals(current,old));
                using var nextSession=new ImageSourceStripSession(next,()=>ReferenceEquals(current,next));
                old.CollectionChanged += (_, _) =>
                {
                    current=next;fixture.Control.Session=nextSession;
                };
                PumpPreviewTask(fixture.Control,fixture.Request(ImageSourceStripRequestKind.ImportFiles,paths:paths));
                Assert.Equal(600, old.Count); Assert.Equal(2, next.Count);
                Assert.Equal(1,nextSession.DisplayIndex);Assert.Equal(1,next.CurrentIndex);
                Assert.False(fixture.Session.IsImporting);
            }
            finally { Directory.Delete(directory, true); }
        });
    }

    [Fact]
    public void Manual_execution_does_not_advance_but_default_successful_execution_does()
    {
        Sta(() =>
        {
            using var fixture=new LocalImageExecutionFixture();var source=fixture.AddSource("source",2);
            var completed=0;fixture.Manager.WorkflowExecutionCompleted+=(_,_)=>completed++;
            PumpLocalExecution(()=>fixture.Manager.RunSingleAsync(fixture.Tab,advanceLocalImage:false));
            Assert.Empty(fixture.Errors);Assert.Equal(1,completed);Assert.Equal(0,source.CurrentIndex);
            PumpLocalExecution(()=>fixture.Manager.RunSingleAsync(fixture.Tab));
            Assert.Empty(fixture.Errors);Assert.Equal(2,completed);Assert.Equal(1,source.CurrentIndex);
            // Existing file with invalid bytes passes path readiness and fails in the actual Runtime decoder.
            File.WriteAllBytes(source.Images[0].FilePath,new byte[]{1});
            PumpLocalExecution(()=>fixture.Manager.RunSingleAsync(fixture.Tab));
            Assert.NotEmpty(fixture.Errors);Assert.Equal(0,source.CurrentIndex);
            using var cancel=new CancellationTokenSource();cancel.Cancel();
            PumpLocalExecution(()=>fixture.Manager.RunSingleAsync(fixture.Tab,cancel.Token));
            Assert.Equal(0,source.CurrentIndex);
        });
    }
    // This execution test needs a temporary manager, not the retired manager window/card fixture.
    private sealed class PreviewSolutionFixture : IDisposable
    {
        private readonly string _root = Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")
            ?? throw new InvalidOperationException("Missing validation root"), "preview-policy", Guid.NewGuid().ToString("N"));
        public SolutionManager Manager { get; }
        public string CreateImage(string name)
        {
            var path = Path.Combine(_root, name);
            var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();
            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(System.Windows.Media.Imaging.BitmapSource.Create(
                2,1,96,96,System.Windows.Media.PixelFormats.Gray8,null,new byte[]{20,90},2)));
            using(var file=File.Create(path))encoder.Save(file);
            return path;
        }
        public PreviewSolutionFixture()
        {
            Directory.CreateDirectory(_root);
            Manager = new SolutionManager(new ApplicationDataLocationService(Path.Combine(_root, "locator.json"))
                .Prepare(Path.Combine(_root, "data")));
        }
        public void Dispose() => Directory.Delete(_root, true);
    }
}
