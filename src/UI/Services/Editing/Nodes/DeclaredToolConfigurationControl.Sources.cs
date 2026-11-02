using SunEyeVision.Plugin.Editing.Wpf;
using System;
using System.Collections.Generic;
using System.Linq;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.Plugin.SDK.Vision.Contracts;
using SunEyeVision.Plugin.SDK.UI.Controls;
using SunEyeVision.Plugin.Wpf.Fields;
using SunEyeVision.Workflow;

namespace SunEyeVision.UI.Services.Editing;

public sealed partial class DeclaredToolConfigurationControl
{
    private readonly List<SourceSelectionField> _sourceFields=new();
    private readonly Dictionary<SourceSelectionField,Func<bool>> _sourceActivity=new();
    private enum SourceKind { Image,Pose,Geometry,Region }
    private sealed record SdkSource(string Identity,AvailableDataSource Source,bool Many);
    private IEnumerable<SourceSelectionField> ActiveSourceFields=>_sourceFields.Where(view=>!_sourceActivity.TryGetValue(view,out var active)||active());
    private bool HasPendingSources=>ActiveSourceFields.Any(view=>view.HasPendingEdits);
    private void RegisterSource(SourceSelectionField view,Func<bool> active){_sourceFields.Add(view);_sourceActivity.Add(view,active);}

