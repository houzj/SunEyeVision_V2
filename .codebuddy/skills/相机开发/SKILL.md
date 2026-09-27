---
name: 相机开发
description: >-
  相机接入与开发技能。用于为本项目新增厂商相机（Basler / Daheng / 其他品牌）或新增接口与传输类型
  （GigE / USB3 / CameraLink / GenTL / 厂商 SDK）的完整落地：契约实现、设备发现、适配器工厂、
  参数类型映射、运行时装配、方案持久化与启动恢复、验收测试。
  触发场景：接入新品牌相机、新增相机适配器、实现 ICameraAdapter / ICameraDiscoveryProvider /
  ICameraAdapterFactory、相机稳定身份（DeviceId）设计、相机参数类型映射、相机连接会话与独占语义、
  相机 SDK 条件编译接入、相机验收清单。
  Trigger keywords：接入相机/新增相机品牌/相机适配器/相机发现器/相机工厂/Basler/Daheng/
  新接口相机/GigE/USB3/GenICam/ICameraAdapter/CameraDescriptor/相机参数映射/相机验收/相机开发。
  DO NOT trigger on：仅调试现有海康相机参数、仅调整相机窗口界面布局、结果渲染与结果层改造、纯文档写作。
---

# 相机开发（新增厂商 / 新增接口）

> 渐进式披露：本文件只放**判定 + 铁律 + 工作流 + 验收**。契约成员清单、范式细节、装配接线点、
> 参考实现索引全部放在 `references/`，按步骤指示按需读取，不要一次性全读。

## 0. Step0 判定（先判定，再动手）

| 你的需求 | 是否走本技能 | 落点 |
|---|---|---|
| 新增**品牌**相机（Basler / Daheng / 其他） | ✅ 全流程 | `src/DeviceDriver/Cameras/<Vendor>/<Sdk>/` |
| 同品牌新增**接口/传输**（如 MVS 的 CameraLink / GenTL） | ✅ 但通常**不新增工厂**，只扩展现有 Provider 的传输过滤与适配器分支 | 既有厂商目录内 |
| 新增长期存在的**传输/协议/错误码/能力位** | ⚠️ 先改 SDK 契约，再走流程 | `src/Plugin.SDK/Devices/Camera/` |
| 只是调参、预览、窗口布局 | ❌ 不用本技能 | UI 层 |
| 只是渲染/结果层 | ❌ 不用本技能 | 结果层 |

判定完成后，先读 `references/01-contract-surface.md` 确认可实现的契约面，再读对应的范式文件。

## 1. 分层铁律（违反任一条即返工）

1. **厂商 SDK 只出现在 `src/DeviceDriver/Cameras/<Vendor>/`**。`Plugin.SDK` 不引用任何厂商类型；UI / Workflow 不得出现厂商分支判断。
2. **契约层不保存 SDK 句柄、线程、连接对象或厂商类型**。`CameraDeviceRef` 只存逻辑引用，物理解析由运行时完成。
3. **稳定身份可复现**：`DeviceId` 必须跨进程重启稳定（序列号优先；无序列号时按传输+型号+IP/MAC 组合），并令 `CameraRef = new CameraDeviceRef(DeviceId)`。同一物理设备重复发现必须**去重**。
4. **参数必须带明确类型**，禁止对参数做无条件强转（如 `Convert.ToBoolean`）。枚举/字符串/数值/命令/矩形分别走 `CameraParameterKind` 对应类型。
5. **报错优先、零静默兜底**（rule-008 / rule-015）：SDK 缺失 → `DriverUnavailable`；设备被占 → `DeviceBusy`；取帧超时 → `FrameTimeout`；触发超时 → `TriggerTimeout`。不得返回假数据、不得吞异常。
6. **连接会话独占**：同一 `CameraDeviceRef` 运行时只允许一个 `CameraSession`；打开必须经 `CameraRuntimeManager.OpenAsync`。关闭管理器/预览窗口**不得**释放方案级连接（只有退出、切换方案、用户主动断开、删除设备才释放）。
7. **不造重复基础设施**（rule-004）：实现既有契约即可，禁止新建平行相机模型、第二套注册表或兼容层。
8. **厂商二进制不入库**（rule-016）：SDK 用「路径探测 + 条件 `Reference` + 条件 `DefineConstants`」，缺 SDK 时必须仍可编译并可运行（报 `DriverUnavailable`）。

