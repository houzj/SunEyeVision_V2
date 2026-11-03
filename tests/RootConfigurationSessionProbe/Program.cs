using System.IO;
using System.Text.Json;
using SunEyeVision.Application;
using SunEyeVision.Application.Editing;
using SunEyeVision.Domain.Persistence;
using SunEyeVision.Domain.Solutions;
using SunEyeVision.Domain.Workflows;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Plugin.Wpf.Controls.Native.Composition.ImageSources;
using SunEyeVision.UI.Services.Editing;
using SunEyeVision.UI.Services;

internal static class Program
{
 [STAThread] private static int Main(string[] args)
 {
  try
  {
   var openingSettings = new RootWorkspaceSettings { RunIntervalMs = 123, StopOnNg = true };
   var settingsWindow = new SunEyeVision.UI.Shell.Windows.RootWorkflowRunSettingsWindow(openingSettings);
   settingsWindow.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.FrameworkElement.LoadedEvent));
   var interval = (SunEyeVision.Plugin.Wpf.Native.Numeric.HikPcUI.ListView.SliderArrange)settingsWindow.FindName("Interval");
   if (settingsWindow.Width != 370 || settingsWindow.Height != 200 || interval.Minimum != 0 || interval.Maximum != 100000 ||
       interval.SliderTickFrequency != 10 || interval.StartValue != 123 || !settingsWindow.Settings.StopOnNg)
    throw new Exception("root run interval window does not retain declared range and loaded settings");
   settingsWindow.Close();
   Console.WriteLine("PASS: interval window loads actual value and preserves separate NG setting on close");
   var directory = new ReleasePayloadAuthority(Path.GetFullPath(args[0])).PrepareRuntimeDirectory();
   using var manager = new DocumentSessionManager(directory);
   var session = manager.CreateNew("根配置事务测试");
   var root = session.GetWorkflows().Single();
   var graph = session.PrepareGraphEdit(root.Id);
   var node = session.CreateNode(graph, "ImageLoad", new CanvasPoint(0, 0));
   session.CommitGraphEdit(graph, "添加图像源");
   var receipts = 0;
   using var context = new RootWorkflowConfigurationContext(session, root, () => true, () => true, _ => receipts++);
   var candidate = context.Candidates().Single(item => item.Node == node && item.FieldId == "sequence.initial");
   var proxy = context.Add(candidate);
   context.Write(proxy, new ConstantSetting(9L));
   if (!context.TryApply(out var error)) throw new Exception("apply: " + error);
   if (receipts != 1 || root.RootConfiguration.RunParameters.Length != 1 ||
       node.Configuration.Fields.Read("sequence.initial") is not ConstantSetting { Value: long value } || value != 9)
    throw new Exception("applied root/node state mismatch");
   context.Undo();
   if (root.RootConfiguration.RunParameters.Length != 0) throw new Exception("undo root mismatch");
   context.Redo();
   if (root.RootConfiguration.RunParameters.Length != 1) throw new Exception("redo root mismatch");
   try { context.Add(context.Candidates().Single(item => item.Node == node && item.FieldId == "sequence.initial")); throw new Exception("duplicate was accepted"); }
   catch (ArgumentException) { }
   if (context.Configuration.RunParameters.Length != 1) throw new Exception("duplicate modified private draft");
   context.Remove(root.RootConfiguration.RunParameters[0]);
   context.Cancel();
   if (root.RootConfiguration.RunParameters.Length != 1) throw new Exception("cancel published draft");
   using var pending = new RootWorkflowConfigurationContext(session, root, () => true, () => true, _ => receipts++);
   pending.Remove(root.RootConfiguration.RunParameters[0]);
   var dataPath = Path.Combine(Path.GetFullPath(args[0]), "_root-probe-data", Guid.NewGuid().ToString("N"));
   Directory.CreateDirectory(dataPath);
   var path = Path.Combine(dataPath, "root.solution");
   var types = new FrozenVariableTypeCatalog(new Dictionary<string, string> { ["Integer"] = "System.Int64", ["Integer[]"] = "System.Int64" });
   session.SaveAs(path, Path.Combine(dataPath, "backup"), dataPath, types, new JsonSerializerOptions(), _ => null);
   if (pending.HasPendingChanges || root.RootConfiguration.RunParameters.Length != 0)
    throw new Exception("save did not accept private root edit");
   pending.Dispose();
   var reopened = manager.Open(path, _ => types, _ => null, new JsonSerializerOptions());
   if (reopened.GetWorkflows().Single().RootConfiguration.RunParameters.Length != 0 ||
       reopened.GetNodes(root.Id).Single().Configuration.Fields.Read("sequence.initial") is not ConstantSetting { Value: long saved } || saved != 9)
    throw new Exception("save/reopen lost root or node values");
   Console.WriteLine("PASS: trusted payload; root/node atomic apply; undo/redo; cancel; save accepts pending edit; reopen");
   var reopenedRoot = reopened.GetWorkflows().Single();
   var graph2 = reopened.PrepareGraphEdit(reopenedRoot.Id);
   var blob = reopened.CreateNode(graph2, "BlobAnalysis", new CanvasPoint(200, 0));
   var image = reopened.GetNodes(reopenedRoot.Id).Single();
   graph2.AddConnection(new ConnectionDefinition("wire", image.Id, blob.Id, "output.image", "input.image"));
   reopened.CommitGraphEdit(graph2, "连接图像源和 Blob");
   using var variableDraft = reopened.Editing.Host.BeginVariableEdit();
   variableDraft.Variables.Add(new GlobalVariable("空数组", Array.Empty<long>(), "Integer[]") { Index = 1 });
   variableDraft.Variables.Add(new GlobalVariable("双元素", new long[] { 1, 2 }, "Integer[]") { Index = 2 });
   variableDraft.Variables.Add(new GlobalVariable("声明标量", new long[] { 1, 2 }, "Integer") { Index = 3 });
   reopened.CommitEdit(DocumentEditProposal.Prepare(reopened.Editing.Host, variables: variableDraft));
   var selections = reopened.CaptureReferenceSelections(reopenedRoot.Id, blob, "input.image");
   if (selections.FieldKind != ReferenceSelectionFieldKind.Input || selections.Outputs.Count != 1 ||
       !ReferenceEquals(selections.Outputs[0].Actual, image) ||
       !selections.Outputs[0].Schema.Outputs.Any(port => port.Id == "output.image") ||
       selections.Variables.Count != 3 ||
       selections.Variables.Where(item => item.Name is "空数组" or "双元素").Any(item => item.Reference.Type.Shape != ValueShape.Sequence) ||
       selections.Variables.Single(item => item.Name == "声明标量").Reference.Type.Shape != ValueShape.Scalar)
    throw new Exception("source identity, output or declared variable shape mismatch");
   var display = reopened.CaptureReferenceSelections(reopenedRoot.Id, blob, "display.text.content");
   var position = reopened.CaptureReferenceSelections(reopenedRoot.Id, blob, "display.text.x");
   if (display.FieldKind != ReferenceSelectionFieldKind.Display || display.Outputs.Count != 2 ||
       !display.Outputs.Any(item => ReferenceEquals(item.Actual, blob)) ||
       !display.Outputs.Any(item => ReferenceEquals(item.Actual, image)) || display.Variables.Count != 0 ||
       position.Outputs.Count != 2 || position.Variables.Count != 0)
    throw new Exception("display sources must be same-flow predecessors and actual self without variables");
   Console.WriteLine("PASS: display expression and position expose predecessor and self without variable candidates");
   var impostor = new NodeDefinition(blob.Id, blob.Name, blob.ToolType, blob.Configuration);
   try { reopened.CaptureReferenceSelections(reopenedRoot.Id, impostor, "input.image"); throw new Exception("foreign instance was accepted"); }
   catch (InvalidOperationException) { }
   var resolved = ImageSourceResolver.Resolve(reopened, reopenedRoot, blob);
   if (resolved.Kind != ImageSourceResolutionKind.Found || !ReferenceEquals(resolved.Source, image) ||
       ImageSourceResolver.Resolve(reopened, reopenedRoot, impostor).Kind != ImageSourceResolutionKind.InvalidSelection)
    throw new Exception("image source projection did not preserve actual node identity");
   Console.WriteLine("PASS: scoped actual target, predecessor schema, zero/two item variable declared shapes, foreign identity rejection");
   var asset = Path.Combine(dataPath, "asset.bmp");
   File.WriteAllBytes(asset, new byte[] { 0x42, 0x4d });
   var imageDraft = reopened.Editing.Host.BeginEdit(reopenedRoot.Id, image.Id);
   var pathsPort = imageDraft.SettingsSchema.Inputs.Single(item => item.Id == "local.paths");
   imageDraft.ApplyFields(new Dictionary<string, ParameterSetting> {
    ["local.paths"] = new ConstantSetting(new SequenceValue(pathsPort.Type, new object?[] { "asset.bmp" }))
   });
   reopened.CommitEdit(DocumentEditProposal.Prepare(reopened.Editing.Host, nodes: new[] { imageDraft }));
   reopened.Save(path, Path.Combine(dataPath, "backup"), types, new JsonSerializerOptions());
   var targetDir = Path.Combine(dataPath, "moved"); Directory.CreateDirectory(targetDir);
   var movedPath = Path.Combine(targetDir, "moved.solution");
   var moved = reopened.SaveAs(movedPath, Path.Combine(dataPath, "backup"), dataPath, types, new JsonSerializerOptions(), _ => null);
   var live = reopened.CreateResourcePathResolver().Resolve("local.paths", "asset.bmp", ResourcePurpose.FileRead);
   if (!live.Exists || !string.Equals(live.Address, asset, StringComparison.OrdinalIgnoreCase) ||
       !string.Equals(reopened.ResourceBaseDirectory, dataPath, StringComparison.OrdinalIgnoreCase))
    throw new Exception("SaveAs changed the live image source resolver");
   var savedAgain = reopened.Save(movedPath, Path.Combine(dataPath, "backup"), types, new JsonSerializerOptions());
   var before = moved.WrittenSnapshot.ResourcePlan.Single(item => item.Reference.EndsWith("asset.bmp", StringComparison.Ordinal));
   var after = savedAgain.WrittenSnapshot.ResourcePlan.Single(item => item.Reference.EndsWith("asset.bmp", StringComparison.Ordinal));
   if (!string.Equals(before.AbsolutePath, asset, StringComparison.OrdinalIgnoreCase) ||
       !string.Equals(after.AbsolutePath, asset, StringComparison.OrdinalIgnoreCase))
    throw new Exception($"resource drift: SaveAs={before.AbsolutePath}; Save={after.AbsolutePath}");
   Console.WriteLine($"PASS: SaveAs and Save retain live image source address {asset}");
   try { reopened.CaptureReferenceSelections(reopenedRoot.Id, blob, "not-a-field"); throw new Exception("unknown field was accepted"); }
   catch (InvalidOperationException) { }
   var disconnect = reopened.PrepareGraphEdit(reopenedRoot.Id);
   disconnect.RemoveConnection(reopenedRoot.Connections.Single());
   reopened.CommitGraphEdit(disconnect, "断开来源");
   if (reopened.CaptureReferenceSelections(reopenedRoot.Id, blob, "input.image").Outputs.Count != 0)
    throw new Exception("disconnected predecessor was retained");
   if (ImageSourceResolver.Resolve(reopened, reopenedRoot, blob).Kind != ImageSourceResolutionKind.None)
    throw new Exception("disconnected image source remained selected");
   var disconnectedDisplay = reopened.CaptureReferenceSelections(reopenedRoot.Id, blob, "display.text.content");
   if (disconnectedDisplay.Outputs.Count != 1 || !ReferenceEquals(disconnectedDisplay.Outputs[0].Actual, blob))
    throw new Exception("display self source was lost after disconnect");
   Console.WriteLine("PASS: unknown field rejected; source query refreshes after disconnect");
   Console.WriteLine("PASS: display self remains selectable after disconnect");
   var movedSession = manager.Open(movedPath, _ => types, _ => null, new JsonSerializerOptions());
   var reopenedImage = movedSession.GetNodes(reopenedRoot.Id).Single(item => item.ToolType == "ImageLoad");
   var persisted = (SequenceValue)((ConstantSetting)reopenedImage.Configuration.Fields.Read("local.paths")).Value!;
   var loadedReference = (string)persisted.Single()!;
   var loadedPath = movedSession.CreateResourcePathResolver().Resolve("local.paths", loadedReference, ResourcePurpose.FileRead);
   if (!loadedPath.Exists || !string.Equals(loadedPath.Address, asset, StringComparison.OrdinalIgnoreCase) ||
       !string.Equals(movedSession.ResourceBaseDirectory, targetDir, StringComparison.OrdinalIgnoreCase))
    throw new Exception("reopened image source does not resolve the original asset");
   Console.WriteLine($"PASS: reopened image source reference {loadedReference} resolves {loadedPath.Address}");
   var movedRoot = movedSession.GetWorkflows().Single();
   var nodeDraft = movedSession.Editing.Host.BeginEdit(movedRoot.Id, reopenedImage.Id);
   var active = true;
   using var sourceView = new ImageSourceDraftContent(movedSession, movedRoot, reopenedImage, () => nodeDraft, () => active);
   IToolSettingsDraft Settings() => new FieldSettingsDraft(nodeDraft.SettingsSchema, nodeDraft.Configuration.Fields);
   void Bind() => sourceView.BindSourceDraft(Settings(), () => { }, fields => nodeDraft.ApplyFields(fields.Capture()));
   Bind();
   var inputView = (ImageSourceInputView)sourceView.Content;
   if (inputView.OpenCameraButton.IsEnabled) throw new Exception("missing camera manager must be disabled");
   var rebase = DocumentEditProposal.Prepare(movedSession.Editing.Host, nodes: new[] { nodeDraft });
   movedSession.CommitEdit(rebase);
   nodeDraft = movedSession.Editing.Host.RebaseAcceptedDraft(nodeDraft, rebase);
   inputView.CameraPicker.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = "失效回调", Tag = "stale-device" });
   inputView.CameraPicker.SelectedIndex = 1;
   if (nodeDraft.Configuration.Fields.Read("camera.device") is not ConstantSetting { Value: string camera } || camera == "stale-device")
    throw new Exception("stale camera callback modified the rebased draft");
   try { sourceView.RefreshSourceDraft(); throw new Exception("old source view survived draft rebase"); }
   catch (InvalidOperationException) { }
   sourceView.RebaseDraft(nodeDraft); sourceView.RefreshSourceDraft();
   if (!ReferenceEquals(inputView, sourceView.Content)) throw new Exception("rebase rebuilt source view and lost pending input");
   active = false;
   try { sourceView.RefreshSourceDraft(); throw new Exception("retired source view remained active"); }
   catch (InvalidOperationException) { }
   sourceView.Dispose();
   try { sourceView.RefreshSourceDraft(); throw new Exception("disposed source view remained active"); }
   catch (ObjectDisposedException) { }
   Console.WriteLine("PASS: image source draft rebase rejects old view, rebinds new draft, protects camera and closes");
   return 0;
  }
  catch(Exception error) { Console.Error.WriteLine(error); return 1; }
 }
}
