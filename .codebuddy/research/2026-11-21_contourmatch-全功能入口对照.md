# 调研报告：轮廓匹配全功能入口对照

- **调研日期**: 2026-11-21
- **调研者**: 主线对话（code-explorer 双线取证）
- **关联方案**: 轮廓匹配全功能对照迁移同步
- **状态**: 已完成（清单阶段）

## 1. 调研背景

用户要求对照原厂做全功能对照迁移。先列全量功能入口清单，再逐项对照同步。本报告只做清单与差异判定，不含实施方案。

## 2. 关键问题清单

1. 原厂参数窗口有几个页签、每页签有哪些分组与控件
2. 原厂模板配置弹窗有哪些区域与控件
3. 节点级入口（帮助/重置/连续执行/停止/执行/确定）是否齐备
4. 输入输出端口与原厂数量差异
5. 本方哪些入口是空实现/未接线
6. 哪些能力被显式拒绝（NOT_READY）

## 3. 原厂证据

### 3.1 页签集合（4 页，参数窗口 420×500）

| # | 页签 | 显示方式 | 证据 |
|---|---|---|---|
| 1 | Tab_Basic Params / 基本参数 | 可滚动，含分组标题 | vm-param-current.json:1；resources.json:41-44 |
| 2 | Tab_Feature Template / 特征模板 | CategoryNoHead 整页面板 | resources.json:45-47；架构对照:306 |
| 3 | Tab_Run Params / 运行参数 | 可滚动 | run-tab.json:1；resources.json:48-50 |
| 4 | ResultShow / 结果显示 | 3 分组（结果判断/图像显示/文本） | result-tab.json:1；resources.json:51-54 |

### 3.2 基本参数页控件

| 分组 | 控件 | 类型 | 证据 |
|---|---|---|---|
| Tab_Image Input | 输入源 | 引用选择 | resources.json:298 |
| Tab_ROI Area | 输出掩膜 | 开关 | resources.json:57-59 |
| Tab_ROI Area | 输入掩膜 | 开关 | resources.json:61-63 |
| Tab_ROI Area | ROI 区域类型 | 单选（图形/图像） | resources.json:65-71 |
| Tab_ROI Area | ROI 创建 | 单选（绘制/继承） | resources.json:73-75 |
| Tab_ROI Area | 外部 ROI 类型 | 继承方式 | resources.json:77-79 |
| Tab_ROI Area | 形状（矩形/圆/多边形） | 按钮组 | 对照文档:24 |
| Tab_ROI Area | 屏蔽区创建 / 屏蔽区 | 单选 + 画笔 | 对照文档:25-26 |
| Tab_ROI Area | 位置修正 | 开关 AssistGroup | resources.json:81-83 |

### 3.3 特征模板页（整页面板，无组头）

| 控件 | 类型 | 默认值 | 禁用条件 | 证据 |
|---|---|---|---|---|
| 创建 | SuperImageButton | — | — | featurematchconfigpanal.xaml:51-71 |
| 载入 | SuperImageButton | — | — | :73-93 |
| 模板列表-序号 | 列宽 28（列头隐藏） | — | — | :96-114 |
| 模板列表-名称 | 列宽 92，TextBox MaxLength=63 | 新建模板1 | 模型非法 | :115-132 |
| 操作列 上移/下移/删除/勾选 | 按钮×3 + CheckBox | 勾选 | 无选中/模型非法→上/下移禁用 | :133-177 |
| 预览图 | 98×98 | 占位图 | 无选中→占位图 | :210,217-226 |
| 编辑模板 | 98×24 | — | 无选中/非法→禁用 | :211,218-225 |
| 删除所有模板 | 98×24 | — | — | :212 |

### 3.4 运行参数页控件

常显：全部搜索模式(False)、最小匹配分数(0.5)、最大匹配个数(1)、匹配极性(下拉，考虑极性)、角度范围(-45~45)、尺度范围类型(单选：尺度范围/尺度范围XY)、尺度范围(1~1)、尺度范围X/Y(仅 XY 模式)、横切角度范围。
高级折叠：最大重叠率(50)、排序方式(分数降序)、阈值模式(自动)、边缘阈值(40，手动时)、考虑噪点(False)、延拓阈值(0)、超时控制(0)、轮廓使能(True)、轮廓点使能(False)。
跟随项：个数为 0 时判定 OK(False)。
证据：resources.json:88-175；parameters.json 对应项。

### 3.5 结果显示页

