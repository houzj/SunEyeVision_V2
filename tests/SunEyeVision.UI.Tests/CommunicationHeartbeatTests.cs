using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using SunEyeVision.DeviceDriver.Communications;
using SunEyeVision.UI.Features.CommunicationManager;
using SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters;
using SunEyeVision.UI.Services.Communication;
using SunEyeVision.Workflow;
using Xunit;

namespace SunEyeVision.UI.Tests;

public sealed class CommunicationHeartbeatTests
{
    [Fact]
    public void Native_type_is_locked_unknown_type_blocks_and_failures_are_visible()
    {
        var row = new CommunicationHeartbeatViewModel { DataType = "int", TargetLabel = "UA.test3" };
        Assert.True(row.ConfigureType(true, "string"));
        Assert.True(row.TypeLocked); Assert.True(row.TypeResolved);
        Assert.False(row.ShowTypeSelector);
        row.DataType = "int";
        Assert.Equal("string", row.DataType);
        Assert.Equal("string", row.Capture().DataType);
        row.Status = "OPC UA 数据点为 String，输出为 Int32，类型不匹配。";
        Assert.True(row.HasStatusIssue);
        Assert.Contains("UA.test3", row.StatusMessage);
        row.Status = "发送成功";
        Assert.False(row.HasStatusIssue);
        row.ConfigureType(true, null);
        Assert.False(row.TypeResolved);
        Assert.Equal("未知类型", row.TypeLabel);
        row.ConfigureType(false, null);
        row.DataTypes = new[] { "int", "float", "string" };
        row.DataType = "int";
        Assert.True(row.ShowTypeSelector);
        Assert.Equal("int", row.DataType);
    }

