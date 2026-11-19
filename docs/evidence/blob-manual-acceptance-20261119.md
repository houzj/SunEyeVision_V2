# Blob 分析人工确认清单

本清单对应 VisionMaster 4.4.0 Build20250516 原厂窗口与当前 Release 单项目产物。人工确认完成前，不能进入 ImageSource。

## 当前产物

- 当前 Release：`Release/SunEyeVision_v1.0.0/SunEyeVision`
- 有效连线方案：`.testdata/agent-desktop/blob-connected/blob-connected.solution`
- 原厂窗口快照：`Release/_validation/AgentDesktopToolReview/blob-comparison-post-algorithm-context/Blob分析1/vm-tool-window.json`
- 自研窗口快照：`Release/_validation/AgentDesktopToolReview/blob-post-context-direct/20261119-050054-Blob分析1/tool-window.json`
- 单项目发布载荷：`SunEyeVision.Tool.BlobAnalysis.Runtime.dll`

## 人工确认项

| 编号 | 检查项 | 原厂对照 | 当前证据 | 人工状态 |
|---|---|---|---|---|
| B1 | 窗口尺寸和标题栏 | 420×500、Blob 标题、关闭按钮 | 双侧 Agent Desktop 快照 | 待确认 |
| B2 | 基本参数页 | 基本参数、图像输入、ROI区域 | 静态顺序门禁、双侧快照 | 待确认 |
| B3 | 图像来源 | 有效连线后来源可选且显示上游图像 | `blob-connected.solution`、输入映射门禁 | 待确认 |
| B4 | ROI 区域 | 图形/图像类型、绘制/继承、屏蔽区和位置修正 | Blob ROI 组装映射、窗口快照 | 待确认 |
| B5 | 运行参数页 | 阈值、极性、数值和范围控件 | Blob 参数静态门禁、双侧快照 | 待确认 |
| B6 | 结果显示页 | 结果判定、结果字段和显示区域 | BlobDisplay.xml、结果投影代码 | 待确认 |
| B7 | 底栏命令 | 连续执行、停止执行、执行、确定 | 双侧 UIA 快照 | 待确认 |
| B8 | 单次执行 | 执行按钮可用，窗口不报错 | Blob 功能审计证据 | 待确认 |
| B9 | 关闭释放 | 关闭后进程和窗口资源退出 | `close-app.json`、强制关闭兜底 | 待确认 |
| B10 | 外观和交互 | 节点、字段、ROI 和命令与原厂体感一致 | 原厂/自研双侧快照 | 待确认 |

## 自动门禁结果

- Blob 单项目 Release 编译通过。
- 发布单元、第三方算法边界、基本页顺序和 GroupLink 映射静态门禁通过。
- Release 启动页已进入 `SunEyeVision · 起始页`，没有启动失败窗口。
- 当前仍需人工确认 B1-B10。自动化证据不替代人工视觉确认。

人工确认完成后，在本文件将 `待确认` 更新为 `通过` 或 `问题`，并记录截图路径和问题编号。确认前不开始 ImageSource。
