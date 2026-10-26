# VM Blob 组件 Runtime / WPF 分层结构取证（2026-10-26）

## 回答

现有原始交付物与静态调用证据**不支持**把 Blob 说成两个源码工程“Runtime 项目 + WPF 项目”。能证明的是一条多层交付/调用结构：

1. `IMVSBlobFindModu.dll` 是 x64、没有 CLI 目录的 native PE；它是 Blob 原生模块交付物。把它称作算法 Runtime 组件是基于模块命名与 SDK 调用链的**结构推断**，并未通过 native 函数体/导出或进程加载证据闭合。
2. `IMVSBlobFindModuCs.dll` 是独立的 .NET Framework 4.6.1、AnyCPU 托管 SDK 适配程序集。核心类型 `IMVSBlobFindModuTool : VM.Core.VmModule` 持有 `BlobFindParam` / `BlobFindResult`，`Run()` 调 `ModuleManager.SyncSelfRun(ID)`。该程序集引用 `VM.Core` 和 `VM.PlatformSDKCS`，没有 WPF/XmlUI 引用，也没有 P/Invoke、ModuleRef 或 native method implementation；因此它不是 Blob 专属 WPF 窗体程序集。
3. UI 侧交付由 `IMVSBlobFindModuAlgorithmTab.xml`、`IMVSBlobFindModuDisplay.xml`、模块/算法 XML 提供 Blob 特有的视图与参数声明；通用 UI 实现位于 `VMControls.WPF.dll`、`Apps.XmlUI.dll`、`Apps.XmlParser.dll`。WPF 源码有可选的 `{ModuleTypeName}Control` 扩展分支；Blob 模块目录未发现 `IMVSBlobFindModuControl.dll`，所以“若 AppDomain 未预载相应扩展，则回退通用参数窗”是源代码支持的条件结论，当前实际进程走哪一分支仍未知。

所以，如果“Runtime + WPF 设计”指**功能分层**，可以初步描述为 native 模块 Runtime + .NET SDK 适配 + XML 驱动的通用 WPF 配置/显示；如果指**两个源码项目/解决方案**，现有证据不足，不能这么定论。

## 有向结构

```text
工作流节点/ModuleTypeName = IMVSBlobFindModu
  ├─ VM.Core.VmModule(object, typeName)
  │    └─ CreateToModuleSet → IMVS_SYNC_ADD_ONE_MODULE_INFO.strModuleDllName
  │         └─ ModuleManager.SyncAddOneModule(...) → SDK Runtime 注册路径
  ├─ IMVSBlobFindModuCs.IMVSBlobFindModuTool : VmModule
  │    ├─ ModuParams : BlobFindParam
  │    ├─ ModuResult : BlobFindResult ← QueryModuleOneResult / SetModuleResult
  │    └─ Run() → ModuleManager.SyncSelfRun(ID)
  │                    └─ ServerSDKAPI.IMVS_SelfExecuteOneModule(handle, ID)
  └─ 参数窗/WPF
       ├─ Blob XML：AlgorithmTab / Algorithm / Display / module metadata
       ├─ 可选扩展：已加载 Assembly 或同目录 IMVSBlobFindModuControl.dll
       └─ 否则：ParamsTabControlBase → ParamsConfigControlBase
                     → ParamsTabUserControl / 通用 DisplayType 模板

IMVSBlobFindModu.dll (x64 native PE) 是上面 SDK Runtime 消费的 Blob 原生交付候选；
其函数/导出、实际加载时机和与 SDK 的二进制 ABI 仍待 native 静态/运行时证据。
```

图中 `SyncAddOneModule → Runtime 注册`、`Run → SelfExecuteOneModule` 是托管源码直接可见的调用边；由 SDK 最终选中/加载哪一个 native 文件，是当前静态材料未闭合的后半段。

## 源码及元数据锚点