    // Current SDK integration only: image/pose reference storage stays owned by the model.
    // In particular, a CLR AffineMatrix is not advertised as a canonical affine value with invented frame names.
    private void BuildSourceEditors(IConfigurationFieldProvider provider)
    {
        _sourceFields.Clear();_sourceActivity.Clear();
        if(_parameters is not MeasureGuideParameters||_fields is null||provider.ConfigurationSchema is not { } schema)return;
        var generation=_configurationGeneration;
        MeasureGuideParameters Owner()=>_parameters as MeasureGuideParameters??throw new InvalidOperationException("当前图像参数已失效。");
        var image=schema.Inputs.SingleOrDefault(port=>port.Type==TypeRef.ResourceOf(ResourceKind.Image));
        var pose=schema.Inputs.SingleOrDefault(port=>port.Type==SpatialDeclarations.Affine2D);
        var leading=0;
        if(image is not null)Add(image,"输入源",false);
        if(pose is not null)Add(pose,"修正来源",true);
        BuildRegionSourceEditors(provider);
        void Add(Port port,string title,bool correction)
        {
            void EnsureCurrent(){if(generation!=_configurationGeneration||!RegionScopeCurrent())throw new InvalidOperationException("当前来源编辑范围已失效。");}
            string Read()
            {
                EnsureCurrent();var guide=Owner();
                var stored=correction?guide.PositionCorrectionSource:guide.ImageSourceRef;
                return stored is null?"尚未选择来源":$"{stored.SourceNodeName} · {stored.DisplayName} ({stored.PropertyName})";
            }
            var view=new SourceSelectionField(title,()=>{EnsureCurrent();var choices=SdkSources(correction).Select(source=>new SourceChoice(source.Identity,
                source.Source.DropdownDisplayText,source.Many)).ToArray();EnsureCurrent();return choices;},Read,choice=>ApplySdkSource(correction,choice,generation)){Tag=port.Id};
            view.ToolTip=correction?"位置修正来源；集合必须明确选择序号。":"输入图像来源，仅列出当前作用域的声明输出。";
            RegisterSource(view,()=>generation==_configurationGeneration&&(!correction||Owner().IsPositionCorrectionEnabled));
            if(correction&&_accessors.SingleOrDefault(field=>field.StoragePath==nameof(MeasureGuideParameters.IsPositionCorrectionEnabled)) is {} enabled)
                _fields.AddAfterField(enabled.Port.Id,view,new(enabled.Port.Id,true));
            else _fields.AddToGroup("基本参数",view,leading++);
        }
    }
    private IReadOnlyList<SdkSource> SdkSources(bool correction)=>SdkSources(correction?SourceKind.Pose:SourceKind.Image);
    private IReadOnlyList<SdkSource> SdkSources(SourceKind kind)
    {
        var sources=new List<SdkSource>();
        if(DataProvider is not DataSourceQueryService query||CurrentNode is not WorkflowNodeBase node||query.FormalOutputContractReader is null)return sources;
        var names=query.GetAvailableDataSources(node.Id,null,true).GroupBy(source=>source.SourceNodeId)
            .ToDictionary(group=>group.Key,group=>group.First().SourceNodeName);
        foreach(var id in query.BindingCandidateNodeIds?.Invoke(node.Id)??names.Keys.ToArray())
        {
            if(id==node.Id||id.StartsWith("__",StringComparison.Ordinal))continue;
            foreach(var output in query.FormalOutputContractReader(id)??Array.Empty<OutputVariable>())
            {
                if(output.Kind!=OutputVariableKind.Data)continue;
                var type=output.DeclaredType??SdkTypeVocabulary.ToSystemType(output.VariableType);
                if(type is null)continue;
                var many=output.IsCollection??type.IsArray;var element=type.IsArray?type.GetElementType()!:type;
                var accepted=kind switch {
                    SourceKind.Image=>!many&&typeof(IVisionImage).IsAssignableFrom(element),
                    SourceKind.Pose=>element==typeof(AffineMatrix),
                    SourceKind.Geometry=>element==typeof(RoiShape)||RoiGeometryValueAdapter.Declaration(element) is not null,
                    SourceKind.Region=>!many&&element==typeof(IReadOnlyRegion),_=>false};
                if(!accepted)continue;
                var identity=id+"/"+BindingClrAdapter.OutputPortId(output);
                sources.Add(new(identity,new AvailableDataSource{SourceNodeId=id,SourceWorkflowId=query.WorkflowId,
                    SourceFieldId=output.FieldId,SourceNodeName=names.GetValueOrDefault(id)??id,PropertyName=output.Name,
                    DisplayName=string.IsNullOrEmpty(output.DisplayName)?output.Name:output.DisplayName,PropertyType=type,
                    VariableType=output.VariableType,IsArraySemantics=many},kind==SourceKind.Pose&&many));
            }
        }
        return sources;
    }
    private string? ApplySdkSource(bool correction,SourceChoiceSelection? choice,long generation)
    {
        if(generation!=_configurationGeneration||_parameters is not MeasureGuideParameters||!RegionScopeCurrent())return "当前编辑范围已失效。";
        var original=_parameters;var node=CurrentNode;var context=ToolEditorCommit.GetDraftContext(this);
        try
        {
            AvailableDataSource? proposed=null;
            if(choice is not null)
            {
                var source=SdkSources(correction).SingleOrDefault(source=>source.Identity==choice.Identity);
                if(source is null)return "来源已不在当前声明与作用域中，请重新选择。";
                if(source.Many!=choice.Index.HasValue||choice.Index is <0)return "集合需明确序号，标量不能指定序号。";
                proposed=source.Source;
                if(choice.Index is int index)proposed.BindingSuffix="["+index.ToString(System.Globalization.CultureInfo.InvariantCulture)+"]";
            }
            if(generation!=_configurationGeneration||!ReferenceEquals(_parameters,original)||!ReferenceEquals(CurrentNode,node)||
                !ReferenceEquals(ToolEditorCommit.GetDraftContext(this),context)||!RegionScopeCurrent())return "读取来源时编辑范围已改变。";
            AcceptParameterCandidate(generation,(candidate,_)=>
            {
                var guide=(MeasureGuideParameters)candidate;
                // Both slots belong to the candidate; changing an array index must persist the exact reference.
                if(correction){guide.PositionCorrectionBindingSource=null;guide.PositionCorrectionBindingSource=proposed;
                    guide.PositionCorrectionSource=ImageSourceReference.FromDataSource(proposed);}
                else{guide.ImageSource=null;guide.ImageSource=proposed;guide.ImageSourceRef=ImageSourceReference.FromDataSource(proposed);}
                var key=correction?nameof(MeasureGuideParameters.PositionCorrectionBindingSource):nameof(MeasureGuideParameters.ImageSource);
                guide.RebindingRequired.Remove(key);guide.BindingValidationPending.Remove(key);
            });
        }
        catch(Exception error) when(error is ArgumentException or InvalidOperationException){return error.Message;}
        OnAcceptedFieldNotification();return null;
    }
}
