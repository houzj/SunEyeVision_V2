# WPF 调试会话契约 v0.3

2026-10-16；main 框架线。范围：WPF宿主普通工具节点的单次/连续/停止/关闭，以及模块输入快照。Group配置页的运行入口另行维护；本次没有完成Group循环/累积或全插件调试UI。

## 源码证据与边界

| 原始代码 | 确认行为 |
|---|---|
| DynamicParamsTabControl.xaml.cs 的三个BottomCommand | 绑定SelfExcuteModule、ContinuousSelfExcuteModule、StopSelfExcuteModule |
| Apps.XmlParser.ParamTab.BottomCommand.SelfExcuteModule | 检查IsBusy/IsReady/IsLoading，清显示值缓存，再调用owner.Run() |
| VM.Core.VmModule.Run | 设置模块/流程自身运行状态，调用ModuleManager.SelfRun(moduleInfo.nNodeID) |
| VM.PlatformSDKCS.ModuleManager.SelfRun | 调用IMVS_SelfExecuteOneModule(handle, nModuleId)；内部准备上游数据仍是原生边界 |
| BottomCommand.ContinuousSelfExcute / StartContinuousSelfExecute | 等待模块结果回调标志，避免重复发起；循环线程每次Sleep(300) |
| BottomCommand.StopSelfExcuteModule / StopContinuousSelfExecute | 关闭连续标志；取消循环、等待任务退出、解绑模块回调 |
| ParamsConfigControlBase.Window_Closed | 关闭连续执行标志，注销多个相机/通信服务，AlgorithmTabRoot.Dispose及DoFinish |
| ParamsConfigWithResultViewControl.DeInit | 停止连续标志并Dispose参数树 |

以上位于analysis/decompiled。停止原生连续标志不等于已证明能中止正在运行的原生算法；原生SelfRun的上游准备、首次执行和设备内部资源策略尚未完整恢复。

## 实际实现

- VisionFramework.NodeDebugExecutor：只执行指定普通模块。输入来自明确提供的缓存记录、变量订阅和上次实际流程调用输入；缺失或错误数据拒绝执行，不静默运行整条流程。
- ExecutionSessionResult.WorkflowInputs：保存实际传入的工作流/Group参数快照，避免内部工具只能依赖顶层流程或丢失入参。
- IToolDebugSession / ToolDebugSession：Idle、RunningOnce、RunningContinuous、Stopping、Disposed；单次参数快照、拒绝重入、连续等待本轮结果、StopAsync等待运行及清理、DisposeAsync后禁止重启。
- ToolEditorContext.Session是新增可空属性，原构造参数及Run委托保留。WPF宿主提供Session；既有插件继续使用context.Run时已进入同一会话服务。
- ToolEditorHost：捕获方案/流程/节点身份，Apply作用于原所属流程；在后台执行算法，UI线程更新结果；清除当前节点及下游旧结果，再显示最新模块结果。完整流程运行会清除调试结果覆盖层。
- 实际参数窗口Closing先取消并等待会话，再排队Close，防止Closing栈内重入；主窗口同样等待全部调试会话结束后关闭。

## 独立宿主策略，不冒充原生等价

- 显式缓存输入策略没有实现原生IMVS_SelfExecuteOneModule的首次准备行为；没有上游结果时提示失败，不能断言VM也如此。
- StopAsync通过合作取消请求中止当前独立模块并等待Dispose；这比已确认的原生“关闭连续标志”更强，不声称原生算法相同。
- 连续模式在Failed/TimedOut/Cancelled结果时停止是独立宿主安全策略；NoMatch继续。300ms默认间隔来自托管循环证据，但原生调度时序不完全等价。
- Group节点调试仍走其模块集配置入口，不经过NodeDebugExecutor。相机/通信设备的关闭释放需要插件线实现并实机验收。
- 既有WinForms兼容类未被本次迁移。插件专用按钮、状态绑定和参数页由插件线对照原始UI接入；宿主实现不等于这些UI已完成。

## 插件接入

```csharp
// 原有单次入口保持有效
var result = await context.Run(values);
// WPF新宿主提供；旧宿主可能为null
if (context.Session is { } session) {
    await session.RunContinuousAsync(() => values, ShowResult);
    // 停止按钮在独立事件中调用：await session.StopAsync();
}
```

宿主的Context/窗口操作应从WPF UI线程调用。算法运行会转至后台。values会在每轮开始复制；completed回调应保持轻量。宿主拥有会话关闭释放，插件不要另启不受宿主管理的重复循环。GUI中不能先await无限连续任务再在同一事件后续调用Stop，应由停止按钮另行发起。

## 验证范围

- tests/VisionFramework.DebugChecks：单模块缓存输入、变量/订阅、实际调用参数快照、无上下游带跑、失败/错误输入、连续不重入、停止等待、关闭及超时释放。
- WPF测试：真实图像源参数窗口+宿主Run回调，在下游配置不完整时仍产生1296宽图像并更新实际Preview；回调不是现有参数页中并不存在的运行按钮，未声称该按钮已还原。
- WPF测试专用慢模块用于验证实际Window.Closing和主窗口关闭会等待异步Dispose；慢模块/测试窗口仅存在于测试工程，不是产品工具插件。
- 本地日志和哈希：analysis/full-audit/debug-session-runtime/results.json、debug-session-source-files.json、debug-session-lifecycle/exit-results.json。

原生差分和调试页完整UI验收仍开放；F-02整体不因这些测试通过而关闭。