    [Fact]
    public void Device_popup_contains_configured_targets_and_selects_through_tree_event()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            CommunicationManagerViewModel? manager = null;
            System.Windows.Window? window = null;
            try
            {
                var directory = Path.Combine(Path.GetTempPath(), "SunEyeHeartbeatPicker", Guid.NewGuid().ToString("N"));
                ServiceInitializer.InitializeSolutionManager(directory);
                var solutions = new SolutionManager(directory);
                var raw = new Communication { Name = "TCP", ProtocolKey = "tcp-client", IsEnabled = false };
                var modbus = new Communication { Name = "PLC", ProtocolKey = "modbus", IsEnabled = false };
                modbus.ModbusAddressPoints.Add(new() { Name = "Address", Count = 1 });
                var opc = new Communication { Name = "UA", ProtocolKey = "opcua", IsEnabled = false };
                opc.OpcUaDataPoints.Add(new() { TagName = "Heartbeat" });
                var solution = new Solution { Communications = new() { raw, modbus, opc } };
                solutions.SetCurrentSolution(solution);
                manager = new CommunicationManagerViewModel(solutions);
                manager.AddHeartbeatCommand.Execute(null);
                var row = Assert.Single(manager.Heartbeats);
                Assert.Equal(4, row.Targets.Count);
                var view = new CommunicationHeartbeatsView { DataContext = manager };
                window = new System.Windows.Window { Content = view, Width = 1200, Height = 650,
                    Left = -10000, Top = -10000, ShowInTaskbar = false, ShowActivated = false };
                window.Show();
                System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                var grid = (System.Windows.Controls.DataGrid)view.FindName("HeartbeatGrid");
                var host = grid.Columns[1].GetCellContent(row);
                var cell = FindVisual<System.Windows.Controls.Grid>(host!)!;
                var button = cell.Children.OfType<System.Windows.Controls.Primitives.ToggleButton>().Single();
                var popup = cell.Children.OfType<System.Windows.Controls.Primitives.Popup>().Single();
                var transitions = new List<string>();
                row.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(row.IsTargetOpen)) transitions.Add("row=" + row.IsTargetOpen); };
                popup.Opened += (_, _) => transitions.Add("opened");
                popup.Closed += (_, _) => transitions.Add("closed");
                button.SetCurrentValue(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty, true);
                System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                Assert.True(popup.IsOpen, $"row={row.IsTargetOpen}; checked={button.IsChecked}; loaded={button.IsLoaded}; target={popup.PlacementTarget}; events={string.Join(",", transitions)}");
                var tree = (System.Windows.Controls.TreeView)((System.Windows.Controls.Border)popup.Child).Child;
                Assert.Same(row, tree.DataContext);
                Assert.Same(row.Targets, tree.ItemsSource);
                Assert.Equal(4, tree.Items.Count);
                var target = row.Targets.Single(item => item.DeviceId == raw.Id);
                var item = Assert.IsType<System.Windows.Controls.TreeViewItem>(tree.ItemContainerGenerator.ContainerFromItem(target));
                item.ApplyTemplate();
                Assert.Equal(System.Windows.Visibility.Hidden, ((System.Windows.UIElement)item.Template.FindName("Expander", item)).Visibility);
                ClickHeader(item);
                System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                Assert.Equal(raw.Id, row.DeviceId);
                Assert.Equal(raw.Id, Assert.Single(solution.CommunicationHeartbeats).DeviceId);
                Assert.Contains("TCP", row.TargetLabel);
                Assert.False(popup.IsOpen);

                foreach (var nativeDevice in new[] { modbus, opc })
                {
                    button.SetCurrentValue(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty, true);
                    System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                    Assert.True(popup.IsOpen);
                    var group = row.Targets.Single(node => node.DeviceId == nativeDevice.Id);
                    var parent = Assert.IsType<System.Windows.Controls.TreeViewItem>(tree.ItemContainerGenerator.ContainerFromItem(group));
                    parent.ApplyTemplate();
                    var presenter = (System.Windows.Controls.ItemsPresenter)parent.Template.FindName("ChildrenPresenter", parent);
                    Assert.Equal(System.Windows.Visibility.Collapsed, presenter.Visibility);
                    Assert.Equal(1, System.Windows.Controls.Grid.GetColumn(presenter));
                    Assert.True(((System.Windows.FrameworkElement)parent.Template.FindName("HeaderBorder", parent)).MinHeight >= 32);
                    var expander = (System.Windows.Controls.Primitives.ToggleButton)parent.Template.FindName("Expander", parent);
                    Assert.True(expander.Width >= 28 && expander.Height >= 32);
                    expander.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0, System.Windows.Input.MouseButton.Left)
                        { RoutedEvent = System.Windows.Input.Mouse.PreviewMouseDownEvent });
                    Assert.True(parent.IsExpanded);
                    Assert.True(popup.IsOpen);
                    Assert.NotEqual(nativeDevice.Id, row.DeviceId);
                    System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                    var leaf = Assert.Single(group.Children);
                    Assert.DoesNotContain(nativeDevice.Name, leaf.Name);
                    var child = Assert.IsType<System.Windows.Controls.TreeViewItem>(parent.ItemContainerGenerator.ContainerFromItem(leaf));
                    child.ApplyTemplate();
                    Assert.Equal(System.Windows.Visibility.Visible, presenter.Visibility);
                    Assert.Equal(System.Windows.Visibility.Hidden, ((System.Windows.UIElement)child.Template.FindName("Expander", child)).Visibility);
                    ClickHeader(parent);
                    Assert.Equal(System.Windows.Visibility.Collapsed, presenter.Visibility);
                    ClickHeader(parent);
                    Assert.True(parent.IsExpanded);
                    child.SetCurrentValue(System.Windows.Controls.TreeViewItem.IsSelectedProperty, true);
                    Assert.True(popup.IsOpen);
                    Assert.NotEqual(nativeDevice.Id, row.DeviceId);
                    ClickHeader(child);
                    System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                    Assert.Equal(nativeDevice.Id, row.DeviceId);
                    Assert.Equal(leaf.TargetId, row.TargetId);
                    Assert.Equal(leaf.TargetId, Assert.Single(solution.CommunicationHeartbeats).TargetId);
                    Assert.Contains(nativeDevice.Name, row.TargetLabel);
                    Assert.False(popup.IsOpen);
                }
                Assert.Equal("单数据", row.ModeDisplay);
                var configChanges = 0;
                row.ConfigurationChanged += (_, _) => configChanges++;
                button.SetCurrentValue(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty, true);
                System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                var selectedGroup = row.Targets.Single(n => n.DeviceId == opc.Id);
                var selectedParent = (System.Windows.Controls.TreeViewItem)tree.ItemContainerGenerator.ContainerFromItem(selectedGroup);
                Assert.True(selectedParent.IsExpanded);
                var selectedChild = (System.Windows.Controls.TreeViewItem)selectedParent.ItemContainerGenerator.ContainerFromItem(selectedGroup.Children.Single());
                Assert.True(selectedChild.IsSelected);
                Assert.Equal(0, configChanges);
                selectedChild.ApplyTemplate();
                ((System.Windows.UIElement)selectedChild.Template.FindName("HeaderBorder", selectedChild)).RaiseEvent(
                    new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0, System.Windows.Input.MouseButton.Right)
                    { RoutedEvent = System.Windows.Input.Mouse.PreviewMouseDownEvent });
                Assert.True(popup.IsOpen);
                Assert.Equal(0, configChanges);
                tree.RaiseEvent(new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice,
                    System.Windows.PresentationSource.FromVisual(tree), 0, System.Windows.Input.Key.Enter)
                    { RoutedEvent = System.Windows.Input.Keyboard.PreviewKeyDownEvent });
                Assert.False(popup.IsOpen);
                Assert.Equal(1, configChanges);
                button.SetCurrentValue(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty, true);
                System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                var rawAgain = (System.Windows.Controls.TreeViewItem)tree.ItemContainerGenerator.ContainerFromItem(row.Targets.Single(n => n.DeviceId == raw.Id));
                rawAgain.SetCurrentValue(System.Windows.Controls.TreeViewItem.IsSelectedProperty, true);
                tree.RaiseEvent(new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice,
                    System.Windows.PresentationSource.FromVisual(tree), 0, System.Windows.Input.Key.Escape)
                    { RoutedEvent = System.Windows.Input.Keyboard.PreviewKeyDownEvent });
                Assert.False(popup.IsOpen);
                Assert.Equal(opc.Id, row.DeviceId);
                Assert.Equal(1, configChanges);
                row.ModeDisplay = "多数据";
                Assert.Equal("Alternate", row.Capture().Mode);
                var contentHost = grid.Columns[3].GetCellContent(row)!;
                Assert.Equal(2, Descendants(contentHost).OfType<System.Windows.Controls.TextBox>().Count());
                Assert.DoesNotContain(Descendants(contentHost).OfType<System.Windows.Controls.TextBlock>(), text => text.Text is "值 A" or "值 B");
                Assert.True(row.TypeLocked);
                Assert.False(row.TypeResolved); // no live OPC metadata in this isolated UI test
                var nativeToggle = FindVisual<SunEyeVision.Plugin.SDK.UI.Controls.ToggleSwitch>(grid.Columns[5].GetCellContent(row)!)!;
                nativeToggle.IsChecked = true;
                Assert.False(row.Enabled);
                Assert.True(row.HasStatusIssue);
                Assert.Contains("节点类型未确认", row.Status);
                row.Enabled = true;
                manager.SelectHeartbeatTarget(row, row.Targets.Single(node => node.DeviceId == ""));
                Assert.False(row.Enabled);
                Assert.Equal("", row.DeviceId);
            }
            catch (Exception ex) { failure = ex; }
            finally { window?.Close(); manager?.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.IsBackground = true; thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(20)), "心跳设备弹层测试超时");
        if (failure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private static void ClickHeader(System.Windows.Controls.TreeViewItem item)
    {
        item.ApplyTemplate();
        var header = (System.Windows.UIElement)item.Template.FindName("HeaderBorder", item);
        header.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0, System.Windows.Input.MouseButton.Left)
            { RoutedEvent = System.Windows.Input.Mouse.PreviewMouseDownEvent });
    }
    private static IEnumerable<System.Windows.DependencyObject> Descendants(System.Windows.DependencyObject parent)
    {
        yield return parent;
        for (var i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            foreach (var child in Descendants(System.Windows.Media.VisualTreeHelper.GetChild(parent, i))) yield return child;
    }

    private static T? FindVisual<T>(System.Windows.DependencyObject parent) where T : System.Windows.DependencyObject
    {
        if (parent is T match) return match;
        for (var i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            if (FindVisual<T>(System.Windows.Media.VisualTreeHelper.GetChild(parent, i)) is { } child) return child;
        return null;
    }

    [Fact]
    public void Editor_has_seven_columns_preserves_second_value_and_syncs_shared_configuration()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            CommunicationManagerViewModel? manager = null;
            try
            {
                var directory = Path.Combine(Path.GetTempPath(), "SunEyeHeartbeatUi", Guid.NewGuid().ToString("N"));
                ServiceInitializer.InitializeSolutionManager(directory);
                var solutions = new SolutionManager(directory);
                var device = new Communication { ProtocolKey = "modbus", IsEnabled = false };
                var point = new ModbusAddressPoint { Name = "Address", StartAddress = 40, Count = 2 };
                device.ModbusAddressPoints.Add(point);
                var solution = new Solution { Communications = new() { device } };
                solutions.SetCurrentSolution(solution);
                manager = new CommunicationManagerViewModel(solutions);
                manager.AddHeartbeatCommand.Execute(null);
                var row = Assert.Single(manager.Heartbeats);
                var target = Assert.Single(Assert.Single(row.Targets.Where(item => item.Children.Count > 0)).Children);
                manager.SelectHeartbeatTarget(row, target);
                Assert.Equal(device.Id, row.DeviceId);
                Assert.Equal(point.Id, row.TargetId);
                Assert.Equal("int", row.DataType);
                Assert.Equal(new[] { "int", "float", "string" }, row.DataTypes);
                row.ContentA = "0"; row.ContentB = "1"; row.Mode = "Alternate";
                var view = new CommunicationHeartbeatsView { DataContext = manager };
                view.Measure(new System.Windows.Size(1150, 700)); view.Arrange(new System.Windows.Rect(0, 0, 1150, 700));
                System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                Assert.Equal(7, ((System.Windows.Controls.DataGrid)view.FindName("HeartbeatGrid")).Columns.Count);
                var grid = (System.Windows.Controls.DataGrid)view.FindName("HeartbeatGrid");
                var delete = FindVisual<System.Windows.Controls.Button>(grid.Columns[6].GetCellContent(row)!)!;
                Assert.Equal(new System.Windows.Thickness(0), delete.Padding);
                Assert.Equal("×", delete.Content);
                Assert.Equal(System.Windows.HorizontalAlignment.Center, delete.HorizontalAlignment);
                row.Status = "测试写入失败：类型不匹配";
                view.UpdateLayout();
                Assert.Contains(Descendants(view).OfType<System.Windows.Controls.TextBlock>(),
                    text => text.Visibility == System.Windows.Visibility.Visible && text.Text.Contains("测试写入失败"));
                var toggleHost = grid.Columns[5].GetCellContent(row);
                var toggle = FindToggle(toggleHost!);
                Assert.NotNull(toggle);
                toggle!.IsChecked = true;
                Assert.True(row.Enabled);
                Assert.True(System.Windows.Data.BindingOperations.IsDataBound(toggle,
                    SunEyeVision.Plugin.SDK.UI.Controls.ToggleSwitch.IsCheckedProperty));
                toggle.IsChecked = false;
                Assert.False(row.Enabled);
                Assert.False(Assert.Single(solution.CommunicationHeartbeats).Enabled);
                Assert.True(row.IsAlternate);
                row.Mode = "Single";
                Assert.Equal("1", row.ContentB);
                Assert.Equal("Single", Assert.Single(solution.CommunicationHeartbeats).Mode);
                Assert.False(manager.HasUnsavedChanges);
                manager.RemoveHeartbeat(row);
                Assert.Empty(solution.CommunicationHeartbeats);
            }
            catch (Exception ex) { failure = ex; }
            finally { manager?.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.IsBackground = true; thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(15)), "心跳页面初始化超时");
        if (failure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }

    [Fact]
    public async Task Native_heartbeat_writes_selected_content_at_bound_modbus_address()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var device = new Communication { ProtocolKey = "modbus", ModbusMode = "TcpClient", UnitId = 7,
            Host = "127.0.0.1", Port = ((IPEndPoint)listener.LocalEndpoint).Port, IsEnabled = true };
        var point = new ModbusAddressPoint { StartAddress = 40, Count = 2, IntBits = "16bit" };
        device.ModbusAddressPoints.Add(point);
        await using var composition = new CommunicationRuntimeComposition();
        await composition.Runtime.ConnectAsync(CommunicationDeviceSnapshotFactory.Create(device), timeout.Token);
        using var peer = await listener.AcceptTcpClientAsync(timeout.Token);
        await using var service = new CommunicationHeartbeatService(composition.Runtime);
        var job = Assert.Single(service.CreateJobs(new Solution { Communications = new() { device },
            CommunicationHeartbeats = new() { new() { DeviceId = device.Id, TargetId = point.Id, Mode = "Alternate",
                DataType = "int", ContentA = "10", ContentB = "20", Enabled = true } } }));
        var sending = job.SendAsync(true, timeout.Token);
        var stream = peer.GetStream();
        var header = new byte[7]; await stream.ReadExactlyAsync(header, timeout.Token);
        var request = new byte[System.Buffers.Binary.BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(4, 2)) - 1];
        await stream.ReadExactlyAsync(request, timeout.Token);
        Assert.Equal(7, header[6]); Assert.Equal(16, request[0]);
        Assert.Equal(40, System.Buffers.Binary.BinaryPrimitives.ReadUInt16BigEndian(request.AsSpan(1, 2)));
        Assert.Equal(1, System.Buffers.Binary.BinaryPrimitives.ReadUInt16BigEndian(request.AsSpan(3, 2)));
        Assert.Equal(new byte[] { 0, 20 }, request[6..]);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(4, 2), 6);
        await stream.WriteAsync(header.Concat(request.Take(5)).ToArray(), timeout.Token);
        Assert.True(await sending);
    }

    [Fact]
    public async Task Invalid_row_does_not_prevent_other_valid_rows_from_running()
    {
        await using var composition = new CommunicationRuntimeComposition();
        await using var service = new CommunicationHeartbeatService(composition.Runtime);
        var device = new Communication { ProtocolKey = "tcp-client" };
        var invalid = new CommunicationHeartbeatDefinition { DeviceId = "missing", Enabled = true };
        var valid = new CommunicationHeartbeatDefinition { DeviceId = device.Id, Enabled = true, ContentA = "alive" };
        var errors = new List<Guid>();
        service.Runner.StatusChanged += (id, _) => errors.Add(id);
        var jobs = service.CreateJobs(new Solution { Communications = new() { device },
            CommunicationHeartbeats = new() { invalid, valid } });
        Assert.Equal(valid.Id, Assert.Single(jobs).Id);
        Assert.Contains(invalid.Id, errors);
    }

    private static SunEyeVision.Plugin.SDK.UI.Controls.ToggleSwitch? FindToggle(System.Windows.DependencyObject parent)
    {
        if (parent is SunEyeVision.Plugin.SDK.UI.Controls.ToggleSwitch toggle) return toggle;
        for (var i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            if (FindToggle(System.Windows.Media.VisualTreeHelper.GetChild(parent, i)) is { } result) return result;
        return null;
    }

    [Fact]
    public async Task Alternate_sends_one_value_per_tick_and_stop_prevents_future_sends()
    {
        await using var runner = new CommunicationHeartbeatRunner();
        var values = new ConcurrentQueue<bool>();
        var received = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        runner.Update(new[] { new CommunicationHeartbeatJob(Guid.NewGuid(), "device/point", 15, true,
            (second, token) => { values.Enqueue(second); if (values.Count == 4) received.TrySetResult(); return Task.FromResult(true); }) });
        await received.Task.WaitAsync(TimeSpan.FromSeconds(3));
        await runner.StopAsync();
        Assert.Equal(new[] { false, true, false, true }, values.Take(4).ToArray());
        var count = values.Count;
        await Task.Delay(60);
        Assert.Equal(count, values.Count);
    }

    [Fact]
    public async Task Failed_or_disconnected_send_does_not_advance_alternation()
    {
        await using var runner = new CommunicationHeartbeatRunner();
        var values = new ConcurrentQueue<bool>();
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        runner.Update(new[] { new CommunicationHeartbeatJob(Guid.NewGuid(), "device/point", 10, true, (second, _) =>
        {
            values.Enqueue(second);
            if (values.Count == 3) done.TrySetResult();
            return Task.FromResult(values.Count != 1);
        }) });
        await done.Task.WaitAsync(TimeSpan.FromSeconds(3));
        await runner.StopAsync();
        Assert.Equal(new[] { false, false, true }, values.Take(3).ToArray());
    }

    [Fact]
    public async Task Updating_waits_for_old_inflight_send_before_starting_replacement()
    {
        await using var runner = new CommunicationHeartbeatRunner();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var newEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var id = Guid.NewGuid();
        runner.Update(new[] { new CommunicationHeartbeatJob(id, "d/a", 10, false, async (_, _) =>
        { entered.TrySetResult(); await release.Task; return true; }) });
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        runner.Update(new[] { new CommunicationHeartbeatJob(id, "d/a", 10, false, (_, _) =>
        { newEntered.TrySetResult(); return Task.FromResult(true); }) });
        try { await Task.Delay(40); Assert.False(newEntered.Task.IsCompleted); }
        finally { release.TrySetResult(); }
        await newEntered.Task.WaitAsync(TimeSpan.FromSeconds(3));
    }

    [Fact]
    public async Task Duplicate_targets_and_invalid_interval_are_rejected()
    {
        await using var runner = new CommunicationHeartbeatRunner();
        var job = new CommunicationHeartbeatJob(Guid.NewGuid(), "d/a", 10, false, (_, _) => Task.FromResult(true));
        Assert.Throws<ArgumentException>(() => runner.Update(new[] { job, job with { Id = Guid.NewGuid() } }));
        Assert.Throws<ArgumentOutOfRangeException>(() => runner.Update(new[] { job with { IntervalMs = 0 } }));
    }

    [Fact]
    public async Task Application_sends_heartbeat_without_manager_window_and_stops_on_solution_close()
    {
        var directory = Path.Combine(Path.GetTempPath(), "SunEyeHeartbeat", Guid.NewGuid().ToString("N"));
        var manager = new SolutionManager(directory);
        await using var composition = new CommunicationRuntimeComposition();
        await using var service = new CommunicationApplicationService(manager, composition);
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var device = new Communication { ProtocolKey = "tcp-client", Host = "127.0.0.1",
            Port = ((IPEndPoint)listener.LocalEndpoint).Port, IsEnabled = true };
        manager.SetCurrentSolution(new Solution { Communications = new() { device }, CommunicationHeartbeats = new()
        {
            new() { DeviceId = device.Id, Enabled = true, Mode = "Alternate", DataType = "string",
                ContentA = "A", ContentB = "B", IntervalMs = 30 }
        } });
        using var peer = await listener.AcceptTcpClientAsync().WaitAsync(TimeSpan.FromSeconds(3));
        await service.ReadyAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var bytes = new byte[4];
        await peer.GetStream().ReadExactlyAsync(bytes, timeout.Token);
        Assert.Equal("ABAB", System.Text.Encoding.UTF8.GetString(bytes));
        manager.CloseSolution();
        await service.ReadyAsync();
        Assert.False(composition.Runtime.IsConnected(device.Id));
    }

    [Fact]
    public void Configuration_roundtrips_stable_target_mode_and_native_type_without_runtime_status()
    {
        var row = new CommunicationHeartbeatViewModel
        { DeviceId = "opc", TargetId = "point", Mode = "Alternate", DataType = "Double",
            ContentA = "1.5", ContentB = "2.5", Enabled = true, Status = "发送成功" };
        var solution = new Solution { CommunicationHeartbeats = new() { row.Capture() } };
        var json = JsonSerializer.Serialize(solution, WorkflowSerializationOptions.Default);
        Assert.DoesNotContain("发送成功", json);
        var loaded = JsonSerializer.Deserialize<Solution>(json, WorkflowSerializationOptions.Default)!;
        var definition = Assert.Single(loaded.CommunicationHeartbeats);
        Assert.Equal(row.Id, definition.Id);
        Assert.Equal("point", definition.TargetId);
        Assert.Equal("Double", definition.DataType);
        Assert.Equal("Alternate", definition.Mode);
        Assert.Equal("2.5", definition.ContentB);
    }
}
