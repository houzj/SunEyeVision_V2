using System.Reflection;
using System.IO;
using SunEyeVision.Application;
using SunEyeVision.DeviceDriver.Communications;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;

using SunEyeVision.UI.Services.Communication;
using SunEyeVision.Application.Communications;
NativeErrors.SetErrorMode(0x0001 | 0x0002 | 0x8000);
try
{
 var eventId=Guid.NewGuid();var fieldId=Guid.NewGuid();
 var fields=new Dictionary<Guid,CommunicationFieldSample>{{fieldId,new(fieldId,"结果","String","START")}};
 var rule=new SunEyeVision.Domain.Solutions.CommunicationTriggerDefinition { TriggerParameterSource="START" };
 if(!CommunicationTriggerEvaluation.EvaluateEquality(rule,eventId,new[]{fieldId},fields).Matches)throw new Exception("正式触发比较失败");
 rule.TriggerParameterSource="OTHER";
 if(CommunicationTriggerEvaluation.EvaluateEquality(rule,eventId,new[]{fieldId},fields).Matches)throw new Exception("错误触发值被接受");
 rule.TriggerParameterSource=null;
 if(!CommunicationTriggerEvaluation.EvaluateEquality(rule,eventId,Array.Empty<Guid>(),new Dictionary<Guid,CommunicationFieldSample>()).Matches)throw new Exception("空来源未关闭比较");
 var serialized=System.Text.Json.JsonSerializer.Serialize(rule);
 if(serialized.Contains("ParameterComparisonEnabled")||serialized.Contains("\"FieldId\""))throw new Exception("写入仍包含旧触发字段");
 foreach(var field in new[]{"FieldId","fieldid","ParameterComparisonEnabled","parametercomparisonenabled"})
 {
    using var json=System.Text.Json.JsonDocument.Parse("{\"SchemaVersion\":5,\"CommunicationTriggers\":[{\""+field+"\":null}]}");
    var rejected=false;try{SunEyeVision.Persistence.SolutionFormatPolicy.ValidateReadable(json.RootElement);}catch(System.Text.Json.JsonException){rejected=true;}
    if(!rejected)throw new Exception("V5 未拒绝旧触发字段："+field);
 }
 Console.WriteLine("PASS V5 旧触发字段大小写拒绝、写出不含旧字段及当前比较语义");
 var root=Path.GetFullPath(Environment.GetEnvironmentVariable("SUNEYE_PRODUCT_ROOT") ?? throw new InvalidOperationException("SUNEYE_PRODUCT_ROOT is required."));
 var directory=new ReleasePayloadAuthority(root).PrepareRuntimeDirectory();
 using var manager=new DocumentSessionManager(directory); var session=manager.CreateNew(); using var activity=session.AcquireActivityLease("通信测试");
 var listener=new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback,0); listener.Start();
 var port=((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
 var device=new SunEyeVision.Domain.Solutions.Communication { Id="tcp",Name="回环",ConnectionType=SunEyeVision.Domain.Solutions.CommunicationType.TCP,Host="127.0.0.1",Port=port,IsEnabled=true };
 typeof(SunEyeVision.Domain.Solutions.SolutionDefinition).GetProperty(nameof(SunEyeVision.Domain.Solutions.SolutionDefinition.Communications))!.SetValue(session.Document,new[]{device});
 session.Document.CommunicationResponses.WorkflowState.Enabled=true;
 var accepted=listener.AcceptTcpClientAsync();
 var runtime=new CommunicationRuntimeComposition(); var service=new CommunicationApplicationService(session,runtime);
 service.ReadyAsync().WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
 if(session.Document.CommunicationResponses.WorkflowIdentifiers.Count!=0)throw new Exception("启动时通信服务静默修改了方案响应编号");
 using var peer=accepted.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
 if(!runtime.Runtime.IsConnected(device.Id))throw new Exception("真实 TCP 回环未连接");
 var request=new SunEyeVision.Plugin.Editing.Wpf.Models.Communication.CommunicationSendRequest("Device",device.Id,"",
    new[]{new SunEyeVision.Plugin.Editing.Wpf.Models.Communication.CommunicationSendInput(Guid.NewGuid(),"","PING","String")},new());
 var send=service.Sends.PrepareAsync(request).WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
 var buffer=new byte[4]; var read=peer.GetStream().ReadAsync(buffer).AsTask();
 if(send.SubmitAsync().WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult()<=0 || read.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult()!=4 || System.Text.Encoding.ASCII.GetString(buffer)!="PING")throw new Exception("真实 TCP 发送失败");
 peer.GetStream().Write(new byte[]{1,2,3});
 var received=service.Receives.ReadAsync(device.Id,"").WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
 if(received.Value is not SunEyeVision.Plugin.Contracts.BinaryValue payload || !payload.ToArray().SequenceEqual(new byte[]{1,2,3}))throw new Exception("真实 TCP 接收失败");
 var delayedSend=service.Sends.PrepareAsync(request).WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
 var missingDevice=new SunEyeVision.Domain.Solutions.Communication { Id="missing",Name="不存在" };
 var rejectedMissing=false;try { service.ConnectDeviceAsync(SunEyeVision.UI.Features.CommunicationManager.CommunicationDeviceSnapshotFactory.Create(missingDevice)).GetAwaiter().GetResult(); } catch(InvalidOperationException) { rejectedMissing=true; }
 if(!rejectedMissing)throw new Exception("不存在设备未拒绝");
 var entered=(IDisposable)typeof(CommunicationApplicationService).GetMethod("EnterOperation",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(service,null)!;
 var first=service.DisposeAsync().AsTask(); var second=service.DisposeAsync().AsTask();
 if(first.IsCompleted || second.IsCompleted)throw new Exception("在途操作期间错误完成释放");
 var switchingRejected=false;try { manager.CreateNew("替换"); } catch(InvalidOperationException) { switchingRejected=true; }
 if(!switchingRejected || !ReferenceEquals(manager.CurrentSession,session))throw new Exception("活动租约未保护会话替换");
 entered.Dispose(); Task.WhenAll(first,second).WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
 var reject=false;try { service.ConnectDeviceAsync(null!).GetAwaiter().GetResult(); } catch(ObjectDisposedException){reject=true;}
 if(!reject)throw new Exception("服务释放后仍可发起操作");
 var rejectDelayed=false;try { delayedSend.SubmitAsync().GetAwaiter().GetResult(); } catch(ObjectDisposedException){rejectDelayed=true;}
 if(!rejectDelayed)throw new Exception("服务释放后旧发送计划仍可提交");
 runtime.DisposeAsync().AsTask().GetAwaiter().GetResult(); activity.Dispose();
 var replacement=manager.CreateNew("替换成功");if(ReferenceEquals(replacement,session))throw new Exception("排空后未切换会话"); listener.Stop();
 Console.WriteLine("PASS 真实目录准入会话（设备定义由测试装配）：真实 TCP 回环连接、发送与接收、在途操作阻止释放、并发关闭共享排空、退役拒新调用、活动租约保护");return 0;
} catch(Exception ex){Console.Error.WriteLine(ex);return 1;}


static class NativeErrors { [System.Runtime.InteropServices.DllImport("kernel32.dll")] public static extern uint SetErrorMode(uint mode); }


