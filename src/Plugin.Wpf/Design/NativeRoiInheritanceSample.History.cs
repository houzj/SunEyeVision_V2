using System.Collections.ObjectModel;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Adapters.Regions;
using SunEyeVision.Plugin.Wpf.Fields;
using SunEyeVision.Plugin.Wpf.Regions;

namespace SunEyeVision.Plugin.Wpf.Design;

public sealed partial class NativeRoiInheritanceSample
{
    private sealed record HistoryState(IReadOnlyDictionary<string,AcceptedFieldValue> Fields,IReadOnlyList<RoiEditItem> LocalItems,
        RoiEditTarget? Selection,RoiEditTarget? LastLocalSelection);
    private readonly Stack<HistoryState> _undo=new(),_redo=new();
    private bool _restoring;
    private HistoryState CaptureHistory()=>new(Fields.CaptureAccepted(),Array.AsReadOnly(_root.LocalItems.ToArray()),
        Session.CurROIData is {} selected?new(selected.Group,selected.Id):null,_lastLocalSelection);
    private RoiEditAcceptance RestoreHistory(RoiEditSnapshot current,bool redo)
    {
        var source=redo?_redo:_undo;var destination=redo?_undo:_redo;
        if(source.Count==0)return RoiEditAcceptance.Reject(current,"没有可恢复历史。");
        var expected=_root;var state=source.Peek();_restoring=true;
        try
        {
            var fields=Freeze(state.Fields.Select(entry=>new KeyValuePair<string,ParameterSetting>(entry.Key,entry.Value.Setting)));
            RoiEditItem? detection=null;var detectionError="";Func<bool>? proof=null;
            try
            {
                // Resolve afresh even for equal definitions: no old run/epoch receipt is part of history.
                if(fields["area.mode"] is ConstantSetting{Value:0L})
                {
                    if(fields["detect.mode"] is ConstantSetting{Value:1L})TryDetect(fields,out detection,out detectionError,out proof);
                    if(fields["mask.mode"] is ConstantSetting{Value:1L}&&fields["mask.source"] is ReferenceSetting mask)Resolve("mask",mask);
                }
                else if(fields["mask.image"] is ReferenceSetting image)ValidateMaskImage(image);
            }
            catch(Exception error)when(error is ArgumentException or InvalidOperationException or OverflowException)
            {return RoiEditAcceptance.Reject(Read(),error.Message);}
            bool Current()=>!_disposed&&ReferenceEquals(expected,_root)&&ComponentsCurrent()&&proof?.Invoke()!=false&&!_disposed&&ReferenceEquals(expected,_root);
            if(!Current())return RoiEditAcceptance.Reject(Read(),"当前来源或编辑根已改变，不能恢复旧历史。");
            var before=CaptureHistory();
            var restored=new Root(expected.Revision+1,fields,state.LocalItems,detection,detectionError,proof);
            Action publish;
            bool installed;
            try
            {
                installed=Fields.TryReplayAccepted(state.Fields,()=>
                {
                    // No callbacks or source reads here. The final preflight already froze all candidates.
                    if(_disposed||!ReferenceEquals(expected,_root)||source.Count==0||!ReferenceEquals(source.Peek(),state))return false;
                    _root=restored;source.Pop();destination.Push(before);_lastLocalSelection=state.LastLocalSelection;return true;
                },Current,out publish);
            }
            catch(Exception error)when(error is ArgumentException or InvalidOperationException or OverflowException)
            {return RoiEditAcceptance.Reject(Read(),error.Message);}
            if(!installed)return RoiEditAcceptance.Reject(Read(),"历史回放准备已失效。");
            var snapshot=Read();var target=state.Selection;
            RoiEditSelection selection=target is not null&&snapshot.Items.Any(row=>row.Group==target.Group&&row.Id==target.Id&&row.Enabled&&row.Visible)
                ?new RoiEditSelection.Select(target):new RoiEditSelection.Clear();
            return new(true,snapshot,RoiEditAcceptance.ChangedTargets(current,snapshot),selection,Publish:publish);
        }
        finally{_restoring=false;}
    }
}
