# 执行节点工具身份丢失修复交接

- 基线提交：a108da498ae21bda155bbf05806b9368d9fa3fe6；分支feature/comm。
- 执行者：Codex单写入，无代理提交、推送或跨AI交接。
- 目标：修复旧方案执行报“轮廓匹配图像来源契约暂不可用”。
- 允许目录：D:/MyWork/SunEyeVision_V2-comm；不修改用户方案或设备，保留既有全部改动。
- 修改文件：src/Workflow/WorkflowNodeFactory.cs；tests/SunEyeVision.UI.Tests/BindingAdaptationB2Tests.cs；既有方案、计划及协作记录。

## 根因和修复

AlgorithmNode构造器把ToolType设为类别Algorithm，WorkflowNodeFactory创建冻结运行节点时没有保留真实toolId。新增EnsureExecutable在转换后执行，静态来源按ToolType读注册表，因Algorithm不是已注册工具ID而得到null，误报契约不可用。通信等动态参数提供契约的工具可以绕过此分支，之前的通信专项未覆盖工厂转换后的静态图像来源。

工厂现在显式设置ToolType=toolId，保留节点身份、参数快照和原绑定。没有跳过预检、解绑、补跑上游或要求重配。独立测试工具声明静态图像和数值输出，验证工厂转换、冻结图像引用和注册契约读取。首次测试复用旧探针ID导致注册表重复元数据，已改用独立探针夹具，没有调整生产注册表行为。

## 验证

- 47项针对性通过；完整UI／工具链142项通过，0失败，包含新增工厂回归。两批有重叠，不能相加。
- 隔离全量Release0错误1155警告，隔离UI Release0错误1049警告。
- 实际隔离启动探针退出0、24工具，不初始化硬件通信；git diff --check通过。
- 证据：artifacts/validation/execution-tool-identity-20261014的identity.trx、regression.trx和isolated-toolbox.json。
- 用户关闭后完成常用目录部署：标准全量Release0错误1140警告，标准UI Release0错误1013警告；重新标准构建并运行142项回归全部通过（standard-regression.trx）。测试与发布的Workflow程序集SHA256一致，避免误用先前隔离程序集作为部署证据。最终常用启动探针退出0、24工具（release-toolbox.json）。

## 未完成和下一步

用户确认关闭后已核实进程退出，完成标准全量／UI构建、常用目录更新和最终启动验证；没有结束用户进程或操作通信设备。

下一步请用户重新打开常用Release，用原方案复测。用户方案未实际运行，不能宣布已验收；不要求重新绑定图像来源。B3服务仍未启用，先收敛执行回归。真实VS设计器、高DPI和用户原方案人工验收待反馈。文档维护技能已同步原因、证据和部署边界。
