using System.Windows;
using System.ComponentModel;
using SunEyeVision.UI.Services.Editing;
using Xunit;
namespace SunEyeVision.UI.Tests;
[Collection("VM canvas UI")]
public sealed class VmGroupWindowLifecycleTests
{
    private static void Sta(Action action)
    {
        Exception? failure=null;var thread=new Thread(()=>{try{action();}catch(Exception ex){failure=ex;}});
        thread.SetApartmentState(ApartmentState.STA);thread.Start();Assert.True(thread.Join(TimeSpan.FromSeconds(30)));if(failure!=null)throw failure;
    }
    [Fact] public void Registered_window_blocks_before_show_and_close_releases_exact_registration()=>Sta(()=>
    {
        var registry=new ParameterWindowRegistry();var changes=0;registry.Changed+=(_,_)=>changes++;
        var window=new Window();var registration=registry.Track(window);Assert.Equal(1,registry.Count);Assert.False(window.IsLoaded);
        Assert.Throws<InvalidOperationException>(()=>registry.Track(window));window.Close();Assert.Equal(0,registry.Count);registration.Dispose();Assert.Equal(2,changes);
    });
    [Fact] public void Canceled_close_retains_editor_scope_and_prevents_document_transition()=>Sta(()=>
    {
        var registry=new ParameterWindowRegistry();var first=new Window();var second=new Window();registry.Track(first);registry.Track(second);
        CancelEventHandler cancel=(_,e)=>e.Cancel=true;first.Closing+=cancel;
        Assert.False(registry.TryCloseAll());Assert.Equal(2,registry.Count);
        first.Closing-=cancel;Assert.True(registry.TryCloseAll());Assert.Equal(0,registry.Count);
    });
    [Fact] public void Failed_show_cleanup_releases_scope_without_closing_another_window()=>Sta(()=>
    {
        var registry=new ParameterWindowRegistry();var first=new Window();var second=new Window();var failed=registry.Track(first);registry.Track(second);
        failed.Dispose();failed.Dispose();Assert.Equal(1,registry.Count);first.Close();Assert.Equal(1,registry.Count);second.Close();Assert.Equal(0,registry.Count);
    });
}