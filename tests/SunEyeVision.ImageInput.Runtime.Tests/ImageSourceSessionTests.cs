using System.IO;
using NSubstitute;
using SunEyeVision.Application;
using SunEyeVision.Domain.Persistence;
using SunEyeVision.Domain.Workflows;

using SunEyeVision.Infrastructure.Persistence;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Infrastructure.ImageSources;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Plugin.Contracts.Acquisition;

using Xunit;
namespace SunEyeVision.ImageInput.Runtime.Tests;
[Collection("正式WPF owner")]
public sealed class ImageSourceSessionTests
{
 [Fact] public void Accepted_source_events_reset_counts_even_without_intermediate_runs_and_private_rebase_preserves_them()=>RuntimeHost.Run(()=>
 {
  var failure=false;var service=Substitute.For<ICameraFrameProvider>();
  service.CaptureAsync(Arg.Any<CameraFrameRequest>(),Arg.Any<CancellationToken>()).Returns(call=>failure
   ?ValueTask.FromException<ICameraFrameLease>(new CameraFrameAcquisitionException(CameraErrorCode.FrameTimeout,"取帧超时"))
   :new ValueTask<ICameraFrameLease>(new TrackedFrame(call.Arg<CameraFrameRequest>().DeviceId).Initialize(call.Arg<CameraFrameRequest>())));
  using var f=new RuntimeFixture(service);f.Camera();f.Edit(("sequence.initial",new ConstantSetting(1L)));
  void Check(DeclaredOutputReader reader,long count,long? sn)
  {
   Assert.Equal(count,reader.Values!.Get<long>("output.camera-failure-count"));
   Assert.Equal(1L,reader.Values.Get<long>("output.module-status"));
   if(sn is {} actual){Assert.True(reader.Snapshot.IsSuccess,reader.Snapshot.ErrorMessage);Assert.Equal(actual,reader.Values.Get<long>("output.sequence"));}
   else Assert.Equal(ExecutionStatus.Failed,reader.Snapshot.Status);
  }
  using(var result=f.Formal())Check(result,0,2);
  failure=true;using(var result=f.Formal())Check(result,1,null);using(var result=f.Formal())Check(result,2,null);
  f.Camera("other");f.Camera("camera");using(var result=f.Formal())Check(result,1,null);
  Assert.True(f.Session.Undo());Assert.True(f.Session.Redo());using(var result=f.Formal())Check(result,1,null);
  f.Edit(("output.mono8",new ConstantSetting(true)));f.Edit(("output.mono8",new ConstantSetting(false)));
  using(var result=f.Formal())Check(result,2,null);
  f.Edit(("sequence.initial",new ConstantSetting(9L)));f.Edit(("sequence.initial",new ConstantSetting(1L)));
  failure=false;using(var result=f.Formal())Check(result,2,2);
  var scope=f.Scope();var draft=f.Draft();
  void Debug(long count,long? sn){using var call=f.Capture(scope,draft);using var result=f.Execute(call);using var reader=result.AcquireReader();Check(reader,count,sn);}
  void Edit(string id,object value)=>draft.ApplyFields(new Dictionary<string,ParameterSetting>{{id,new ConstantSetting(value)}});
  Debug(0,2);failure=true;Debug(1,null);
  Edit("source.kind","local");Edit("source.kind","camera");Debug(1,null);
  Edit("output.mono8",true);Edit("output.mono8",false);Debug(2,null);
  Edit("sequence.initial",9L);Edit("sequence.initial",1L);failure=false;Debug(2,2);
  var proposal=SunEyeVision.Application.Editing.DocumentEditProposal.Prepare(f.Session.Editing.Host,nodes:new[]{draft});
  f.Session.CommitEdit(proposal);draft=f.Session.Editing.Host.RebaseAcceptedDraft(draft,proposal);
  Debug(2,3);
  Edit("sequence.initial",1L);Debug(2,4);
  using(var result=f.Formal())Check(result,2,3);
 });

 [Fact] public void Classified_capture_failures_publish_counts_without_sn_or_images_and_keep_private_owners_separate()=>RuntimeHost.Run(()=>
 {
  var service=Substitute.For<ICameraFrameProvider>();var failure=0;var frames=new List<TrackedFrame>();
  service.CaptureAsync(Arg.Any<CameraFrameRequest>(),Arg.Any<CancellationToken>()).Returns(call=>
  {
   if(failure==1)return ValueTask.FromException<ICameraFrameLease>(new CameraFrameAcquisitionException(CameraErrorCode.FrameTimeout,"取帧超时"));
   if(failure==2)return ValueTask.FromException<ICameraFrameLease>(new IOException("未分类服务错误"));
   var frame=new TrackedFrame(call.Arg<CameraFrameRequest>().DeviceId);frames.Add(frame);
   return new ValueTask<ICameraFrameLease>(frame.Initialize(call.Arg<CameraFrameRequest>()));
  });
  using var f=new RuntimeFixture(service);f.Camera();f.Edit(("sequence.initial",new ConstantSetting(1L)));
  void Check(DeclaredOutputReader reader,long count,long? sn)
  {
   Assert.Equal(count,reader.Values!.Get<long>("output.camera-failure-count"));
   Assert.Equal(1L,reader.Values.Get<long>("output.module-status"));
   if(sn is {} sequence){Assert.True(reader.Snapshot.IsSuccess,reader.Snapshot.ErrorMessage);Assert.Equal(sequence,reader.Values.Get<long>("output.sequence"));}
   else
   {
    Assert.Equal(ExecutionStatus.Failed,reader.Snapshot.Status);Assert.True(reader.Values.Read("output.sequence").IsMissing);
    Assert.True(reader.Values.Read("output.device-frame-id").IsMissing);Assert.True(reader.Values.Read("output.image").IsMissing);
    Assert.Contains(reader.Snapshot.Diagnostics,issue=>issue.Code=="IMAGE_SOURCE_CAPTURE");
    Assert.All(reader.Snapshot.Diagnostics,issue=>Assert.Null(issue.NativeErrorCode));
   }
  }
  using(var result=f.Formal())Check(result,0,2);
  failure=1;using(var result=f.Formal())Check(result,1,null);using(var result=f.Formal())Check(result,2,null);
  failure=2;using(var result=f.Formal()){Assert.False(result.Snapshot.IsSuccess);Assert.Null(result.Values);Assert.All(result.Snapshot.Diagnostics,issue=>Assert.Null(issue.NativeErrorCode));}
  failure=0;using(var result=f.Formal())Check(result,2,3);
  var scope=f.Scope();failure=1;
  using(var call=f.Capture(scope))using(var result=f.Execute(call))using(var reader=result.AcquireReader())Check(reader,1,null);
  failure=0;using(var call=f.Capture(scope))using(var result=f.Execute(call))using(var reader=result.AcquireReader())Check(reader,1,2);
  f.Camera("other");failure=1;using(var result=f.Formal())Check(result,1,null);
  failure=0;using(var result=f.Formal())Check(result,1,4);
  f.Paths(f.Write("reset-mode.pgm",40));f.Edit(("source.kind",new ConstantSetting("local")));
  using(var result=f.Formal())Assert.True(result.Snapshot.IsSuccess,result.Snapshot.ErrorMessage);
  f.Camera("other");failure=1;using(var result=f.Formal())Check(result,1,null);
  Assert.All(frames,frame=>Assert.True(frame.IsDisposed));
 });

