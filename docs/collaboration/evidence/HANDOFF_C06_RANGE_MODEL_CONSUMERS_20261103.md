# C06三范围完整消费者候选交接

- 基线提交：`0c4b454de2a939bfcb51c010d928cb79778460b0`。
- 当前分支：`codex/c06-range-model-consumers-20261103`；执行者：`display_neutral_implementation`。
- 任务目标：消费公共原Integer/Float/Between完整迁移，复用真实Blob面积、圆度、矩形度三范围贯通原UI、局部模型、引用、提案、执行及保存消费者。
- 写入边界：本线只修改真实范围测试、C05阶段断言和既有验证runner；Plugin.Wpf产品由公共唯一写者提供固定提交。未改SDK、Workflow、算法、主方案或Catalog，未新建工作树。
- 最终产品：`1909cef2e9f33dcfeef430c7b9e14357c8757e27`；`src/tools`的8项变更Git内容对象逐项等于公共最终`c507b5bcd`，无产品canonical差异。
- 最终测试提交：`03b8d0170`。各轨按其真实归档快照核对，不能将此后测试观察修正移签为较早的测试源码。

## 本线实施与复用

复用三个真实视图的`ReadNumericModels(Field/CurrentPage/All)`和原唯一Rebuild初始化，未添加公开接口、业务范围模型或算法规则。C05测试仅将旧阶段的paired NotApplicable断言改为实际参与，文本XY仍NotApplicable，其他强断言保留。

同一个真实范围夹具新增每范围8组完整专项，沿实际窗口、字段、来源选择器、原CBox和Slider、模型回读、宿主接纳及生命周期运行。原三范围Core14、Sensitivity1、Publication1各16组完整保留；独立集合只是运行分组，未缩小旧范围或改变实际几何/物理帧/筛选集合oracle。

runner按合法scope和caseSet分开csproj、out、TEMP、一次性方案、日志与隐藏桌面，回执显式记范围/集合；三范围同一固定Release并行运行。未复制主仓产物、程序或厂商运行库。

## 完整结果

UI003：0错误、6553既有警告，2:38.72；全量full003：0错误、6555既有警告，1:00.93。所有原句柄自然终态，后续仅测试观察修正，没有重编产品。

| 同版真实轨 | 归档位置（Release/_validation/S4-20261025） | 结果 |
| --- | --- | --- |
| 面积C06模型与消费者 | C06RangeModels/Area/C06/attempt-002 | 8组，exit0 |
| 圆度C06模型与消费者 | C06RangeModels/Circularity/C06/attempt-003 | 8组，exit0 |
| 矩形度C06模型与消费者 | C06RangeModels/Rectangularity/C06/attempt-003 | 8组，exit0 |
| 面积原完整生产链 | BlobRangeProduction/Area/All/attempt-002 | 16组，exit0 |
| 圆度原完整生产链 | BlobRangeProduction/Circularity/All/attempt-001 | 16组，exit0 |
| 矩形度原完整生产链 | BlobRangeProduction/Rectangularity/All/attempt-001 | 16组，exit0 |
| C05真实生产链 | C05NumberModels/attempt-005 | 6组，exit0 |
| B08真实混合窗口 | B08NumericComposition/attempt-007 | 12组，exit0 |
| 已加载数值控件 | NumericContract/attempt-003 | 68组，exit0 |

合计158组。9轨实际加载共126条记录，逐路径来自本候选Release且SHA全部匹配，测试回执与归档源码快照SHA全部一致。公共C06九组及C05十组是公共线的独立候选成绩，回执以该线实际载荷为准，不算本线分数或移签到本载荷。

真实链已覆盖：原CurValue/HasValue/isInitValue区分，先左后右回读、左Check临时右读不安装模型、局部读失败不造正式默认；模型/引用/执行值分离；不同普通值的受控投影与真实popup无编辑出口；两端原始输入、倒序和宿主拒绝的整组OldValue恢复；回退同步右读和新raw不被随后恢复覆盖；无泵送的Slider排队换根/禁用/隐藏重显退役；两端独立模块/变量/索引、取消确认、历史、配置和片段复制、保存新对象重开执行、真实多形筛选敏感样本、变量及物理帧publication冻结。

## 载荷与观察修正边界

候选`release-payload.json`源为最终产品1909，buildId为`development-c2634788779e40f79c59cdeec44329d9`；实际201项逐条哈希核对零差异，manifest SHA为`820D175EE73FDF143D73927539D6963D07CB8C550BDBFC0FEBAAC9AC713DCA75`。主仓正式570项独立验，不声称候选包装等价，也不复制补齐。14项变更源码（8产品/6测试及runner）的raw SHA、规范Git对象、每轨extra快照/实体日志、loaded和封包证明见同目录`C06_RANGE_MODEL_CONSUMERS_CANDIDATE_20261103.json`。

旧候选失败全部保留：3e/c576的公共无编辑原事件差异由公共后续源码修正；三个首轮C06第5的观察问题分别是高级区重建后收起，以及观察helper在队列退役前意外Pump。后续在原业务断言不变下补实际高级区导航、无泵送缓存读取和Bind前/同步清理后/Pump后的模型与引用实体。

最终两浮点第7的失败属于另两处测试手动Bind后未重显高级区，未实际形成pending或触达宿主。修正后日志明确记录`oldVisible=false/oldActive=false`到`visible=true/active=true`，两端实际pending，以及拒绝`refusals=1/accepted=false`，原模型/raw/identity全部通过。面积All首轮第2因测试Get将合法null Candidate误当缺成员，恢复原null语义后仅重跑失败All16。以上没有放宽原断言，不当作产品缺陷，未覆盖旧快照。

## 未做、人工验收与下一步

S3算法优化未开启；现有HALCON算法仅用于验证真实参数消费和资源/快照。缺producer的其他判定范围继续S1真实预览和明确保护，不记业务已实现。VS设计器、自然键鼠/焦点、剪贴板、DPI与统一人工验收另记。

本线未扩旧Loaded42的可见桌面测试平台，未重跑无关导出/几何或图文All44；主线按正式同版范围自行验收。候选与主仓正式、源码集成与人工验收分别记录。产品及测试已冻结，主线接管Catalog/O06、正式验收和阶段收口。原CURRENT_TASK脏内容按主线唯一写入边界保留，未覆盖或提交。
