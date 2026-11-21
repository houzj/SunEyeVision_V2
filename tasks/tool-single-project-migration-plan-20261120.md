# 双项目工具单项目迁移计划

## 目标

按 `.opencode/skills/tool-production` 的证据先行流程，逐个把仍保留 `Runtime + Wpf` 的六个工具迁移为单项目结构。工具层保留原厂职责映射，公共参数页、ROI、结果显示和窗口生命周期继续复用已验证的平台链路。

## 工具顺序

1. 划片拆分 `DivideImage`
2. Blob 标签分析 `BlobLabels`
3. 轮廓匹配 `ContourMatch`
4. 灰度匹配 `GrayMatch`
5. 图形定位 `MarkFind`
6. 条件分支 `If`

## 单工具闭口标准

- 原厂参数、页签、分组、端口、结果、资源和生命周期证据已登记。
- 单项目 `.csproj` 同时声明 Runtime 和 Editor 角色。
- `SunEyeVision.slnx`、`product/ToolComponents.props`、工具箱目录和清单同步。
- 旧 `Runtime/*.csproj` 与 `Wpf/*.csproj` 已从解决方案和产品注册中移除。
- 声明测试、执行测试和统一探针通过。
- Release 构建完成后重新封存载荷，启动准入读取到最新哈希。
- Agent Desktop 窗口打开、页签切换、关闭和进程退出通过。
- 人工视觉差异和剩余 `unconfirmed` 单独记录，算法等价没有黄金样本时保持未确认。

## 每个工具的纵向任务

### Phase A：证据和结构

- [ ] 确认原厂发布单元、页签 XML、显示 XML、参数对象和控件资源。
- [ ] 盘点当前 Runtime/Wpf 文件职责、项目引用和产品注册。
- [ ] 生成工具目标结构和差异清单。

### Phase B：单项目迁移

- [ ] 合并源码到 `Algorithms/Parameters/Results/Editor/Resources`。
- [ ] 固定单项目程序集身份和 Runtime/Editor 双角色声明。
- [ ] 同步解决方案、产品组件和工具箱注册。
- [ ] 删除旧项目注册和旧项目文件，不保留双轨兼容层。

### Phase C：验证和闭口

- [ ] 运行清单、结构、第三方边界和工具专属静态门禁。
- [ ] 更新声明/执行测试和统一生产探针。
- [ ] 运行 Release 前置检查、构建、封存和载荷哈希检查。
- [ ] 运行 Agent Desktop 工具窗口验证并保存快照。
- [ ] 更新工具清单、证据文件和 `CURRENT_TASK.md`。

## 当前执行点

当前工具为划片拆分 `DivideImage`。在其满足单工具闭口标准前，不迁移 Blob 标签分析。

## 风险和处理

| 风险 | 处理 |
|---|---|
| 旧 Runtime/Wpf 项目仍被解决方案引用 | 先更新注册和解决方案，再删除旧项目文件 |
| 单项目共享程序集角色计数错误导致启动卡页 | 构建后先做组件双角色探针，再封存并启动 |
| 原厂动态字段行为没有证据 | 登记 `unconfirmed`，不写猜测性替代 |
| Release 哈希过期 | 构建后强制运行 `Seal-ReleasePayload.ps1` |
| 多工具同时修改公共框架 | 公共框架只由主线修改，工具迁移按顺序进行 |