 [Fact] public void Source_sequence_survives_cursor_changes_skips_local_failures_and_isolates_debug_owners()=>RuntimeHost.Run(()=>
 {
  using var f=new RuntimeFixture();
  var path=f.Write("sequence.pgm",40);f.Paths(path);f.Edit(("sequence.initial",new ConstantSetting(10L)));
  long Run(){using var reader=f.Formal();Assert.True(reader.Snapshot.IsSuccess,reader.Snapshot.ErrorMessage);return reader.Values!.Get<long>("output.sequence");}
  Assert.Equal(11L,Run());
  f.Edit(("output.mono8",new ConstantSetting(true)));Assert.Equal(12L,Run());
  f.Edit(("local.auto-switch",new ConstantSetting(false)));Assert.Equal(13L,Run());
  File.Delete(path);using(var failed=f.Formal())Assert.False(failed.Snapshot.IsSuccess);
  f.Write("sequence.pgm",40);Assert.Equal(14L,Run());
  f.Edit(("stitch.enabled",new ConstantSetting(true)),("stitch.height",new ConstantSetting(2L)));
  using(var failed=f.Formal())Assert.False(failed.Snapshot.IsSuccess);
  f.Edit(("stitch.enabled",new ConstantSetting(false)));Assert.Equal(15L,Run());
  var first=f.Scope();
  long Debug(DocumentDebugSession scope){using var call=f.Capture(scope);using var result=f.Execute(call);Assert.True(result.IsSuccess,result.ErrorMessage);using var reader=result.AcquireReader();return reader.Values!.Get<long>("output.sequence");}
  Assert.Equal(11L,Debug(first));Assert.Equal(12L,Debug(first));Assert.Equal(11L,Debug(f.Scope()));
  f.Edit(("sequence.initial",new ConstantSetting(20L)));Assert.Equal(21L,Run());
  Assert.Equal(21L,Debug(first));
  using(var edit=f.Session.Editing.Host.BeginVariableEdit())
  {
   edit.Variables.Add(new SunEyeVision.Domain.Solutions.GlobalVariable("SN输入字符","go","String"));
   edit.Variables.Add(new SunEyeVision.Domain.Solutions.GlobalVariable("SN过滤字符","stop","String"));
   f.Session.CommitEdit(SunEyeVision.Application.Editing.DocumentEditProposal.Prepare(f.Session.Editing.Host,variables:edit));
  }
  var variables=f.Session.Document.GlobalVariables.ToArray();
  f.Edit(("trigger.filter-enabled",new ConstantSetting(true)),
   ("trigger.text",new VariableSetting(new VariableReference(f.Session.Document.Id,variables[0].Id,DataType.Text))),
   ("trigger.filter-text",new VariableSetting(new VariableReference(f.Session.Document.Id,variables[1].Id,DataType.Text))));
  using(var filtered=f.Formal())Assert.Equal(ExecutionStatus.NotExecuted,filtered.Snapshot.Status);
  f.Edit(("trigger.filter-enabled",new ConstantSetting(false)));Assert.Equal(22L,Run());
 });

 [Fact] public void Camera_sequence_skips_capture_failure_but_consumes_a_frame_before_stitch_failure()=>RuntimeHost.Run(()=>
 {
  var service=Substitute.For<ICameraFrameProvider>();var failCapture=false;var frames=new List<TrackedFrame>();
  service.CaptureAsync(Arg.Any<CameraFrameRequest>(),Arg.Any<CancellationToken>()).Returns(call=>
  {
   if(failCapture)return ValueTask.FromException<ICameraFrameLease>(new IOException("采集失败"));
   var frame=new TrackedFrame();frames.Add(frame);return new ValueTask<ICameraFrameLease>(frame.Initialize(call.Arg<CameraFrameRequest>()));
  });
  using var f=new RuntimeFixture(service);f.Camera();f.Edit(("sequence.initial",new ConstantSetting(5L)));
  long Run(){using var reader=f.Formal();Assert.True(reader.Snapshot.IsSuccess,reader.Snapshot.ErrorMessage);return reader.Values!.Get<long>("output.sequence");}
  Assert.Equal(6L,Run());failCapture=true;using(var failed=f.Formal())Assert.False(failed.Snapshot.IsSuccess);
  failCapture=false;Assert.Equal(7L,Run());
  f.Edit(("stitch.enabled",new ConstantSetting(true)),("stitch.height",new ConstantSetting(2L)));
  using(var failed=f.Formal())
  {
   Assert.False(failed.Snapshot.IsSuccess);
   Assert.Equal(8L,failed.Values!.Get<long>("output.sequence"));
   Assert.Equal(0L,failed.Values.Get<long>("output.module-status"));
   Assert.Equal(0xE0000502u,Assert.Single(failed.Snapshot.Diagnostics).NativeErrorCode);
   Assert.Equal("42",failed.Values.Get<string>("output.device-frame-id"));
   Assert.Equal("camera",failed.Values.Get<string>("output.device"));
   Assert.True(failed.Values.Read("output.image").IsMissing);
   Assert.True(failed.Values.Read("output.gray-image").IsMissing);
  }
  f.Edit(("stitch.enabled",new ConstantSetting(false)));Assert.Equal(9L,Run());
  Assert.All(frames,frame=>Assert.True(frame.IsDisposed));
 });

 [Fact] public void Character_filter_suppresses_formal_and_private_camera_reads_before_capture()=>RuntimeHost.Run(()=>
 {
  var camera = Substitute.For<ICameraFrameProvider>();
  using var f = new RuntimeFixture(camera);
  f.Camera();
  using (var variables = f.Session.Editing.Host.BeginVariableEdit())
  {
   variables.Variables.Add(new SunEyeVision.Domain.Solutions.GlobalVariable("输入字符", "go", "String"));
   variables.Variables.Add(new SunEyeVision.Domain.Solutions.GlobalVariable("触发字符", "stop", "String"));
   f.Session.CommitEdit(SunEyeVision.Application.Editing.DocumentEditProposal.Prepare(f.Session.Editing.Host, variables: variables));
  }
  var definitions = f.Session.Document.GlobalVariables.ToArray();
  VariableSetting Reference(int index) => new(new VariableReference(f.Session.Document.Id, definitions[index].Id, DataType.Text));
  f.Edit(("trigger.filter-enabled", new ConstantSetting(true)), ("trigger.text", Reference(0)), ("trigger.filter-text", Reference(1)));
  using (var formal = f.Formal())
  {
   Assert.Equal(ExecutionStatus.NotExecuted, formal.Snapshot.Status);
   Assert.Null(formal.Values);
   Assert.Empty(formal.Snapshot.Diagnostics);
  }
  using (var invocation = f.Capture(f.Scope()))
  using (var debug = f.Execute(invocation))
  {
   Assert.Equal(ExecutionStatus.NotExecuted, debug.Status);
   Assert.Null(debug.Output);
   Assert.Empty(debug.Diagnostics);
  }
  _ = camera.DidNotReceive().CaptureAsync(Arg.Any<CameraFrameRequest>(), Arg.Any<CancellationToken>());
 });

