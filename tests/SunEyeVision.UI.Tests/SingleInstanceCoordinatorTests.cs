using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SunEyeVision.UI.Services;
using Xunit;

namespace SunEyeVision.UI.Tests;

public sealed class SingleInstanceCoordinatorTests
{
    [Fact]
    public async Task Preparing_transition_rejects_reentry_before_touching_services()
    {
        // Exercise just the admission guard, without constructing real services or touching user data.
        var model = (SunEyeVision.UI.Shell.Windows.MainWindowViewModel)
            System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(SunEyeVision.UI.Shell.Windows.MainWindowViewModel));
        var field = model.GetType().GetField("_transitionPreparationInProgress",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        field.SetValue(model, true);
        Assert.False(model.CanAcceptExternalActivation);
        Assert.False(await model.PrepareSolutionTransitionAsync());
        field.SetValue(model, false);
        Assert.True(model.CanAcceptExternalActivation);
    }

    [Fact]
    public void External_solution_entry_reuses_stop_and_save_transition()
    {
        var type = typeof(SunEyeVision.UI.Shell.Windows.MainWindowViewModel);
        var method = type.GetMethod("OpenExternalSolutionAsync",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var attribute = method.GetCustomAttributes(typeof(System.Runtime.CompilerServices.AsyncStateMachineAttribute), false)
            .Cast<System.Runtime.CompilerServices.AsyncStateMachineAttribute>().Single();
        var body = attribute.StateMachineType.GetMethod("MoveNext",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)!
            .GetMethodBody()!.GetILAsByteArray()!;
        var token = type.GetMethod("PrepareSolutionTransitionAsync")!.MetadataToken;
        Assert.Contains(System.Linq.Enumerable.Range(0, body.Length - 4),
            i => (body[i] == 0x28 || body[i] == 0x6f) && BitConverter.ToInt32(body, i + 1) == token);
    }

    private static string Name() => "SunEyeVision.Test.Instance." + Guid.NewGuid().ToString("N");
    private static Task<InstanceActivationResult> Accept(InstanceActivationRequest request)
        => Task.FromResult(InstanceActivationResult.Delivered);

    [Fact]
    public void Only_one_owner_even_on_same_caller_thread_and_with_external_handle()
    {
        var name = Name();
        var first = SingleInstanceCoordinator.TryStart(Accept, name);
        Assert.NotNull(first);
        using var external = Mutex.OpenExisting(@"Local\" + name);
        Assert.Null(SingleInstanceCoordinator.TryStart(Accept, name));
        first!.Dispose();
        // A stale external handle cannot prevent a new owner after the real owner exits.
        using var next = SingleInstanceCoordinator.TryStart(Accept, name);
        Assert.NotNull(next);
    }

    [Fact]
    public async Task Concurrent_launches_get_exactly_one_owner()
    {
        var name = Name();
        var owners = new ConcurrentBag<SingleInstanceCoordinator>();
        try
        {
            await Task.WhenAll(System.Linq.Enumerable.Range(0, 12).Select(_ => Task.Run(() =>
            {
                var owner = SingleInstanceCoordinator.TryStart(Accept, name);
                if (owner != null) owners.Add(owner);
            })));
            Assert.Single(owners);
        }
        finally { foreach (var owner in owners) owner.Dispose(); }
    }

    [Fact]
    public async Task Activate_and_solution_path_are_delivered_once_without_opening_files()
    {
        var name = Name();
        var received = new ConcurrentQueue<string?>();
        using var owner = SingleInstanceCoordinator.TryStart(request =>
        {
            received.Enqueue(request.SolutionPath);
            return Task.FromResult(InstanceActivationResult.Delivered);
        }, name);
        Assert.NotNull(owner);
        Assert.True((await SingleInstanceCoordinator.SendAsync(null, name)).Accepted);
        var path = @"D:\Isolated Tests\方案 1.solution";
        Assert.True((await SingleInstanceCoordinator.SendAsync(path, name)).Accepted);
        Assert.Equal(new string?[] { null, path }, received.ToArray());
    }

    [Fact]
    public async Task Closing_rejects_handoff_and_keeps_instance_exclusive()
    {
        var name = Name();
        using var owner = SingleInstanceCoordinator.TryStart(Accept, name);
        Assert.NotNull(owner);
        owner!.BeginClosing();
        var response = await SingleInstanceCoordinator.SendAsync(null, name);
        Assert.False(response.Accepted);
        Assert.Contains("退出", response.Message);
        Assert.Null(SingleInstanceCoordinator.TryStart(Accept, name));
    }

    [Fact]
    public async Task Unresponsive_instance_times_out_without_allowing_another_owner()
    {
        var name = Name();
        var pending = new TaskCompletionSource<InstanceActivationResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var owner = SingleInstanceCoordinator.TryStart(_ => pending.Task, name);
        Assert.NotNull(owner);
        var response = await SingleInstanceCoordinator.SendAsync(null, name, TimeSpan.FromMilliseconds(200));
        Assert.False(response.Accepted);
        Assert.Contains("不会启动第二个", response.Message);
        Assert.Null(SingleInstanceCoordinator.TryStart(Accept, name));
    }

    [Fact]
    public async Task Invalid_client_cannot_break_listener_or_trigger_activation()
    {
        var name = Name();
        var count = 0;
        using var owner = SingleInstanceCoordinator.TryStart(_ =>
        {
            Interlocked.Increment(ref count);
            return Task.FromResult(InstanceActivationResult.Delivered);
        }, name);
        Assert.NotNull(owner);
        using (var client = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly))
        {
            await client.ConnectAsync(2000);
            var bytes = new byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(bytes, int.MaxValue);
            await client.WriteAsync(bytes);
        }
        Assert.True((await SingleInstanceCoordinator.SendAsync(null, name)).Accepted);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Expired_request_is_not_delivered_later()
    {
        var name = Name();
        var count = 0;
        using var owner = SingleInstanceCoordinator.TryStart(_ =>
        {
            Interlocked.Increment(ref count);
            return Task.FromResult(InstanceActivationResult.Delivered);
        }, name);
        using var client = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        await client.ConnectAsync(2000);
        var payload = JsonSerializer.SerializeToUtf8Bytes(new InstanceActivationRequest(null, DateTimeOffset.UtcNow.AddMinutes(-1)));
        var header = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(header, payload.Length);
        await client.WriteAsync(header);
        await client.WriteAsync(payload);
        await client.ReadExactlyAsync(header);
        var response = new byte[BinaryPrimitives.ReadInt32LittleEndian(header)];
        await client.ReadExactlyAsync(response);
        Assert.False(JsonSerializer.Deserialize<InstanceActivationResult>(response)!.Accepted);
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task Busy_response_is_preserved_and_does_not_spawn_an_owner()
    {
        var name = Name();
        using var owner = SingleInstanceCoordinator.TryStart(_ => Task.FromResult(new InstanceActivationResult(false, "正在切换方案")), name);
        var result = await SingleInstanceCoordinator.SendAsync(@"D:\test.solution", name);
        Assert.False(result.Accepted);
        Assert.Equal("正在切换方案", result.Message);
        Assert.Null(SingleInstanceCoordinator.TryStart(Accept, name));
    }
}