| 层 | 原始文件/成员 | 观察到的行为 | 证据等级 |
|---|---|---|---|
| Native 模块 | 安装文件 `IMVSBlobFindModu.dll`，SHA `4DE695D8C3051045C9010739686F94C32D66A36EE486207FE749FB183FFF4C9A` | x64 PE；REA managed 检查报告无 CLI directory，因此没有 .NET metadata/CIL。文件名、模块 XML 和注册路径将其定位为 Blob native 模块候选 | 文件身份/格式为已观察；Runtime 角色为推断 |
| .NET SDK 适配 | `IMVSBlobFindModuCs.dll`，SHA `3106AF2E9344A97288553BFBBFA86AFD81E2384188B452734F56825F63E0A78D`，MVID `1d550689-0379-4822-8b69-8713d2324342` | AnyCPU、.NET Framework 4.6.1；引用 `mscorlib`、`VM.Core`、`VM.PlatformSDKCS`。REA 类型表包含 `IMVSBlobFindModuTool`、`BlobFindParam`、`BlobFindResult`、`BlobFindRoiManager` 及算法枚举 | 元数据已观察；REA Evidence `ev_22697cccc2f26bd806da658c3f0a7217a16a9f900a5ac474834b84a33f904f6d` |
| SDK 适配调用 | 反编译 `IMVSBlobFindModuTool`：构造器 `base(param, "IMVSBlobFindModu")`；`Run()` 调 `ServerSDKManager.serverSDKManager.mModuleManager.SyncSelfRun(ID)`；结果属性读 `QueryModuleOneResult` 并 `SetModuleResult` | Blob 专属 C# 层包装公共 VM 模块/SDK，公开参数和结果对象并请求 SDK 单模块执行；它没有自己定义 WPF 窗体 | 静态反编译；二进制 SHA 与 MVID 绑定于上项 |
| Runtime 注册/执行桥 | `VM.Core.VmModule.CreateToModuleSet` / `CreateToModuleSet(...,typeName,...)`；`VM.PlatformSDKCS.ModuleManager.SyncSelfRun` | 创建信息把模块类型名写到 `strModuleDllName` 并调用 `SyncAddOneModule`；单步执行最终调用 `ServerSDKAPI.IMVS_SelfExecuteOneModule(handle,nModuleId)` | 源码确认托管调用链；native Runtime 内部实现未取到 |
| 通用 WPF Host | `VMControls.WPF.ProcedureConfigControl.GetExternTabWin` | 先找已加载的 `<ModuleTypeName>Control` 程序集，再查模块目录 `<ModuleTypeName>Control.dll`；找到 `TabWinBase` 类型时实例化，返回 null 才由调用方创建 `ParamsTabControlBase` | 源码分支已确认；实际运行分支未知 |
| Blob 参数/显示声明 | `IMVSBlobFindModu.xml`、`IMVSBlobFindModuAlgorithm.xml`、`IMVSBlobFindModuAlgorithmTab.xml`、`IMVSBlobFindModuDisplay.xml`、公共 `Group.xml` | AlgorithmTab 通过 `GroupLinkItem` 链公共组（如 `ImageSourceGroup`、`BottomCommandGroup` 等）；XML 数据由公共参数解析器和 WPF Host 消费 | 安装资源静态解析；不等同原机控件像素/运行时观察 |

## 身份与取证边界

Blob 模块目录为 `D:/Program Files/VisionMaster4.4.0/Applications/Module(sp)/x64/Location/IMVSBlobFindModu/`，清单共 11 个文件：native DLL、Cs DLL、模块 XML、Cs XML 文档、算法 XML、AlgorithmTab XML、Display XML、三张图片和 ToolItemInfo。清单中没有同目录 `IMVSBlobFindModuControl.dll`。该否定结论仅限此目录；WPF 源码允许 AppDomain 中已有扩展程序集，当前无目标进程加载程序集观察。

安装通用程序集身份：

- `VMControls.WPF.dll` SHA `99FFA401C8784472913AD43813D68A8A5577890C00E149506E3E5664FA61C97D`
- `Apps.XmlUI.dll` SHA `639D0FB9DFCA81E38DF690E201B73874CE006D1361B689DDC6D5F41F7B0B04FD`
- `Apps.XmlParser.dll` SHA `78EC7AE1E5ADEFFF29197099C7109B153A3F75D092B7C3E9DC253D8C1B25A8D1`
- `VM.Core.dll` SHA `936763F6C8A1BF9FCF59F22ACEBCA8FEB09B5F3D5701AE77C82BDC81E626190D`

REA Evidence：`IMVSBlobFindModu.dll` managed classification `ev_866e95cb49d1fddb99cf32ea154ae8b76be2b58321d8abd0044674d972ad55e7`；`IMVSBlobFindModuCs.dll` native-boundary check `ev_a1bbcd0b2da30907939ee67db0a8e259852640d4d97f5bcadbf3fb72a94caecd`（0 P/Invoke、0 ModuleRef、0 native impl）；其成员/CIL检查 `ev_e2d94e338072eadedce0ed9e7457dd6c1ba8a132cde53efffa114c40611eb364`。原生分析尝试因 REA 可用 provider 均不可用（Ghidra 未配置、Hopper 不支持 Windows、IDA 未配置）而未获得 native 导出/调用图；未执行或加载目标。

## 尚未闭合的证据

1. 无原厂 `.sln`/`.csproj` 或构建清单，不能把已安装程序集边界反推成原始源码项目边界。
2. 未解析 native DLL 的导出、导入、初始化入口及 SDK 到 Blob native 实现的精确调用 ABI。
3. 未证明 SDK 实际按 `strModuleDllName` 从该目录装载指定 DLL；需要 Runtime 侧源码/原生函数或受支持的原机模块加载证据。
4. 未证明目标进程是否已经预加载 Blob Control assembly；因此 Generic `ParamsTabControlBase` 是候选回退，不是本次运行的既定分支。
5. 无运行时 WPF CLR 类型、实际 DataTemplate key、主题资源和参数窗行为证据。

## 可复验入口

- 当前 REA 安装程序集证据由 `analysis/selfdev-support/VM-CONTROL-BLOB-ASSEMBLY-20261024/root/` 下来源清单与 SHA 锚点索引；Blob 目录文件清单见 `source/source-findings.json` 的 `blobDirectoryInventory`（作者归属未确认，但本目录项已由统筹以安装文件重新核 SHA）。
- 通用 Host 源：`source/VMControls.WPF.ProcedureConfigControl.cs`；Blob SDK 基类/注册源：`source/VM.Core.VmModule.cs`；SDK 执行源：`source/VM.PlatformSDKCS.ModuleManager.cs`。
- 仅静态离线分析，没有启动 VM、加载目标程序集、修改产品或 `tool-plugins`。