结果判断组：忽略匹配个数、数量判断(+范围)、角度判断(+范围)、尺度判断(+范围)、X尺度判断(+范围)、Y尺度判断(+范围)、分数判断(+范围)、匹配点X/Y判断(+范围)、Box点X/Y判断(+范围)、高级折叠。
图像显示组：检测区域、匹配结果、匹配点、匹配轮廓点、基准点、运行点、匹配框、盲区。
文本显示组：结果文本 + 颜色/字号/透明度/坐标。
证据：resources.json:176-295,305；result-tab.json:1。

### 3.6 模板配置弹窗（900×534）

| 区域 | 控件 | 证据 |
|---|---|---|
| 顶部 | 选择当前图像 / 选择其他图像 / 模板存图(开关,默认开) / 保存模板工程 / 全部屏蔽 | hppyramid...style.xaml:513-535 |
| 图像工具条 | 移动图像(默认选中) / 扇形掩膜 / 扇圆形掩膜(隐藏) / 矩形掩膜 / 多边形掩膜 / 选择模板匹配中心 / 生成模型 / 擦除轮廓点(随轮廓点使能) / 绘制 ROI / 清空 / 撤销 / 重做 | :564-757 |
| 缩放条 | 放大 / 缩小 / 1:1 / 全屏 | :766-769 |
| 画布 | ImageViewBox（ROI 类型，多边形≤64 点） | :778 |
| 右侧列表 | 名称 / 可见性眼睛 / 符号下拉 / 删除 | :796-835 |
| 右侧参数组 | 8 行：尺度模式 / 速度尺度(1-10) / 特征尺度(1-10) / 阈值模式 / 边缘阈值(1-255) / 链长模式 / 最小链长(2-1000) / 建模类型 | :857-906 |
| 底栏 | 匹配中心 FixX/FixY / 锁定 / 确定 | 实机 vm-param-current.json:1 |
| 子弹窗 | 橡皮擦大小面板 / 画笔大小面板 / 文件对话框(*.contourmxml) | :676-743；DialogUI:10 |

弹窗显隐：全部屏蔽=开 → 隐藏掩膜形状与橡皮擦，显示画笔；匹配点锁定 → 隐藏选中心按钮。

### 3.7 节点级入口

| 入口 | 原厂 |
|---|---|
| 窗口标题「{节点ID} {工具类型}」 | 有（paramstabcontrol:14-21） |
| 帮助按钮 | 有 |
| 重置参数 | 有（运行参数页可见） |
| 连续执行 / 停止执行 / 执行 / 确定 | 有（底栏） |
| 执行中进度遮罩 | 有 |
| 双击/右键菜单/工具栏 | 未取证（无 Modu 主程序集反编译） |

### 3.8 端口

- 输入端口 5 个、输出端口 22 个（evidence.json:13-14），逐项明细未取证。
- 已知 input.image 必需。

## 4. 本方现状

### 4.1 页签与组装

4 页签齐备（基本参数/特征模板/运行参数/结果显示）；特征模板页整页被 ContourMatchTemplatePanel 占据（ContourMatchParameterEditor.xaml.cs:62-66）；基本页 ROI 区 + 位置修正由 PageBuilt 插入（:80-94）。

### 4.2 字段规模

66 字段（模板 10 + 运行 25 + 结果判断 31）；9 个不生成 presentation、8 个被参数页抑制改在配置窗口、TrainingRois 不呈现。

### 4.3 空实现 / 未接线（关键差异）

| 项 | 现状 | 证据 |
|---|---|---|
| 列表「上移/下移」 | 空 lambda `() => { }` | ContourMatchTemplatePanel.cs:297-302 |
| 列表「加入搜索列表」勾选 | 无绑定、无副作用 | :306-308 |
| 列表预览图 | 固定资源图，Reload 不刷新 | :33,122-124 |
| 列表多行（多模板） | 单模型，只一行「轮廓模板」 | :276-281 |
| 配置窗口样本回写 | `_ = acceptDocument` 丢弃 | TemplateConfigWindow.xaml.cs:31 |
| 配置窗口确定 | 只置 _confirmed，不写草稿、不关窗 | TemplateConfigData.cs:341-346 |
| 配置窗口 8 项参数回写 | 无写回 _draft 路径（只 LoadFromDraft 读） | TemplateConfigData.cs:62-69,120-136 |
| 旧配置窗口 900×534 | 无引用=死代码 | ContourMatchTemplateConfigWindow.cs:24 |
| CreateTemplate()（走 RoiInput 建模型） | 未被调用 | ContourMatchTemplatePanel.cs:201-221 |
| 圆环继承 / 圆环参数行 | ShowAnnulusSource=false | ContourMatchRoiComposer.cs:31,53 |
| ROI 形状 | 仅 Box/Polygon（与原厂 ROISelection 一致） | ContourMatchParameterEditor.xaml.cs:86-87 |

