using System.Reflection;
using System.Windows.Threading;
using SunEyeVision.Application;
using SunEyeVision.Domain.Workflows;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Infrastructure.Managers.Plugin;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.PresentationProbe;
using SunEyeVision.UI.Features.WorkflowUI.Canvas;
using Xunit;

namespace SunEyeVision.CanvasProjection.Tests;

public sealed class PresentationAdmissionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Removing_node_retires_actual_wpf_view_and_undo_does_not_revive_capability(bool empty)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var toolId = empty ? "EmptyPresentationProbe" : "PresentationProbe";
                var definition = empty ? (IToolSettingsFields)new EmptyPresentationProbeDefinition() : new PresentationProbeDefinition();
                var component = new ToolComponentRegistration(new ToolComponentMetadata("presentation-probe", toolId, ToolComponentRole.Runtime, FoundationContract.Major, FoundationContract.Minor), definition.GetType());
                var directory = (RuntimeDirectorySnapshot)typeof(RuntimeDirectorySnapshot).GetMethod("Prepare", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, new object[] { new[] { component } })!;
                using var session = DocumentSession.CreateNew(directory);
                var workflow = session.GetWorkflows().Single();
                using var canvas = new DocumentCanvasProjection(session, workflow.Id);
                var node = canvas.CreateNode(toolId, new CanvasPoint(0, 0));
                var admission = session.AdmitPresentation(workflow.Id, node.Id);
                var fields = new FieldSettings(definition.SettingsSchema.Inputs, new Dictionary<string, ParameterSetting>());
                using var view = DesktopToolPresentation.CreateSettingsView(admission, fields);
                Assert.True(view.TryCapture(out _, out _));
                var element = view.View;
                canvas.Delete(new[] { Assert.Single(canvas.Nodes) }, Array.Empty<CanvasConnectionProjection>());
                Assert.False(admission.IsCurrent);
                Assert.ThrowsAny<Exception>(() => view.CaptureAcceptedFields());
                var frame = new DispatcherFrame();
                Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => frame.Continue = false));
                Dispatcher.PushFrame(frame);
                Assert.False(element.IsEnabled);
                Assert.True(canvas.Undo());
                Assert.False(admission.IsCurrent);
                Assert.True(session.AdmitPresentation(workflow.Id, node.Id).IsCurrent);
            }
            catch (Exception error) { failure = error; }
            finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "WPF 测试线程未及时退出。");
        if (failure is not null) throw new InvalidOperationException("WPF 呈现生命周期验证失败。", failure);
    }
}




