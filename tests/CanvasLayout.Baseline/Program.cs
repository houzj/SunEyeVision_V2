using SunEyeVision.Domain.Workflows;
using System.IO;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Workflow;

try
{
var payload = Path.GetFullPath(args[0]);
var output = Path.GetFullPath(args[1]);
var directory = new ReleasePayloadAuthority(payload).PrepareRuntimeDirectory();
var manager = new SolutionManager(Path.Combine(Path.GetDirectoryName(output)!, "catalog"), directory);
var document = manager.CreateUntitledSolution();
var flow = document.Workflows[0];
flow.Nodes.Add(new WorkflowNodeBase("source", "source", "source", "ImageLoad") { RuntimeConfiguration = RuntimeToolConfiguration.Create(directory, "ImageLoad"), PositionX = 12.5, PositionY = 37.25 });
flow.Nodes.Add(new WorkflowNodeBase("target", "target", "target", "ImageLoad") { RuntimeConfiguration = RuntimeToolConfiguration.Create(directory, "ImageLoad"), PositionX = 200.5, PositionY = 300.25 });
flow.Connections.Add(new WorkflowConnection { Id = "golden-edge", SourceNodeId = "source", TargetNodeId = "target", ManualPathPoints = new[] { new CanvasPoint(12.5, 37.25), new CanvasPoint(80.75, 37.25), new CanvasPoint(80.75, 300.25), new CanvasPoint(200.5, 300.25) } });
if (!manager.SaveSolution(output)) throw new InvalidOperationException(manager.LastError);
Console.WriteLine(output);
return 0;
}
catch(Exception error) { Console.Error.WriteLine(error); return 1; }
