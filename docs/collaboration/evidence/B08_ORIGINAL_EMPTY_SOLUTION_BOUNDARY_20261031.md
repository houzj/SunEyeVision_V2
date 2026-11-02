# B08 原厂空方案：可构造入口与尚未解除的隔离条件

范围为 VM 4.4 本地反编译与之前已保存样本，仅做只读静态核查；没有启动 SDK、改变现有进程、修改安装或清理共享缓存。结论：**有明确的方案/流程/模块构造入口，但没有证据证明当前环境能创建完全隔离且无隐式附加内容的空方案；不能现在执行。**

## 已确认的托管入口与副作用

1. `VM.Core.VmSolution` 静态初始化创建托管 `instance`、读取 `CurrentPathHelper.Instance.GetCurrentPath() + ModulePathGenerate.txt`，创建虚拟流程并启动后台线程。托管实例初建不证明原生服务为空或已初始化完毕。
2. 显式接口 `IVmSolution.CreatNewInstance()` 先 `TempVmSolution.Init()`、清 `SolutionPassword`，然后 `SolutionManager.CloseSolution()` → `ServerSDKAPI.IMVS_CloseSolution(CVmObjectBase.mhandle)`，替换实例并 `CreatNewProcedure()`。其中关闭操作有真实服务端副作用；在未证明服务端完全归当前自有进程前，**不能调用它**。
3. `VmSolution.StartNewInstance()` 初始化 `TempVmSolution`、替换托管实例并调用 `ProcessManager.SyncAddOneProcess`；所读方法不调用上述 `CloseSolution`，也不等于清空原生服务端，不能作为空方案保证。`TempVmSolution` 的静态构造注册大量方案、模块、回调、通信与相机事件，`Init()` 空方法本身触发该类型初始化；是否产生外部资源及回调取决于事件实现。
4. 可选流程构造 `new VmProcedure(VmSolution.Instance)`：继承 `VmModule(object,"VmProcedure")` 的流程分派，经虚方法 `VmProcedure.CreateToModuleSet` → `ProcessManager.SyncAddOneProcess` → `IMVS_AddOneProcess`，`InitIO` 并加入 `Instance.Modules`。另有 `IVmSolution.CreateProcedure` → `CreateToProcessInfo` → 同一 SDK 新增流程后 `ChangToProcess`。这些入口会调用服务而非纯内存创建。选择其中一条且验证返回 ID/实际列表，避免重复建流程。
5. `new VmModule(procedure,"ImageSourceModule")` 和 `new VmModule(procedure,"IMVSBlobFindModu")` 经 `CreateToModuleSet` 构造 `IMVS_SYNC_ADD_ONE_MODULE_INFO`，`ModuleManager.SyncAddOneModule` → `ServerSDKAPI.IMVS_AddOneModule`，注册 AllModules 并触发模块增加事件；所读 attempt03 已证 Blob 在该类型流程内创建，不能由此声称新建 ImageSource 已在空方案实测。
6. 流程显式接口 `IVmModuleSet.AddLocalVariableModule()` 有条件调用 `AddContainerRelateModule` 建 type 25 的 LocalVariableModule；`VariableManager.AddVar`、`SetVarIntValue(int[])`、`GetVarIntValue` 是三项来源候选，须真实回读。输出订阅、控制连接、图像写入与安全销毁仍须逐步记录；它们不因为有构造方法就被证明会成功。

这里的入口标注有“内部使用”，不是承诺厂商支持对第三方稳定公开。原 SDK 创建/连接会走 `CVmObjectBase.mhandle` 所指的服务；尚未确认该 handle 在新宿主中的服务归属、服务进程隔离、是否重用现有 VisionMaster 或与运行实例共享方案。不能通过试调用 `CloseSolution` 探测隔离。

## 条件性可执行准备方案（此处未执行）

待确认独立服务实例与实际 ModuleProxy 清理范围，并取得新的具体独占窗口授权后，构建单独、一次性宿主目录：

1. 审查并固定 VM 4.4 版本、应用资源/ModulePathGenerate、运行环境与 `CreateGlobalInitToolInstanceEx` 原 ABI/依赖；初始化的日志、许可、COM、服务、线程及 GPU 副作用逐项检查。原 attempt03 Ex 成功只证明该次上下文，不证明新进程无副作用。初始化后核对实际服务/句柄身份和既有方案清单；不满足独占立即退出，不调用 CloseSolution。
2. 仅在已经证明服务是自有且为空时，使用上述**一种**流程构造入口；核对服务端与托管端列表、父子关系和模块类型。然后分别调用两个模块构造函数、局部变量接口与三项赋值；确认无意外第三方节点、无旧脚本/附件及回调副作用。避免加载参考 `fixture.sol`；保存独立方案后，对归档中 `VmServer.xml`、脚本条目、附件及二进制再做只读审核，不能假设新建时绝无默认全局脚本。
3. 在自有目录新建受控灰度 BMP 并记录 SHA，仅连接该新图与 Blob，记录 IO 身份、真实三项回读与控制连接，再移植 V2 的 ALL/固定/动态索引矩阵；实际源、索引、原始绑定结构、执行错误和重开状态均要分别记录。V2 当前依赖 `fixture.sol` 的 Load，不是空方案探针，不应直接运行。
4. 退出只处理自有对象的 Dispose/Destroy；记录进程自然结束与清单变化。不得自动关闭他人方案、终止进程、删除共享清单或旧缓存。所有文件保存在自有目录，先确认实际副作用后再决定是否另行申请处置。

**尚不能给出可安全执行的完整运行命令**：独立服务/句柄合同、初始化副作用、默认脚本与保存结构、图像源新建能力和共享历史清理都未获足够证据，当前也已观察到用户 VisionMaster 进程。上述是可实现的具体构造方案，不是实机成功证明；现有 V2 必须先修改并审查，不能用环境变量代替人类授权。

## 证据身份

反编译文本（SHA256）：

- `analysis/decompiled/VM.Core/VM.Core/VmSolution.cs` `C141E2C53913B233B768782D6757FAE5997ECAF638B35DB2DDF98F5EF951A13E`。
- `VmProcedure.cs` `FDDE074D17C50C37F8D0BFE097CF20F904CDB4B59D4AC39612FC023C3F0B69DA`；`VmModule.cs` `53E2F0F9032C4631B59CF0EB2A00E06D50469D94706E7E370E9CC7E1059C72CE`。
- `VmModuleSet.cs` `F3B98C4436686B0A9BABF8CDE4F5F6C83814664B8F7835DFABBEB02E2A64A346`；`TempVmSolution.cs` `2683DD70DC4AA88FA2D263E9117BDC00B1153DEEDA52E03417DFA7EE42859721`。
- `analysis/decompiled/VM.PlatformSDKCS.Audit/VM.PlatformSDKCS/ProcessManager.cs` `06FEE7C7B5387FEA086ABE5073BE5DD2606AD121D9BC64B0AB9335BACE100414`；`ModuleManager.cs` `783A45B1518F713223ECA2B5E59291BE973552C3E08FEF0EA0D4BFCCC2FBD922`。
- 原 SDK 样本和实际共享清理风险见 [V2 离线审计](B08三元素索引探针V2与离线前置审计-20261031.md)、[Proxy 路径审计](B08代理路径与单次运行前检决策-20261031.md)。
