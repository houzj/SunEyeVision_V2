using SunEyeVision.Plugin.Contracts;
using Xunit;
namespace SunEyeVision.WorkflowGraph.Tests;
public sealed class RunConfigurationTests
{
 [Fact] public void Empty_real_session_runs_without_losing_actual_root_identity()
 {
  using var f=new SessionGraphFixture();using var batch=f.Session.CaptureRunBatch(new[]{f.Root.Id});using var result=batch.RunAsync(_=>{}).GetAwaiter().GetResult();
  Assert.True(result.Workflows[f.Root.Id].Success);Assert.Same(f.Root,f.Session.GetWorkflows().Single());
 }
 [Fact] public void Unknown_root_is_rejected_before_execution()
 {
  using var f=new SessionGraphFixture();Assert.ThrowsAny<Exception>(()=>f.Session.CaptureRunBatch(new[]{"foreign-root"}));Assert.Same(f.Session,f.Manager.CurrentSession);
 }
}
