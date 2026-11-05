# rea 进度

更新时间：2026-11-05 14:40
当前卡：T-0005（状态：claimed）

## 一、已完成（可核验）

- [T-0005] 工具层批次 R1 —— `docs/collaboration/evidence/VM工具逆向批次R1-在研与流程控制-20261105.html`（38,722 B）
- [T-0005] 工具层批次 R2 —— `VM工具逆向批次R2-查找与卡尺-20261105.html`（29,348 B）；结论：33 声明域 / 27 有效、卡尺双 ROI（`ROIBOX` 5 标量、`ROIANNULUS` 6 标量）、11 种 ROI 形状 Tab、45→复核 44 种 Style
- [T-0005] 工具层批次 R3 —— `VM工具逆向批次R3-图像处理与拼合-20261105.html`（48,509 B）；结论：34 模块、专用编辑器 7/34=20.6%、`ROISelecter` 逐工具声明形状集合、`Style` 只约束基数不约束字段名、全局库外 3 个内联 Style（`ACQUIMAGE`/`DEBUG`/`GENERALINFO`）
- [T-0005] 工具层批次 R4 —— `VM工具逆向批次R4-缺陷识别与颜色-20261105.html`（37,808 B）；结论：声明 52 / 实物 24（深度学习 28 个未安装）、`ToolItemInfo.xml` 才是工具箱准入（`IMVSMarkInspModu` 反例）、缺陷结果族 8 字段跨 12 模块同构
- [T-0005] 工具层批次 R5 + 191 总表收口 —— `VM工具逆向批次R5-计算标定通信与总表-20261105.html`（42,481 B）；结论：191 清单 / 163 目录 / 162 有效 / 159 工具箱 / 65 专用编辑器；差异四类（未装 28、清单外 5、改名 2 组、空目录 1）
- [T-0005-1 启动时序] `VM启动时序与失败表现矩阵-20261105.html`（35,935 B）；结论：11 日志子系统、6 个版本键（`MVDAlgorithmSDK_DeepLearning` 为空 = 深度学习未装的官方证据）、`AutoLoadSolution.cfg` 四字段、崩溃落文本栈
- [T-0005-1 方案格式版本] `VM方案格式版本与兼容迁移-20261105.html`（27,218 B）；结论：方案是 ZIP 包（7 条目）、版本字段为 `SoftVersion/Version@CurrentVersion="V4.4.0"`（产品串非整数）、版本不符走 `CompatibleTool.exe` 五阶段迁移 + 另存新文件、`Failed Module Num:4` 只 WARN 不阻断、字段改名靠 `CompatibleTool.xml` 的 `SrcName→DstName`
- [T-0005-2 插件准入与身份链] `VM插件准入与工具身份链-20261105.html`；结论：三层元数据（分类 `ToolGroup.xml` 15 个 / 工具 `ToolItemInfo.xml` 159 个 / 清单 `ModulePathGenerate.txt` 191 行）、分类显示名≠目录名（`Location→Locating`）、`priority` 含 6 缺位、我方 `ToolGroups.xml` 的 `Order` 与原厂 `priority` 逐一相同但机制自建

## 二、进行中

- T-0005 剩余第 3 项：`VM日志落点保留与文件级机制-20261105.html` —— 日志 11 子系统落点/滚动阈值/备份数 + 偏好机制 + 恢复目录。数据已取齐（见下"实测数字"），待落盘。

## 三、阻塞（需要裁决）

- 无 A/C 类阻塞（不需要开原机窗口即可继续静态取证）。
- 一处**待中心裁决**（B 类，不影响我继续）：我方 `ValidateSchema`「版本≠1 拒绝」与原厂「版本不符走迁移 + 另存」方向相反（见方案版本文档 §2 的 P0 行）。我倾向：整数 schema 版本继续管内部结构，但对外版本不符时改为「只读打开 + 提示 + 另存」，不硬拒。是否采纳由中心决定，我不改代码。

## 四、实测数字

取证方式：只读文件解析 + ZIP 内存解包（`System.IO.Compression.ZipFile.OpenRead`），PowerShell 5.1，未启动原厂程序、未写注册表。

- 工具层分母：清单声明 191 / 实物目录 163 / 空目录 1 / 有效 162 / 有 `ToolItemInfo.xml` 159 / 有 `Control.dll` 65
- 分类数：16（清单）vs 17 目录（含 `.CommonControl`）
- 未安装：28（DeepLearning 14 + RegisterLearning 6 + Recognition 8）
- 清单外：5（Global 4 + Calibration 1）；改名：2 组（`SortModule`↔`DataSortModule`、`DataStatisticsModule`↔`DataRecordModule`）
- ROI 形状词目：9 种（`Box`/`UroBox`/`SectorRing`/`Sector`/`OverSector`/`Polygon`/`Point`/`Line`/`CircleMes`）
- 全局 Style 去重：44（R2 曾记 45，差 1 未定位，已记账不回改）
- 方案样本：5 份 `.prc`，全部 `CurrentVersion="V4.4.0"`，`SaveInfo` 2023-11-06 by `CompatibleTool.exe`
- 迁移日志：`log\UI\CompatiableTool.log` 2,967 B 全文，`V4.3.0` → `V4.4.0`，`Failed Module Num:4`
- 日志子系统：11 个目录、实测总量约 293 MB、`GateWay` 配置 `10MB × 5 备份` 与实测 `GateWay.log.1..5` 完全吻合

## 五、未验项

- 迁移**失败**时的日志与产物（只有成功样例）
- 方案版本**高于**当前软件时（降级打开）的表现 —— 无任何证据
- `.sol` 包内条目是否与 `.prc` 完全同构（本批只解 `.prc`）
- 模块级加载失败的具体日志行（`ModuleProxy.log` 3.6 MB / `ModuleFrame.log` 5.0 MB 未读）
- 我方节点实例是否用 `Guid`（原件 `Guid` + `Index` 并存）
- 真实启动阶段顺序（未启动程序，§1.2 全为推断）

## 六、越界与夹带（有则写，无则写"无"）

- 无。本线只写 `docs/collaboration/evidence/VM*.html` 与本进度/回执文件；未碰 `src/`、`tests/`、`bus/README.md`、`bus/status.json` 中他人卡片；未 commit / 未 push。
- 夹带说明：对我方 `src/Plugin.SDK/Metadata/ToolGroups.xml`、`artifacts/.../release-payload.json` **只读**（用于对照校正），未修改。
