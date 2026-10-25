using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
using SunEyeVision.UI.Features.Dialogs;
using SunEyeVision.UI.Shell.Windows;
using Xunit;

namespace SunEyeVision.UI.Tests;

[Collection("Editor commit UI")]
public sealed class SolutionDialogOwnerTests
{
    [Theory]
    [InlineData("YesButton", MessageBoxResult.Yes)]
    [InlineData("NoButton", MessageBoxResult.No)]
    [InlineData("CancelButton", MessageBoxResult.Cancel)]
    public void Deferred_closing_keeps_real_owner_and_original_three_button_results(string button, MessageBoxResult expected) => Sta(() =>
    {
        var owner = Host(); owner.Show(); owner.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        var frame = new DispatcherFrame(); bool approved = false, scheduled = false, finished = false;
        int prompts = 0; MessageBoxResult result = MessageBoxResult.None; Exception? failure = null;
        CancelEventHandler closing = (_, args) =>
        {
            if (approved) return;
            args.Cancel = true;
            if (scheduled) return;
            scheduled = true;
            owner.Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    Answer(owner, button, dialog => { prompts++; AssertOwned(owner, dialog); }, error => failure = error);
                    result = ThemeMessageDialog.Show("Owner verification", "保存方案", MessageBoxButton.YesNoCancel,
                        ThemeMessageDialog.MessageIcon.Question, owner);
                    if (result != MessageBoxResult.Cancel) { approved = true; owner.Close(); }
                    finished = true;
                }
                catch (Exception error) { failure = error; }
                finally { frame.Continue = false; }
            }));
        };
        owner.Closing += closing;
        try
        {
            owner.Close(); owner.Close(); // Two close requests must still schedule only one confirmation.
            Dispatcher.PushFrame(frame);
            if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
            Assert.True(finished); Assert.Equal(expected, result); Assert.Equal(1, prompts);
            Assert.Equal(expected == MessageBoxResult.Cancel, owner.IsVisible);
        }
        finally { approved = true; owner.Closing -= closing; owner.Close(); }
    });

    [Fact]
    public void Actual_transition_error_keeps_host_owner_and_reentrant_request_is_rejected() => Sta(() =>
    {
        var owner = Host(); owner.Show(); owner.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle); Exception? failure = null; int prompts = 0;
        // No execution manager: force the actual production transition catch before any document mutation.
        var model = (MainWindowViewModel)RuntimeHelpers.GetUninitializedObject(typeof(MainWindowViewModel));
        typeof(MainWindowViewModel).GetProperty("SolutionDialogOwner", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(model, owner);
        try
        {
            Answer(owner, "OkButton", dialog =>
            {
                prompts++; AssertOwned(owner, dialog); Assert.Equal("方案切换", dialog.Title);
                Assert.False(model.PrepareSolutionTransitionAsync(owner: owner).GetAwaiter().GetResult());
            }, error => failure = error);
            Assert.False(model.PrepareSolutionTransitionAsync().GetAwaiter().GetResult());
            if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
            Assert.Equal(1, prompts); Assert.True(owner.IsVisible); Assert.True(model.CanAcceptExternalActivation);
        }
        finally { model.EndSolutionTransition(); owner.Close(); }
    });

    private static Window Host() => new() { Title = "S1 isolated owner test", Width = 360, Height = 160,
        ShowActivated = false, Left = -10000, Top = -10000 };
    private static void AssertOwned(Window owner, ThemeMessageDialog dialog)
    {
        Assert.Same(owner, dialog.Owner); Assert.True(dialog.IsVisible);
        Assert.Equal(new WindowInteropHelper(owner).Handle, new WindowInteropHelper(dialog).Owner);
        Assert.Equal(WindowStartupLocation.CenterOwner, dialog.WindowStartupLocation);
        Assert.False(dialog.ShowInTaskbar); Assert.False(dialog.Topmost);
    }
    private static void Answer(Window owner, string button, Action<ThemeMessageDialog> inspect, Action<Exception> failed)
    {
        owner.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
        {
            ThemeMessageDialog? dialog = null;
            try
            {
                dialog = owner.OwnedWindows.OfType<ThemeMessageDialog>().Single();
                inspect(dialog);
                ((Button)dialog.FindName(button)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }
            catch (Exception error) { failed(error); dialog?.Close(); }
        }));
    }
    private static readonly Lazy<Dispatcher> TestDispatcher = new(() =>
    {
        var ready = new TaskCompletionSource<Dispatcher>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                app.Resources.MergedDictionaries.Add(new ResourceDictionary
                {
                    Source = new Uri("/SunEyeVision.UI;component/Themes/Dark/DarkTheme.xaml", UriKind.Relative)
                });
                ready.SetResult(Dispatcher.CurrentDispatcher);
                Dispatcher.Run();
            }
            catch (Exception error) { ready.TrySetException(error); }
        }) { IsBackground = true, Name = "Owned dialog test dispatcher" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return ready.Task.WaitAsync(TimeSpan.FromSeconds(20)).GetAwaiter().GetResult();
    });

    private static void Sta(Action action)
    {
        TestDispatcher.Value.InvokeAsync(action).Task.WaitAsync(TimeSpan.FromSeconds(20)).GetAwaiter().GetResult();
    }
}