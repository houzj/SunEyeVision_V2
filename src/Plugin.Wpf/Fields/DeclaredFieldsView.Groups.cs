using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Adapters.Fields;
using SunEyeVision.Plugin.Wpf.Adapters.References;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using SunEyeVision.Plugin.Wpf.Presenters.Numeric;
using SunEyeVision.Plugin.Wpf.Adapters.Text;
using System.Globalization;

namespace SunEyeVision.Plugin.Wpf.Fields;

public sealed partial class DeclaredFieldsView
{
    private Action<IReadOnlyDictionary<string,ParameterSetting>> _writeGroup=null!;
    private Func<FieldGroupCandidate,string?>? _validateComposition;
    private Func<bool>? _isCurrent;
    private ReferenceFieldOptions? _groupReferences;
    private IReadOnlyDictionary<string,NativeIntegerRadioOptions> _integerRadios=new Dictionary<string,NativeIntegerRadioOptions>();
    private readonly HashSet<string> _pairedFields=new(StringComparer.Ordinal);
    private IReadOnlyDictionary<string,FieldPresentation> _groupPresentations=new Dictionary<string,FieldPresentation>();
    private bool _submitting;
    private IReadOnlyDictionary<string,TextExpressionOptions> _textExpressions=new Dictionary<string,TextExpressionOptions>();
    private bool CurrentGroup()
    {
        if(_disposed)return false;
        var current=_isCurrent?.Invoke()!=false;
        return current&&!_disposed;
    }