 [Fact] public void Retired_image_source_invocation_cannot_create_state_and_last_owner_releases_it_once()=>RuntimeHost.Run(()=>
 {
  using var f = new RuntimeFixture();
  f.Paths(f.Write("state.pgm", 40));
  var identityOwner = new ToolExecutionOwner();
  var identity = identityOwner.Begin("source", Guid.NewGuid(), "state-lifetime");
  try
  {
   var actual = f.Session.CaptureHostInputContext(f.Flow.Id, f.Node)!;
   var input = f.Factory.Create(actual)!;
   var draft = f.Draft();
   var fields = draft.Configuration.Fields.Select(draft.SettingsSchema.Inputs).Materialize(new Dictionary<string, object?>());
   var run = identity.ToInvocationIdentity(f.Node.Id);
   var request = new HostInputPreparationContext(actual.Declaration, fields, run, CancellationToken.None, new Gate(identity));
   var prepared = RuntimeHost.Wait(input.PrepareAsync(request).AsTask());
   var inner = Substitute.For<IToolExecutionContext>();
   inner.Identity.Returns(run);
   inner.Cancellation.Returns(CancellationToken.None);
   var state = Assert.IsAssignableFrom<IImageSourceProcessingContext>(prepared.BindContext(inner));
   var resource = new CountedResource();
   Assert.Same(resource, state.GetProcessingState(() => resource));
   identity.CompleteCallbacks();
   var created = false;
   Assert.Throws<InvalidOperationException>(() => state.GetProcessingState(() => { created = true; return resource; }));
   Assert.False(created);
   f.Factory.Dispose();
   Assert.Equal(0, resource.Disposals);
   RuntimeHost.Wait(prepared.DisposeAsync().AsTask());
   input.Dispose();
   Assert.Equal(1, resource.Disposals);
   input.Dispose();
   f.Factory.Dispose();
   Assert.Equal(1, resource.Disposals);
  }
  finally { identity.CompleteCallbacks(); identityOwner.Retire(); }
 });

 [Fact] public void Stitch_history_survives_parameter_changes_and_is_private_to_each_editor()=>RuntimeHost.Run(()=>
 {
  using var f = new RuntimeFixture();
  string Rows(string name, params byte[] rows)
  {
   var path = Path.Combine(f.DirectoryPath, name);
   var header = System.Text.Encoding.ASCII.GetBytes($"P5\n4 {rows.Length}\n255\n");
   File.WriteAllBytes(path, header.Concat(rows.SelectMany(value => Enumerable.Repeat(value, 4))).ToArray());
   return path;
  }
  var a = Rows("a.pgm", 10, 20, 30, 40);
  var b = Rows("b.pgm", 50, 60, 70, 80);
  f.Paths(a, b);
  f.Edit(("local.auto-switch", new ConstantSetting(false)), ("stitch.enabled", new ConstantSetting(true)),
   ("stitch.start", new ConstantSetting(1L)), ("stitch.height", new ConstantSetting(2L)));
  byte[] FirstColumn(IReadOnlyImage image)
  {
   var bytes = image.CopyPixels();
   return Enumerable.Range(0, image.Height).Select(row => bytes[row * image.Layout.RowStrideBytes]).ToArray();
  }
  using var first = f.Formal();
  Assert.True(first.Snapshot.IsSuccess, first.Snapshot.ErrorMessage);
  Assert.Equal(new byte[] { 0, 0, 10, 20, 30, 40 }, FirstColumn(RuntimeFixture.Image(first)));
  f.Factory.SelectIndex(f.Session.CaptureHostInputContext(f.Flow.Id, f.Node)!, 1);
  using (var second = f.Formal())
   Assert.Equal(new byte[] { 20, 30, 50, 60, 70, 80 }, FirstColumn(RuntimeFixture.Image(second)));
  Assert.Equal(new byte[] { 0, 0, 10, 20, 30, 40 }, FirstColumn(RuntimeFixture.Image(first)));
  using (var scope = f.Scope())
  using (var invocation = f.Capture(scope))
  using (var debug = f.Execute(invocation))
  using (var reader = debug.AcquireReader())
  {
   Assert.True(debug.IsSuccess, debug.ErrorMessage);
   var image = reader.Values!.Get<RecordValue>("output.image").Get<IReadOnlyImage>("data");
   Assert.Equal(new byte[] { 0, 0, 10, 20, 30, 40 }, FirstColumn(image));
  }
  f.Edit(("stitch.start", new ConstantSetting(0L)));
  using (var changed = f.Formal())
   Assert.Equal(new byte[] { 60, 70, 50, 60, 70, 80 }, FirstColumn(RuntimeFixture.Image(changed)));
  f.Edit(("stitch.enabled", new ConstantSetting(false)));
  using (var bypass = f.Formal())
   Assert.Equal(new byte[] { 50, 60, 70, 80 }, FirstColumn(RuntimeFixture.Image(bypass)));
  f.Edit(("stitch.enabled", new ConstantSetting(true)));
  using (var resumed = f.Formal())
   Assert.Equal(new byte[] { 50, 60, 50, 60, 70, 80 }, FirstColumn(RuntimeFixture.Image(resumed)));
  f.Edit(("stitch.height", new ConstantSetting(1L)));
  using (var resized = f.Formal())
   Assert.Equal(new byte[] { 0, 50, 60, 70, 80 }, FirstColumn(RuntimeFixture.Image(resized)));
  f.Edit(("stitch.start", new ConstantSetting(4L)));
  using (var invalid = f.Formal())
  {
   Assert.False(invalid.Snapshot.IsSuccess);
   Assert.Equal(0xE0000502u,Assert.Single(invalid.Snapshot.Diagnostics).NativeErrorCode);
   Assert.Equal(0L,invalid.Values!.Get<long>("output.module-status"));
   Assert.True(invalid.Values.Read("output.image").IsMissing);
  }
  f.Edit(("stitch.start", new ConstantSetting(0L)));
  using (var recovered = f.Formal())
   Assert.Equal(new byte[] { 50, 50, 60, 70, 80 }, FirstColumn(RuntimeFixture.Image(recovered)));
  f.Edit(("stitch.height", new ConstantSetting(0L)));
  using (var zero = f.Formal())
   Assert.Equal(new byte[] { 50, 60, 70, 80 }, FirstColumn(RuntimeFixture.Image(zero)));
  f.Edit(("stitch.height", new ConstantSetting(1L)));
  File.WriteAllBytes(b, System.Text.Encoding.ASCII.GetBytes("P5\n5 4\n255\n")
   .Concat(new byte[] { 50, 60, 70, 80 }.SelectMany(value => Enumerable.Repeat(value, 5))).ToArray());
  using (var invalidWidth = f.Formal())
  {
   Assert.False(invalidWidth.Snapshot.IsSuccess);
   Assert.Equal(0xE0000502u,Assert.Single(invalidWidth.Snapshot.Diagnostics).NativeErrorCode);
   Assert.Equal(0L,invalidWidth.Values!.Get<long>("output.module-status"));
   Assert.True(invalidWidth.Values.Read("output.image").IsMissing);
  }
  Rows("b.pgm", 50, 60, 70, 80);
  using (var restoredWidth = f.Formal())
   Assert.Equal(new byte[] { 50, 50, 60, 70, 80 }, FirstColumn(RuntimeFixture.Image(restoredWidth)));
  f.Factory.Dispose();
  Assert.Equal(new byte[] { 0, 0, 10, 20, 30, 40 }, FirstColumn(RuntimeFixture.Image(first)));
 });