## 2. 技能产出物（新增一个厂商的完整交付面）

| # | 产出 | 位置 |
|---|---|---|
| 1 | 发现器 | `src/DeviceDriver/Cameras/<Vendor>/**/XxxCameraDiscoveryProvider.cs` |
| 2 | 适配器工厂 | `src/DeviceDriver/Cameras/<Vendor>/**/XxxCameraAdapterFactory.cs` |
| 3 | 适配器 | `src/DeviceDriver/Cameras/<Vendor>/**/XxxCameraAdapter.cs` |
| 4 | 参数映射（复杂参数集时必须独立成文件） | 同目录 `XxxParameterMapper.cs` |
| 5 | 运行时装配（**唯一登记点**） | `src/DeviceDriver/Cameras/CameraRuntimeComposition.cs` |
| 6 | SDK 依赖声明（若用厂商 SDK） | `src/DeviceDriver/SunEyeVision.DeviceDriver.csproj` |
| 7 | 测试 | `tests/SunEyeVision.DeviceDriver.Tests/`、`tests/SunEyeVision.Plugin.SDK.Tests/` |
| 8 | 文档（按 rule-006 归类） | `docs/05-框架架构/<年>/<月>/<日>/…` + 重建文档索引 |

## 3. 工作流（逐步执行，每步都要有验证）

> 每步格式：**动作 → 验证判据**。任一步验证不过，停下修好再进下一步。

### Step 1 明确身份与传输
动作：确定 `Manufacturer` / `Model` / `Transport` / `Protocol` / 身份键（序列号优先）与 `Metadata` 约定。
验证：把设备信息打印成 `CameraDescriptor`，**连续两次发现结果完全一致**（`DeviceId` 稳定）。

### Step 2 生成骨架
动作：运行 `scripts/scaffold-camera-vendor.ps1` 生成三件套（发现器/工厂/适配器）。脚本生成的骨架**已是诚实实现**（无 SDK 时返回空/抛 `DriverUnavailable`），不得留 `NotImplementedException` 占位。
验证：`dotnet build SunEyeVision.slnx -c Debug` = 0 error；此时未装配，行为不变。

### Step 3 实现发现器
动作：按 `references/02-paradigm-discovery.md` 实现 `DiscoverAsync`（transports 过滤、SDK 后台调用、失败隔离、身份生成、缓存设备信息供工厂/适配器使用）。
验证：无 SDK → 返回空数组且**不阻断其他发现器**；有 SDK → 能枚举设备；同一设备多次发现只出现一次。

### Step 4 实现适配器
动作：按 `references/03-paradigm-adapter.md` 实现连接/断开/单帧采集/连续采集/软硬触发/参数 schema/读写回读/命令，并**就地映射厂商错误码**。
验证：软触发超时得到 `TriggerTimeout`；参数写入后回读值一致；重复 `Connect` 得到明确错误而非静默成功。

### Step 5 实现工厂
动作：`CanCreate` 依据 `Protocol` / `Transport` / `Manufacturer` / `DeviceId` 前缀 / `Metadata` 判定；`Create` 在无 SDK 时抛 `DriverUnavailable`。
验证：与既有 MVS 工厂同时注册时，各设备只被**一个**工厂接管（互不误吞）。

### Step 6 运行时装配
动作：在 `CameraRuntimeComposition` 的发现器数组与工厂数组登记新厂商（见 `references/04-runtime-wiring.md` 原文）。
验证：`Discovery.DiscoverAsync()` 能列出新厂商设备；`Runtime.OpenAsync(ref, …)` 能建立 `CameraSession`；第二次打开同一 ref 被拒。

### Step 7 SDK 依赖接入（若使用厂商 SDK）
动作：`SunEyeVision.DeviceDriver.csproj` 加 SDK 根探测（环境变量优先、固定回退路径）、条件 `Reference`、条件 `DefineConstants`。
验证：把 SDK 路径指向不存在的位置，仍能编译通过且运行时给出 `DriverUnavailable` 明确错误。

