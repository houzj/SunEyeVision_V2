# Blob 本次检测/屏蔽/扇环结果显示完整设计（2026-11-02）

## 基线与边界
候选独占目录 blob-reference-chain-20261101，分支 codex/blob-roi-result-display-20261102，产品基线 c8311cffe1f93c9ad379c6f110e629891a97ca2e。旧分支保留；主仓和正式 Release 不写。先整体设计后实施，不运行 VM。

## 原件与复用判定
原件 IMVSBlobFindModuDisplay.xml（REA architecture-recovery/tool-rendering-entry-audit.resources）：ROI 为 rect，DetectCenterX/Y/Width/Height/Angle；ExternROI 为 polygon，BlindPolygonPointsX/Y，固定橙色 FillColor=#FF5400；Annular 为 annularsector，DetectAnnulus 六参数。三者 Status 均 ModuStatus，蓝色 OK、红色 NG；ROI/Annular opacity1/width2，ExternROI opacity0.5/width2。显示页顺序保持九行，未实现的最大内接矩形/轮廓点/基准点/运行点仍保护。其余原静态材料见 B08-结果显示配置到视口静态证据-20261101.md；原本体像素/坐标/时序不因本次接通而变为已证。

现有 BlobInput.Read 先按本次 InputValues 物化继承；BlobSession.ExecuteCore 已取得冻结 localRoi、真实 input.Frame 和 domain，再按 enabled 物化并裁剪检测/屏蔽。BlobOutput 被 DeclaredOutputSnapshot.Output 持有，publication reader 保留其资源；故无需算法输出端口或第二解析器。正式显示 owner 已存在 RuntimeToolConfiguration.Fields 的独立 EditorSettings，样式修订与 RunIdentity 分离；复用事务、历史、复制、JSON、预览 token 和原子 DisplayScene。

## 单一几何快照
拟增 Contracts Presentation 的 IToolResultGeometryOutput.ResultGeometry，只包含不可变 ResultGeometrySnapshot：实际 SourceFrame、CoordinateFrame、执行状态及按角色分组的纯几何值。形状沿既有 RoiDeclarations.Shape 的只读 RecordValue（框架冻结值）复用，避免新造第二几何模型。角色稳定标识由显示 provider 声明，样式也按角色绑定；Blob 私有角色 inspection/mask/annulus。无资源、无 Current 回调、无可订阅 Schema 端口。BlobOutput 在成功 ExecuteCore 的同次冻结输入处生成，非当前草稿；失败/取消不发布新快照。

仅 enabled 且实际参与本次计算的形状进入快照；visible 不是执行开关，不决定结果图形。禁用形状不会展示为实际检测。矩形检测归 ROI；annulus/sector归扇环；检测 polygon 及多形屏蔽是明确本方适配，沿真实已执行几何展示，不称原厂矩形/单polygon等价。FullDomain/None 用 coverage 状态保留，不将非矩形 domain 伪造为矩形；无名义形状时不画假框。像素 Region/位置变换缺可证明名义几何时保守不发布对应图形，不读旧几何冒充。

## 消费与帧/资源
MainWindowResultDisplay 只从身份一致的当前 snapshot.Output 提取快照，并随现 ResultDisplayState 传递；完整 publication/node/document 校验保持。每 pane 必须同时校验 SourceFrame 与 receipt.Frame、CoordinateFrame 与 receipt.Coordinates，包括空 Blob 结果。新几何不依赖 Blobs.Count，故零检测仍可展示本次实际 ROI。publication lease 持有成功输出；纯元数据无需额外 native owner，撤场先撤 authority，再释放原 scene/reader。任何 proof/provider/prepare 失败清本次整景，身份/epoch门禁不清同步重入的新 owner。

原 ShapeLayer/DisplayScene 单次合并准备。扩纯非视觉投影以实现原 IAnnularSector，并沿已证负扫角反向遍历保持弧域；不采样成多边形。Polygon/rectangle 填充使用原 IShape.Fill，不另加 renderer。与可编辑 ROI attachment、只读继承预览分别独立 layer 实例，不覆盖它们。全景预算合计形状与顶点，不靠角色分组绕开。

## 正式字段与交互
在原 ROI/屏蔽/扇环三个槽启用既有眼睛/样式五角色，以 Constant-only EditorSettings 持久；默认仅创建时注入，不覆盖重开。原三个 Draft 占位移除，对应正式行插回同一九行位置；其他四项继续禁用。不新增常驻说明，扩展与无名义几何边界用短 Tooltip。沿既有合法草稿预览、非法 pending 拒绝、取消恢复最新正式、确认提交后新 token；样式不触发算法或失效结果，算法/ROI变更仍走正式失效。

