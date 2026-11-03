using SunEyeVision.Application.Communications;
using SunEyeVision.Domain.Solutions;
using Xunit;

public sealed class CommunicationStateTests
{
    [Fact] public void Device_state_rejects_old_or_different_definition_and_uses_one_snapshot()
    {
        var device=new Communication { Id="1", IsEnabled=true, ProtocolKey="tcp-client" };
        var other=new Communication { Id="1" };
        using var state=new CommunicationRuntimeState(device, action => action());
        var update=new CommunicationStateSnapshot(CommunicationRuntimeStatus.Connected,true,false,true,"连接","错误");
        Assert.False(state.TryApply(other,update));
        Assert.True(state.TryApply(device,update));
        Assert.True(state.IsActuallyConnected); Assert.True(state.IsConnectionEnabled);
        Assert.True(state.HasRuntimeConnectionError); Assert.False(state.CanToggleRuntime);
        state.Dispose(); Assert.False(state.TryApply(device,update with { IsRuntimeOperationInProgress=false }));
    }
    [Fact] public void Background_definition_change_posts_and_late_notification_is_rejected()
    {
        var device=new Communication { IsEnabled=true };
        var pending=new System.Collections.Concurrent.ConcurrentQueue<Action>();
        using var state=new CommunicationRuntimeState(device,pending.Enqueue);
        var changes=0;state.PropertyChanged += (_,_)=>changes++;
        Task.Run(()=>device.IsEnabled=false).GetAwaiter().GetResult();
        Assert.Equal(0,changes);
        Assert.True(pending.TryDequeue(out var posted)); posted!();
        Assert.True(changes>0);
        Task.Run(()=>device.IsEnabled=true).GetAwaiter().GetResult();
        state.Dispose();var before=changes;
        while(pending.TryDequeue(out posted))posted();
        Assert.Equal(before,changes);
    }
    [Fact] public void Point_updates_are_explicit_bounded_and_reject_retired_instance()
    {
        var point=new ModbusAddressPoint(); var other=new ModbusAddressPoint { Id=point.Id };
        using var state=new ModbusPointRuntimeState(point);
        Assert.False(state.TryApply(other,new(Value:"wrong")));
        Assert.True(state.TryApply(point,new(Value:"1",HasReceivedData:true,AppendRecord:"one",HistoryLimit:1)));
        Assert.True(state.TryApply(point,new(Value:"",HasReceivedData:false,AppendRecord:"two",HistoryLimit:1)));
        Assert.Equal("",state.CurrentValue);Assert.False(state.HasReceivedData);Assert.Equal(new[]{"two"},state.ReceiveRecords);
        Assert.True(state.TryApply(point,new(ClearRecords:true))); Assert.Empty(state.ReceiveRecords);
        state.Dispose();Assert.False(state.TryApply(point,new(Value:"late")));
    }
    [Fact] public async Task State_rejects_cross_thread_writes()
    {
        var point=new ModbusAddressPoint(); var state=new ModbusPointRuntimeState(point);
        var error=await Task.Run(()=>Record.Exception(()=>state.TryApply(point,new(Value:"late"))));
        Assert.IsType<InvalidOperationException>(error);
    }
}

