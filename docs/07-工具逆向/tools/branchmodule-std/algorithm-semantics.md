# 分支模块 VM 算法语义

- 工具 ID：branchmodule-std
- 语义状态：source-partial；以下步骤只把源码能证明的行为标为 source-confirmed

## 已确认执行链

1. 创建：BranchModule_STDTool 的三个构造函数均创建 BranchParam(ID)；工具继承 VmModule。
2. 参数：BranchParam.Inputint 将 List<int> 写入 InputItem 的 inputint；IndexUserSet 通过 ParamItem 读写 IndexByValue/IndexByBit。
3. 拓扑约束：SpModuleCheck 检查宿主或 HostModule 的 NextModules.Count，达到 32 时返回 false，并可显示本地化超限提示。
4. 执行触发：公开 Run() 调用 mModuleManager.SyncSelfRun(ID)；当前托管切片没有分支算法体，不能推断路线选择谓词。
5. 结果：ModuResult 查询 QueryModuleOneResult(ID)，再用 SetModuleResult 填充 BranchResult；结果字段是 outputint、ModuStatus、BranchMatch。
6. 复制恢复：Paste 解析旧 ModuleInfoList 的 $/# 令牌并按旧模块 ID 查找新对象，PasteFinish 回写新 ID；这是复制/粘贴重映射，不等同于运行算法。
7. 参数同步：ModuleOperate 清空 NextModuleListChange，作为拓扑变化通知的已确认副作用。

## 尚未确认的 VM 算法步骤

| 步骤 | 当前状态 | 必须补的证据 |
|---|---|---|
| inputint 如何映射到具体 NextModules 路线 | unknown | Process/ProcessAlg 或原生调用体、运行时分支结果 |
| 值索引与位索引的边界、负值和多路行为 | unknown | 原厂受控运行和返回码 |
| BranchResult 字段的原生填充和 ModuStatus 枚举 | unknown | 结果包解析/SDK成员或 ABI 追踪 |
| Save/Load、Stop/Cancel、DeInit/DestroyModule | unknown | 序列化和生命周期调用链 |
| UI 条件显隐与主题状态 | unknown | Algorithm XML/XAML绑定与模板资源 |

证据：analysis/decompiled/BranchModule_STD.WorkflowAudit/BranchModule_STDCs/BranchModule_STDTool.cs、BranchParam.cs、BranchResult.cs；资源哈希与控件索引见 evidence.json 和 ui-preview.json。