### Step 8 持久化与启动恢复（**不需要改代码**，但必须验证）
动作：确认无需修改 `SolutionRepository` / `App` / `CameraManagerViewModel`（它们靠身份匹配自动适配，见 `references/04-runtime-wiring.md`）。
验证：保存方案 → 重启 → 设备自动重新发现并按 `DeviceId`/序列号/MAC 重新绑定；按 `IsEnabled` / `AutoConnect` 恢复连接；**失败或超过 10 秒仍能进入主界面**。

### Step 9 测试
动作：按 `references/05-acceptance-and-tests.md` 补齐三层测试：契约单测（无硬件）、运行时语义（用模拟相机）、真机诊断（`[Trait("Category","RealHardware")]`）。
验证：`dotnet test SunEyeVision.slnx -c Debug` 通过；真机用例在无设备时通过 Trait 过滤跳过。

### Step 10 逐条验收 + 交付
动作：按 §4 清单逐条签字；文档按 rule-006 归类并重建索引；提交使用「代码提交」技能（`git commit -F` UTF-8 文件、无 BOM）。
验证：§4 全部为「通过」，且构建与测试均绿。

## 4. 验收清单（12 条，逐条给判据）

| # | 验收项 | 判据 |
|---|---|---|
| 1 | 发现设备与稳定身份 | 两次发现 `DeviceId` 一致；`CameraRef` 由 `DeviceId` 生成 |
| 2 | 重复发现不重复登记 | 同一设备多次发现后 `Runtime.Devices` 无重复 |
| 3 | 连接 / 断开 / 重复连接 | 重复连接被拒（`DeviceBusy` 或等价明确错误） |
| 4 | 单次采集与连续采集 | 均能取到 `CameraFrame`，帧所有权与释放正确 |
| 5 | 软件触发与触发超时 | 触发无帧时抛 `TriggerTimeout`，不静默返回空帧 |
| 6 | 关键参数读取与写入 | 写入后回读一致；只读参数不被写入 |
| 7 | 关闭设备管理器不影响连接 | 关窗后 `State` 仍为已连接 |
| 8 | 关闭预览窗口不改变预览前连接状态 | 关闭后回到预览前的连接状态，不残留采集 |
| 9 | 保存方案后重启可恢复 | 重新发现并绑定成功，`AutoConnect` 生效 |
| 10 | SDK 缺失 / 设备离线 / 设备被占 | 三种情形均给出明确错误码与可读信息 |
| 11 | 方案加载失败/超时仍进主界面 | 超过 10 秒超时保护生效，主界面可用 |
| 12 | 测试通过 | 真机诊断测试 + 适配器/运行时单元测试通过 |

## 5. references 索引（按需读取）

| 文件 | 何时读 |
|---|---|
| `references/01-contract-surface.md` | 开始实现前：契约与驱动契约的完整成员清单（含 `文件:行号`） |
| `references/02-paradigm-discovery.md` | Step 3：发现器范式（身份生成、去重、失败隔离、传输过滤、SDK 条件编译） |
| `references/03-paradigm-adapter.md` | Step 4：适配器范式（并发控制、采集、流、触发、参数映射、错误映射） |
| `references/04-runtime-wiring.md` | Step 6/8：装配点、启动恢复、持久化、测试工程登记、必改 vs 不需改 |
| `references/05-acceptance-and-tests.md` | Step 9/10：测试范式、构建测试命令、12 条验收的可执行做法 |
| `references/06-reference-impl-mvs.md` | 需要照抄既有范式时：MVS 参考实现的逐点索引（文件 → 方法 → 行号） |

## 6. 相关规则（技能内必须遵守）

- `rule-004` 方案设计要求：复用现有基础设施，禁止重复造轮子与兼容层。
- `rule-008` 开发与代码纯净原则：零兼容、报错优先、YAGNI/KISS，禁止死代码与静默兜底。
- `rule-015` 禁止兜底回退：静默兜底一律视为缺陷。
- `rule-016` MSBuild 项目配置：csproj 改动（条件引用、常量定义）遵循既有约定。
- `rule-006` 文档管理政策：新增/更新文档必须按 `docs/` 顶层分类 + 年/月/日归类，并更新文档索引。
- `rule-003` 日志系统：诊断日志走统一日志接口，测试期诊断用 `CameraDiagnosticScope` 关联。
