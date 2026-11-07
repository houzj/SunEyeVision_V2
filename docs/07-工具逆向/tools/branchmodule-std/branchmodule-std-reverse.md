# 分支模块（BranchModule_STD）工具逆向主文档

- 稳定工具 ID：`branchmodule-std`
- 原厂工具：`BranchModule_STD`
- 原厂类别：Logic
- UI 组装类别：复合与控制工具（P4）
- 当前状态：`source-partial`
- 原厂目录：`D:\Program Files\VisionMaster4.4.0\Applications\Module(sp)\x64\Logic\BranchModule_STD`

## 源码确认的对象与入口

| 责任 | 原厂类型/成员 | 证据 |
|---|---|---|
| 工具壳 | `BranchModule_STDCs.BranchModule_STDTool : VmModule` | `analysis/decompiled/BranchModule_STD.WorkflowAudit/BranchModule_STDCs/BranchModule_STDTool.cs` |
| 参数 | `BranchParam : CModuleParamBase`；`Inputint`、`IndexUserSet` | `.../BranchParam.cs` |
| 结果 | `BranchResult : CModuleResultBase`；`outputint`、`ModuStatus`、`BranchMatch` | `.../BranchResult.cs` |
| 执行入口 | `new Run()` 调用 `ServerSDKManager.serverSDKManager.mModuleManager.SyncSelfRun(ID)` | `.../BranchModule_STDTool.cs` |
| 结果桥 | `ModuResult` 查询 `QueryModuleOneResult(ID)`，再调用 `SetModuleResult(ref ..., ref _result, ModuResultMemType)` | `.../BranchModule_STDTool.cs` |
| 参数同步 | `ModuleOperate` 将 `Params["NextModuleListChange"]` 置空 | `.../BranchModule_STDTool.cs` |
| 拷贝重映射 | `Paste` 读取 `IUserStringData["ModuleInfoList"]`，按 `$`、`#` 拆分旧模块 ID/路线，映射到新 ID；`PasteFinish` 写入新对象 | `.../BranchModule_STDTool.cs` |
| 分支限制 | `SpModuleCheck` 检查宿主 `NextModules.Count >= 32`，超限返回 `false` | `.../BranchModule_STDTool.cs` |
| 参数键 | `IndexUserSet`；输入结构名 `inputint` | `.../BranchParam.cs` |
| 参数取值 | `IndexUserSetEnum.IndexByValue/IndexByBit`，通过 `ParamItem.GetEnumValue/SetEnumValue` 管理 | `.../BranchParam.cs` |

## UI 与资源

资源身份来自 `resources.json` 中的原厂 XML 哈希。已确认资源根节点：`BranchModule_STD.xml`=`ParamRoot`、`BranchModule_STDAlgorithm.xml`=`AlgorithmParamList`、`BranchModule_STDAlgorithmTab.xml`=`AlgorithmTabRoot`、`BranchModule_STDCs.xml`=`doc`、`BranchModule_STDDisplay.xml`=`ParamRoot`、`ToolItemInfo.xml`=`ToolBoxItemData`。

清单确认的 UI 组装为一个 `Tab_Basic Params`，包含 `ButtonSelecter(ConditionInput)`、`RadioSelecter_Branch(IndexUserSet)`、两个 `HeadBar`（值/位索引）和 `ListParam(ModuleInfoList)`，并接入 `BottomCommandGroup`。完整绑定表达式、可见性触发器和模板资源尚未恢复。

## 输入、输出与执行链

- 输入：`Inputint` 是 `List<int>`，写入 `InputItem`，结构名为 `inputint`。
- 运行：公开 `Run()` 仅触发服务端同步自运行；本地类没有算法实现体，因此不能把分支选择算法或路线调度规则写成已确认事实。
- 结果：`ModuResult` 通过 SDK 结果查询桥装配 `BranchResult`。源码默认值为 `outputint=0`、`ModuStatus=0`、`BranchMatch=""`。
- 输出：XML 清单登记 `outputint:int`、`ModuStatus:int`、`BranchMatch:string` 和运行时间项 `ModuRunTime:float`；可见性、订阅能力和强制性以 `parameters.json` 为准。
- 宿主拓扑：分支上限明确为 32 个 `NextModules`；`ModuleInfoList` 是复制/粘贴时携带的旧模块 ID 与路线信息，不等同于已确认的执行算法。

## 保存、恢复与释放

已确认 `Paste/PasteFinish` 的 ID 重映射和参数回写；未在当前托管源码切片中找到序列化文件格式、加载入口、停止取消、`Dispose/DeInit/DestroyModule` 的具体实现。当前卡不把推测的释放顺序标为原厂行为。

## 未决证据（必须继续取证）

1. `BranchModule_STDAlgorithm.xml` 中 `ConditionInput` 的绑定路径、条件表达式和索引路线生成规则。
2. `ModuleInfoList` 的完整语法、分支顺序与运行时消费者。
3. `BranchResult` 三个字段如何由原生结果包填充，及 `ModuStatus` 的状态码枚举。
4. Save/Load、Stop/Cancel、DeInit/DestroyModule 的真实调用链。
5. XML 控件模板、主题状态与 `BottomCommandGroup` 的事件绑定。
6. 原厂运行时对值索引、位索引、无效输入和 32 路边界的行为。

本卡因此是源码级 `source-partial`，不是完整 VM 等价实现。下一步应优先恢复上述成员/资源和一次真实宿主差分运行，再推进 S0–S7 退出门。

