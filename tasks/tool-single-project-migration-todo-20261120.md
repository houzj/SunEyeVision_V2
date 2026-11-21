# 双项目工具单项目迁移任务清单

## DivideImage / 划片拆分

- [ ] 原厂证据、页签、端口和结果映射清单
- [ ] 当前 Runtime/Wpf 与单项目骨架差异清单
- [ ] 合并 Algorithms、Parameters、Results、Editor、Resources
- [ ] 更新 slnx、ToolComponents.props、ToolboxDisplayCatalog 和 manifest
- [ ] 删除旧 Runtime/Wpf 项目注册与项目文件
- [ ] 声明测试、执行测试、ControlNamingProbe
- [ ] Release 构建、封存、载荷哈希检查
- [ ] Agent Desktop 打开、页签、关闭和进程退出
- [ ] 更新证据、清单和 CURRENT_TASK

## BlobLabels / Blob 标签分析

- [ ] 等 DivideImage 完整闭口后开始

## ContourMatch / 轮廓匹配

- [ ] 等 BlobLabels 完整闭口后开始

## GrayMatch / 灰度匹配

- [ ] 等 ContourMatch 完整闭口后开始

## MarkFind / 图形定位

- [ ] 等 GrayMatch 完整闭口后开始

## If / 条件分支

- [ ] 等 MarkFind 完整闭口后开始

## Checkpoint

- [ ] 六个工具均为单项目结构
- [ ] 六个工具的旧 Runtime/Wpf 项目均已移除
- [ ] 六个工具均有独立 Release 和运行验证记录
