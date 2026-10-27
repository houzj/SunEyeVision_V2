using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections.Immutable;
namespace SunEyeVision.Workflow;
public sealed record RootParameterProxy(Guid Id,string NodeId,string ToolType,string FieldId,string StoragePath,string DisplayName);
/// <summary>根运行参数曝光目录。值与订阅仍属于其正式节点，不复制成另一份运行参数。</summary>
public sealed record RootWorkflowConfiguration
{
 public ImmutableArray<RootParameterProxy> RunParameters {get;init;}=ImmutableArray<RootParameterProxy>.Empty;
 public void Validate()
 {
  if(RunParameters.IsDefault)throw new ArgumentException("根运行参数目录不能为空对象。");
  if(RunParameters.Any(p=>p==null||p.Id==Guid.Empty||string.IsNullOrWhiteSpace(p.NodeId)||string.IsNullOrWhiteSpace(p.ToolType)||string.IsNullOrWhiteSpace(p.FieldId)||string.IsNullOrWhiteSpace(p.StoragePath)||string.IsNullOrWhiteSpace(p.DisplayName)||p.DisplayName.Length>128))throw new ArgumentException("根运行参数身份、来源及名称必须完整。");
  if(RunParameters.Select(p=>p.Id).Distinct().Count()!=RunParameters.Length||RunParameters.Select(p=>(p.NodeId,p.FieldId)).Distinct().Count()!=RunParameters.Length)throw new ArgumentException("根运行参数身份或曝光来源不能重复。");
 }
 public RootWorkflowConfiguration Remap(IReadOnlyDictionary<string,string> nodeIds)
  =>this with{RunParameters=RunParameters.Select(p=>p with{Id=Guid.NewGuid(),NodeId=nodeIds.GetValueOrDefault(p.NodeId)??p.NodeId}).ToImmutableArray()};
}