### 4.4 NOT_READY（12 条，仅执行边界拒绝）

链长模式非自动、建模类型非全轮廓、X/Y 尺度≠1、X/Y 尺度判断开启、阈值类型非自动、考虑噪点开启、延拓阈值≠0、输出轮廓开启、全部搜索模式开启、多模板重叠率≠50、尺度范围类型≠0、横切角度≠0。
证据：ContourMatchSettings.cs:233-262；调用点 ContourMatchSession.cs:32-34。

### 4.5 端口

输入 3 个（input.image 必需、input.roi、input.fixture）+ 搜索区域 6 项 + ROI 组合 19 项 + 位置修正 20 项；输出 11 个（run-time/status/count/scores/match-points/angles/scales/boxes/outline-points/fixture/rows）。
原厂为输入 5 / 输出 22，逐项明细未取证，无法逐端口对照。

## 5. 原厂 ↔ 本方逐项对照

| 维度 | 原厂 | 本方 | 判定 |
|---|---|---|---|
| 页签数量与标题 | 4（含 CategoryNoHead 特征模板） | 4，同 | 已一致 |
| 基本参数页控件 | 掩膜输入/输出、ROI 类型、创建方式、外部 ROI、形状、屏蔽区、位置修正 | ROI 组合 + 位置修正；掩膜/外部 ROI 部分 | 部分一致 |
| 特征模板页面板 | 创建/载入/列表(序号/名称/操作)/预览/编辑/删除全部 | 全部有，但上下移空实现、勾选无绑定、预览不刷新、单行 | 有入口，行为缺失 |
| 运行参数页控件 | 常显 7 + 高级 9 + 跟随 1 | 同 25 项（X/Y 尺度等 4 项无 presentation） | 基本一致 |
| 结果显示页 | 3 组（判断/图像/文本） | 3 组齐备 | 已一致 |
| 模板配置窗口 | 900×534 全区域 | 有壳，样本回写/确定/参数回写断链 | 有壳，链路断 |
| 节点级入口 | 帮助/重置/连续/停止/执行/确定 | 齐备（宿主统一） | 已一致 |
| 端口 | 输入 5 / 输出 22 | 输入 3 / 输出 11（明细未取证） | 未取证，无法判定 |
| 多模板 | 支持多模板列表 | 单模板 | 未实现 |

## 6. 结论与建议

### 6.1 必须优先同步（断链，影响可用性）

1. 模板配置窗口样本回写与确定按钮断链（`_ = acceptDocument`、Confirm 不写草稿不关窗）
2. 配置窗口右侧 8 项参数无写回 draft 路径
3. 模板列表上下移空实现、加入搜索列表勾选无绑定、预览图不刷新

### 6.2 功能级差异（需原厂语义补证后实施）

4. 多模板列表（原厂多行 + 上/下移/删除/勾选语义）
5. 基本参数页掩膜输入/输出、外部 ROI 类型
6. 圆环继承与圆环参数行

### 6.3 能力边界（保持显式拒绝，不改）

12 条 NOT_READY：执行边界显式拒绝，编辑期允许切换（已对齐原厂参数页行为）。

### 6.4 未取证项（不得据此实施）

- 输入 5 / 输出 22 端口逐项名称与类型
- 节点双击/右键/工具栏入口
- 列表 Sign 下拉选项集合
- 运行参数高级项折叠默认态

### 6.5 后续行动

- [ ] 用户确认同步优先级
- [ ] 移交「方案设计」输出实施方案

## 6.6 补充取证结果（原厂 XML 原件，2026-11-22）

