# 工具双项目改单项目迁移记录

## 当前事实

Blob 分析和 ImageSource 当前各有 Runtime 与 Wpf 两个项目。Wpf 项目承载编辑器入口、专属视图、设计时数据和 XAML。发布清单把两个项目登记为同一 ToolId 的 Runtime/Editor 两个组件。

## 不能先删除 Wpf 项目的原因

当前发布合同要求每个产品组件工程声明唯一的 `ToolComponentRole`、`ToolComponentId`、`ToolComponentEntryType` 和程序集名。`installer/Product.Components.ps1` 会逐项核对项目属性，`installer/Release.Common.ps1` 还要求每个带组件声明的程序集只拥有一个组件声明。Runtime 与 Editor 共用一个程序集目前会在发布封存和载荷校验阶段被拒绝。

## 单项目目标

每个工具只保留一个项目和一个程序集。该程序集同时包含算法入口、参数/结果声明、编辑器入口、WPF 视图、设计时数据和工具资源。Runtime 与 Editor 仍保留各自的组件身份和入口类型，角色差异由同一程序集内的两个组件声明表达。

```text
tools/SunEyeVision.Tool.BlobAnalysis/
├─ SunEyeVision.Tool.BlobAnalysis.csproj
├─ Algorithms/
├─ Parameters/
├─ Results/
├─ Editor/
│  ├─ BlobEditorFactory.cs
│  ├─ Views/
│  └─ Design/
└─ Resources/
```

## 迁移顺序

1. 发布合同允许同一程序集声明一个 Runtime 组件和一个 Editor 组件，并按组件身份校验入口类型。
2. 将 Blob Wpf 项目的源文件、XAML 和资源并入 Blob 单项目，开启 `UseWPF` 并引用公共 WPF 框架。
3. 在同一程序集保留 Runtime 与 Editor 两个 `ToolComponentAttribute`，更新产品组件清单让两个组件指向同一项目和同一 DLL。
4. 更新载荷封存、组件目录、编辑器解析和测试宿主，确认同一程序集的双角色加载只创建一个 Runtime 定义和一个 Editor 工厂。
5. 重新构建 Blob Release，运行发布载荷、组件目录、参数窗口和生命周期门禁。
6. 仅在 Blob 单项目发布链闭合后删除 `SunEyeVision.Tool.BlobAnalysis.Wpf.csproj`，再按相同路径迁移 ImageSource。

## 禁止事项

- 不能先删除 Wpf 项目再用兼容回退维持旧编辑器加载。
- 不能把 Editor 入口从发布清单移除来伪装成单项目完成。
- 不能改变 Runtime/Editor 的 ToolId、入口类型或原厂 UI 组装职责。

## 当前状态

本记录建立于 2026-11-19。发布合同已支持同一程序集的 Runtime/Editor 双声明。Blob WPF 源码已并入 `tools/SunEyeVision.Tool.BlobAnalysis/Editor/`，Blob 单项目 Release 编译和正式载荷封存通过，旧 `tools/SunEyeVision.Tool.BlobAnalysis.Wpf/` 已删除。ImageSource 仍保留 Runtime + Wpf 双项目，下一阶段按同一流程迁移。

启动验证补充：共享程序集准入和 Runtime-only 过滤修复后，Release 进程已由 Agent Desktop 进入 `SunEyeVision · 起始页`，没有“启动失败”窗口。一次优雅关闭超过 Agent Desktop 截止时间，强制关闭确认进程退出，审核脚本已加入优雅关闭超时后的强制退出兜底。

工具推进门禁：Blob 的原厂/自研静态结构、Release 和 Agent Desktop 证据完成后，必须等待人工确认。人工确认前只继续补 Blob 证据，不进入 ImageSource 或其他工具迁移。ImageSource 的单项目试探改动已撤回。
