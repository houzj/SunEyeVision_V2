using SunEyeVision.UI.Shared;
using Xunit;

namespace SunEyeVision.UI.Tests;

public sealed class ImageSourceAcceptanceTests
{
    [Fact]
    public void Rejected_add_does_not_change_entries_selection_or_edit_version()
    {
        var source=new ImageInputSource("source");
        var first=new ImageInfo{FilePath="a"};source.AddImages(new[]{first},selectFirstAdded:true);
        var version=source.EditVersion;
        source.ConfigurationWriter=candidate=>
        {
            Assert.Single(source.Images);Assert.Same(first,source.CurrentImage);
            Assert.Equal(new[]{"a","b"},candidate.Configuration.FilePaths);
            throw new InvalidOperationException("reject");
        };
        Assert.Throws<InvalidOperationException>(()=>source.AddImages(new[]{new ImageInfo{FilePath="b"}},selectFirstAdded:true));
        Assert.Single(source.Images);Assert.Same(first,source.CurrentImage);Assert.Equal(version,source.EditVersion);
    }
    [Fact]
    public void Run_selection_is_accepted_before_the_live_item_changes()
    {
        var source=new ImageInputSource("source");var image=new ImageInfo{FilePath="a"};source.AddImage(image);
        source.ConfigurationWriter=_=>{Assert.False(image.IsForRun);throw new InvalidOperationException("reject");};
        Assert.Throws<InvalidOperationException>(()=>image.IsForRun=true);
        Assert.False(image.IsForRun);Assert.Empty(source.ToConfig().ForRunPaths);
        source.ConfigurationWriter=candidate=>Assert.Equal(new[]{"a"},candidate.Configuration.ForRunPaths);
        image.IsForRun=true;Assert.True(image.IsForRun);
    }
    [Fact]
    public void Accepted_notification_failure_does_not_roll_back_or_repeat_the_writer()
    {
        var source=new ImageInputSource("source");var calls=0;
        source.ConfigurationWriter=_=>calls++;
        source.CollectionChanged+=(_,_)=>throw new InvalidOperationException("observer");
        source.AddImages(new[]{new ImageInfo{FilePath="a"}},selectFirstAdded:true);
        Assert.Equal(1,calls);Assert.Single(source.Images);Assert.Equal(0,source.CurrentIndex);
        Assert.Equal("observer",source.LastNotificationError);
    }
    [Fact]
    public void Removed_entries_cannot_submit_to_the_previous_owner()
    {
        var source=new ImageInputSource("source");var image=new ImageInfo{FilePath="a"};source.AddImage(image);
        var calls=0;source.ConfigurationWriter=_=>calls++;
        source.RemoveImage(image);var accepted=calls;
        image.IsForRun=true;
        Assert.Equal(accepted,calls);Assert.Empty(source.Images);Assert.Empty(source.ToConfig().ForRunPaths);
    }
    [Fact]
    public void Runtime_cursor_never_becomes_persisted_selection_through_a_binding_echo()
    {
        var source=new ImageInputSource("source");var a=new ImageInfo{FilePath="a"};var b=new ImageInfo{FilePath="b"};
        source.AddImages(new[]{a,b},selectFirstAdded:true);var version=source.EditVersion;
        var calls=0;source.ConfigurationWriter=_=>calls++;
        Assert.True(source.TryPresentRuntimeCursor(version,b.Id,"b","b"));
        source.CurrentIndex=1; // Two-way display echo, not a new user choice.
        Assert.Equal(0,calls);Assert.Equal(0,source.ToConfig().CurrentIndex);Assert.Equal(version,source.EditVersion);
        source.CurrentIndex=0;Assert.Equal(1,calls);
    }
    [Fact]
    public void Scalar_edits_preserve_entry_enumeration_and_never_persist_the_runtime_cursor()
    {
        var source=new ImageInputSource("source");var a=new ImageInfo{FilePath="a"};var b=new ImageInfo{FilePath="b"};
        source.AddImages(new[]{a,b},selectFirstAdded:true);
        var resets=0;source.CollectionChanged+=(_,_)=>resets++;
        var accepted=new List<int>();source.ConfigurationWriter=config=>accepted.Add(config.Configuration.CurrentIndex);
        Assert.True(source.TryPresentRuntimeCursor(source.EditVersion,b.Id,"b","b"));
        foreach(var image in source.Images)image.IsForRun=true;
        Assert.Equal(0,resets);Assert.All(source.Images,image=>Assert.True(image.IsForRun));
        Assert.True(source.TryPresentRuntimeCursor(source.EditVersion,b.Id,"b","b"));
        source.RunMode=ImageRunMode.运行选择;
        Assert.True(source.TryPresentRuntimeCursor(source.EditVersion,b.Id,"b","b"));
        source.AutoSwitchEnabled=false;
        Assert.Equal(4,accepted.Count);Assert.All(accepted,index=>Assert.Equal(0,index));
        Assert.Equal(1,source.CurrentIndex);
        Assert.Equal(0,source.ToConfig().CurrentIndex);Assert.Equal(0,resets);
        source.AddImage(new ImageInfo{FilePath="c"});Assert.Equal(1,resets);
    }

    [Fact]
    public void Acceptance_callback_does_not_own_the_projected_candidate_collections()
    {
        var source=new ImageInputSource("source");
        source.ConfigurationWriter=candidate=>candidate.Configuration.FilePaths.Clear();
        source.AddImage(new ImageInfo{FilePath="a"});
        Assert.Equal(new[]{"a"},source.ToConfig().FilePaths);
    }
}
