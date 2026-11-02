# 显示原件族中立命名候选交接（2026-11-03）

- 基线提交：99537c5abcf4f0861acc9f8b00b6c02d75abe843。
- 当前分支：codex/blob-display-neutral-20261103。
- 当前执行者：内部独立实施线 display_neutral_implementation；本工作区唯一产品写者。
- 任务目标：显示原件完整依赖闭包改为职责命名，保持原件布局、字段、行为、算法及家族隔离。
- 允许修改目录：本独立工作区显示源码、主题、资产、实际消费者及必要测试引用；Catalog、原始来源清单、O06 主方案及协作协议不在写入范围。
- 已修改文件：精确 765 项目标及 SHA、652 项路径迁移、命名空间/类型/成员/URI/LogicalName 见 DISPLAY_NEUTRAL_TARGET_MAP_20261103.json。

## 已完成的实施

VMBaseControls 改为 ImageView，VMControls 改为 Primitives，独立 VMUtility 改为 Primitives.Platform，RenderInterface 改为 Display.Contracts，HikVision.Styles 改为 Primitives.Styles。7 个品牌类型与颜色类型/成员按角色命名。不同原件族未合并；Numeric 原件只更新其对本显示族的引用。C#、XAML、资源基名、pack URI、动态光标路径、消费者与测试同批更新，不留旧名称定义或兼容桥。

40 项实际移动的二进制图像/光标与基线逐字节相同；真实程序集资源也已逐项取出核对。16 项 Aero2 依赖按原来源文件 SHA 指向对应本项目 BAML，其中 NumericUpDown 保留独立 Numeric Aero2；ExpanderButton 的箭头指向本显示族原 PNG。17 项旧外部 URI 的证据逐条收入映射，不修改旧 provenance。

唯一获授权的附带文案修改：移除 BlobParameterEditor.Bind 后强制覆盖 display.text.x/y/content 的过期 ToolTip；不改功能合同，不删除其他未支持保护提示。新增相称的真实 Aero2/原箭头资源入口检查；首次漏加载正式样式的测试失败已保留，补齐真实样式作用域后通过，未据此改产品行为。

## 验证与边界

自行 restore 后构建 UI 与全量 Release，均加 HalconSourceRoot 指向主仓已有 HALCON 源目录；未复制现成 Release 或厂商运行库。最终 UI004 和 full003 均零错误（警告分别6523/6514），六轨33+44+7+4+12+24共124组及资源入口1组通过；每轨实际加载产物SHA与本候选、测试源码SHA均逐项一致，现有警告如实保留。最终候选载荷身份与回执见 DISPLAY_NEUTRAL_CANDIDATE_20261103.json；资源及类型审计包含在该文件。实际程序集不存在旧显示命名空间类型定义。

设计时由用户另验；本线未启动原 VM/SDK 或用户桌面程序。候选通过不等于主仓正式构建、人工验收、整个 Blob/S2/S3 完成。

## 未完成与交接

主线按映射同步 Catalog 的当前目标路径及目标 SHA，再集成后正式构建/相称验收；原 assembly/type/MVID/sourceSha 及历史证据不得改写。工作流独立分支可以继续原命名开发，集成时应用本精确映射。没有因为本次机械迁名提前展开算法、共享显示合同或其他控件家族。

本工作区 CURRENT_TASK 另记本线状态但不纳入提交，避免覆盖主线任务记录。原主方案阶段维护由主线统一执行。