 [Fact] public void Cancelled_read_identity_rejects_before_admission_with_cancellation()=>RuntimeHost.Run(()=>
 {
  using var f=new RuntimeFixture();using var cancellation=new CancellationTokenSource();var owner=new ToolExecutionOwner();var identity=owner.Begin("source",Guid.NewGuid(),"cancelled",cancellation.Token);bool admitted=false;
  using var input=new ImageSourceHostInputPreparer("unused",7){LocalReadAdmission=(_,_)=>{admitted=true;return "unused";}};var draft=f.Draft();
  var values=new InputValues(draft.SettingsSchema.Inputs,draft.SettingsSchema.Inputs.Where(port=>draft.Configuration.Fields.Read(port.Id) is ConstantSetting).ToDictionary(port=>port.Id,port=>((ConstantSetting)draft.Configuration.Fields.Read(port.Id)).Value));
  var context=new HostInputPreparationContext(f.Session.GetNodeHostInputDeclaration(f.Flow.Id,f.Node)!,values,identity.ToInvocationIdentity(f.Node.Id),cancellation.Token,new Gate(identity));
  cancellation.Cancel();Assert.ThrowsAny<OperationCanceledException>(()=>RuntimeHost.Wait(input.PrepareAsync(context).AsTask()));Assert.False(admitted);identity.CompleteCallbacks();owner.Retire();
 });
 [Fact] public void Local_debug_missing_frame_is_consumed_and_next_request_reads_the_following_frame()=>RuntimeHost.Run(()=>
 {
  using var f=new RuntimeFixture();var good=f.Write("good.pgm",20);f.Paths(good,Path.Combine(f.DirectoryPath,"missing.pgm"));f.Edit(("local.auto-switch",new ConstantSetting(true)));var scope=f.Scope();
  using(var fail=f.Capture(scope)){Assert.ThrowsAny<Exception>(()=>{using var result=f.Execute(fail);if(!result.IsSuccess)throw new InvalidOperationException(result.ErrorMessage);});}
  using var next=f.Capture(scope);using var result=f.Execute(next);Assert.True(result.IsSuccess,result.ErrorMessage);using var reader=result.AcquireReader();Assert.Equal(good,reader.Values!.Get<string>("output.file"));Assert.Null(f.Session.AcquirePublishedResult(f.Flow.Id,f.Node.Id));
 });
 [Fact] public void A_local_read_that_finishes_after_cancellation_disposes_its_new_resource()=>RuntimeHost.Run(()=>
 {
  using var f=new RuntimeFixture();
  using var cancellation=new CancellationTokenSource();
  var owner=new ToolExecutionOwner();
  var identity=owner.Begin("source",Guid.NewGuid(),"cancelled-read",cancellation.Token);
  try
  {
   using var input=new ImageSourceHostInputPreparer("source.pgm",7)
   {
    LocalReadAdmission=(_,_)=>"source.pgm"
   };
   var draft=f.Draft();
   var values=draft.Configuration.Fields.Select(draft.SettingsSchema.Inputs).Materialize(new Dictionary<string,object?>());
   var run=identity.ToInvocationIdentity(f.Node.Id);
   var context=new HostInputPreparationContext(f.Session.GetNodeHostInputDeclaration(f.Flow.Id,f.Node)!,values,
    run,cancellation.Token,new Gate(identity));
   var prepared=input.PrepareAsync(context).AsTask().GetAwaiter().GetResult();
   try
   {
    var inner=Substitute.For<IToolExecutionContext>();
    inner.Identity.Returns(run);
    inner.Cancellation.Returns(cancellation.Token);
    var bound=prepared.BindContext(inner);
    Assert.Throws<InvalidOperationException>(()=>prepared.BindContext(inner));
    var path=Assert.IsAssignableFrom<ILocalImageReadContext>(bound).AcceptLocalImage();
    var cache=Assert.IsAssignableFrom<ILocalImageCacheContext>(bound);
    var image=new CountedResource();
    Assert.ThrowsAny<OperationCanceledException>(()=>cache.OpenLocalImage(path,
     ()=>{cancellation.Cancel();return image;},_=>throw new InvalidOperationException("已取消的读取不能创建缓存副本。")));
    Assert.Equal(1,image.Disposals);
   }
   finally{prepared.DisposeAsync().AsTask().GetAwaiter().GetResult();}
  }
  finally{identity.CompleteCallbacks();owner.Retire();}
 });
 [Fact] public void Debug_owners_keep_independent_cursors_and_a_reopened_editor_starts_fresh()=>RuntimeHost.Run(()=>
 {
  using var f=new RuntimeFixture();var paths=new[]{f.Write("a.pgm",11),f.Write("b.pgm",22),f.Write("c.pgm",33)};f.Paths(paths);f.Edit(("local.auto-switch",new ConstantSetting(true)));var first=f.Scope();var second=f.Scope();
  string Run(DocumentDebugSession scope){using var call=f.Capture(scope);using var result=f.Execute(call);Assert.True(result.IsSuccess,result.ErrorMessage);using var reader=result.AcquireReader();return reader.Values!.Get<string>("output.file");}
  Assert.Equal(paths[1],Run(first));Assert.Equal(paths[1],Run(second));Assert.Equal(paths[2],Run(first));first.Dispose();Assert.Equal(paths[1],Run(f.Scope()));Assert.Equal(paths[2],Run(second));
 });
 [Fact] public void Selected_run_does_not_resolve_the_excluded_configured_start()
 {
  var paths=Substitute.For<IResourcePathResolver>();
  paths.Resolve("local.paths","selected",ResourcePurpose.FileRead).Returns(new ResourcePath("selected","resolved",true));
  var plan=ImageSourcePolicy.CreateLocalPlan("node",new ImageSourcePolicy
  {
   NodeId="node",FilePaths=new(){"excluded","selected"},CurrentIndex=0,RunMode=1,ForRunPaths=new(){"selected"}
  },paths);
  Assert.Null(plan.SelectedEntryId);
  var entry=Assert.Single(plan.Entries);
  Assert.Equal(1,entry.OriginalIndex);
  Assert.Equal("resolved",entry.Address);
  paths.Received(1).Resolve("local.paths","selected",ResourcePurpose.FileRead);
  paths.DidNotReceive().Resolve(Arg.Any<string>(),"excluded",ResourcePurpose.FileRead);
 }
 [Fact] public void Local_path_cache_is_bounded_and_isolated_from_a_new_debug_scope()=>RuntimeHost.Run(()=>
 {
  using var f=new RuntimeFixture();
  var first=f.Write("first.pgm",40);
  var second=f.Write("second.pgm",80);
  f.Paths(first,second);
  f.Edit(("local.path-cache",new ConstantSetting(1L)),("local.auto-switch",new ConstantSetting(false)));
  using(var reader=f.Formal())Assert.Equal(new byte[]{40,40},RuntimeFixture.Image(reader).CopyPixels());
  var context=f.Session.CaptureHostInputContext(f.Flow.Id,f.Node)!;
  f.Factory.SelectIndex(context,1);
  using(var reader=f.Formal())Assert.Equal(new byte[]{80,80},RuntimeFixture.Image(reader).CopyPixels());
  f.Factory.SelectIndex(context,0);
  File.Delete(first);
  File.Delete(second);
  using(var reader=f.Formal())
  {
   Assert.True(reader.Snapshot.IsSuccess,reader.Snapshot.ErrorMessage);
   Assert.Equal(new byte[]{40,40},RuntimeFixture.Image(reader).CopyPixels());
  }
  using(var call=f.Capture(f.Scope()))
  using(var result=f.Execute(call))
   Assert.False(result.IsSuccess);
  f.Factory.SelectIndex(context,1);
  using(var reader=f.Formal())Assert.False(reader.Snapshot.IsSuccess);
 });
 [Fact] public void Sdk_file_is_scoped_to_one_formal_capture_and_never_falls_back_to_local_paths()=>RuntimeHost.Run(()=>
 {
  using var f=new RuntimeFixture();
  f.Paths(f.Write("local.pgm",10));
  var sdk=f.Write("external.pgm",80);
  f.Edit(("source.kind",new ConstantSetting("sdk")));
  using(var missing=f.Formal())Assert.False(missing.Snapshot.IsSuccess);
  var inputs=new Dictionary<string,string>{{f.Node.Id,sdk}};
  using(var batch=f.Session.CaptureRunBatch(new[]{f.Flow.Id},hostFactory:f.Factory.ForSdkFiles(f.Flow.Id,inputs)))
  {
   inputs.Clear();
   using var result=RuntimeHost.Wait(Task.Run(()=>batch.RunAsync(_=>{})));
   f.Session.DrainRuntimeChanges();
   Assert.True(result.Workflows[f.Flow.Id].Success);
   using var reader=f.Session.AcquirePublishedResult(f.Flow.Id,f.Node.Id);
   Assert.Equal(new byte[]{80,80},RuntimeFixture.Image(reader!).CopyPixels());
   Assert.Equal("sdk",reader!.Values!.Get<string>("output.source-kind"));
   Assert.Equal(0L,reader.Values.Get<long>("output.sequence"));
   Assert.Empty(reader.Values.Get<string>("output.file"));
   Assert.Empty(reader.Values.Get<string>("output.file-name"));
  }
  using(var missing=f.Formal())Assert.False(missing.Snapshot.IsSuccess);
  Assert.Equal("sdk",f.Node.Configuration.Fields.Get<string>("source.kind"));
 });
 [Fact] public void Sdk_memory_copies_input_and_publishes_a_fresh_rgb_frame()=>RuntimeHost.Run(()=>
 {
  using var f=new RuntimeFixture();
  f.Edit(("source.kind",new ConstantSetting("sdk")),("output.mono8",new ConstantSetting(true)));
  var buffer=new byte[]{255,0,0,0,0,255,99,99};
  var image=new SdkImageInput(new ImageLayout(2,1,8,PixelLayout.Rgb24),buffer);
  var inputs=f.Factory.ForSdkImages(f.Flow.Id,new Dictionary<string,SdkImageInput>{{f.Node.Id,image}});
  Array.Clear(buffer);
  using var reader=f.Formal(inputs);
  Assert.True(reader.Snapshot.IsSuccess,reader.Snapshot.ErrorMessage);
  var rgb=RuntimeFixture.Image(reader);
  Assert.Equal(reader.Snapshot.Identity,rgb.Frame.ProducerRun);
  Assert.Equal("output.image",rgb.Frame.SourcePortId);
  Assert.Equal(PixelLayout.Rgb24,rgb.Layout.Format);
  Assert.Equal(new byte[]{255,0,0,0,0,255},rgb.CopyPixels());
  var gray=reader.Values!.Get<RecordValue>("output.gray-image").Get<IReadOnlyImage>("data");
  Assert.Equal(new byte[]{75,29},gray.CopyPixels());
  Assert.Empty(reader.Values.Get<string>("output.file"));
  using var domain=rgb.EffectiveDomain.AcquireReader();
  Assert.Equal(new[]{new RegionRun(0,0,1)},domain.ReadRuns(0,1).ToArray());
 });
 [Fact] public void Formal_and_private_selected_runs_start_in_the_filtered_list_and_keep_the_configured_start()=>RuntimeHost.Run(()=>
 {
  using var f=new RuntimeFixture();
  var first=f.Write("first.pgm",40);
  var last=f.Write("last.pgm",80);
  f.Paths(Path.Combine(f.DirectoryPath,"excluded-missing.pgm"),first,last);
  f.Edit(("local.run-mode",new ConstantSetting("selected")),("local.selected",new ConstantSetting(new[]{last,first})));
  var scope=f.Scope();
  foreach(var (file,pixel) in new[]{(first,(byte)40),(last,(byte)80)})
  {
   using var formal=f.Formal();
   using var call=f.Capture(scope);
   using var result=f.Execute(call);
   using var debug=result.AcquireReader();
   foreach(var reader in new[]{formal,debug})
   {
    Assert.True(reader.Snapshot.IsSuccess,reader.Snapshot.ErrorMessage);
    Assert.Equal(file,reader.Values!.Get<string>("output.file"));
    Assert.Equal(new[]{pixel,pixel},RuntimeFixture.Image(reader).CopyPixels());
   }
  }
  Assert.Equal(0L,f.Node.Configuration.Fields.Get<long>("local.start"));
 });
 [Theory][InlineData(false)][InlineData(true)] public void Invalid_debug_draft_preserves_previous_cursor_when_the_draft_is_restored(bool empty)=>RuntimeHost.Run(()=>
 {
  using var f=new RuntimeFixture();var path=f.Write("a.pgm",20);f.Paths(path);var scope=f.Scope();using(var call=f.Capture(scope))using(var result=f.Execute(call))Assert.True(result.IsSuccess,result.ErrorMessage);
  var draft=f.Draft();draft.ApplyFields(empty?new Dictionary<string,ParameterSetting>{{"local.paths",new ConstantSetting(Array.Empty<string>())},{"local.start",new ConstantSetting(-1L)}}:new Dictionary<string,ParameterSetting>{{"local.run-mode",new ConstantSetting("selected")},{"local.selected",new ConstantSetting(Array.Empty<string>())}});
  Assert.ThrowsAny<Exception>(()=>{using var invalid=f.Capture(scope,draft);using var result=f.Execute(invalid);if(!result.IsSuccess)throw new InvalidOperationException(result.ErrorMessage);});
  using var restored=f.Capture(scope);using var good=f.Execute(restored);Assert.True(good.IsSuccess,good.ErrorMessage);using var reader=good.AcquireReader();Assert.Equal(path,reader.Values!.Get<string>("output.file"));
 });
 [Fact] public void Local_debug_freezes_private_settings_base_and_sequence_without_publishing_formal_state()=>RuntimeHost.Run(()=>
 {
  using var f=new RuntimeFixture();var path=f.Write("relative.pgm",20);f.Paths("relative.pgm");f.Session.SaveAs(Path.Combine(f.DirectoryPath,"saved.solution"),Path.Combine(f.DirectoryPath,"recovery"),f.DirectoryPath,new FrozenVariableTypeCatalog(Array.Empty<KeyValuePair<string,string>>()),new System.Text.Json.JsonSerializerOptions(),scope=>new ComplexVariableValueCodec(scope));
  var scope=f.Scope();var draft=f.Draft();using var call=f.Capture(scope,draft);using var result=f.Execute(call);Assert.True(result.IsSuccess,result.ErrorMessage);using var reader=result.AcquireReader();Assert.Equal(path,reader.Values!.Get<string>("output.file"));Assert.Equal(8L,reader.Values.Get<long>("output.sequence"));Assert.Equal(new byte[]{20,20},RuntimeFixture.Image(reader).CopyPixels());
  draft.ApplyFields(new Dictionary<string,ParameterSetting>{{"sequence.initial",new ConstantSetting(88L)}});Assert.Throws<InvalidOperationException>(call.AcceptParameters);Assert.Equal(7L,Assert.IsType<ConstantSetting>(f.Node.Configuration.Fields.Read("sequence.initial")).Value);Assert.Null(f.Session.AcquirePublishedResult(f.Flow.Id,f.Node.Id));
 });
 [Theory][InlineData("empty")][InlineData("excluded")][InlineData("base")] public void Invalid_local_debug_never_uses_a_default_or_upstream_image(string failure)=>RuntimeHost.Run(()=>
 {
  using var f=new RuntimeFixture();if(failure=="empty")f.Paths();if(failure=="excluded"){f.Paths(f.Write("a.pgm",40));f.Edit(("local.run-mode",new ConstantSetting("selected")),("local.selected",new ConstantSetting(Array.Empty<string>())));}if(failure=="base")f.Paths("relative.pgm");
  Assert.ThrowsAny<Exception>(()=>{using var call=f.Capture(f.Scope());using var result=f.Execute(call);if(!result.IsSuccess)throw new InvalidOperationException(result.ErrorMessage);});Assert.Null(f.Session.AcquirePublishedResult(f.Flow.Id,f.Node.Id));
 });
 [Fact] public void Formal_request_releases_input_when_run_cannot_start()=>RuntimeHost.Run(()=>
 {
  using var f=new RuntimeFixture();f.Paths(f.Write("a.pgm",10));var graph=f.Session.PrepareGraphEdit(f.Flow.Id);var second=f.Session.CreateNode(graph,"ImageLoad",new(350,120));f.Session.CommitGraphEdit(graph,"第二个真实来源");var ready=f.Session.Editing.Host.BeginEdit(f.Flow.Id,second.Id);ready.ApplyFields(new Dictionary<string,ParameterSetting>{{"local.paths",f.Node.Configuration.Fields.Read("local.paths")},{"local.start",new ConstantSetting(0L)}});f.Session.CommitEdit(SunEyeVision.Application.Editing.DocumentEditProposal.Prepare(f.Session.Editing.Host,nodes:new[]{ready}));var first=new CountedPreparer();var count=0;
  var factory=new DelegateFactory(_=>++count==1?first:throw new InvalidOperationException("第二来源捕获失败"));Assert.Throws<InvalidOperationException>(()=>f.Session.CaptureRunBatch(new[]{f.Flow.Id},hostFactory:factory));Assert.Equal(1,first.Disposals);Assert.Null(f.Session.AcquirePublishedResult(f.Flow.Id,f.Node.Id));
 });
 [Fact] public void Formal_request_reentry_is_rejected_without_disrupting_the_first_run()=>RuntimeHost.Run(()=>
 {
  using var f=new RuntimeFixture();f.Paths(f.Write("a.pgm",33));using var gate=new BlockingFactory(f.Factory);using var batch=f.Session.CaptureRunBatch(new[]{f.Flow.Id},hostFactory:gate);var running=Task.Run(()=>batch.RunAsync(_=>{}));RuntimeHost.Wait(gate.Entered.Task);
  try{Assert.Throws<InvalidOperationException>(()=>RuntimeHost.Wait(batch.RunAsync(_=>{})));Assert.Throws<InvalidOperationException>(batch.Dispose);}finally{gate.Release.TrySetResult();}
  using var result=RuntimeHost.Wait(running);f.Session.DrainRuntimeChanges();Assert.True(result.Workflows[f.Flow.Id].Success);batch.Dispose();
 });
 [Fact] public void Direct_formal_source_inputs_and_debug_share_pixels_domain_but_keep_distinct_run_and_frame_identities()=>RuntimeHost.Run(()=>
 {
  using var f=new RuntimeFixture();f.Paths(f.Write("a.pgm",40));using var formal=f.Formal();using var call=f.Capture(f.Scope());using var result=f.Execute(call);using var debug=result.AcquireReader();Assert.True(result.IsSuccess,result.ErrorMessage);
  var a=RuntimeFixture.Image(formal);var b=RuntimeFixture.Image(debug);Assert.Equal(a.CopyPixels(),b.CopyPixels());Assert.NotEqual(a.Frame,b.Frame);Assert.NotEqual(a.Frame.ProducerRun,b.Frame.ProducerRun);Assert.Equal(result.Identity,b.Frame.ProducerRun);using var da=a.EffectiveDomain.AcquireReader();using var db=b.EffectiveDomain.AcquireReader();Assert.Equal(da.ReadRuns(0,4096).ToArray(),db.ReadRuns(0,4096).ToArray());
 });
 [Fact] public void Direct_formal_execution_rejects_missing_scheduler_input_and_persistent_constant_bypass()=>RuntimeHost.Run(()=>
 {
  using var f=new RuntimeFixture();var path=f.Write("a.pgm",50);f.Paths(path);using(var batch=f.Session.CaptureRunBatch(new[]{f.Flow.Id})){using var result=RuntimeHost.Wait(Task.Run(()=>batch.RunAsync(_=>{})));Assert.False(result.Workflows[f.Flow.Id].Success);f.Session.DrainRuntimeChanges();}
  var draft=f.Draft();draft.ApplyFields(new Dictionary<string,ParameterSetting>{{"input.local-address",new ConstantSetting(path)},{"input.sequence",new ConstantSetting(7L)}});
  try{f.Session.CommitEdit(SunEyeVision.Application.Editing.DocumentEditProposal.Prepare(f.Session.Editing.Host,nodes:new[]{draft}));}catch(ArgumentException){}
  using(var bypass=f.Session.CaptureRunBatch(new[]{f.Flow.Id})){using var rejected=RuntimeHost.Wait(Task.Run(()=>bypass.RunAsync(_=>{})));Assert.False(rejected.Workflows[f.Flow.Id].Success);f.Session.DrainRuntimeChanges();}
  f.Edit(("input.local-address",new UnsetSetting()),("input.sequence",new UnsetSetting()));using var good=f.Formal();Assert.True(good.Snapshot.IsSuccess);Assert.Equal(new byte[]{50,50},RuntimeFixture.Image(good).CopyPixels());
 });
 [Fact] public void Camera_debug_uses_real_run_identity_and_disposes_owned_frame()=>RuntimeHost.Run(()=>
 {
  using var frame=new TrackedFrame();var service=Substitute.For<ICameraFrameProvider>();CameraFrameRequest? captured=null;
  service.CaptureAsync(Arg.Any<CameraFrameRequest>(),Arg.Any<CancellationToken>()).Returns(call=>{captured=call.Arg<CameraFrameRequest>();return new ValueTask<ICameraFrameLease>(frame.Initialize(captured));});
  using var f=new RuntimeFixture(service);f.Camera();using var call=f.Capture(f.Scope());using var result=f.Execute(call);Assert.True(result.IsSuccess,result.ErrorMessage);using var reader=result.AcquireReader();
  Assert.Equal("camera",captured!.DeviceId);Assert.Equal(TimeSpan.FromMilliseconds(1234),captured.Timeout);Assert.Equal(CameraRequestPurpose.Workflow,captured.Purpose);Assert.Equal(result.Identity,captured.ProducerRun);
  Assert.True(frame.IsDisposed);Assert.Equal(1,frame.Disposals);Assert.Equal("42",reader.Values!.Get<string>("output.device-frame-id"));Assert.Equal(new byte[]{30,80},RuntimeFixture.Image(reader).CopyPixels());
  Assert.Equal(new[] { "Image:Width:2 Height:1", "FrameNum:42" }, result.ResultRecords);
 });
 [Theory][InlineData("wrong-device")][InlineData("wrong-run")][InlineData("missing-device")][InlineData("service-failure")] public void Camera_debug_rejects_invalid_capture_without_publication_or_leak(string failure)=>RuntimeHost.Run(()=>
 {
  using var frame=new TrackedFrame(failure=="wrong-device"?"other":"camera",failure=="wrong-run");var service=Substitute.For<ICameraFrameProvider>();int captures=0;
  service.CaptureAsync(Arg.Any<CameraFrameRequest>(),Arg.Any<CancellationToken>()).Returns(call=>{captures++;if(failure=="service-failure")return ValueTask.FromException<ICameraFrameLease>(new IOException("采集失败"));return new ValueTask<ICameraFrameLease>(frame.Initialize(call.Arg<CameraFrameRequest>()));});
  using var f=new RuntimeFixture(service);f.Camera(failure=="missing-device"?"":"camera");
  Assert.ThrowsAny<Exception>(()=>{using var call=f.Capture(f.Scope());using var result=f.Execute(call);if(!result.IsSuccess)throw new InvalidOperationException(result.ErrorMessage);});
  Assert.Equal(failure=="missing-device"?0:1,captures);Assert.Equal(failure is "wrong-device" or "wrong-run"?1:0,frame.Disposals);Assert.Null(f.Session.AcquirePublishedResult(f.Flow.Id,f.Node.Id));
 });
 [Fact] public void Camera_cancel_before_debug_capture_does_not_call_provider()=>RuntimeHost.Run(()=>
 {
  var service=Substitute.For<ICameraFrameProvider>();using var f=new RuntimeFixture(service);f.Camera();using var cancellation=new CancellationTokenSource();cancellation.Cancel();
  Assert.ThrowsAny<Exception>(()=>{using var call=f.Capture(f.Scope(),cancellation:cancellation.Token);using var result=f.Execute(call);if(!result.IsSuccess)throw new InvalidOperationException(result.ErrorMessage);});Assert.Empty(service.ReceivedCalls());Assert.Null(f.Session.AcquirePublishedResult(f.Flow.Id,f.Node.Id));
 });
 [Theory][InlineData(false)][InlineData(true)] public void Camera_retains_primary_and_cleanup_failure(bool cancelled)=>RuntimeHost.Run(()=>
 {
  using var frame=new TrackedFrame(cancelled?"camera":"other",throwOnDispose:true);using var cancellation=new CancellationTokenSource();var service=Substitute.For<ICameraFrameProvider>();
  service.CaptureAsync(Arg.Any<CameraFrameRequest>(),Arg.Any<CancellationToken>()).Returns(call=>{var item=frame.Initialize(call.Arg<CameraFrameRequest>());if(cancelled)cancellation.Cancel();return new ValueTask<ICameraFrameLease>(item);});
  using var f=new RuntimeFixture(service);f.Camera();using var invocation=f.Capture(f.Scope(),cancellation:cancellation.Token);
  var error=Assert.ThrowsAny<Exception>(()=>f.Execute(invocation));IEnumerable<Exception> errors=error is AggregateException aggregate?aggregate.Flatten().InnerExceptions:new[]{error};
  Assert.Contains(errors,e=>e.Message=="frame-release-failed");Assert.Contains(errors,e=>cancelled?e is OperationCanceledException:e.Message.Contains("设备或本次运行身份不匹配"));Assert.True(frame.IsDisposed);Assert.Equal(1,frame.Disposals);
 });
 [Fact] public void Formal_camera_result_reader_survives_owned_input_release()=>RuntimeHost.Run(()=>
 {
  using var frame=new TrackedFrame();var service=Substitute.For<ICameraFrameProvider>();service.CaptureAsync(Arg.Any<CameraFrameRequest>(),Arg.Any<CancellationToken>()).Returns(call=>new ValueTask<ICameraFrameLease>(frame.Initialize(call.Arg<CameraFrameRequest>())));
  using var f=new RuntimeFixture(service);f.Camera();using var reader=f.Formal();Assert.True(reader.Snapshot.IsSuccess,reader.Snapshot.ErrorMessage);Assert.True(frame.IsDisposed);Assert.Equal(new byte[]{30,80},RuntimeFixture.Image(reader).CopyPixels());
  var history = f.Session.ReadResultHistory(f.Flow.Id, f.Node);
  Assert.Equal(new[] { "Image:Width:2 Height:1", "FrameNum:42" }, history.Select(row => row.Content));
  Assert.All(history, row => Assert.Equal(1L, row.RowIndex));
 });
 [Fact] public void Camera_acquisition_resolves_exposure_and_gain_from_the_formal_upstream_result()=>RuntimeHost.Run(()=>
 {
  using var frame=new TrackedFrame();
  var service=Substitute.For<ICameraFrameProvider>();
  CameraFrameRequest? captured=null;
  service.CaptureAsync(Arg.Any<CameraFrameRequest>(),Arg.Any<CancellationToken>()).Returns(call=>
  {
   captured=call.Arg<CameraFrameRequest>();
   return new ValueTask<ICameraFrameLease>(frame.Initialize(captured));
  });
  using var f=new RuntimeFixture(service);
  f.Camera();
  var graph=f.Session.PrepareGraphEdit(f.Flow.Id);
  var local=f.Session.CreateNode(graph,"ImageLoad",new(10,120));
  f.Session.CommitGraphEdit(graph,"添加曝光与增益的上游来源");
  var localDraft=f.Session.Editing.Host.BeginEdit(f.Flow.Id,local.Id);
  localDraft.ApplyFields(new Dictionary<string,ParameterSetting>
  {["local.paths"]=new ConstantSetting(new[]{f.Write("upstream.pgm",60)})});
  var cameraDraft=f.Draft();
  var port=f.Session.GetNodeSchema(f.Flow.Id,local.Id)!.Outputs.Single(item=>item.Id=="output.module-time-ms");
  var reference=new ReferenceSetting(new OutputReference(local.Id,port.Id,port.Type,scopeId:f.Flow.Id));
  cameraDraft.ApplyFields(new Dictionary<string,ParameterSetting>
  {["camera.exposure-time"]=reference,["camera.gain"]=reference});
  f.Session.CommitEdit(SunEyeVision.Application.Editing.DocumentEditProposal.Prepare(f.Session.Editing.Host,nodes:new[]{localDraft,cameraDraft}));
  using var reader=f.Formal();
  Assert.True(reader.Snapshot.IsSuccess,reader.Snapshot.ErrorMessage);
  using var upstream=f.Session.AcquirePublishedResult(f.Flow.Id,local.Id);
  var time=upstream!.Values!.Get<double>(port.Id);
  Assert.NotNull(captured);
  Assert.Equal(time,captured.ExposureTime);
  Assert.Equal(time,captured.Gain);
  Assert.Equal(reader.Snapshot.Identity,captured.ProducerRun);
 });
 [Fact] public void Camera_debug_cancellation_disposes_late_frame_and_close_waits_for_drain()=>RuntimeHost.Run(()=>
 {
  using var frame=new TrackedFrame();var service=Substitute.For<ICameraFrameProvider>();var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var late=new TaskCompletionSource<ICameraFrameLease>(TaskCreationOptions.RunContinuationsAsynchronously);CameraFrameRequest? captured=null;
  service.CaptureAsync(Arg.Any<CameraFrameRequest>(),Arg.Any<CancellationToken>()).Returns(call=>{captured=call.Arg<CameraFrameRequest>();entered.TrySetResult();return new ValueTask<ICameraFrameLease>(late.Task);});
  using var f=new RuntimeFixture(service);f.Camera();var scope=f.Scope();using var call=f.Capture(scope);var task=Task.Run(call.ExecuteAsync);RuntimeHost.Wait(entered.Task);
  try{Assert.Throws<InvalidOperationException>(call.Dispose);Assert.Throws<InvalidOperationException>(scope.Dispose);Assert.ThrowsAny<Exception>(()=>RuntimeHost.Wait(call.ExecuteAsync()));call.Cancel();}finally{late.TrySetResult(frame.Initialize(captured!));}
  Assert.ThrowsAny<OperationCanceledException>(()=>RuntimeHost.Wait(task));Assert.True(frame.IsDisposed);Assert.Equal(1,frame.Disposals);call.Dispose();scope.Dispose();Assert.Null(f.Session.AcquirePublishedResult(f.Flow.Id,f.Node.Id));
 }); [Fact] public void Debug_snapshot_is_single_use_and_retained_output_readers_survive_result_close()=>RuntimeHost.Run(()=>
 {
  using var f=new RuntimeFixture();f.Paths(f.Write("a.pgm",60));var scope=f.Scope();var abandoned=f.Capture(scope);abandoned.Dispose();abandoned.Dispose();Assert.ThrowsAny<Exception>(()=>f.Execute(abandoned));using var call=f.Capture(scope);using var result=f.Execute(call);var reader=result.AcquireReader();var image=RuntimeFixture.Image(reader);using var pixels=image.AcquirePixels();using var domain=pixels.AcquireDomainReader();var expected=domain.ReadRuns(0,4096).ToArray();reader.Dispose();result.Dispose();Assert.Throws<ObjectDisposedException>(()=>image.Frame);Assert.Equal(new byte[]{60,60},pixels.GetRow(0).ToArray());pixels.Dispose();Assert.Throws<ObjectDisposedException>(()=>pixels.GetRow(0).ToArray());Assert.Equal(expected,domain.ReadRuns(0,4096).ToArray());domain.Dispose();Assert.Throws<ObjectDisposedException>(()=>domain.SourceFrame);Assert.ThrowsAny<Exception>(()=>f.Execute(call));call.Dispose();
 });
 private sealed class Gate(ToolExecutionIdentity identity):IHostInvocationGate{public bool TryInvoke(Action callback)=>identity.TryInvokeCallback(callback);}
 private sealed class CountedResource:IDisposable{public int Disposals;public void Dispose()=>Disposals++;}
 private sealed class DelegateFactory(Func<HostInputFactoryContext,IHostInputPreparer> create):IHostInputFactory{public IHostInputPreparer Create(HostInputFactoryContext context)=>create(context);}
 private sealed class CountedPreparer:IHostInputPreparer{public int Disposals;public string CapabilityId=>"image-source";public int CapabilityVersion=>1;public IReadOnlyList<string> Services=>new[]{"local-image-read"};public ValueTask<IPreparedHostInputs> PrepareAsync(HostInputPreparationContext context)=>throw new InvalidOperationException();public void Dispose()=>Disposals++;}
 private sealed class BlockingFactory(IHostInputFactory actual):IHostInputFactory,IDisposable
 {
  internal readonly TaskCompletionSource Entered=new(TaskCreationOptions.RunContinuationsAsynchronously),Release=new(TaskCreationOptions.RunContinuationsAsynchronously);
  public IHostInputPreparer? Create(HostInputFactoryContext context)=>new Block(actual.Create(context)!,this);public void Dispose()=>Release.TrySetResult();
  private sealed class Block(IHostInputPreparer actual,BlockingFactory owner):IHostInputPreparer{public string CapabilityId=>actual.CapabilityId;public int CapabilityVersion=>actual.CapabilityVersion;public IReadOnlyList<string> Services=>actual.Services;public async ValueTask<IPreparedHostInputs> PrepareAsync(HostInputPreparationContext context){owner.Entered.TrySetResult();await owner.Release.Task;return await actual.PrepareAsync(context);}public void Dispose()=>actual.Dispose();}
 }
 private sealed class TrackedFrame(string device="camera",bool wrongRun=false,bool throwOnDispose=false):ICameraFrameLease,IDisposable
 {
  private FrozenImageResource? _image;public bool IsDisposed{get;private set;}public int Disposals{get;private set;}
  public string DeviceId=>device;public long DeviceFrameId=>42;public DateTimeOffset Timestamp=>DateTimeOffset.Parse("2026-10-28T01:02:03+00:00");
  public IReadOnlyImage Image=>_image??throw new ObjectDisposedException(nameof(TrackedFrame));
  public ICameraFrameLease Initialize(CameraFrameRequest request)
  {
   var identity=wrongRun ? request.ProducerRun!.Value with { RequestId=Guid.NewGuid() } : request.ProducerRun;
   var address=new ImageFrameAddress(Guid.NewGuid(),"camera:"+device,identity);
   _image=new FrozenImageResource(new ImageLayout(2,1,2,PixelLayout.Gray8),address,new CoordinateFrame("camera:"+device,CoordinateDomain.Image,CoordinateUnit.Pixel),new byte[]{30,80},new[]{new RegionRun(0,0,1)});return this;
  }
  public ValueTask DisposeAsync(){if(IsDisposed)return ValueTask.CompletedTask;IsDisposed=true;Disposals++;_image?.Dispose();_image=null;if(throwOnDispose)throw new InvalidOperationException("frame-release-failed");return ValueTask.CompletedTask;}
  public void Dispose(){if(!IsDisposed)DisposeAsync().GetAwaiter().GetResult();}
 }
}