取证源：`C:\Program Files\VisionMaster4.4.0\Applications\Module(sp)\x64\Location\IMVSContourMatchModu\`（IMVSContourMatchModu.xml / AlgorithmTab.xml / ToolItemInfo.xml）

### 6.6.1 端口明细（IMVSContourMatchModu.xml，输入 5 组 / 输出 22 项）

输入：
1. `InputImage`(IMAGE)：InImage / InImageWidth / InImageHeight / InImagePixelFormat
2. `InputROI`(ROIBOX)：ROI CenterPoint{roicenterx,roicentery} / roiwidth / roiheight / roiangle
3. `Position Correction Info`(FIXTURE)：Fixtured Point{X,Y} / FixtureInitAngle / FixtureInitScaleX/Y / Unfixtured Point{X,Y} / FixtureRunAngle / FixtureRunScaleX/Y
4. `InputMask`(IMAGE)：InMask / InMaskWidth / InMaskHeight / InMaskPixelFormat
5. `InputBlindPolygonString`(string)

输出（22）：ModuRunTime、ModuStatus、MatchStatus[]、MatchNum、MatchModelIndex[]、MatchModelName[]、
MatchRect[](中心X/Y+宽/高/角)、Match Point[](X/Y)、MatchScale[]、MatchScaleX[]、MatchScaleY[]、MatchScore[]、SkewX[]、ROI(检测区域)、MatchOutline(pointset)、MatchOutlineNum、ContourPoints[](X/Y,RW)、ContourIndex[](RW)、BlindPolygon[]、BlindPolygonString[]、OutputMask(IMAGE)、Output Position Correction Info(FIXTURE, Visible=false)。

本方差异：缺 InputMask、InputBlindPolygonString；输出缺 MatchStatus/MatchModelIndex/MatchModelName/MatchScaleX/Y/SkewX/ROI/MatchOutline/MatchOutlineNum/ContourPoints/ContourIndex/BlindPolygon/OutputMask/Output Position Correction。

### 6.6.2 运行参数页原厂默认值与触发器（AlgorithmTab.xml 原件）

| 控件 | 标签 key | 默认值 | 可见 | 触发器 |
|---|---|---|---|---|
| UseMatchAllMode | RunParam_Use Match All Mode | False | 是 | CurValue=True → MultiModelMaxOverlap 可见 |
| MultiModelMaxOverlap | NMS Overlap Threshold | 50 | **否（常驻但不显示）** | 由 UseMatchAllMode 控制 |
| MinScore | Min Match Score | **0.8** | 是 | — |
| MaxMatchNum | RunParam_Max Number to find | 1 | 是 | — |
| Polarity | Match Polarity | 1 | 是 | — |
| RangAngle | AngleRange | 0 | 是 | — |
| RangeScaleType | RangeScaleType | 0（尺度范围） | 是 | =0→RangScale 可见；=1→RangScaleX 可见 |
| RangScale | Scale Range | 1（范围 0.1~10） | 否 | — |
| RangScaleX / RangScaleY | X/Y Scale Range | 1 | 否 | 由 RangeScaleType=XY 控制 |
| RangSkewX | RangSkewX | 0 | 否 | — |
| Expand | — | — | — | False→MaxOverlap 以下折叠 |
| MaxOverlap | Overlap Threshold | 50 | 折叠内 | — |
| SortType | RunParam_Sort | 2 | 折叠内 | — |
| MatchThresholdFlag | ThresholdType | 0（自动） | 折叠内 | =2（手动）→MatchThresholdHigh 可见 |
| MatchThresholdHigh | EdgeThreshold | 40 | 否 | 由阈值类型=手动控制 |
| SpotterFlag | Mottle Considered | False | 折叠内 | — |
| MatchExtentRate | Extension Threshold | 0 | 折叠内 | — |
| TimeOut | RunParam_Overtime Control | 0 | 折叠内 | — |
| OutLineEnable | RunParam_Contour Enabled | True | 折叠内 | =False→OutLinePointEnable 隐藏 |
| OutLinePointEnable | RunParam_ContourPoint Enabled | False | 折叠内 | 由 OutLineEnable 控制 |

### 6.6.3 结果判断页原厂默认值（AlgorithmTab.xml 原件）

| 控件 | 标签 key | 默认值 | 范围控件默认值 |
|---|---|---|---|
| OKWhenNumIsZero | OKWhenMatchNumIsZero | False | — |
| NumLimitEnable | Quantity Check | False | NumAngleLimit=0 |
| AngleLimitEnable | Angle Check | False | RangeAngleLimit=**0** |
| ScaleLimitEnable | Scale Check | False | RangeScaleLimit=0.1 |
| Expand | — | — | 以下进折叠 |
| ScaleXLimitEnable | X Scale Check | False | RangeScaleXLimit=0.1 |
| ScaleYLimitEnable | Y Scale Check | False | RangeScaleYLimit=0.1 |
| ScoreLimitEnable | Score Check | False | RangeScoreLimit=0 |
| MatchPointX/YLimitEnable | Match Point X/Y Check | False | Range=**0**（MinValue -99999） |
| BoxPointX/YLimitEnable | Central Point X/Y Check | False | Range=**0**（MinValue -99999） |

图像显示组 9 项：ROI / Match Result / Match Point / Match Contour Point / Fixtured Point（默认隐藏）/ Unfixtured Point（默认隐藏）/ Text / ExternROI / **BoundingBox**。

### 6.6.4 新增差异清单（可直接实施项）

| # | 差异 | 原厂证据 | 本方现状 |
|---|---|---|---|
| D1 | MinScore 默认值 | 0.8 | 0.5 |
| D2 | MultiModelMaxOverlap 应有 presentation，由全部搜索模式控制可见 | AlgorithmTab 触发器 | 无 presentation（被 continue） |
| D3 | RangScaleX/Y 应有 presentation，由尺度范围类型=XY 控制可见 | 同上 | 无 presentation |
| D4 | RangSkewX 应有 presentation | 同上 | 无 presentation |
| D5 | OutLinePointEnable 由 OutLineEnable=False 隐藏 | 同上 | 无联动 |
| D6 | MatchThresholdHigh 由阈值类型=手动条件显示 | 同上 | 高级折叠且无 presentation |
| D7 | RangScale 允许 0.1~10 | 同上 | Validate 限 0.5~2.0 |
| D8 | 图像显示补 BoundingBox（匹配框） | 同上 | 缺 |
| D9 | 判断范围默认值：角度 0、匹配点/中心点 0 | 同上 | 角度 -180、匹配点/中心点 -99999 |
| D10 | Fixtured/Unfixtured Point 默认隐藏（由位置修正触发） | 同上 | 默认显示 |
| D11 | 基本参数页补输入掩膜/输出掩膜/外部 ROI 分组 | AlgorithmTab 原件 | 缺 |
| D12 | 端口补 InputMask、InputBlindPolygonString 及 14 个输出 | Modu.xml | 缺 |

## 6.7 D1–D12 实施结果（2026-11-22）

| 项 | 实施结果 |
|---|---|
| D1 最小匹配分数 | 声明与 CLR 默认值均改为 0.8 |
| D7 尺度范围 | 校验放开为 0.1~10 |
| D9 判断范围默认值 | 角度 0、匹配点/中心点 0（范围下限仍可到原值） |
| D2 多模板重叠率 | 补齐行，紧随全部搜索模式并按它显隐 |
| D5 是否输出轮廓 | 随“是否获取轮廓”显隐 |
| D3 尺度范围X/Y | 补齐行，按尺度范围类型=XY 显隐 |
| D4 横切角度 | 补齐行（原厂默认隐藏） |
| D6 匹配边缘阈值 | 按阈值类型=手动显隐 |
| D8 匹配框显示项 | 设计性合并（原厂 BoundingBox 与 MatchResults 共用框架唯一矩形槽位），不重复提供 |
| D10 基准点/运行点 | 默认隐藏，对齐原厂 CustomVisible=False |
| D11 基本页掩膜/外部ROI | 已实现（ROI composer 已覆盖），无需改动 |
| D12 输出端口 | 补 ModelIndex / ModelName / ScaleX / ScaleY / SkewX / DetectRoi 六项 |

### D12 端口差异最终状态

| 原厂输出 | 本方 | 状态 |
|---|---|---|
| MatchModelIndex | `output.model-index` | 已补（单模板恒 0） |
| MatchModelName | `output.model-name` | 已补（取训练样本名，未命名时为空串） |
| MatchScaleX / MatchScaleY | `output.scale-x` / `output.scale-y` | 已补（等比，等于 MatchScale） |
| SkewX | `output.skew-x` | 已补（算法无对应语义，恒 0，不造假） |
| ROI（检测区域） | `output.roi` | 已补（搜索区域外接框） |
| MatchStatus | 结果行 `judgment-passed` | 语义合并，不重复 |
| MatchOutline / MatchOutlineNum / ContourPoints / ContourIndex | `output.outline-points` | 语义合并，不重复 |
| BlindPolygon / BlindPolygonString | 未提供 | 盲区绘制未实现（算法边界） |
| OutputMask | 未提供 | 掩膜图像输出未实现（算法边界） |
| Output Position Correction Info | `output.fixture` | 已存在 |

## 7. 参考资料

- 原厂：`docs/07-工具逆向/tools/imvscontourmatchmodu/parameters.json`、`resources.json`、`docs/07-工具逆向/decompiled/ModuleControls/IMVSContourMatchModuControl/*`、`FeatureMatchCommonControl/base/featurematchconfigpanal.xaml`
- 实机：`docs/evidence/vm-contourmatch-ui-20261121/*.json`、`docs/evidence/contourmatch-goldencase-20261121/*`
- 本方：`tools/SunEyeVision.ContourMatch/**`
