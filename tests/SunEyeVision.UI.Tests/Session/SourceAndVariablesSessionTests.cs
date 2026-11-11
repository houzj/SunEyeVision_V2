using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Editing.Wpf;
using SunEyeVision.Plugin.Wpf.Fields;
using SunEyeVision.Plugin.Wpf.Presenters.Input.ImageSources;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using SunEyeVision.Plugin.Wpf.Presenters.Regions;
using SunEyeVision.Plugin.Wpf.Adapters.Regions;
using SunEyeVision.Plugin.Wpf.Controls.Native.Composition.Results.ResultShell;
using SunEyeVision.UI.Features.GlobalVariableManager;
using SunEyeVision.Plugin.Wpf.Controls.Native.Composition.ImageSources;
using SunEyeVision.UI.Services.Editing;
using SunEyeVision.UI.Services.Images;
using SunEyeVision.UI.Features.Camera;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Xunit;
using SunEyeVision.Application.Editing;
using SunEyeVision.Domain.Solutions;
using SunEyeVision.Infrastructure.Persistence;
using SunEyeVision.Persistence;
using SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters;
namespace SunEyeVision.UI.Tests;
[Collection("Editor commit UI")]
public sealed class SourceAndVariablesSessionTests
{
    [Fact]
    public void Embedded_source_reopens_without_the_original_file_and_feeds_the_same_image_to_blob_and_the_source_strip()=>UiRuntime.Run(()=>
    {
        using var f=new DocumentUiFixture();var source=f.Add();var blob=f.Add("BlobAnalysis",350,240);f.Connect(source,blob);
        var image=Path.Combine(f.DirectoryPath,"embedded-source.bmp");var encoder=new BmpBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(8,8,96,96,PixelFormats.Gray8,null,Enumerable.Repeat((byte)200,64).ToArray(),8)));
        using(var file=File.Create(image))encoder.Save(file);
        f.ConfigureImage(source,image);f.Fields(source,("local.save-images",new ConstantSetting(true)));
        f.Fields(blob,("field:43bda03261df55b88f963bd18f2ba4d9",new ConstantSetting(100L)));
        f.Save("embedded-production.solution");File.Delete(image);
        var session=ServiceInitializer.SolutionManager.Open(Path.Combine(f.DirectoryPath,"embedded-production.solution"),
            bytes=>VariableVocabularyCapture.FromCandidate(bytes.Span),scope=>new ComplexVariableValueCodec(scope),new System.Text.Json.JsonSerializerOptions());
        var actual=session.FindNode(f.Root.Id,source.Id)!;var root=session.GetWorkflows().Single(flow=>flow.Id==f.Root.Id);
        using var binding=new ImageSourceProjectionBinding(session,root,actual,()=>true,()=>true,()=>true);
        var reference=Assert.Single(actual.Configuration.Fields.Get<SequenceValue>("local.paths"));Assert.StartsWith("suneye-file:",Assert.IsType<string>(reference));
        var projected=Assert.Single(binding.Source.Images);Assert.True(File.Exists(projected.FilePath));
        f.Run();
        using var result=session.AcquirePublishedResult(root.Id,source.Id);using var blobResult=session.AcquirePublishedResult(root.Id,blob.Id);
        Assert.True(result?.Snapshot.IsSuccess,result?.Snapshot.ErrorMessage);Assert.True(blobResult?.Snapshot.IsSuccess,blobResult?.Snapshot.ErrorMessage);
        var sourceImage=result!.Values!.Get<RecordValue>("output.image").Get<IReadOnlyImage>("data");
        var binary=blobResult!.Values!.Get<RecordValue>("field:8ca10000000040008000000000000006").Get<IReadOnlyImage>("data");
        Assert.Equal(sourceImage.Frame,binary.Frame);Assert.All(sourceImage.CopyPixels(),pixel=>Assert.Equal(200,pixel));
        Assert.Equal("embedded-source.bmp",result.Values.Get<string>("output.file-name"));
        Assert.Equal(2L,result.Values.Get<long>("output.sequence"));
    });

    [Fact]
    public void Accepted_folder_shows_initial_files_before_execution_and_keeps_rows_until_a_new_folder_or_receipt()=>UiRuntime.Run(()=>
    {
        using var f=new DocumentUiFixture();var source=f.Add();var manual=f.Image();f.ConfigureImage(source,manual);
        var folder=Path.Combine(f.DirectoryPath,"subscribed");Directory.CreateDirectory(folder);
        string Image(string name)
        {
            var path=Path.Combine(folder,name);var encoder=new BmpBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(2,2,96,96,PixelFormats.Gray8,null,new byte[]{10,20,30,40},2)));
            using(var stream=File.Create(path))encoder.Save(stream);return path;
        }
        var tenth=Image("10.bmp");var second=Image("2.bmp");Image("ignored.BMP");
        f.Fields(source,("local.subscribe-folder",new ConstantSetting(true)),("local.folder-path",new ConstantSetting(folder)),
            ("local.refresh-folder",new ConstantSetting(false)));
        using var binding=new ImageSourceProjectionBinding(f.Session,f.Root,source,()=>true,()=>true,()=>true);
        Assert.Equal(new[]{second,tenth},binding.Source.Images.Select(image=>image.FilePath));
        Assert.Null(f.Session.AcquirePublishedResult(f.Root.Id,source.Id));
        var first=binding.Source.Images[0];Image("3.bmp");
        binding.RefreshFromDocument();Assert.Same(first,binding.Source.Images[0]);Assert.Equal(2,binding.Source.Count);
        f.Fields(source,("output.mono8",new ConstantSetting(true)));
        binding.RefreshFromDocument();Assert.Same(first,binding.Source.Images[0]);Assert.Equal(2,binding.Source.Count);
        var missing=Path.Combine(f.DirectoryPath,"missing-folder");
        f.Fields(source,("local.folder-path",new ConstantSetting(missing)));
        binding.RefreshFromDocument();Assert.Empty(binding.Source.Images);Assert.NotNull(binding.LastPresentationError);
        f.Fields(source,("local.folder-path",new ConstantSetting(folder)));
        binding.RefreshFromDocument();Assert.Equal(3,binding.Source.Count);
        f.Fields(source,("local.subscribe-folder",new ConstantSetting(false)));
        binding.RefreshFromDocument();Assert.Equal(manual,Assert.Single(binding.Source.Images).FilePath);
    });

    [Fact]
    public void Source_failure_retires_the_blob_input_but_preserves_borrowed_frames_and_recovers_with_a_new_frame()=>UiRuntime.Run(()=>
    {
        using var fixture = new DocumentUiFixture();
        var source = fixture.Add();
        var blob = fixture.Add("BlobAnalysis", 350, 240);
        fixture.Connect(source, blob);
        var path = fixture.Image(200, 8);
        fixture.ConfigureImage(source, path);
        fixture.Fields(blob, ("field:43bda03261df55b88f963bd18f2ba4d9", new ConstantSetting(100L)));
        fixture.Run();
        using var previousSource = fixture.Session.AcquirePublishedResult(fixture.Root.Id, source.Id);
        using var previousBlob = fixture.Session.AcquirePublishedResult(fixture.Root.Id, blob.Id);
        Assert.True(previousBlob?.Snapshot.IsSuccess, previousBlob?.Snapshot.ErrorMessage);
        var oldImage = previousSource!.Values!.Get<RecordValue>("output.image").Get<IReadOnlyImage>("data");
        var oldBinary = previousBlob!.Values!.Get<RecordValue>("field:8ca10000000040008000000000000006").Get<IReadOnlyImage>("data");
        Assert.Equal(2L, previousSource.Values.Get<long>("output.sequence"));
        File.Delete(path);
        fixture.Run();
        using (var failed = fixture.Session.AcquirePublishedResult(fixture.Root.Id, source.Id))
        {
            Assert.Equal(SunEyeVision.Plugin.Runtime.ExecutionStatus.Failed, failed!.Snapshot.Status);
            Assert.True(failed.Values is null || !failed.Values.Contains("output.image"));
            Assert.NotEmpty(failed.Snapshot.Diagnostics);
        }
        Assert.Null(fixture.Session.AcquirePublishedResult(fixture.Root.Id, blob.Id));
        Assert.All(oldImage.CopyPixels(), pixel => Assert.Equal(200, pixel));
        Assert.Equal(255, oldBinary.CopyPixels()[18]);
        File.WriteAllBytes(path, System.Text.Encoding.ASCII.GetBytes("P5\n8 8\n255\n").Concat(Enumerable.Repeat((byte)60, 64)).ToArray());
        fixture.Run();
        using var recoveredSource = fixture.Session.AcquirePublishedResult(fixture.Root.Id, source.Id);
        using var recoveredBlob = fixture.Session.AcquirePublishedResult(fixture.Root.Id, blob.Id);
        Assert.True(recoveredBlob?.Snapshot.IsSuccess, recoveredBlob?.Snapshot.ErrorMessage);
        Assert.Equal(3L, recoveredSource!.Values!.Get<long>("output.sequence"));
        var newImage = recoveredSource.Values.Get<RecordValue>("output.image").Get<IReadOnlyImage>("data");
        var newBinary = recoveredBlob!.Values!.Get<RecordValue>("field:8ca10000000040008000000000000006").Get<IReadOnlyImage>("data");
        Assert.NotEqual(oldImage.Frame, newImage.Frame);
        Assert.Equal(newImage.Frame, newBinary.Frame);
        Assert.All(newImage.CopyPixels(), pixel => Assert.Equal(60, pixel));
        Assert.Equal(0, newBinary.CopyPixels()[18]);
        Assert.All(oldImage.CopyPixels(), pixel => Assert.Equal(200, pixel));
    });

    [Fact]
    public void Saved_image_source_reopens_with_filter_and_clear_references_but_without_previous_stitch_pixels()=>UiRuntime.Run(()=>
    {
        using var fixture = new DocumentUiFixture();
        var source = fixture.Add();
        var blob = fixture.Add("BlobAnalysis", 350, 240);
        fixture.Connect(source, blob);
        fixture.ConfigureImage(source, fixture.Image(200, 8));
        fixture.Fields(blob, ("field:43bda03261df55b88f963bd18f2ba4d9", new ConstantSetting(100L)));
        using (var edit = fixture.Session.Editing.Host.BeginVariableEdit())
        {
            edit.Variables.Add(new GlobalVariable("输入字符", "go", "String"));
            edit.Variables.Add(new GlobalVariable("触发字符", "go", "String"));
            edit.Variables.Add(new GlobalVariable("清空拼接", 0L, "Integer"));
            fixture.Session.CommitEdit(DocumentEditProposal.Prepare(fixture.Session.Editing.Host, variables: edit));
        }
        var variables = fixture.Session.Document.GlobalVariables.ToArray();
        VariableSetting Reference(int index, TypeRef type) => new(new VariableReference(fixture.Session.Document.Id, variables[index].Id, type));
        fixture.Fields(source, ("trigger.filter-enabled", new ConstantSetting(true)),
            ("trigger.text", Reference(0, DataType.Text)), ("trigger.filter-text", Reference(1, DataType.Text)),
            ("stitch.enabled", new ConstantSetting(true)), ("stitch.start", new ConstantSetting(0L)),
            ("stitch.height", new ConstantSetting(2L)), ("stitch.clear", new ConstantSetting(true)),
            ("stitch.clear-value", Reference(2, TypeRef.ScalarOf(ScalarKind.Int64))));
        fixture.Run();
        CheckImage(fixture.Session, 0, 2);
        fixture.Run();
        CheckImage(fixture.Session, 200, 3);
        fixture.Save("source-state.solution");
        var session = ServiceInitializer.SolutionManager.Open(Path.Combine(fixture.DirectoryPath, "source-state.solution"),
            bytes => VariableVocabularyCapture.FromCandidate(bytes.Span), scope => new ComplexVariableValueCodec(scope),
            new System.Text.Json.JsonSerializerOptions());
        Assert.NotEqual(fixture.Session.EditSessionId, session.EditSessionId);
        var restored = session.FindNode(fixture.Root.Id, source.Id)!;
        foreach (var pair in new[] { ("trigger.text", 0), ("trigger.filter-text", 1), ("stitch.clear-value", 2) })
            Assert.Equal(variables[pair.Item2].Id, Assert.IsType<VariableSetting>(restored.Configuration.Fields.Read(pair.Item1)).Reference.VariableId);
        Assert.True(restored.Configuration.Fields.Get<bool>("trigger.filter-enabled"));
        Assert.Equal(2L, restored.Configuration.Fields.Get<long>("stitch.height"));
        fixture.Run();
        CheckImage(session, 0, 2);
        fixture.Run();
        CheckImage(session, 200, 3);
        Change(1, "stop");
        fixture.Run();
        using (var filtered = session.AcquirePublishedResult(fixture.Root.Id, source.Id))
            Assert.Equal(SunEyeVision.Plugin.Runtime.ExecutionStatus.NotExecuted, filtered!.Snapshot.Status);
        Assert.Null(session.AcquirePublishedResult(fixture.Root.Id, blob.Id));
        Change(1, "go");
        Change(2, 1L);
        fixture.Run();
        CheckImage(session, 0, 4);

        void Change(int index, object value)
        {
            using var edit = session.Editing.Host.BeginVariableEdit();
            edit.SetValue(variables[index].Id, value);
            session.CommitEdit(DocumentEditProposal.Prepare(session.Editing.Host, variables: edit));
        }
        void CheckImage(SunEyeVision.Application.DocumentSession owner, byte prefix, long sequence)
        {
            using var imageResult = owner.AcquirePublishedResult(fixture.Root.Id, source.Id);
            using var blobResult = owner.AcquirePublishedResult(fixture.Root.Id, blob.Id);
            Assert.True(imageResult?.Snapshot.IsSuccess, imageResult?.Snapshot.ErrorMessage);
            Assert.Equal(sequence, imageResult!.Values!.Get<long>("output.sequence"));
            Assert.True(blobResult?.Snapshot.IsSuccess, blobResult?.Snapshot.ErrorMessage);
            var image = imageResult!.Values!.Get<RecordValue>("output.image").Get<IReadOnlyImage>("data");
            var binary = blobResult!.Values!.Get<RecordValue>("field:8ca10000000040008000000000000006").Get<IReadOnlyImage>("data");
            Assert.Equal(image.Frame, binary.Frame);
            Assert.Equal(10, image.Layout.Height);
            Assert.All(image.CopyPixels().Take(16), pixel => Assert.Equal(prefix, pixel));
            Assert.All(image.CopyPixels().Skip(16), pixel => Assert.Equal(200, pixel));
            var pixels = binary.CopyPixels();
            Assert.Equal(prefix == 0 ? 0 : 255, pixels[10]);
            Assert.Equal(255, pixels[26]);
        }
    });

    [Fact]
    public void Character_filter_stops_the_blob_chain_without_advancing_the_local_cursor()=>UiRuntime.Run(()=>
    {
        using var fixture = new DocumentUiFixture();
        var source = fixture.Add();
        var blob = fixture.Add("BlobAnalysis", 350, 240);
        fixture.Connect(source, blob);
        fixture.ConfigureImage(source, fixture.Image(50, 8), fixture.Image(200, 8));
        using (var variables = fixture.Session.Editing.Host.BeginVariableEdit())
        {
            variables.Variables.Add(new GlobalVariable("输入字符", "go", "String"));
            variables.Variables.Add(new GlobalVariable("触发字符", "stop", "String"));
            fixture.Session.CommitEdit(DocumentEditProposal.Prepare(fixture.Session.Editing.Host, variables: variables));
        }
        var definitions = fixture.Session.Document.GlobalVariables.ToArray();
        VariableSetting Reference(int index) => new(new VariableReference(fixture.Session.Document.Id, definitions[index].Id, DataType.Text));
        fixture.Fields(source, ("trigger.filter-enabled", new ConstantSetting(true)),
            ("trigger.text", Reference(0)), ("trigger.filter-text", Reference(1)));
        fixture.Run();
        using (var filtered = fixture.Session.AcquirePublishedResult(fixture.Root.Id, source.Id))
        {
            Assert.Equal(SunEyeVision.Plugin.Runtime.ExecutionStatus.NotExecuted, filtered!.Snapshot.Status);
            Assert.Null(filtered.Values);
        }
        Assert.Null(fixture.Session.AcquirePublishedResult(fixture.Root.Id, blob.Id));
        using (var variables = fixture.Session.Editing.Host.BeginVariableEdit())
        {
            variables.SetValue(definitions[1].Id, "go");
            fixture.Session.CommitEdit(DocumentEditProposal.Prepare(fixture.Session.Editing.Host, variables: variables));
        }
        fixture.Run();
        using var loaded = fixture.Session.AcquirePublishedResult(fixture.Root.Id, source.Id);
        using var result = fixture.Session.AcquirePublishedResult(fixture.Root.Id, blob.Id);
        Assert.True(result?.Snapshot.IsSuccess, result?.Snapshot.ErrorMessage);
        var image = loaded!.Values!.Get<RecordValue>("output.image").Get<IReadOnlyImage>("data");
        Assert.All(image.CopyPixels(), pixel => Assert.Equal(200, pixel));
        Assert.Equal(image.Frame, result!.Values!.Get<RecordValue>("field:8ca10000000040008000000000000006")
            .Get<IReadOnlyImage>("data").Frame);
    });

    [Fact]
    public void Image_source_stitch_settings_and_clear_subscription_feed_the_formal_blob_image()=>UiRuntime.Run(()=>
    {
        using var fixture = new DocumentUiFixture();
        var clearProducer = fixture.Add();
        var source = fixture.Add("ImageLoad", 250, 200);
        var blob = fixture.Add("BlobAnalysis", 450, 300);
        fixture.ConfigureImage(clearProducer, fixture.Image(255, 4));
        var image = Path.Combine(fixture.DirectoryPath, "rows.pgm");
        File.WriteAllBytes(image, System.Text.Encoding.ASCII.GetBytes("P5\n4 4\n255\n")
            .Concat(new byte[] { 10, 20, 30, 40 }.SelectMany(value => Enumerable.Repeat(value, 4))).ToArray());
        fixture.ConfigureImage(source, image);
        fixture.Connect(clearProducer, source);
        fixture.Connect(source, blob);
        fixture.Fields(blob, ("field:43bda03261df55b88f963bd18f2ba4d9", new ConstantSetting(25L)));
        fixture.Main.SelectedNode = source;
        fixture.Main.OpenDebugWindowCommand.Execute(source);
        UiRuntime.Pump();
        var window = Assert.Single(System.Windows.Application.Current.Windows.OfType<RuntimeToolEditorWindow>());
        try
        {
            UiRuntime.AwaitEditorReady(window);
            var editor = Assert.Single(UiRuntime.Visuals<DeclaredSettingsView>(window.Editor));
            Assert.True(editor.TrySetFields(new Dictionary<string, ParameterSetting>
            {
                ["stitch.enabled"] = new ConstantSetting(true), ["stitch.start"] = new ConstantSetting(1L),
                ["stitch.height"] = new ConstantSetting(2L), ["local.auto-switch"] = new ConstantSetting(false)
            }));
            window.UpdateLayout();
            Assert.True(UiRuntime.Visuals<DeclaredFieldRow>(window.Editor).Single(row => row.FieldId == "stitch.height").IsVisible);
            Assert.False(UiRuntime.Visuals<DeclaredFieldRow>(window.Editor).Single(row => row.FieldId == "stitch.clear-value").IsVisible);
            Assert.True(editor.TrySetFields(new Dictionary<string, ParameterSetting> { ["stitch.clear"] = new ConstantSetting(true) }));
            window.UpdateLayout();
            Assert.True(UiRuntime.Visuals<DeclaredFieldRow>(window.Editor).Single(row => row.FieldId == "stitch.clear-value").IsVisible);
            Assert.True(editor.TrySetFields(new Dictionary<string, ParameterSetting> { ["stitch.enabled"] = new ConstantSetting(false) }));
            window.UpdateLayout();
            foreach (var id in new[] { "stitch.start", "stitch.height", "stitch.clear", "stitch.clear-value" })
                Assert.False(UiRuntime.Visuals<DeclaredFieldRow>(window.Editor).Single(row => row.FieldId == id).IsVisible);
            Assert.True(editor.TrySetFields(new Dictionary<string, ParameterSetting> { ["stitch.enabled"] = new ConstantSetting(true) }));
            window.UpdateLayout();
            Assert.True(UiRuntime.Visuals<DeclaredFieldRow>(window.Editor).Single(row => row.FieldId == "stitch.clear-value").IsVisible);
            Assert.Equal(2L, editor.CaptureAcceptedFields().Get<long>("stitch.height"));
            Assert.True(editor.TrySetFields(new Dictionary<string, ParameterSetting> { ["stitch.clear"] = new ConstantSetting(false) }));
            var context = Assert.IsType<ProductionEditorContext>(ToolEditorCommit.GetDraftContext(window.Editor));
            Assert.True(context.TryCommitEditor(out var error), error);
        }
        finally { UiRuntime.Await(window.CloseAndDrainAsync()); }

        void RunAndCheck(params byte[] expected)
        {
            fixture.Run();
            using var loaded = fixture.Session.AcquirePublishedResult(fixture.Root.Id, source.Id);
            using var result = fixture.Session.AcquirePublishedResult(fixture.Root.Id, blob.Id);
            Assert.True(result?.Snapshot.IsSuccess, result?.Snapshot.ErrorMessage);
            var input = loaded!.Values!.Get<RecordValue>("output.image").Get<IReadOnlyImage>("data");
            var binary = result!.Values!.Get<RecordValue>("field:8ca10000000040008000000000000006").Get<IReadOnlyImage>("data");
            Assert.Equal(input.Frame, binary.Frame);
            Assert.Equal(6, binary.Height);
            var pixels = binary.CopyPixels();
            Assert.Equal(expected, Enumerable.Range(0, 6).Select(row => pixels[row * binary.Layout.RowStrideBytes + 2]).ToArray());
        }
        RunAndCheck(0, 0, 0, 0, 255, 255);
        RunAndCheck(0, 255, 0, 0, 255, 255);
        var output = fixture.Session.GetNodeSchema(fixture.Root.Id, clearProducer.Id)!.Outputs.Single(port => port.Id == "output.image");
        fixture.Fields(source, ("stitch.clear", new ConstantSetting(true)),
            ("stitch.clear-value", new ReferenceSetting(new OutputReference(clearProducer.Id, output.Id, output.Type,
                new[] { "width" }, scopeId: fixture.Root.Id))));
        RunAndCheck(0, 0, 0, 0, 255, 255);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Queued_folder_requests_keep_the_current_configuration_when_an_old_callback_arrives(bool oldArrivesLast)=>UiRuntime.Run(()=>
    {
        using var fixture = new DocumentUiFixture();
        var source = fixture.Add();
        var blob = fixture.Add("BlobAnalysis", 350, 240);
        fixture.Connect(source, blob);
        string Folder(string name, byte gray)
        {
            var directory = Directory.CreateDirectory(Path.Combine(fixture.DirectoryPath, name)).FullName;
            var bitmap = BitmapSource.Create(8, 8, 96, 96, PixelFormats.Bgr24, null,
                Enumerable.Repeat(gray, 8 * 8 * 3).ToArray(), 24);
            var encoder = new BmpBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var output = File.Create(Path.Combine(directory, name + ".bmp"));
            encoder.Save(output);
            return directory;
        }
        var oldFolder = Folder("old", 0);
        var currentFolder = Folder("current", 255);
        fixture.Fields(source, ("local.subscribe-folder", new ConstantSetting(true)),
            ("local.folder-path", new ConstantSetting(oldFolder)),
            ("local.trigger-folder-run", new ConstantSetting(true)));
        var old = fixture.Session.CaptureHostInputContext(fixture.Root.Id, source)!;
        var draft = fixture.Session.Editing.Host.BeginEdit(fixture.Root.Id, source.Id);
        draft.ApplyFields(new Dictionary<string, ParameterSetting> { ["local.folder-path"] = new ConstantSetting(currentFolder) });
        fixture.Session.CommitEdit(DocumentEditProposal.Prepare(fixture.Session.Editing.Host, nodes: new[] { draft }));
        var current = fixture.Session.CaptureHostInputContext(fixture.Root.Id,
            fixture.Session.FindNode(fixture.Root.Id, source.Id)!)!;
        void Queue(SunEyeVision.Application.HostInputFactoryContext context) =>
            UiRuntime.Call(fixture.Main, "QueueFolderRun", System.Windows.Threading.Dispatcher.CurrentDispatcher, context);
        Queue(oldArrivesLast ? current : old);
        Queue(oldArrivesLast ? old : current);
        SunEyeVision.UI.Services.Workflow.WorkflowExecutionSnapshot? execution = null;
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while ((execution?.CompletedRunCount ?? 0) == 0 && DateTime.UtcNow < deadline)
        {
            UiRuntime.Pump();
            execution = UiRuntime.Call(fixture.Main, "GetFormalExecutionSnapshot", fixture.Root.Id)
                as SunEyeVision.UI.Services.Workflow.WorkflowExecutionSnapshot;
            Thread.Sleep(10);
        }
        Assert.Equal(1L, execution?.CompletedRunCount);
        using var image = fixture.Session.AcquirePublishedResult(fixture.Root.Id, source.Id);
        using var result = fixture.Session.AcquirePublishedResult(fixture.Root.Id, blob.Id);
        Assert.Equal("current.bmp", image!.Values!.Get<string>("output.file-name"));
        Assert.True(result?.Snapshot.IsSuccess, result?.Snapshot.ErrorMessage);
        var binary = result!.Values!.Get<RecordValue>("field:8ca10000000040008000000000000006").Get<IReadOnlyImage>("data");
        Assert.Equal(255, binary.CopyPixels()[3 * binary.Layout.RowStrideBytes + 3]);
    });

    [Fact]
    public void Subscribed_folder_feeds_blob_and_requests_a_run_when_a_file_is_added()=>UiRuntime.Run(()=>
    {
        using var fixture = new DocumentUiFixture();
        var source = fixture.Add();
        var blob = fixture.Add("BlobAnalysis", 350, 240);
        fixture.Connect(source, blob);
        var manual = fixture.Image(255, 8);
        fixture.ConfigureImage(source, manual);
        var directory = Path.Combine(fixture.DirectoryPath, "subscribed");
        Directory.CreateDirectory(directory);
        string BitmapFile(string folder, string name, byte gray)
        {
            var path = Path.Combine(folder, name);
            var pixels = Enumerable.Repeat(gray, 8 * 8 * 3).ToArray();
            var bitmap = BitmapSource.Create(8, 8, 96, 96, PixelFormats.Bgr24, null, pixels, 24);
            var encoder = new BmpBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var output = File.Create(path);
            encoder.Save(output);
            return path;
        }
        BitmapFile(directory, "10.bmp", 255);
        BitmapFile(directory, "2.bmp", 0);
        BitmapFile(directory, "0.BMP", 255);
        var child = Directory.CreateDirectory(Path.Combine(directory, "child"));
        BitmapFile(child.FullName, "1.bmp", 255);
        fixture.Main.SelectedNode = source;
        fixture.Main.OpenDebugWindowCommand.Execute(source);
        UiRuntime.Pump();
        var window = Assert.Single(System.Windows.Application.Current.Windows.OfType<RuntimeToolEditorWindow>());
        try
        {
            UiRuntime.AwaitEditorReady(window);
            var editor = Assert.Single(UiRuntime.Visuals<DeclaredSettingsView>(window.Editor));
            var folderRow = UiRuntime.Visuals<DeclaredFieldRow>(window.Editor).Single(row => row.FieldId == "local.folder-path");
            Assert.True(editor.TrySetFields(new Dictionary<string, ParameterSetting>
            {
                ["local.subscribe-folder"] = new ConstantSetting(true),
                ["local.folder-path"] = new ConstantSetting(directory)
            }));
            window.UpdateLayout();
            Assert.True(folderRow.IsVisible);
            Assert.Equal("OpenFileDialog", Assert.IsType<FieldRowModel>(folderRow.Content).TemplateKey);
            var context = Assert.IsType<ProductionEditorContext>(ToolEditorCommit.GetDraftContext(window.Editor));
            Assert.True(context.TryCommitEditor(out var error), error);
        }
        finally { UiRuntime.Await(window.CloseAndDrainAsync()); }

        void CheckFrame(string file, byte binaryValue)
        {
            using var image = fixture.Session.AcquirePublishedResult(fixture.Root.Id, source.Id);
            using var result = fixture.Session.AcquirePublishedResult(fixture.Root.Id, blob.Id);
            Assert.True(image?.Snapshot.IsSuccess, image?.Snapshot.ErrorMessage);
            Assert.True(result?.Snapshot.IsSuccess, result?.Snapshot.ErrorMessage);
            Assert.Equal(file, image!.Values!.Get<string>("output.file-name"));
            var binary = result!.Values!.Get<RecordValue>("field:8ca10000000040008000000000000006").Get<IReadOnlyImage>("data");
            Assert.Equal(binaryValue, binary.CopyPixels()[3 * binary.Layout.RowStrideBytes + 3]);
        }
        fixture.Run();
        CheckFrame("2.bmp", 0);
        Assert.Equal(2, fixture.Main.ActiveInputSource!.Count);
        Assert.True(fixture.Main.ActiveInputSource.IsReadOnly);
        fixture.Run();
        CheckFrame("10.bmp", 255);
        Assert.Equal(manual, Assert.Single(source.Configuration.Fields.Get<SequenceValue>("local.paths").Cast<string>()));

        fixture.Fields(source, ("local.trigger-folder-run", new ConstantSetting(true)), ("local.auto-switch", new ConstantSetting(false)));
        UiRuntime.Pump();
        var completed = Assert.IsType<SunEyeVision.UI.Services.Workflow.WorkflowExecutionSnapshot>(
            UiRuntime.Call(fixture.Main, "GetFormalExecutionSnapshot", fixture.Root.Id));
        var count = completed.CompletedRunCount;
        var next = BitmapFile(fixture.DirectoryPath, "1.bmp", 255);
        File.Move(next, Path.Combine(directory, "1.bmp"));
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (completed.CompletedRunCount == count && DateTime.UtcNow < deadline)
        {
            UiRuntime.Pump();
            Thread.Sleep(10);
        }
        Assert.Equal(count + 1, completed.CompletedRunCount);
        CheckFrame("1.bmp", 255);
        fixture.Fields(source, ("local.refresh-folder", new ConstantSetting(false)));
        UiRuntime.Pump();
        count = completed.CompletedRunCount;
        next = BitmapFile(fixture.DirectoryPath, "0.bmp", 0);
        File.Move(next, Path.Combine(directory, "0.bmp"));
        deadline = DateTime.UtcNow.AddSeconds(10);
        while (completed.CompletedRunCount == count && DateTime.UtcNow < deadline)
        {
            UiRuntime.Pump();
            Thread.Sleep(10);
        }
        Assert.Equal(count + 1, completed.CompletedRunCount);
        CheckFrame("0.bmp", 0);
        fixture.Fields(source, ("local.trigger-folder-run", new ConstantSetting(false)));
        fixture.Save("folder.solution");
        var reopened = ServiceInitializer.SolutionManager.Open(Path.Combine(fixture.DirectoryPath, "folder.solution"),
            _ => new SunEyeVision.Domain.Persistence.FrozenVariableTypeCatalog(Array.Empty<KeyValuePair<string, string>>()),
            _ => null, new System.Text.Json.JsonSerializerOptions());
        var restored = reopened.FindNode(fixture.Root.Id, source.Id)!;
        Assert.True(restored.Configuration.Fields.Get<bool>("local.subscribe-folder"));
        Assert.Equal(directory, restored.Configuration.Fields.Get<string>("local.folder-path"));
        Assert.Equal(manual, Assert.Single(restored.Configuration.Fields.Get<SequenceValue>("local.paths").Cast<string>()));
        fixture.Run();
        using var published = reopened.AcquirePublishedResult(fixture.Root.Id, blob.Id);
        Assert.True(published?.Snapshot.IsSuccess, published?.Snapshot.ErrorMessage);
    });

    [Fact]
    public void Stored_image_variable_uses_the_same_frame_in_editor_roi_preview_and_formal_blob_execution()=>UiRuntime.Run(()=>
    {
        using var fixture = new DocumentUiFixture();
        var blob = fixture.Add("BlobAnalysis");
        fixture.Fields(blob, ("field:43bda03261df55b88f963bd18f2ba4d9", new ConstantSetting(100L)));
        using var resources = new DecodedResourcesScope();
        var codec = new ComplexVariableValueCodec(resources);
        var raster = codec.CreateRaster(new StoredRasterLayout(4, 1, 4, StoredRasterFormat.Gray8), new byte[] { 0, 99, 100, 255 });
        var variable = new GlobalVariable("图像", raster, "Image");
        var options = new System.Text.Json.JsonSerializerOptions();
        fixture.Save("image-variable.solution");
        var file = Path.Combine(fixture.DirectoryPath, "image-variable.solution");
        var archive = VariableArchive.Encode(new[] { variable }, options, codec);
        var solution = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(file))!;
        solution["GlobalVariables"] = System.Text.Json.Nodes.JsonNode.Parse(archive)!["Variables"]!.DeepClone();
        File.WriteAllText(file, solution.ToJsonString());
        var session = ServiceInitializer.SolutionManager.Open(file, bytes => VariableVocabularyCapture.FromCandidate(bytes.Span),
            scope => new ComplexVariableValueCodec(scope), options);
        var node = session.FindNode(fixture.Root.Id, blob.Id)!;
        fixture.Main.SelectedNode = node;
        fixture.Main.OpenDebugWindowCommand.Execute(node);
        UiRuntime.Pump();
        var window = Assert.Single(System.Windows.Application.Current.Windows.OfType<RuntimeToolEditorWindow>());
        var roiSession = SelectImageVariable();
        var frame = roiSession.Frame;
        Assert.True(roiSession.IsCurrent);
        var context = Assert.IsType<ProductionEditorContext>(ToolEditorCommit.GetDraftContext(window.Editor));
        Assert.True(context.TryCommitEditor(out var error), error);
        UiRuntime.Await(window.CloseAndDrainAsync());
        Assert.False(roiSession.IsCurrent);
        node = session.FindNode(fixture.Root.Id, blob.Id)!;
        Assert.Equal(variable.Id, Assert.IsType<VariableSetting>(node.Configuration.Fields.Read("input.image")).Reference.VariableId);

        NativeRoiSession SelectImageVariable()
        {
            UiRuntime.AwaitEditorReady(window);
            window.UpdateLayout();
            UiRuntime.Pump();
            var row = Assert.Single(UiRuntime.Visuals<DeclaredFieldRow>(window.Editor).Where(field => field.FieldId == "input.image"));
            var combo = Assert.IsType<System.Windows.Controls.ComboBox>(row.InputElement);
            var choice = Assert.Single(combo.Items.OfType<DeclaredFieldRow.ImageSourceChoice>(),
                item => item.Setting is VariableSetting setting && setting.Reference.VariableId == variable.Id);
            combo.SelectedItem = choice;
            UiRuntime.Pump();
            Assert.IsType<VariableSetting>(row.Setting);
            var shell = Assert.Single(UiRuntime.Visuals<ResultWorkspaceShell>(fixture.Results));
            shell.SetCurrentValue(ResultWorkspaceShell.IsDoubleImageProperty, true);
            UiRuntime.Pump();
            var roi = Assert.Single(UiRuntime.Visuals<NativeRoiEditor>(window.Editor).Where(editor => !editor.PolygonOnly));
            Assert.True(roi.PrepareInteraction?.Invoke());
            return Assert.IsType<NativeRoiSession>(UiRuntime.Field<NativeRoiSession>(roi, "_session"));
        }
        IReadOnlyImage previewImage;
        using (var scope = session.OpenDebugSession(fixture.Root.Id, node))
        using (var invocation = scope.Capture(session.Editing.Host.BeginEdit(fixture.Root.Id, node.Id), null, _ => { },
            previewInputs: new[] { "input.image" }))
        using (var preview = invocation.CaptureInputs())
        {
            previewImage = preview.Inputs.Get<IReadOnlyImage>("input.image");
            Assert.Equal(frame, previewImage.Frame);
            Assert.Equal(new byte[] { 0, 99, 100, 255 }, previewImage.CopyPixels());
        }
        Assert.Throws<ObjectDisposedException>(() => previewImage.CopyPixels());
        using var batch = session.CaptureRunBatch(new[] { fixture.Root.Id });
        var run = batch.RunAsync(_ => { });
        UiRuntime.Await(run);
        using var result = run.Result;
        Assert.True(result.Workflows[fixture.Root.Id].Success, string.Join("；", result.Workflows[fixture.Root.Id].Errors));
        using var published = session.AcquirePublishedResult(fixture.Root.Id, node.Id);
        var binary = published!.Values!.Get<RecordValue>("field:8ca10000000040008000000000000006").Get<IReadOnlyImage>("data");
        Assert.Equal(frame, binary.Frame);
        Assert.Equal(new byte[] { 0, 0, 255, 255 }, binary.CopyPixels());
    });

 [Fact] public void Result_arrows_move_both_sources_in_one_run_and_skip_unselected_files()=>UiRuntime.Run(()=>
 {
  using var fixture=new DocumentUiFixture();
  var first=fixture.Add();
  var second=fixture.Add("ImageLoad",200,250);
  var blob=fixture.Add("BlobAnalysis",430,140);
  var a=new[]{fixture.Image(150,6),fixture.Image(160,6),fixture.Image(170,6)};
  var b=new[]{fixture.Image(180,6),fixture.Image(190,6)};
  fixture.ConfigureImage(first,a);
  fixture.ConfigureImage(second,b);
  fixture.Connect(first,blob);
  fixture.Fields(first,("local.run-mode",new ConstantSetting("selected")),
   ("local.selected",new ConstantSetting(new[]{a[0],a[2]})),("local.auto-switch",new ConstantSetting(false)));
  fixture.Fields(second,("local.auto-switch",new ConstantSetting(false)));
  fixture.Main.SelectedNode=first;
  UiRuntime.Pump();
  string ReadFile(string node)
  {
   using var reader=fixture.Session.AcquirePublishedResult(fixture.Root.Id,node);
   Assert.NotNull(reader);
   Assert.True(reader.Snapshot.IsSuccess);
   return reader.Values!.Get<string>("output.file-name");
  }
  fixture.Run();
  Assert.Equal(Path.GetFileName(a[0]),ReadFile(first.Id));
  Assert.Equal(Path.GetFileName(b[0]),ReadFile(second.Id));
  var history=fixture.Session.HistoryEntries.Count;
  var run=Assert.IsType<SunEyeVision.UI.Services.Workflow.WorkflowExecutionSnapshot>(
   UiRuntime.Call(fixture.Main,"GetFormalExecutionSnapshot",fixture.Root.Id));
  var count=run.CompletedRunCount;
  UiRuntime.Await(fixture.Main.NavigateLocalImagesAsync(1));
  Assert.Equal(count+1,run.CompletedRunCount);
  Assert.Equal(Path.GetFileName(a[2]),ReadFile(first.Id));
  Assert.Equal(Path.GetFileName(b[1]),ReadFile(second.Id));
  using (var result=fixture.Session.AcquirePublishedResult(fixture.Root.Id,blob.Id))
  {
   Assert.NotNull(result);
   Assert.True(result.Snapshot.IsSuccess);
  }
  Assert.Equal(2,fixture.Main.ActiveImageSourceBinding!.Strip.DisplayIndex);
  UiRuntime.Await(fixture.Main.NavigateLocalImagesAsync(-1));
  Assert.Equal(Path.GetFileName(a[0]),ReadFile(first.Id));
  Assert.Equal(Path.GetFileName(b[0]),ReadFile(second.Id));
  Assert.Equal(history,fixture.Session.HistoryEntries.Count);
  Assert.Equal(0L,first.Configuration.Fields.Get<long>("local.start"));
  Assert.Equal(0L,second.Configuration.Fields.Get<long>("local.start"));
 });

 [Fact] public void A_retired_thumbnail_request_cannot_change_the_next_formal_read()=>UiRuntime.Run(()=>
 {
  using var fixture=new DocumentUiFixture();
  var source=fixture.Add();
  var first=fixture.Image(150,6);
  var last=fixture.Image(180,6);
  fixture.ConfigureImage(source,first,last);
  fixture.Fields(source,("local.auto-switch",new ConstantSetting(false)));
  fixture.Main.SelectedNode=source;
  UiRuntime.Pump();
  var binding=fixture.Main.ActiveImageSourceBinding!;
  var strip=binding.Strip;
  var stale=new ImageSourceActivation(fixture.Session,fixture.Root,source,binding.Source,binding.Source.Images[1],1,strip.Version);
  UiRuntime.Await(strip.RequestAsync(new(ImageSourceStripRequestKind.SetRunSelection,strip.Version,itemIds:new[]{strip.Items[1].Id}),CancellationToken.None));
  Assert.False(binding.OwnsNavigation(stale));
  var request=Assert.IsAssignableFrom<Task>(UiRuntime.Call(fixture.Main,"ActivateFormalImageSourceAsync",stale));
  Assert.Throws<InvalidOperationException>(()=>UiRuntime.Await(request));
  fixture.Run();
  using var reader=fixture.Session.AcquirePublishedResult(fixture.Root.Id,source.Id);
  Assert.Equal(first,reader!.Values!.Get<string>("output.file"));
 });

 [Fact] public void Import_appends_without_selecting_and_the_first_read_reaches_blob()=>UiRuntime.Run(()=>
 {
  using var fixture=new DocumentUiFixture();
  var source=fixture.Add();
  var blob=fixture.Add("BlobAnalysis",350,240);
  fixture.Connect(source,blob);
  fixture.Main.SelectedNode=source;
  UiRuntime.Pump();
  string Image(string name, byte level)
  {
   var file=Path.Combine(fixture.DirectoryPath,name);
   var pixels=Enumerable.Repeat(level,36).ToArray();
   var bitmap=BitmapSource.Create(6,6,96,96,PixelFormats.Gray8,null,pixels,6);
   var encoder=new PngBitmapEncoder();
   encoder.Frames.Add(BitmapFrame.Create(bitmap));
   using var stream=File.Create(file);
   encoder.Save(stream);
   return file;
  }
  var strip=fixture.Main.ActiveImageSourceBinding!.Strip;
  void Import(params string[] paths)
  {
   UiRuntime.Await(strip.RequestAsync(new(ImageSourceStripRequestKind.ImportFiles,strip.Version,paths:paths),CancellationToken.None));
   UiRuntime.Pump();
  }
  Import(Image("first.png",180),Image("second.png",200));
  Assert.Equal(2,strip.Items.Count);
  Assert.Equal(-1,strip.ConfiguredStartIndex);
  Assert.Equal(-1,strip.DisplayIndex);
  Assert.Equal(-1L,source.Configuration.Fields.Get<long>("local.start"));
  Assert.Null(fixture.Session.AcquirePublishedResult(fixture.Root.Id,source.Id));
  Assert.Null(fixture.Session.AcquirePublishedResult(fixture.Root.Id,blob.Id));

  fixture.Run();
  using (var published=fixture.Session.AcquirePublishedResult(fixture.Root.Id,source.Id))
  {
   Assert.NotNull(published);
   Assert.True(published.Snapshot.IsSuccess);
   Assert.Equal("first.png",published.Values!.Get<string>("output.file-name"));
  }
  using (var published=fixture.Session.AcquirePublishedResult(fixture.Root.Id,blob.Id))
  {
   Assert.NotNull(published);
   Assert.True(published.Snapshot.IsSuccess);
  }
  Assert.Equal(0,strip.DisplayIndex);
  Import(Image("third.png",220));
  Assert.Equal(3,strip.Items.Count);
  Assert.Equal(0,strip.DisplayIndex);
  Assert.Equal(-1L,source.Configuration.Fields.Get<long>("local.start"));
 });

 [Fact] public void Camera_selector_uses_the_production_settings_entry_and_runtime_identity()=>UiRuntime.Run(()=>
 {
  var device=new SunEyeVision.Domain.Solutions.Device {Id="solution-record",Settings=new(){{"DeviceId","runtime-camera"}}};
  Assert.Equal("runtime-camera",CameraManagerViewModel.RuntimeDeviceId(device));
  using var f=new DocumentUiFixture();var source=f.Add();var node=f.Add(x:350,y:240);
  f.Connect(source,node);
  var timing=f.Session.GetNodeSchema(f.Root.Id,source.Id)!.Outputs.Single(port=>port.Id=="output.module-time-ms");
  var exposure=new ReferenceSetting(new OutputReference(source.Id,timing.Id,timing.Type,scopeId:f.Root.Id,storage:timing.Storage));
  f.Fields(node,("source.kind",new ConstantSetting("camera")),("camera.device",new ConstantSetting("removed-camera")),
   ("camera.exposure-time",exposure));
  f.Main.SelectedNode=node;f.Main.OpenDebugWindowCommand.Execute(node);UiRuntime.Pump();
  var window=System.Windows.Application.Current.Windows.OfType<RuntimeToolEditorWindow>().Single();
  UiRuntime.AwaitEditorReady(window);
  var selector=Assert.Single(UiRuntime.Visuals<ImageSourceInputView>(window.Editor));
  Assert.True(selector.IsCamera);
  Assert.True(selector.OpenCameraButton.IsEnabled);
  Assert.Contains("设备已移除",selector.CameraStatus.Text);
  var selected=Assert.IsAssignableFrom<object>(selector.CameraPicker.SelectedItem);
  Assert.IsNotType<System.Windows.Controls.ComboBoxItem>(selected);
  Assert.Equal("removed-camera",selector.CameraPicker.SelectedValue);
  Assert.Same(selector.FindResource("Shared.ImageButtonStyle"),selector.OpenCameraButton.Style);
  var view=Assert.Single(UiRuntime.Visuals<DeclaredSettingsView>(window.Editor));
  var page=Assert.IsType<DeclaredFieldsView>(view.Content);
  var cameraReferences=UiRuntime.Visuals<DeclaredFieldRow>(window.Editor)
   .Where(row=>row.FieldId is "camera.exposure-time" or "camera.gain").ToArray();
  Assert.Equal(2,cameraReferences.Length);
  Assert.All(cameraReferences,row=>Assert.True(row.IsVisible));
  Assert.IsType<ReferenceSetting>(cameraReferences.Single(row=>row.FieldId=="camera.exposure-time").Setting);
  Assert.IsType<UnsetSetting>(cameraReferences.Single(row=>row.FieldId=="camera.gain").Setting);
  var context=Assert.IsType<ProductionEditorContext>(ToolEditorCommit.GetDraftContext(window.Editor));
  Assert.True(view.TrySetFields(new Dictionary<string,ParameterSetting> { ["sequence.initial"] = new ConstantSetting(11L) }));
  UiRuntime.Pump();
  Assert.Same(selected,selector.CameraPicker.SelectedItem);
  selector.CameraPicker.SelectedIndex=0;
  UiRuntime.Pump();
  Assert.Same(page,view.Content);
  Assert.Single(selector.CameraPicker.Items);
  Assert.Equal(string.Empty,context.PreparedDraft.Configuration.Fields.Get<string>("camera.device"));
  Assert.IsType<ReferenceSetting>(context.PreparedDraft.Configuration.Fields.Read("camera.exposure-time"));
  Assert.Equal(11L,context.PreparedDraft.Configuration.Fields.Get<long>("sequence.initial"));
  Assert.Equal("removed-camera",node.Configuration.Fields.Get<string>("camera.device"));
  Assert.True(view.TrySetFields(new Dictionary<string,ParameterSetting> { ["source.kind"] = new ConstantSetting("local") }));
  UiRuntime.Pump();
  Assert.All(cameraReferences,row=>Assert.False(row.IsVisible));
  Assert.Same(page,view.Content);
  Assert.True(view.TrySetFields(new Dictionary<string,ParameterSetting> { ["source.kind"] = new ConstantSetting("sdk") }));
  UiRuntime.Pump();
  Assert.False(selector.IsCamera);
  Assert.All(cameraReferences,row=>Assert.False(row.IsVisible));
  Assert.Equal("sdk",context.PreparedDraft.Configuration.Fields.Get<string>("source.kind"));
  Assert.Equal("camera",node.Configuration.Fields.Get<string>("source.kind"));
  UiRuntime.Await(window.CloseAndDrainAsync());
  Assert.Null(window.Editor.OpenCameraSettings);
 });
 [Fact] public void Source_strip_start_edit_undo_and_stale_request_use_one_actual_session()=>UiRuntime.Run(()=>
 {
  using var f=new DocumentUiFixture();var node=f.Add();f.ConfigureImage(node,f.Image(11),f.Image(22));f.Main.SelectedNode=node;UiRuntime.Pump();
  var binding=f.Main.ActiveImageSourceBinding;Assert.NotNull(binding);var strip=binding.Strip;Assert.Equal(2,strip.Items.Count);Assert.True(strip.CanEdit);
  Assert.True(f.Main.ImageResult.HasActiveImageSource,"选中图像源节点后结果区图像源条应显示（含0张图）。");
  var version=strip.Version;var request=new ImageSourceStripRequest(ImageSourceStripRequestKind.SelectStart,version,strip.Items[1].Id);
  UiRuntime.Await(strip.RequestAsync(request,CancellationToken.None));Assert.Equal(1L,Assert.IsType<ConstantSetting>(node.Configuration.Fields.Read("local.start")).Value);
  f.Session.Undo();UiRuntime.Pump();Assert.Equal(0L,Assert.IsType<ConstantSetting>(node.Configuration.Fields.Read("local.start")).Value);
  Assert.Throws<InvalidOperationException>(()=>UiRuntime.Await(strip.RequestAsync(request,CancellationToken.None)));
  f.Main.SelectedNode=null;UiRuntime.Pump();Assert.Null(f.Main.ActiveImageSourceBinding);Assert.False(f.Main.ImageResult.HasActiveImageSource);Assert.False(strip.CanEdit);Assert.False(strip.CanNavigate);
 });
 [Fact] public void Source_strip_tracks_only_the_viewport_without_changing_the_formal_start()=>UiRuntime.Run(()=>
 {
  using var fixture = new DocumentUiFixture();
  var node = fixture.Add();
  fixture.ConfigureImage(node, Enumerable.Range(0, 96).Select(index => fixture.Image((byte)index)).ToArray());
  fixture.Main.SelectedNode = node;
  UiRuntime.Pump();
  var source = fixture.Results.ImagePreviewContent;
  var list = Assert.IsType<System.Windows.Controls.ListBox>(source.FindName("lst"));
  var session = Assert.IsType<ImageSourceStripSession>(source.Session);
  HashSet<string> Visible() => Assert.IsType<HashSet<string>>(UiRuntime.Field<HashSet<string>>(session, "_visibleIds"));
  Assert.Contains(session.Items[0].Id, Visible());
  Assert.DoesNotContain(session.Items[^1].Id, Visible());
  var version = session.Version;
  list.ScrollIntoView(list.Items[^1]);
  list.UpdateLayout();
  UiRuntime.Pump();
  Assert.Contains(session.Items[^1].Id, Visible());
  Assert.DoesNotContain(session.Items[0].Id, Visible());
  source.IsExpanded = false;
  UiRuntime.Pump();
  Assert.Empty(Visible());
  Assert.Equal(version, session.Version);
  Assert.Equal(0, session.ConfiguredStartIndex);
  Assert.Null(fixture.Session.AcquirePublishedResult(fixture.Root.Id, node.Id));
 });
 [Fact] public void Variable_window_private_candidate_commit_and_temporary_gate_keep_the_same_participant()=>UiRuntime.Run(()=>
 {
  using var f=new DocumentUiFixture();bool current=true,editable=true;
  var vm=new GlobalVariableManagerViewModel(f.Session,()=>current,()=>editable);var window=new GlobalVariableManagerDialog(vm){Owner=f.Host,ShowInTaskbar=false};window.Show();UiRuntime.Pump();
  try
  {
   vm.SelectedGroup=vm.Groups.First();Assert.True(vm.AddVariableCommand.CanExecute(null));vm.AddVariableCommand.Execute(null);var candidate=Assert.Single(vm.CurrentVariables);candidate.Value="正式值";
   Assert.Empty(f.Session.Document.GlobalVariables);Assert.True(vm.HasPendingChanges);
   editable=false;vm.RefreshAvailability();Assert.True(vm.IsValid);Assert.False(vm.CanEdit);Assert.False(vm.AddVariableCommand.CanExecute(null));
   editable=true;vm.RefreshAvailability();f.Session.Editing.Apply(vm.ScopeId);Assert.Equal("正式值",Assert.Single(f.Session.Document.GlobalVariables).Value);Assert.True(vm.IsValid);
   Assert.False(vm.ImportCommand.CanExecute(null));Assert.False(vm.ExportCommand.CanExecute(null));Assert.False(vm.OpenTargetParameterCommand.CanExecute(null));
  }
  finally{window.Close();UiRuntime.Pump();}
  Assert.False(vm.IsValid);current=false;
 });
}