## 集中验收矩阵
1. Model：四种检测形状、四形多屏蔽、disabled、FullDomain/None、空结果，核同次冻结几何而非后改草稿；实际区域敏感点与元数据相符；取消无新结果。
2. Core：保存/复制/重开显示三样式；纯显示不进入执行输入/current；正常算法及引用变化仍失效；原 schema 无新增假输出。
3. Window：真实 Blob Host 确认/取消、样式/眼睛/Undo/Redo，九行原顺序；同次图形与文本、全域无假框/空结果有实际ROI；同尺寸异帧拒绝、换publication/换源/关窗退役；mask编辑层共存。
4. 原子/异常：renderer弧域、fill、预算，旧 proof重入和reader释放复用既有保护并定向补测；独立候选源码冻结后UI/全量构建、专项、相关现有显示/ROI回归，逐轨实际Loaded DLL核哈希。
5. 自然键鼠、VS设计器、VM像素/角度/时序等价没有新实机证据不得声称通过。组合继承正式生产者、图像mask/屏蔽继承编码/动态文本位置/位置修正不在本批扩基础。

## 实施状态
2026-11-02：工作区干净并已从精确基线创建分支；只读盘点完成；共享文件边界已报主线，尚未改产品或启动构建测试。进程CIM查询被当前环境拒绝，无本线活动句柄，不据此猜测GUI空闲。

## 实施合同补充
- 名义几何表示本次输入实际采用的形状，不宣称等于裁剪后像素Region；FullDomain保留coverage不画假框。帧与域由本次图像的SourceFrame、BlobSchema.ImageFrame及发布结果资源证明，视口必须完整匹配。
- 总状态颜色是本方本次BusinessJudgment（BlobOutput.IsPass）映射蓝/红，不是逐Blob色，也不宣称VM ModuStatus/LimitJudge等价。
- 快照复制每个组与RecordValue（RecordValue/SequenceValue内部深冻结），校验原声明、kind、必需参数、有限值/尺寸/扫角/点集；角色唯一。输入已有数量准入16/64、屏蔽多项为用户扩展，展示资源预算仍全景合计。
- 缺名义几何仅对应role无图形；整体物理帧proof失败则清整景，不能留下其他同scene过期图文。全景准备失败也按原原子策略清本次owner，不虚称保留旧场景。
- 负扫角原IAnnularSector以end为起点且range取正表示同弧域；整环360、内半径0必须专项实测。填充沿原IShape.Fill，无新renderer。

## 冻结002实际边界与责任
- `Plugin.Contracts/Results/Presentation/ResultGeometrySnapshot.cs`：固定角色Inspection/Mask/Annulus，coverage None/FullDomain/Explicit/Unavailable；`IToolResultGeometryOutput.ResultGeometry`供实际输出实现，组及集合重建冻结并检查形状声明/参数，SourceFrame与Coordinates分别证明物理帧和逻辑像素域。`ResultDisplayAppearance`三种可选样式均为纯值，独立于RunIdentity。
- Blob `BlobRoiDisplayProjection`从唯一私有Record无状态转换现RoiDeclarations；原Wpf投影复用该方法后仍执行原编辑校验。`BlobResultGeometry`只收本次resolved输入的enabled名义形状，按固定角色顺序冻结，`BlobSession`成功创建BlobOutput时传递；不进入ToValues/Schema，不可订阅，也不持有native资源。
- `MainWindowResultDisplay`只在原publication/document/node校验通过后取同snapshot.Output元数据。`ResultWorkspacePresenter.Display`先借publication，再核几何SourceFrame/Coordinates；空结果同样核。角色缺名义几何不造值；整体proof失败清整景，prepare失败不保留半套新景。旧owner重入保护沿原事务。
- `NativeRoiResultProjection`实现纯只读IAnnularSector，其他矩形/Polygon沿原NativeShapeProjection；新增可选Fill保持原shape管线。DisplayScene总计shape/vertex/text预算，未占附件及继承预览layer。屏蔽默认橙填充、透明度0.5；四形/多项仅称本方用户扩展。
- 原九行保留顺序：Blob、最大内接矩形、ROI、轮廓点、基准点、运行点、文本、屏蔽、扇环。三槽由正式Constant-only五样式字段替换原UI-only，占位剩4项；总显示28/Editor83/算法55。原结果页与文本七角色不另排布局。
- 首轮UI与全量自然exit0；确认终态后集中修正角色顺序、三弹层标题、坐标声明一致性并冻结002。001成功不代替002最终载荷验证。测试在产品冻结期间单独编写，不与构建交叉改产品。
