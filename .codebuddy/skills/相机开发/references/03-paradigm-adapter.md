# 03 适配器范式（Step 4）

照抄对象：`src/DeviceDriver/Cameras/Hikvision/Mvs/MvsCameraAdapter.cs`。
行号索引见 `06-reference-impl-mvs.md`。

## 1. 并发与生命周期骨架

| 关注点 | 约定 | 参考 |
|---|---|---|
| 两把锁分离 | `_gate` 串行化**参数/命令**操作；`_captureGate` 串行化**取帧**，互不阻塞 | `MvsCameraAdapter.cs:28-29` |
| 连接 | 创建句柄 → 打开设备；**打开失败必须销毁句柄**再抛错 | `:64-85`（create `:70`、open `:73`、失败销毁 `:76-77`） |
| 断开 | 先停抓取 → 关设备 → 销毁句柄 | `:87-100` |
| 重复连接 | 由运行时（`CameraRuntimeManager.cs:85`）拒绝；适配器自身也应返回明确错误，不得静默成功 | — |
| `State` | 按 `CameraState` 语义推进（Connected / Capturing / Faulted…），不得长期停在中间态 | — |

## 2. 单帧采集 `CaptureAsync`

流程（`MvsCameraAdapter.cs:102-179`）：
1. 开诊断作用域（带上请求里的关联 ID）。
2. 取 `_captureGate`。
3. 调厂商取帧接口；**无数据/超时 → `FrameTimeout`**（`:125-137`），其他错误 → `Unknown`（`:140`）。
4. 依实际字节长度判定通道数/像素格式（`:152-153`），构造 `CameraFrame(cameraRef, frameId, timestamp, image, metadata)`。
5. `finally` 中**无条件释放厂商 buffer**（`:167`）——泄漏在这里最容易出现。

## 3. 连续采集

| 方法 | 要点 | 参考 |
|---|---|---|
| `StartStreamAsync` | 启动后台 `RunStreamAsync` 任务；`ICameraFrameSink.OnFrameAsync` 返回成功后**所有权移交接收方**，不要重复释放 | `:181-206`、`:200` |
| 流循环容错 | `FrameTimeout` 容忍并继续下一帧，不因单帧超时中断流 | `:912-960`、`:927-933` |
| `StopStreamAsync` | 先让 SDK `StopGrabbing`，再等待循环退出（避免关设备与取帧竞争） | `:208-223`、`:882-910` |

## 4. 触发

| 触发类型 | 做法 | 参考 |
|---|---|---|
| 软件触发 | 调厂商「软触发」命令节点；**仅在已启动软件触发预览时允许**，否则明确报错；失败抛 `TriggerTimeout` | `:373-393`（失败 `:386`） |
| 硬件触发 | **不改写设备配置**，由 `TriggerMode` / `TriggerSource` 参数与流循环自然生效 | 流循环 `:912-960` |

> 铁律：预览/触发**不得擅自切换触发模式或强制连续采集**（设计文档 §行为约定 1、3）。

## 5. 参数映射（本技能第二关键点）

### 5.1 Schema 生成
- 递归枚举厂商参数树，带**深度上限**与 **visited 去重**防环（`MvsCameraAdapter.cs:225-312`，深度上限 32/255、去重 `:279`）。
- 节点类型 → `CameraParameterKind` 映射（`:403-412`，无法归类用 `Custom`）。
- 读写权限 → `IsReadable` / `IsWritable` / `IsReadOnly`（`:414-417`、`:431`）；实现态 `IsImplemented`(`:418`)、可用态 `IsAvailable`(`:419`)。
- 不可用/自定义/命令等异常节点，返回**带 `StatusMessage` 的描述符**而不是丢弃（`:443-455`）。
- 需要停机才能改的参数标 `RequiresStopped`（`:437`、`:507`）；会改变 schema 的标 `AffectsSchema`（`:438-439`）。
- 跨厂商稳定标识用 `StandardFeature`（映射表 `:477-505`）——UI 与工作流只认它，不认厂商参数名。

### 5.2 读值
按 `Kind` 分派到对应类型读取接口（`:527-609`）。布尔**必须**走厂商布尔读取接口得到 `bool`（`:561-564`），**禁止**把枚举/整型硬转成 bool（例如 MVS `17301505` 是 `PixelFormat=Mono8` 枚举值，不是布尔）。

### 5.3 写值 + 回读
- 写之前先处理「自动控制」类互斥项（如关闭自动曝光/自动增益），否则写入会被设备忽略（`:691-745`，`:701-704`）。
- 按 `Kind` 分派写入接口（`:717-725`）；写入失败抛 `ParameterUnsupported`（`:730`）。
- 数值转换统一 `Convert.ToInt64 / ToSingle / ToBoolean / ToUInt32` + `CultureInfo.InvariantCulture`（`:719-723`）。
- **写入后必须回读**（`ReadParametersAsync` `:645-689`）把设备真实值返回，供 UI 区分「写成功」与「图像变化」（设计文档 §行为约定 4）。

### 5.4 命令
`ExecuteCommandAsync` 只执行「命令型节点」，不做隐式状态切换；SDK 缺失或节点不可用 → `ParameterUnsupported`（`:777-800`、`:787`、`:792`）。

## 6. 错误映射（就地映射，不集中兜底）

| 场景 | 错误码 | 参考 |
|---|---|---|
| 未找到设备 / 缓存无设备信息 | `CameraNotFound` | `MvsCameraAdapterFactory.cs:28-31` |
| 建/开设备失败 | `ConnectionRejected` | `MvsCameraAdapter.cs:72`、`:77` |
| 取帧超时 / 无数据 | `FrameTimeout` | `:136`、`:148` |
| 其他未知 SDK 错误 | `Unknown` | `:140` |
| 软触发无响应 | `TriggerTimeout` | `:386` |
| 参数/命令不支持或写入失败 | `ParameterUnsupported` | `:245`、`:264`、`:267`、`:712`、`:714`、`:730`、`:787`、`:792` |
| SDK 未安装 | `DriverUnavailable` | `:82`、`:177`、`:204`、`:391`、`:798`；工厂 `:34-35` |

约定：
- 每处异常消息都要带 `sdk=0x{错误码:X8}` 这类可诊断信息。
- 统一经 `LogCamera(...)`（`:520-525`）记录，并附 `CameraDiagnosticScope.CurrentCorrelationId`，便于把一次调参/一次取帧的日志串起来（rule-003）。
- `Dispose` 在无 SDK 时**空实现**、`GetParameterSchemaAsync` 在无 SDK 时返回**空 schema**（`:810-812`、`:308-311`）——释放与查询不得抛异常。

## 7. 本步验证判据

1. 无 SDK：`ConnectAsync` / `CaptureAsync` / `StartStreamAsync` / 软触发 / 命令均抛 `DriverUnavailable`；`Dispose` 与 schema 查询不抛。
2. 有设备：单帧与连续采集都能拿到帧，帧被正确释放（无泄漏）。
3. 触发无帧 → `TriggerTimeout`，不是空帧或静默成功。
4. 写入关键参数后回读值与设备一致；只读参数写入被拒。
5. 连续采集期间断开/关窗：流能停下，无后台取帧残留。