    /// <summary>Atomically propose existing fields without capturing unrelated pending text.
    /// Paired numeric endpoints remain owned by their original range composition and binding validation.</summary>
    public bool TrySetFields(IReadOnlyDictionary<string,ParameterSetting> proposals)
        =>SubmitFields(proposals,true);
    public IReadOnlyDictionary<string,AcceptedFieldValue> CaptureAccepted()
    {
        Dispatcher.VerifyAccess();ObjectDisposedException.ThrowIf(_disposed,this);
        return new System.Collections.ObjectModel.ReadOnlyDictionary<string,AcceptedFieldValue>(
            _fieldStates.ToDictionary(state=>state.Port.Id,state=>state.CaptureAccepted(),StringComparer.Ordinal));
    }
    /// <summary>Install an owner's complete prepared history state without invoking the ordinary draft writer.
    /// The owner callback must only compare/install its root and history cursor; it must not publish notifications.</summary>
    public bool TryReplayAccepted(IReadOnlyDictionary<string,AcceptedFieldValue> values,Func<bool> installOwner,Func<bool> isCurrent,out Action publish)
    {
        ArgumentNullException.ThrowIfNull(values);ArgumentNullException.ThrowIfNull(installOwner);ArgumentNullException.ThrowIfNull(isCurrent);
        publish=()=>{};Dispatcher.VerifyAccess();if(_submitting||_disposed)return false;
        _submitting=true;var installed=false;
        try
        {
            if(values.Count!=_fieldStates.Count||_fieldStates.Any(state=>!values.ContainsKey(state.Port.Id)))
                throw new ArgumentException("历史回放必须包含完整且唯一的字段集。");
            bool Current()=>CurrentGroup()&&isCurrent()&&!_disposed;
            foreach(var range in _ranges)range.ValidateReplay(values);
            ValidatePrepared(values.Where(entry=>entry.Value.ConstantBackup is not null).ToDictionary(entry=>entry.Key,
                entry=>(ParameterSetting)entry.Value.ConstantBackup!,StringComparer.Ordinal),false,false);
            installed=DeclaredFieldState.ReplayAccepted(_fieldStates.Select(state=>(state,values[state.Port.Id])).ToArray(),
                changes=>ValidatePrepared(changes,false,false),Current,installOwner,out var notify);
            if(installed)
            {
                var published=false;
                publish=()=>
                {
                    if(published)return;published=true;
                    try{SunEyeVision.Plugin.Wpf.Adapters.Regions.NativeRoiCallbacks.Run(_ranges.ToArray().Select(range=>(Action)range.ReplayAccepted).Append(notify));}
                    finally{_submitting=false;}
                };
            }
            return installed;
        }
        finally{if(!installed)_submitting=false;}
    }
    private bool ProposeSingle(string id,ParameterSetting proposal)
        =>SubmitFields(new Dictionary<string,ParameterSetting>{{id,proposal}},false);
    private bool SubmitFields(IReadOnlyDictionary<string,ParameterSetting> proposals,bool replacePending)
    {
        ArgumentNullException.ThrowIfNull(proposals);
        Dispatcher.VerifyAccess();
        if(_submitting||_disposed)return false;
        _submitting=true;
        try
        {
            if(!CurrentGroup())return false;
            var changes=proposals.ToArray();
            var states=_fieldStates.ToDictionary(x=>x.Port.Id,StringComparer.Ordinal);
            if(changes.Any(x=>!states.ContainsKey(x.Key)||x.Value is null))throw new ArgumentException("组合提案含未知字段或空设置。");
            if(changes.Any(x=>_pairedFields.Contains(x.Key)))throw new ArgumentException("范围端点必须通过原双端组合及绑定上下文联合编辑。");
            return SubmitCore(changes.Select(x=>(states[x.Key],x.Value)).ToArray(),true,replacePending);
        }
        finally{_submitting=false;}
    }
    private bool SubmitRange(IReadOnlyList<(DeclaredFieldState State,ParameterSetting Proposal)> proposals,Action installed)
    {
        return _submitting&&CurrentGroup()&&SubmitCore(proposals,false,false,installed);
    }
    private IDisposable? BeginRange()
    {
        Dispatcher.VerifyAccess();
        if(_submitting||_disposed)return null;
        _submitting=true;
        try
        {
            if(CurrentGroup())return new SubmissionScope(this);
            _submitting=false;return null;
        }
        catch{_submitting=false;throw;}
    }
    private sealed class SubmissionScope(DeclaredFieldsView owner):IDisposable
    {
        private DeclaredFieldsView? _owner=owner;
        public void Dispose(){var value=_owner;_owner=null;if(value is not null)value._submitting=false;}
    }
    private bool SubmitCore(IReadOnlyList<(DeclaredFieldState State,ParameterSetting Proposal)> proposals,bool validateReferences,bool replacePending,Action? installed=null)
        =>DeclaredFieldState.TrySetGroup(ExpandAssistProposals(proposals),_writeGroup,changes=>ValidatePrepared(changes,validateReferences,true),CurrentGroup,replacePending,installed);
    private void ValidatePrepared(IReadOnlyDictionary<string,ParameterSetting> changes,bool validateReferences,bool validateComposition)
        {
            foreach(var change in changes)
            {
                if(change.Value is ConstantSetting {Value:string text}&&_textExpressions.TryGetValue(change.Key,out var expression))
                {
                    if(!CurrentGroup())throw new InvalidOperationException("文本字段所属编辑范围已失效。");
                    var error=expression.ValidateText?.Invoke(text);
                    if(!CurrentGroup())throw new InvalidOperationException("文本验证期间编辑范围已改变。");
                    if(!string.IsNullOrEmpty(error))throw new ArgumentException(error);
                }
                var state=_fieldStates.Single(x=>x.Port.Id==change.Key);
                if(_inlineConstants.Contains(change.Key))InlineConstantRules.Validate(state.Port,_groupPresentations[change.Key],change.Value);
                if(change.Value is ConstantSetting number&&state.Port.Type.Shape==ValueShape.Scalar
                    &&state.Port.Type.Scalar is ScalarKind.Int64 or ScalarKind.Float64)
                {
                    var presentation=_groupPresentations[change.Key];
                    if(!_inlineConstants.Contains(change.Key)&&NativeNumberPresenter.UsesOriginalModes(state.Port,presentation))NativeNumericCommit.EnsureRepresentable(number);
                    var value=Convert.ToDouble(number.Value,CultureInfo.InvariantCulture);
                    if(presentation.Layout is {} layout&&(value<layout.Minimum||value>layout.Maximum))throw new ArgumentException("数值超出声明的编辑范围。");
                    if(!_inlineConstants.Contains(change.Key)&&number.Value is double real&&presentation.Layout?.DecimalPlaces is {} precision&&Math.Round(real,precision,MidpointRounding.ToEven)!=real)
                        throw new ArgumentException("组合提案超出声明的编辑精度，请提供规范值。");
                }
                if(_integerRadios.TryGetValue(change.Key,out var options)&&options.Validate(change.Value) is {} invalid)throw new ArgumentException(invalid);
                if(validateReferences&&_groupReferences?.ValidateProposal is {} validate)
                {
                    var error=validate(state.Port,DeclaredFieldState.CopySetting(change.Value));
                    if(!CurrentGroup())return;
                    if(!string.IsNullOrEmpty(error))throw new ArgumentException(error);
                }
            }
            if(validateComposition&&_validateComposition is {} validateGroup)
            {
                var current=_fieldStates.ToDictionary(x=>x.Port.Id,x=>x.Accepted,StringComparer.Ordinal);
                var error=validateGroup(new FieldGroupCandidate(current,changes));
                if(!CurrentGroup())return;
                if(!string.IsNullOrEmpty(error))throw new ArgumentException(error);
            }
        }
}
